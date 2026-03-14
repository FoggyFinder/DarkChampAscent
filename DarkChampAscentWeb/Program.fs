open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.HttpOverrides
open AspNet.Security.OAuth.Discord
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.OAuth
open System
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Http
open Conf.WebUiConf
open Db
open GameLogic.Champs
open GameLogic.Battle
open NetCord
open DiscordBot
open NetCord.Rest
open Serilog
open NetCord.Gateway
open GameLogic.Shop
open Types
open DarkChampAscent.Account
open System.Security.Claims
open Microsoft.AspNetCore.Identity
open System.Text.Json
open System.Text.Json.Serialization
open DTO

Log.Logger <-
    (new LoggerConfiguration())
          .Enrich.FromLogContext()
          .WriteTo.Console()
          .WriteTo.File("log.txt", rollingInterval=RollingInterval.Month)
          .CreateLogger()

open System.Collections.Concurrent
open Helpers

let options = 
    JsonFSharpOptions()
        .WithUnionExternalTag()      // {"Ok": ...} instead of {"Case":"Ok","Fields":[...]}
        .WithUnionUnwrapSingleFieldCases() // unwraps single-field cases
        .ToJsonSerializerOptions()

let opt =
    JsonFSharpOptions()
        .WithUnionUnwrapSingleCaseUnions(false)
        .ToJsonSerializerOptions()

let ofJson (value: 'a) : HttpHandler =
    fun ctx ->
        let json = JsonSerializer.Serialize(value, options)
        Response.ofPlainText json ctx

let apiOk (value: 'a) : HttpHandler = ofJson (Ok value)
let apiError (msg: string) (statusCode: int) : HttpHandler =
    Response.withStatusCode statusCode >> ofJson (Error msg)
let apiResult (res:Result<'a, string>) = ofJson res
let apiUnauthorized = apiError "Unauthorized" 401
let apiNotFound    = apiError "Not found" 404
let apiBadRequest msg = apiError msg 400

let withRateLimit (maxAttempts: int) (window: TimeSpan) (getKey: HttpContext -> string) (handler: HttpHandler) : HttpHandler =
    fun ctx ->
        let key = try getKey ctx with _ -> "unknown"
        if RateLimiting.checkRateLimit key maxAttempts window then
            handler ctx
        else
            (Response.withStatusCode 429 >> ofJson (Error "Too many requests. Please try again later.")) ctx

open GameLogic.Monsters

let builder = WebApplication.CreateBuilder()

let getAccount (result: AuthenticateResult) : Account option =
    if not result.Succeeded then None
    else
        let claims  = result.Principal.Claims |> Seq.toList
        let isCustom = result.Principal.HasClaim(ClaimTypes.AuthenticationMethod, AuthenticationHandler.Custom)
        let isWeb3 = result.Principal.HasClaim(ClaimTypes.AuthenticationMethod, AuthenticationHandler.Web3)
        if isCustom || isWeb3 then
            let nickname    = result.Principal.FindFirstValue ClaimTypes.Name
            let customIdStr = result.Principal.FindFirstValue ClaimTypes.NameIdentifier
            match nickname, customIdStr with
            | null, _ | _, null -> None
            | nickname, customIdStr ->
                match UInt64.TryParse customIdStr with
                | true, customId ->
                    if isCustom then
                        CustomUser(nickname, customId) |> Account.Custom |> Some
                    else
                        Web3User(nickname, customId) |> Account.Web3 |> Some
                | false, _ ->
                    Log.Error($"Invalid customId {customIdStr} | {isCustom} | {isWeb3}")
                    None
        else
            let nameO =
                claims |> List.tryPick (fun c -> if c.Type = ClaimTypes.Name then Some c.Value else None)
            let idO =
                claims |> List.tryPick (fun c ->
                    if c.Type = ClaimTypes.NameIdentifier then
                        match UInt64.TryParse c.Value with
                        | true, v -> Some v
                        | _ -> None
                    else None)
            let picO =
                claims |> List.tryPick (fun c ->
                    if c.Type = "urn:discord:avatar:hash" then Some c.Value else None)
            match idO with
            | Some id ->
                let name' = nameO |> Option.defaultValue "User"
                DiscordUser(name', id, picO) |> Account.Discord |> Some
            | None ->
                Log.Error("Missing id. Claims")
                claims |> Seq.iter (fun c -> Log.Error($"{c.Type} = {c.Value}"))
                None

let authenticate (ctx: HttpContext) =
    task {
        let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
        return getAccount result
    }

let meHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let response =
                match ao with
                | Some user -> apiOk user
                | None -> apiUnauthorized
            return! response ctx
        }

let loginDiscordHandler : HttpHandler =
    fun ctx ->
        let props = AuthenticationProperties(RedirectUri = "/auth/discord/callback", IsPersistent = true)
        ctx.ChallengeAsync(DiscordAuthenticationDefaults.AuthenticationScheme, props)

let loginCustomHandler : HttpHandler =
    withRateLimit 5 (TimeSpan.FromMinutes 15.0)
        (fun ctx -> $"login:{ctx.Connection.RemoteIpAddress}")
        (fun ctx ->
            task {
                let db  = ctx.Plug<SqliteStorage>()
                let frm = ctx.Request.Form
                let res =
                    match frm.tryGetFormValue "nickname", frm.tryGetFormValue "password" with
                    | Some nickname, Some providedPassword ->
                        if db.UserNameExists nickname then
                            match db.GetCustomUserInfoByNickname nickname with
                            | Some (cId, storedHash) ->
                                let user   = IdentityUser(UserName = nickname)
                                let hasher = PasswordHasher<IdentityUser>()
                                match hasher.VerifyHashedPassword(user, storedHash, providedPassword) with
                                | PasswordVerificationResult.Success ->
                                    let authUser = CustomUser(nickname, uint64 cId)
                                    Ok (AuthenticationHandler.createCustomTicket authUser CookieAuthenticationDefaults.AuthenticationScheme, authUser)
                                | PasswordVerificationResult.SuccessRehashNeeded ->
                                    let authUser = CustomUser(nickname, uint64 cId)
                                    let newHash  = hasher.HashPassword(user, providedPassword)
                                    db.UpdatePassword(int64 cId, newHash) |> ignore
                                    Ok (AuthenticationHandler.createCustomTicket authUser CookieAuthenticationDefaults.AuthenticationScheme, authUser)
                                | _ -> Error "Invalid password"
                            | None -> Error "User not found"
                        else Error "User not found"
                    | _ -> Error "Please fill in all fields"

                let! response =
                    task {
                        match res with
                        | Ok (ticket, authUser) ->
                            do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ticket.Principal, ticket.Properties)
                            return Account.Custom authUser |> Ok
                        | Error msg -> return Error msg
                    }
                return! apiResult response ctx
            })

let web3ChallengeHandler : HttpHandler =
    fun ctx ->
        task {
            let db = ctx.Plug<SqliteStorage>()
            match ctx.Request.Form.tryGetFormValue "wallet" with
            | Some wallet ->
                let nonce    = Guid.NewGuid().ToString("N")
                let expires  = DateTime.UtcNow.AddMinutes 5.0
                let! txnB64 = Blockchain.getTxB64((wallet.ToString()), $"auth:{nonce}")
                let r =
                    match db.SaveNonce(wallet, nonce, expires) with
                    | Ok ()   -> Ok (NonceDTO(txnB64, nonce))
                    | Error e -> Error e
                return! apiResult r ctx
            | _ ->
                return! apiError "wallet param required" 400 ctx
        }

let loginWeb3Handler : HttpHandler =
    withRateLimit 5 (TimeSpan.FromMinutes 15.0)
        (fun ctx -> $"login:{ctx.Connection.RemoteIpAddress}")
        (fun ctx ->
            task {
                let db  = ctx.Plug<SqliteStorage>()
                let frm = ctx.Request.Form
                let res =
                    match frm.tryGetFormValue "wallet",
                          frm.tryGetFormValue "signedTxnB64",
                          frm.tryGetFormValue "nonce" with
                    | Some wallet, Some signedTxnB64, Some nonce ->
                        match db.GetNonce wallet with
                        | Some (storedNonce, expiresAt) when storedNonce = (nonce.ToString()) && DateTime.UtcNow < expiresAt ->
                            match db.DeleteNonce wallet with
                            | Ok () ->
                                if Blockchain.verifyAlgorandTxnSignature (wallet.ToString()) (signedTxnB64.ToString()) $"auth:{nonce}" then
                                    let userR =
                                        match db.FindUserIdByWallet wallet with
                                        | Some uId ->
                                            Web3User(wallet, uint64 uId) |> Ok
                                        | None ->
                                            match db.TryRegisterWeb3User wallet with
                                            | Ok uid ->
                                                let uid' = uint64 uid
                                                CommonHelpers.updateChamps(db, UserId.Web3 uid', [wallet.ToString()]) |> ignore
                                                Web3User(wallet, uid') |> Ok
                                            | Error err -> Error err
                                    match userR with
                                    | Ok authUser ->
                                        Ok (AuthenticationHandler.createWeb3Ticket authUser CookieAuthenticationDefaults.AuthenticationScheme, authUser)
                                    | Error err ->
                                        Error err
                                else
                                    Error "Invalid signature"
                            | Error _ -> Error "Unexpected error. Please, try again later"
                        | Some _ -> Error "Nonce expired"
                        | None   -> Error "No challenge found for this wallet"
                    | _ -> Error "Please fill in all fields"

                let! response =
                    task {
                        match res with
                        | Ok (ticket, authUser) ->
                            do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ticket.Principal, ticket.Properties)
                            return Account.Web3 authUser |> Ok
                        | Error msg ->
                            return Error msg
                    }

                return! apiResult response ctx
            })

let regPostHandler : HttpHandler =
    withRateLimit 5 (TimeSpan.FromMinutes 15.0)
        (fun ctx -> $"register:{ctx.Connection.RemoteIpAddress}")
        (fun ctx ->
            task {
                let db  = ctx.Plug<SqliteStorage>()
                let frm = ctx.Request.Form
                let res =
                    match frm.tryGetFormValue "nickname", frm.tryGetFormValue "password" with
                    | Some nickname, Some password ->
                        match Validation.validateNickname (nickname.ToString()), Validation.validatePassword (password.ToString()) with
                        | Ok validNickname, Ok validPassword ->
                            if db.UserNameExists nickname then
                                Error "This name is already taken"
                            else
                                let user       = IdentityUser(UserName = validNickname)
                                let hasher     = PasswordHasher<IdentityUser>()
                                let hashedPwd  = hasher.HashPassword(user, validPassword)
                                db.TryRegisterCustomUser(validNickname, hashedPwd)
                                |> Result.map (fun cId ->
                                    let authUser = CustomUser(validNickname, uint64 cId)
                                    AuthenticationHandler.createCustomTicket authUser CookieAuthenticationDefaults.AuthenticationScheme, authUser)
                        | Error nicknameErr, _ -> Error nicknameErr
                        | _, Error passwordErr -> Error passwordErr
                    | _ -> Error "Please fill in all fields"

                let! response =
                    task {
                        match res with
                        | Ok (ticket, authUser) ->
                            do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ticket.Principal, ticket.Properties)
                            return Ok (Account.Custom authUser)
                        | Error msg ->
                            return Error msg
                    }
                return! apiResult response ctx
            })

let logoutHandler : HttpHandler =
    fun ctx ->
        task {
            do! ctx.SignOutAsync CookieAuthenticationDefaults.AuthenticationScheme
            return! (apiOk true) ctx
        }

let accountHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let response =
                match ao with
                | Some a ->
                    let db  = ctx.Plug<SqliteStorage>()
                    let dId = a.ID
                    match a with
                    | Account.Discord d -> db.TryRegisterDiscordUser d.DiscordId |> ignore
                    | Account.Custom _ 
                    | Account.Web3 _ -> ()
                    match db.GetUserWallets dId, db.GetUserChampsCount dId,
                          db.GetUserMonstersCount dId, db.GetUserRequestsCount dId,
                          db.GetUserBalance dId with
                    | Ok wallets, Some champs, Some monsters, Some requests, Some balance ->
                        let dcPrice = db.GetNumKey Db.DbKeysNum.DarkCoinPrice
                        let dto = AccountDTO(UserAccount(a, wallets, balance, int champs, int monsters, int requests), dcPrice)
                        apiOk dto
                    | _ -> apiError "Can't fetch user data, please try again later" 500
                | None -> apiUnauthorized
            return! response ctx
        }

let registerNewWalletHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let response =
                match ao with
                | Some user ->
                    let db = ctx.Plug<SqliteStorage>()
                    match ctx.Request.Form.TryGetValue "wallet" with
                    | true, s ->
                        let wallet = s.ToString().Trim()
                        if Blockchain.isValidAddress wallet then
                            db.RegisterNewWallet(user.ID, wallet)
                            |> apiOk
                        else
                            try Log.Error($"{user.ID} attempts to register invalid {wallet} address")
                            with exn -> Log.Error(exn, $"attempt to register {wallet} address")
                            apiBadRequest "Invalid wallet address"
                    | false, _ -> apiBadRequest "Missing wallet field"
                | None -> apiUnauthorized
            return! response ctx
        }

open System.Threading.Tasks
open Services
let battleParticipantsHandler : HttpHandler =
    fun ctx ->
        task {
            let res = ctx.Response
            res.Headers["Content-Type"] <- "text/event-stream"
            res.Headers["Cache-Control"] <- "no-cache"
            res.Headers["Connection"] <- "keep-alive"
            res.Headers["X-Accel-Buffering"] <- "no"  // important if behind nginx
            do! res.Body.FlushAsync()
            let s = ctx.Plug<RoundParticipantsService>()
            let ct = ctx.RequestAborted
            let update (participants:Result<RoundParticipantDTO list, string>) =
                async {
                    let json = JsonSerializer.Serialize(participants, options)
                    let msg = $"data: {json}\n\n"
                    let bytes = System.Text.Encoding.UTF8.GetBytes(msg)
                    do! res.Body.WriteAsync(bytes, 0, bytes.Length, ct) |> Async.AwaitTask
                    do! res.Body.FlushAsync() |> Async.AwaitTask
                }
            s.CurrentValue |> Option.iter(fun p -> Async.Start(update p, ct))
            let d = s.RoundParticipantsChanged.Subscribe(fun participants ->
                Async.Start(update participants, ct)
            )
            while not ct.IsCancellationRequested do
                try
                    do! Task.Delay(TimeSpan.FromMinutes(1.), ct)
                with
                | :? OperationCanceledException -> ()
                | ex -> Log.Error("battleParticipantsHandler:" + ex.ToString())
            d.Dispose()
        }

let battleRoundInfoHandler : HttpHandler =
    fun ctx ->
        task {
            let res = ctx.Response
            res.Headers["Content-Type"] <- "text/event-stream"
            res.Headers["Cache-Control"] <- "no-cache"
            res.Headers["Connection"] <- "keep-alive"
            res.Headers["X-Accel-Buffering"] <- "no"
            do! res.Body.FlushAsync()
            let s = ctx.Plug<RoundStatusService>()
            let ct = ctx.RequestAborted

            let update (roundInfo:Result<RoundInfoDTO, string>) =
                async {
                    let json = JsonSerializer.Serialize(roundInfo, options)
                    let msg = $"data: {json}\n\n"
                    let bytes = System.Text.Encoding.UTF8.GetBytes(msg)
                    do! res.Body.WriteAsync(bytes, 0, bytes.Length, ct) |> Async.AwaitTask
                    do! res.Body.FlushAsync() |> Async.AwaitTask
                }
            s.CurrentValue |> Option.iter(fun p -> Async.Start(update p, ct))
            let d = s.RoundStatusChanged.Subscribe(fun rsr ->
                Async.Start(update rsr, ct)
            )
            while not ct.IsCancellationRequested do
                try
                    do! Task.Delay(TimeSpan.FromMinutes(1.), ct)
                with
                | :? OperationCanceledException -> ()
                | ex -> Log.Error("battleRoundInfoHandler:" + ex.ToString())
            d.Dispose()
        }

let battleHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao      = authenticate ctx
            let rdb      = ctx.Plug<SqliteWebUiStorage>()
            let db       = ctx.Plug<SqliteStorage>()
            let battle   =
                rdb.GetCurrentBattleInfo()
                |> Result.map(fun cbi ->
                    cbi.WithMonsterImg(FileUtils.mapToLocalImg cbi.Monster.Picture))

            let userChamps =
                ao |> Option.map (fun user -> db.GetAvailableUserChamps user.ID)

            let history =
                match battle with
                | Ok cbo -> rdb.GetBattleHistory(cbo.BattleNum, cbo.Monster.Name)
                | Error _ -> Error "Unknown error"
            
            let dto = BattleDTO(battle, history, userChamps)
            let response = apiOk dto
            return! response ctx
        }

let joinBattleHandler : HttpHandler =
    fun ctx ->
        task {
            let! result  = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let rdb      = ctx.Plug<SqliteWebUiStorage>()
            let s = ctx.Plug<RoundParticipantsService>()
            if result.Succeeded then
                let champIdO =
                    match ctx.Request.Form.TryGetValue "champ" with
                    | true, s -> match UInt64.TryParse s with true, v -> Ok v | _ -> Error "Invalid champ field value"
                    | _ -> Error "missing champ field"
                let moveO =
                    ctx.Request.Form.tryGetFormValue "move"
                    |> Option.map (fun s -> Parser.enumFromIntAsStr<Move>(s.ToString()))
                    |> Option.defaultValue (Error "missing move field")

                let response =
                    match champIdO, moveO with
                    | Ok champId, Ok move ->
                        let rar = { ChampId = champId; Move = move }
                        match rdb.PerformAction rar with
                        | Ok _ ->
                            let t () =
                                task {
                                    let client = ctx.Plug<GatewayClient>()
                                    let name, ipfs = rdb.GetChampNameIPFSById rar.ChampId |> Option.defaultValue ("", "")
                                    let joinedRoundComponent =
                                        ComponentContainerProperties(
                                            [ ComponentSectionProperties(
                                                  ComponentSectionThumbnailProperties(
                                                      ComponentMediaProperties($"https://ipfs.dark-coin.io/ipfs/{ipfs}")),
                                                  [ TextDisplayProperties($"**{name}**")
                                                    TextDisplayProperties("joined round!") ]) ])
                                    let mp =
                                        MessageProperties()
                                            .WithComponents([ joinedRoundComponent ])
                                            .WithFlags(MessageFlags.IsComponentsV2)
                                    Utils.sendMsgToLogChannel client mp |> ignore
                                }
                            t () |> ignore
                            s.ForceRescan()
                            Ok (())
                        | Error e -> Error e
                    | Error err1, Error err2 -> Error(err1 + Environment.NewLine + err2)
                    | Error err, _ | _, Error err -> Error err
                return! apiResult response ctx
            else
                return! apiUnauthorized ctx
        }

let shopHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db = ctx.Plug<SqliteStorage>()
            let response =
                match db.GetShopItems(), db.GetNumKey Db.DbKeysNum.DarkCoinPrice with
                | Some items, Some price ->
                    let userBalance = ao |> Option.bind (fun u -> db.GetUserBalance u.ID)
                    ShopDTO(items, price, userBalance)
                    |> apiOk 
                | _ -> apiError "Shop unavailable" 500
            return! response ctx
        }

let buyItemHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db  = ctx.Plug<SqliteStorage>()
            let response =
                match ao with
                | Some user ->
                    match ctx.Request.Form.TryGetValue "shopitem" with
                    | true, s ->
                        match Enum.TryParse<ShopItem> s with
                        | true, v ->
                            match db.BuyItem(user.ID, v, 1) with
                            | Ok _    -> apiOk ()
                            | Error e -> apiError (string e) 400
                        | _ -> apiBadRequest "Invalid shop item"
                    | _ -> apiBadRequest "Missing shopitem field"
                | None -> apiUnauthorized
            return! response ctx
        }

let storageHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let response =
                match ao with
                | Some user ->
                    let db  = ctx.Plug<SqliteStorage>()
                    let uId = user.ID
                    match db.GetUserStorage uId, db.GetUserChamps uId with
                    | Some items, Some champs ->
                        UserStorageDTO(items, champs) |> apiOk
                    | _ -> apiError "Could not fetch storage" 500
                | None -> apiUnauthorized
            return! response ctx
        }

let useItemHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db  = ctx.Plug<SqliteStorage>()
            let response =
                match ao with
                | Some user ->
                    let shopItem =
                        ctx.Request.Form.tryGetFormValue "useitem"
                        |> Option.bind (fun s -> match Enum.TryParse<ShopItem> s with true, v -> Some v | _ -> None)
                    let champId =
                        ctx.Request.Form.tryGetFormValue "champ"
                        |> Option.bind (fun s -> match UInt64.TryParse s with true, v -> Some v | _ -> None)
                    match shopItem, champId with
                    | Some si, Some cid ->
                        db.UseItemFromStorage(user.ID, si, cid) |> apiOk
                    | _ -> apiBadRequest "Missing useitem or champ"
                | None -> apiUnauthorized
            return! response ctx
        }

let myChampsHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let response =
                match ao with
                | Some user ->
                    let db = ctx.Plug<SqliteStorage>()
                    match db.GetUserChampsInfo user.ID with
                    | Some champs -> apiOk champs
                    | _ -> apiError "Could not fetch champs" 500
                | None -> apiUnauthorized
            return! response ctx
        }

let champHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao      = authenticate ctx
            let route    = Request.getRoute ctx
            let champId  = route.GetInt64 "id"
            let db       = ctx.Plug<SqliteStorage>()
            let userBalance =
                ao |> Option.bind (fun u ->
                    let uId = u.ID
                    db.ChampBelongsToAUser(uint64 champId, uId)
                    |> Option.bind (fun b -> if b then db.GetUserBalance uId else None))
            let response =
                match db.GetChampInfoById champId, db.GetNumKey Db.DbKeysNum.DarkCoinPrice with
                | Some champ, Some price ->
                    ChampDTO(champ, userBalance, price) |> apiOk
                | _ -> apiNotFound
            return! response ctx
        }

let renameChampHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db  = ctx.Plug<SqliteStorage>()
            let response =
                match ao with
                | Some user ->
                    let oldNameO = ctx.Request.Form.tryGetFormValue "oldname" |> Option.map string
                    let newNameO = ctx.Request.Form.tryGetFormValue "newname" |> Option.map string
                    let chmpIdO  =
                        ctx.Request.Form.tryGetFormValue "chmpId"
                        |> Option.bind (fun s -> match UInt64.TryParse s with true, v -> Some v | _ -> None)
                    match oldNameO, newNameO, chmpIdO with
                    | Some oldName, Some newName, Some cId ->
                        // TODO: use cId instead of oldName lookup
                        match db.RenameChamp(user.ID, oldName, newName) with
                        | Ok _ -> apiOk ()
                        | Error e -> apiError (string e) 400
                    | _ -> apiBadRequest "Missing fields"
                | None -> apiUnauthorized
            return! response ctx
        }

let lvlUpHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db  = ctx.Plug<SqliteStorage>()
            match ao with
            | Some user ->
                let cIdR =
                    match ctx.Request.Form.TryGetValue "champ" with
                    | true, s -> match UInt64.TryParse s with true, v -> Ok v | _ -> Error "Invalid champ field value"
                    | _ -> Error "missing champ field"
                let chr =
                    ctx.Request.Form.tryGetFormValue "char"
                    |> Option.map (fun s -> Parser.enumFromIntAsStr<Characteristic>(s.ToString()))
                    |> Option.defaultValue (Error "missing char field")
                let res =
                    match cIdR, chr with
                    | Ok cId, Ok ch ->
                        match db.ChampBelongsToAUser(cId, user.ID) with
                        | Some true ->
                            if db.LevelUp(cId, ch) then Ok ()
                            else Error "Level up failed"
                        | _ -> Error "Not your champ"
                    | Error err1, Error err2 -> Error(err1 + Environment.NewLine + err2)
                    | Error err, _ | _, Error err -> Error err
                return! apiResult res ctx
            | None -> return! apiUnauthorized ctx
        }

let champsUnderEffectsHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let response =
                match ao with
                | Some user ->
                    let db = ctx.Plug<SqliteStorage>()
                    match db.GetLastRoundId() with
                    | Some roundId ->
                        match db.GetUserChampsUnderEffect(user.ID, roundId) with
                        | Ok champs -> apiOk champs
                        | Error e   -> apiError (string e) 500
                    | None -> apiError "No round found" 404
                | None -> apiUnauthorized
            return! response ctx
        }

let rescanHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db  = ctx.Plug<SqliteStorage>()
            match ao with
            | Some user ->
                let response =
                    match db.GetUserWallets user.ID with
                    | Ok xs ->
                        let confirmed = xs |> List.choose (fun ar -> if ar.IsConfirmed then Some ar.Wallet else None)
                        CommonHelpers.updateChamps(db, user.ID, confirmed)
                    | Error e -> Error e
                return! apiResult response ctx
            | None -> return! apiUnauthorized ctx
        }

let myMonstersHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let response =
                match ao with
                | Some user ->
                    let db = ctx.Plug<SqliteStorage>()
                    match db.GetUserMonsters user.ID, db.GetNumKey Db.DbKeysNum.DarkCoinPrice with
                    | Ok monsters, Some dcPrice ->
                        let userBalance = db.GetUserBalance user.ID
                        let monsters' =
                            monsters
                            |> List.map(fun m -> m.WithMonsterImg (FileUtils.mapToLocalImg m.Pic))
                        UserMonstersDTO(monsters', dcPrice, userBalance) |> apiOk
                    | _ -> apiError "Could not fetch monsters" 500
                | None -> apiUnauthorized
            return! response ctx
        }

let monstrHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao   = authenticate ctx
            let route = Request.getRoute ctx
            let mId   = route.GetInt64 "id"
            let db    = ctx.Plug<SqliteStorage>()
            let isOwned =
                ao |> Option.bind (fun u -> db.MonsterBelongsToAUser(uint64 mId, u.ID))
                   |> Option.defaultValue false
            let response =
                match db.GetMonsterById mId with
                | Some monstr ->
                    MonsterDTO({ monstr with Picture = FileUtils.mapToLocalImg monstr.Picture }, uint64 mId, isOwned)
                    |> apiOk
                | _ -> apiNotFound
            return! response ctx
        }

let renameMonstrHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db  = ctx.Plug<SqliteStorage>()
            let response =
                match ao with
                | Some user ->
                    let mIdO =
                        ctx.Request.Form.tryGetFormValue "mnstrid"
                        |> Option.bind (fun s -> match UInt64.TryParse s with true, v -> Some v | _ -> None)
                    let nameO = ctx.Request.Form.tryGetFormValue "mnstrname" |> Option.map string
                    match mIdO, nameO with
                    | Some mId, Some newName ->
                        if db.MonsterBelongsToAUser(mId, user.ID) |> Option.defaultValue false then
                            match db.RenameUserMonster(user.ID, newName, int64 mId) with
                            | Ok _ -> apiOk ()
                            | Error e -> apiError (string e) 400
                        else apiError "Not your monster" 403
                    | _ -> apiBadRequest "Missing fields"
                | None -> apiUnauthorized
            return! response ctx
        }

let monstersUnderEffectsHandler : HttpHandler =
    fun ctx ->
        task {
            let db  = ctx.Plug<SqliteStorage>()
            let response =
                match db.GetLastRoundId() with
                | Some roundId ->
                    match db.GetMonstersUnderEffect roundId with
                    | Ok monsters ->
                        monsters
                        |> List.map(fun m -> m.WithMonsterImg(FileUtils.mapToLocalImg m.Pic))
                        |> apiOk
                    | Error e -> apiError (string e) 500
                | _ -> apiError "No round" 404
            return! response ctx
        }

let createMonsterHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db  = ctx.Plug<SqliteStorage>()
            let response =
                match ao with
                | Some user ->
                    let mtypeO =
                        ctx.Request.Form.tryGetFormValue "mtype"
                        |> Option.map (fun s -> Parser.enumFromIntAsStr<MonsterType>(s.ToString()))
                        |> Option.defaultValue (Error("Missing mtype"))
                    let msubtypeO =
                        ctx.Request.Form.tryGetFormValue "msubtype"
                        |> Option.map (fun s -> Parser.enumFromIntAsStr<MonsterSubType>(s.ToString()))
                        |> Option.defaultValue (Error("Missing msubtype"))
                    match mtypeO, msubtypeO with
                    | Ok mtype, Ok msubtype ->
                        db.CreateGenRequest(user.ID, mtype, msubtype)
                    | Error err1, Error err2 -> Error(err1 + Environment.NewLine + err2)
                    | Error err, _ | _, Error err -> Error err
                    |> apiResult
                | None -> apiUnauthorized
            return! response ctx
        }

let myRequestsHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let response =
                match ao with
                | Some user ->
                    let db = ctx.Plug<SqliteStorage>()
                    db.GetPendingUserRequests user.ID |> apiResult
                | None -> apiUnauthorized
            return! response ctx
        }

let donateHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db  = ctx.Plug<SqliteStorage>()
            let response =
                match ao with
                | Some user ->
                    match ctx.Request.Form.tryGetFormValue "amount" with
                    | Some s ->
                        match Decimal.TryParse s with
                        | true, v ->
                            match db.Donate(user.ID, v) with
                            | Ok _ ->
                                let client = ctx.Plug<GatewayClient>()
                                let card   = user.Nickname |> DiscordBot.Components.donationCard v
                                let msg    =
                                    MessageProperties()
                                        .WithComponents([ card ])
                                        .WithFlags(MessageFlags.IsComponentsV2)
                                        .WithAllowedMentions(AllowedMentionsProperties.None)
                                Utils.sendMsgToLogChannel client msg |> ignore
                                apiOk ()
                            | Error e -> apiError e 400
                        | _ -> apiBadRequest "Invalid amount"
                    | _ -> apiBadRequest "Missing amount"
                | None -> apiUnauthorized
            return! response ctx
        }

[<RequireQualifiedAccess>]
module LeaderboardHandlers =
    let champsHandler : HttpHandler =
        fun ctx ->
            task {
                let db = ctx.Plug<SqliteStorage>()
                let response = db.GetChampLeaderboard()
                return! apiResult response ctx
            }

    let monstersHandler : HttpHandler =
        fun ctx ->
            task {
                let db = ctx.Plug<SqliteStorage>()
                let response =
                    match db.GetMonsterLeaderboard() with
                    | Ok xs ->
                        xs |> List.map(fun m -> m.WithMonsterImg(FileUtils.mapToLocalImg m.Pic))
                        |> Ok
                    | Error e -> Error e
                return! apiResult response ctx
            }

    let donatersHandler : HttpHandler =
        let names = ConcurrentDictionary<uint64, string>()
        fun ctx ->
            task {
                let db     = ctx.Plug<SqliteStorage>()
                let client = ctx.Plug<RestClient>()
                let! response =
                    match db.GetTopInGameDonaters() with
                    | Ok xs ->
                        task {
                            let newIds =
                                xs |> List.choose (fun d ->
                                    match d.Donater with
                                    | Donater.Discord dId ->
                                        let id' = uint64 dId
                                        if names.ContainsKey id' then None else Some id'
                                    | _ -> None)
                            let! us = Task.WhenAll([ for dId in newIds -> task { return! client.GetUserAsync dId } ])
                            us |> Array.iter (fun u -> names.TryAdd(u.Id, u.GlobalName) |> ignore)
                            let rows =
                                xs |> List.map (fun d ->
                                    let name =
                                        match d.Donater with
                                        | Donater.Discord dId ->
                                            match names.TryGetValue(uint64 dId) with
                                            | true, n -> n
                                            | _ -> string dId
                                        | Donater.Unknown wallet  -> wallet
                                        | Donater.Custom (_, n)   -> n
                                    {| name = name; amount = d.Amount |})
                            return apiOk rows
                        }
                    | _ -> task { return apiError "Could not fetch donaters" 500 }
                return! response ctx
            }

    let unknownDonatersHandler : HttpHandler =
        fun ctx ->
            task {
                let db = ctx.Plug<SqliteStorage>()
                let response = db.GetTopDonaters()
                return! apiResult response ctx
            }

module TxHandlers =
    open DarkChampAscent.Api
    let createTxHandler : HttpHandler =
        fun ctx ->
            task {
                let! ao = authenticate ctx
                match ao with
                | Some user ->
                    let! res = task {
                        let txR =
                            match ctx.Request.Form.tryGetFormValue "tx" with
                            | Some s ->
                                try
                                    let tx = JsonSerializer.Deserialize<Tx>(s.ToString(), opt)
                                    Ok tx
                                with ex ->
                                    Error (ex.ToString())
                            | None -> Error "missing tx field"
                        match txR with
                        | Ok tx ->
                            match user with
                            | Account.Web3 web3 ->
                                match tx with
                                | Tx.Deposit amount ->
                                    let! tx = BlockchainUtils.createDepositTx web3.Wallet amount
                                    return Ok tx
                                | Tx.Confirm _ -> return Error "Not supported"
                            | _ ->
                                match tx with
                                | Tx.Deposit _ -> return Error "Not supported"
                                | Tx.Confirm (wallet, code) ->
                                    let! txnB64 = Blockchain.getTxB64(wallet, $"confirm:{code}")
                                    return Ok txnB64
                        | Error err -> return Error err
                    }
                    return! apiResult res ctx
                | None -> return! apiUnauthorized  ctx
            }
    open Blockchain
    let submitTxHandler : HttpHandler =
        fun ctx ->
            task {
                let! ao = authenticate ctx
                match ao with
                | Some user -> 
                    let! res = task {
                        match user with
                        | Account.Web3 _ ->
                            match ctx.Request.Form.tryGetFormValue "signedTxnB64" with
                            | Some s ->
                                let! txstatus = Blockchain.sendTX64 (s.ToString())
                                match txstatus with
                                | TxStatus.Confirmed(tx, _) -> return Ok tx
                                | TxStatus.Unconfirmed tx -> return Ok tx
                                | TxStatus.Error err -> return Error (err.ToString())
                            | None -> return Error "Missing tx"
                        | _ -> return Error "Web3 users only"
                    }
                    return! apiResult res ctx
                | None -> return! apiUnauthorized ctx
            }

    let verifyTxHandler : HttpHandler =
        fun ctx ->
            task {
                let! ao = authenticate ctx
                match ao with
                | Some user -> 
                    let! res = task {
                        match user with
                        | Account.Discord _ | Account.Custom _ ->
                            match ctx.Request.Form.tryGetFormValue "signedTxnB64" with
                            | Some s ->
                                let! (sender, note) = Blockchain.decodeTX64 (s.ToString())
                                let args = note.Split(":")
                                match args with
                                | [| "confirm"; code |] ->
                                    let db = ctx.Plug<SqliteStorage>()
                                    if db.ConfirmWallet(sender, code) then
                                        match db.FindDiscordIdByWallet sender with
                                        | Some discordId ->
                                            let client = ctx.Plug<GatewayClient>()
                                            Utils.addDiscordRole client (uint64 discordId)
                                        | None -> ()
                                        // ignore errors here, users can re-scan their champs later
                                        CommonHelpers.updateChamps (db, user.ID, [sender]) |> ignore
                                        return Ok (())
                                    else
                                        return Error "Wallet not confirmed"
                                | _ -> return Error "Invalid note"
                            | None -> return Error "Missing tx"
                        | _ -> return Error "Only discord/custom users"
                    }
                    return! apiResult res ctx
                | None -> return! apiUnauthorized ctx
            }

let homeHandler : HttpHandler =
    fun ctx ->
        task {
            let db = ctx.Plug<SqliteStorage>()
            let response =
                match db.GetNumKey DbKeysNum.Rewards, db.GetNumKey DbKeysNum.DarkCoinPrice with
                | Some rewards, dcPriceO -> RewardsPriceDTO(rewards, dcPriceO) |> apiOk
                | _ -> apiError "Could not fetch home data" 500
            return! response ctx
        }

let statsHandler : HttpHandler =
    fun ctx ->
        task {
            let db = ctx.Plug<SqliteWebUiStorage>()
            let response =
                match db.GetStats() with
                | Some s -> apiOk s
                | _ -> apiError "Could not fetch stats" 500
            return! response ctx
        }

let discordCallbackHandler (frontendUrl: string) : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(DiscordAuthenticationDefaults.AuthenticationScheme)
            match getAccount result with
            | Some (Account.Discord d) ->
                // Re-issue as cookie so the Fable app can use it
                let claims =
                    [ Claim(ClaimTypes.NameIdentifier, string d.DiscordId)
                      Claim(ClaimTypes.Name, d.Nickname)
                      match d.PicRaw with Some h -> Claim("urn:discord:avatar:hash", h) | None -> () ]
                let identity   = ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
                let principal  = ClaimsPrincipal(identity)
                let properties = AuthenticationProperties(IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7.0))
                do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties)
                let response = Response.redirectTemporarily (frontendUrl + "/account") ctx
                return! response
            | _ ->
                let response = Response.redirectTemporarily (frontendUrl + "/login") ctx
                return! response
        }

let onTicketReceived =
    System.Func<_, _>(fun (ctx: TicketReceivedContext) -> System.Threading.Tasks.Task.CompletedTask)

let dAuth (options: OAuthOptions) =
    let discord = builder.Configuration.GetSection("Configuration:Discord").Get<DiscordConfiguration>()
    options.ClientId       <- discord.ClientId
    options.ClientSecret   <- discord.ClientSecret
    options.CallbackPath   <- PathString discord.CallBack
    options.SaveTokens     <- true
    options.CorrelationCookie.SameSite     <- SameSiteMode.Lax
    options.CorrelationCookie.SecurePolicy <- CookieSecurePolicy.Always
    options.Scope.Add("identify")
    options.Events <- OAuthEvents(OnTicketReceived = onTicketReceived)

open NetCord.Hosting.Gateway
open Conf

builder.Logging.AddSerilog(dispose = true) |> ignore
builder.Services.AddDiscordGateway() |> ignore
builder.Services.AddDistributedMemoryCache() |> ignore
builder.Services.AddSession(fun opt ->
    opt.IdleTimeout        <- TimeSpan.FromMinutes 10.0
    opt.Cookie.HttpOnly    <- true
    opt.Cookie.IsEssential <- true) |> ignore

// CORS – allow Fable dev server and production origin

let frontendOrigin =
    match Environment.GetEnvironmentVariable("frontendorigin") with
    | null -> "http://localhost:5173"
    | str -> str

let frontendUrl = frontendOrigin + "/"

builder.Services.AddCors(fun options ->
    options.AddDefaultPolicy(fun policy ->
        policy
            .WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials() // required for cookie auth
        |> ignore)) |> ignore

builder
    .Services
        .AddSingleton<SqliteWebUiStorage>()
        .AddSingleton<SqliteStorage>()
        .AddSingleton<RoundParticipantsService>()
        .AddHostedService(fun sp -> sp.GetRequiredService<RoundParticipantsService>())
        .AddSingleton<RoundStatusService>()
        .AddHostedService(fun sp -> sp.GetRequiredService<RoundStatusService>())
        .AddAuthorization()
        .AddAuthentication(fun options ->
            options.DefaultScheme          <- CookieAuthenticationDefaults.AuthenticationScheme
            options.DefaultChallengeScheme <- DiscordAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(fun opt ->
            opt.Cookie.Name        <- Cookie.Name
            opt.LoginPath          <- PathString "/auth/login/discord"
            opt.LogoutPath         <- PathString "/auth/logout"
            opt.ExpireTimeSpan     <- TimeSpan.FromDays 7.0
            opt.Cookie.SameSite    <- SameSiteMode.Lax
            opt.Cookie.SecurePolicy <- CookieSecurePolicy.Always
            opt.Cookie.MaxAge      <- TimeSpan.FromDays 7.0
            // Return 401 JSON instead of redirect for API consumers
            opt.Events <- CookieAuthenticationEvents(
                OnRedirectToLogin = fun ctx ->
                    ctx.Response.StatusCode <- 401
                    ctx.Response.ContentType <- "application/json"
                    System.Threading.Tasks.Task.CompletedTask))
        .AddDiscord(dAuth)
    |> ignore

builder.Services.AddOptions<Configuration>().BindConfiguration(nameof Configuration) |> ignore
builder.Services.AddOptions<DbConfiguration>().BindConfiguration("Configuration:Db") |> ignore
builder.Services.AddOptions<DiscordConfiguration>().BindConfiguration("Configuration:Discord") |> ignore

open DarkChampAscent.Api

let endpoints =
    [
        Pattern.Auth, meHandler
        Pattern.AuthLoginDiscord, loginDiscordHandler
        Pattern.AuthDiscordCallback, discordCallbackHandler frontendUrl
        Pattern.AuthLogin, loginCustomHandler
        Pattern.AuthRegister, regPostHandler
        Pattern.AuthLogout, logoutHandler
        Pattern.AuthWeb3Challenge, web3ChallengeHandler
        Pattern.AuthWeb3Login, loginWeb3Handler

        Pattern.Account, accountHandler
        Pattern.AccountNewWallet, registerNewWalletHandler

        Pattern.Battle, battleHandler
        Pattern.BattleJoin, joinBattleHandler
        Pattern.BattleParticipants, battleParticipantsHandler
        Pattern.BattleStatusInfo, battleRoundInfoHandler

        Pattern.Shop, shopHandler
        Pattern.ShopBuyItem, buyItemHandler
        Pattern.Storage, storageHandler
        Pattern.StorageUseItem, useItemHandler

        Pattern.Champs, myChampsHandler
        Pattern.ChampsUnderEffects, champsUnderEffectsHandler
        Pattern.ChampsRename, renameChampHandler
        Pattern.ChampsLevelUp, lvlUpHandler
        Pattern.ChampsRescan, rescanHandler
        Pattern.ChampsDetail None, champHandler

        Pattern.Monsters, myMonstersHandler
        Pattern.MonstersUnderEffects, monstersUnderEffectsHandler
        Pattern.MonstersRename, renameMonstrHandler
        Pattern.MonstersCreate, createMonsterHandler
        Pattern.MonstersDetail None, monstrHandler

        Pattern.Requests, myRequestsHandler

        Pattern.Donate, donateHandler

        Pattern.LeaderboardChamps, LeaderboardHandlers.champsHandler
        Pattern.LeaderboardMonsters, LeaderboardHandlers.monstersHandler
        Pattern.LeaderboardDonaters, LeaderboardHandlers.donatersHandler
        Pattern.LeaderboardUnknownDonaters, LeaderboardHandlers.unknownDonatersHandler

        Pattern.Home, homeHandler
        Pattern.Stats, statsHandler

        Pattern.CreateTx, TxHandlers.createTxHandler
        Pattern.SubmitTx, TxHandlers.submitTxHandler
        Pattern.VerifyTx, TxHandlers.verifyTxHandler
    ]
    |> List.map(fun (pattern, handler) ->
        let f = match pattern.Method with | Method.Get -> get | Method.Post -> post
        f pattern.Str handler)

let wapp = builder.Build()

wapp.UseForwardedHeaders(
    ForwardedHeadersOptions(
        ForwardedHeaders = (ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto))) |> ignore
wapp.UseHsts()        |> ignore
wapp.UseSession()     |> ignore
wapp.UseStaticFiles() |> ignore
wapp.UseCors()        |> ignore
wapp.UseRouting()     |> ignore
wapp.UseAuthentication() |> ignore
wapp.UseAuthorization()  |> ignore

wapp.UseFalco(endpoints).Run()