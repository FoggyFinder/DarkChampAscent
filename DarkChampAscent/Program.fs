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
                          db.GetUserMonstersCount dId, db.GetUserRequestsCount dId with
                    | Ok wallets, Some champs, Some monsters, Some requests ->
                        let dcPrice = db.GetNumKey Db.DbKeysNum.DarkCoinPrice
                        let dto = AccountDTO(UserAccount(a, wallets, int champs, int monsters, int requests), dcPrice)
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
open Conf
open Algorand.Utils
open Algorand.Algod.Model.Transactions

[<RequireQualifiedAccess>]
module SSEHelper =
    let handler (signal:Utils.IReadOnlySignal<'a>) (apply:('a -> 'a) option): HttpHandler =
        fun ctx ->
            task {
                let res = ctx.Response
                res.Headers["Content-Type"] <- "text/event-stream"
                res.Headers["Cache-Control"] <- "no-cache"
                res.Headers["Connection"] <- "keep-alive"
                res.Headers["X-Accel-Buffering"] <- "no"
                do! res.Body.FlushAsync()
                let ct = ctx.RequestAborted

                let update v =
                    task {
                        let v' = match apply with | Some f -> f v | None -> v
                        let json = JsonSerializer.Serialize(v', options)
                        let msg = $"data: {json}\n\n"
                        let bytes = System.Text.Encoding.UTF8.GetBytes(msg)
                        do! res.Body.WriteAsync(bytes, 0, bytes.Length, ct)
                        do! res.Body.FlushAsync()
                    }
                do! update(signal.Value)
                let d = signal.Publish.Subscribe (fun v -> update v |> ignore)
                while not ct.IsCancellationRequested do
                    try
                        do! Task.Delay(TimeSpan.FromMinutes(1.), ct)
                    with
                    | :? OperationCanceledException -> ()
                    | ex -> Log.Error("sseHandler:" + ex.ToString())
                d.Dispose()
            }

let battleParticipantsHandler : HttpHandler =
    fun ctx ->
        task {
            let s = ctx.Plug<BattleService>()
            return! SSEHelper.handler s.RoundParticipants None ctx
        }

let battleRoundInfoHandler : HttpHandler =
    fun ctx ->
        task {
            let s = ctx.Plug<BattleService>()
            return! SSEHelper.handler s.RoundStatus None ctx
        }

let battleInfoHandler : HttpHandler =
    fun ctx ->
        task {
            let s = ctx.Plug<BattleService>()
            let apply =
                Some(Option.map(fun (bi:BattleInfoDTO) ->
                    bi.WithMonsterImg (FileUtils.mapToLocalImg bi.CurrentBattleInfo.Monster.Picture)))
            return! SSEHelper.handler s.BattleStatus apply ctx
        }

let battleHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let db = ctx.Plug<SqliteStorage>()
            let response = 
                match ao with
                | Some user ->
                    db.GetAvailableUserChamps user.ID
                    |> Result.map Some
                | None -> Ok (Some([]))
                |> apiResult
            
            return! response ctx
        }

let joinBattleHandler : HttpHandler =
    fun ctx ->
        task {
            let! ao = authenticate ctx
            let bs = ctx.Plug<BattleService>()
            match ao with
            | Some a ->
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
                    | Ok champId, Ok move -> bs.JoinRound(a.ID, { ChampId = champId; Move = move } )
                    | Error err1, Error err2 -> Error(err1 + Environment.NewLine + err2)
                    | Error err, _ | _, Error err -> Error err
                return! apiResult response ctx
            | None ->
                return! apiUnauthorized ctx
        }

let shopHandler : HttpHandler =
    fun ctx ->
        task {
            let db = ctx.Plug<SqliteStorage>()
            let response =
                match db.GetShopItems(), db.GetNumKey Db.DbKeysNum.DarkCoinPrice with
                | Some items, Some price ->
                    ShopDTO(items, price)
                    |> apiOk 
                | _ -> apiError "Shop unavailable" 500
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
                        db.UseItemFromStorage(user.ID, si, cid) |> apiResult
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
            let belongsToAUser =
                ao |> Option.bind (fun u ->
                    let uId = u.ID
                    db.ChampBelongsToAUser(uint64 champId, uId))
                |> Option.defaultValue false
            let response =
                match db.GetChampInfoById champId, db.GetNumKey Db.DbKeysNum.DarkCoinPrice with
                | Some champ, Some price ->
                    ChampDTO(champ, belongsToAUser, price) |> apiOk
                | _ -> apiNotFound
            return! response ctx
        }

let champsNamesHandler : HttpHandler =
    fun ctx ->
        task {
            let db = ctx.Plug<SqliteStorage>()
            let response = db.GetChampsNames() |> apiOk
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
                        | Some true -> db.LevelUp(cId, ch)
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

let champsDefeatedHandler : HttpHandler =
    fun ctx ->
        task {
            let response =
                let db = ctx.Plug<SqliteStorage>()
                match db.GetLastRoundId() with
                | Some roundId ->
                    match db.GetDefeatedChamps roundId with
                    | Ok champs -> apiOk champs
                    | Error e   -> apiError (string e) 500
                | None -> apiError "No round found" 404
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
                        let monsters' =
                            monsters
                            |> List.map(fun m -> m.WithMonsterImg (FileUtils.mapToLocalImg m.Pic))
                        UserMonstersDTO(monsters', dcPrice) |> apiOk
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

let monstersDefeatedHandler : HttpHandler =
    fun ctx ->
        task {
            let db  = ctx.Plug<SqliteStorage>()
            let response =
                match db.GetLastRoundId() with
                | Some roundId ->
                    match db.GetDefeatedMonsters roundId with
                    | Ok monsters ->
                        monsters
                        |> List.map(fun m -> m.WithMonsterImg(FileUtils.mapToLocalImg m.Pic))
                        |> apiOk
                    | Error e -> apiError (string e) 500
                | _ -> apiError "No round" 404
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
                    match db.GetTopDonaters(), db.GetLatestDonations() with
                    | Ok xs, Ok latest ->
                        task {
                            let newIds =
                                let xs1 = 
                                    xs |> List.choose (fun d ->
                                        match d.Donater with
                                        | Donater.Discord dId ->
                                            let id' = uint64 dId
                                            if names.ContainsKey id' then None else Some id'
                                        | _ -> None)
                                    |> Set.ofList
                                let xs2 = 
                                    latest |> List.choose (fun d ->
                                        match d.Donater with
                                        | Donater.Discord dId ->
                                            let id' = uint64 dId
                                            if names.ContainsKey id' then None else Some id'
                                        | _ -> None)
                                    |> Set.ofList
                                xs1 |> Set.union xs2
                            let! us = Task.WhenAll([ for dId in newIds -> task { return! client.GetUserAsync dId } ])
                            us |> Array.iter (fun u -> names.TryAdd(u.Id, u.GlobalName) |> ignore)
                            let leaderboard =
                                xs |> List.map (fun d ->
                                    let name =
                                        match d.Donater with
                                        | Donater.Discord dId ->
                                            match names.TryGetValue(uint64 dId) with
                                            | true, n -> n
                                            | _ -> string dId
                                        | Donater.Unknown wallet  -> wallet
                                        | Donater.Custom (_, n)   -> n
                                    DonationDTO(name, d.Amount))
                            let latest' =
                                latest |> List.map (fun d ->
                                    let name =
                                        match d.Donater with
                                        | Donater.Discord dId ->
                                            match names.TryGetValue(uint64 dId) with
                                            | true, n -> n
                                            | _ -> string dId
                                        | Donater.Unknown wallet  -> wallet
                                        | Donater.Custom (_, n)   -> n
                                    LatestDonationDTO(name, d.Amount, d.Tx))
                            return apiOk (TopDonatersDTO(leaderboard, latest'))
                        }
                    | _ -> task { return apiError "Could not fetch donaters" 500 }
                return! response ctx
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
                            let db = ctx.Plug<SqliteStorage>()
                            let isValid = db.IsTxValid(user.ID, tx)
                            match isValid with
                            | Ok () ->
                                match tx with
                                // special case
                                | Tx.Confirm (wallet, code) -> 
                                    if user.IsWeb3 then
                                        return Error "Not supported"
                                    else
                                        let! txnB64 = Blockchain.getTxB64(wallet, $"confirm:{code}")
                                        return Ok txnB64
                                | _ ->
                                    let amountO =
                                        match tx with
                                        | Tx.Donate (_, amount) -> Some amount
                                        | Tx.Confirm (_, _) -> None
                                        | Tx.BuyItem (_, item, amount) ->
                                            db.GetNumKey Db.DbKeysNum.DarkCoinPrice
                                            |> Option.map(fun dcPrice ->
                                                let price = Math.Round(Shop.getPrice item / dcPrice, 6)
                                                Math.Round(decimal amount * price, 6))
                                        | Tx.RenameChamp (_, _, _) ->
                                            db.GetNumKey Db.DbKeysNum.DarkCoinPrice
                                            |> Option.map(fun dcPrice -> Math.Round(Shop.RenamePrice / dcPrice, 6))
                                        | Tx.CreateCustomMonster (_, _, _) ->
                                            db.GetNumKey Db.DbKeysNum.DarkCoinPrice
                                            |> Option.map(fun dcPrice -> Math.Round(Shop.GenMonsterPrice / dcPrice, 6))
                                    match amountO with
                                    | Some amount ->
                                        let wallet = builder.Configuration.GetSection("Configuration:Wallet").Get<WalletConfiguration>()
                                        let! tx = BlockchainUtils.createTx tx.Wallet wallet.GameWallet amount tx.Note
                                        return Ok tx
                                    | None ->
                                        return Error ("Unexpected error, try again later")
                            | Error err -> return Error err
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
                        let txR =
                            match ctx.Request.Form.tryGetFormValue "tx" with
                            | Some s ->
                                try
                                    let tx = JsonSerializer.Deserialize<Tx>(s.ToString(), opt)
                                    Ok tx
                                with ex ->
                                    Error (ex.ToString())
                            | None -> Error "missing tx field"
                        let db = ctx.Plug<SqliteStorage>()
                        match txR with
                        | Ok tx ->
                            match ctx.Request.Form.tryGetFormValue "signedTxnB64" with
                            | Some s ->
                                let txnB64 = s.ToString()
                                let signedTxnBytes = Convert.FromBase64String(txnB64)
                                let signedTx = Encoder.DecodeFromMsgPack<SignedTransaction>(signedTxnBytes)
                                let amount =
                                    match signedTx.Tx with
                                    | :? AssetTransferTransaction as att ->
                                        decimal att.AssetAmount / Algo6Decimals
                                    | _ ->
                                        Log.Information($"{signedTx.Tx.GetType()}")
                                        0M
                                let isValid =
                                    match tx with
                                    | Tx.Confirm _ -> Ok (())
                                    | _ ->
                                        db.IsTxValid(user.ID, tx, amount)
                                match isValid with
                                | Ok () ->
                                    let! txstatus = Blockchain.sendTX64 txnB64
                                    
                                    match txstatus with
                                    | TxStatus.Confirmed(txId, _) ->
                                        
                                        let ptx =
                                            ParsedTx(txId, signedTx.Tx.Sender.EncodeAsString(), amount, 
                                                System.Text.Encoding.UTF8.GetString signedTx.Tx.Note, Some tx)
                                        let! b =
                                            Helpers.retry (fun () -> db.ProcessParsedValidTx ptx |> Result.isOk)
                                                5 (TimeSpan.FromSeconds(5.))
                                        if b then
                                            match tx with
                                            | Tx.Confirm _ 
                                            | Tx.CreateCustomMonster _
                                            | Tx.RenameChamp _
                                            | Tx.BuyItem _ -> ()
                                            | Tx.Donate (_, _) ->
                                                let sender =
                                                    match user with
                                                    | Account.Discord du -> DUtils.mention du.DiscordId
                                                    | Account.Custom cu -> cu.Nickname
                                                    | Account.Web3 w3 ->
                                                        match db.FindDiscordIdByWallet w3.Wallet with
                                                        | Some dId -> DUtils.mention (uint64 dId)
                                                        | None -> w3.Wallet
                                                let sendMsg() = task {
                                                    let uri = $"https://allo.info/tx/{txId}"
                                                    let card = Components.donationCard amount sender (Some uri)
                                                    let newDonationMessage =
                                                            MessageProperties()
                                                                .WithComponents([ card ])
                                                                .WithFlags(MessageFlags.IsComponentsV2)
                                                                .WithAllowedMentions(AllowedMentionsProperties.None)
                                                    let gclient = ctx.Plug<GatewayClient>()
                                                    do! DUtils.sendMsgToLogChannel gclient newDonationMessage
                                                }
                                                sendMsg() |> ignore

                                            let msg =
                                                match tx with
                                                | Tx.Confirm _ -> "Done!"
                                                | Tx.CreateCustomMonster _ ->
                                                    "Request created. We use external AIPG API so don't have full control over it. Processing may take up to 5-10 minutes. You can track progress on 'My Requests' page."
                                                | Tx.RenameChamp _ -> "Done!"
                                                | Tx.BuyItem _ -> "Done! You can check 'Storage' to see all available items"
                                                | Tx.Donate (_, _) -> "Thank you!"
                                            return Ok $"{msg} TxId: {txId}"
                                        else
                                            return Error $"Tx ({txId}) was confirmed but unexpected error occured while processing"
                                    | TxStatus.Unconfirmed tx -> return Error ($"Sorry, tx {tx} was recorded but not confirmed in time. Please try again later. Current one will be refunded within 24 hrs.")
                                    | TxStatus.Error err -> return Error (err.ToString())
                                | Error err ->
                                    return Error err
                            | None -> return Error "Missing tx"
                        | Error err -> return Error err
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
                                            DUtils.addDiscordRole client (uint64 discordId)
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
            let db = ctx.Plug<SqliteStorage>()
            let wallet = builder.Configuration.GetSection("Configuration:Wallet").Get<WalletConfiguration>()
            
            let response =
                match db.GetStats() with
                | Some (gs, pw, rewards) -> 
                    let ts =
                        TStats(
                            rewards.TryFind WalletType.Burn |> Option.map(fun v ->
                                WalletValue(wallet.BurnWallet, v)),
                            rewards.TryFind WalletType.DAO |> Option.map(fun v ->
                                WalletValue(wallet.DAOWallet, v)),
                            rewards.TryFind WalletType.Reserve |> Option.map(fun v ->
                                WalletValue(wallet.ReserveWallet, v)),
                            rewards.TryFind WalletType.Dev |> Option.map(fun v ->
                                WalletValue(wallet.DevsWallet, v)),
                            rewards.TryFind WalletType.Staking |> Option.map(fun v ->
                                WalletValue(wallet.StakingWallet, v)))
                    Stats(gs, ts, pw) |> apiOk
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
open Microsoft.Extensions.Hosting
open NetCord.Hosting.Services.ApplicationCommands
open NetCord.Hosting.Services.ComponentInteractions
open NetCord.Services.ComponentInteractions

builder.Logging.AddSerilog(dispose = true) |> ignore
builder.Services
    .AddDiscordGateway(fun options ->
        options.Intents <- GatewayIntents.GuildMessages
                          ||| GatewayIntents.GuildMessageReactions
                          ||| GatewayIntents.Guilds
                          ||| GatewayIntents.GuildUsers
                          ||| GatewayIntents.GuildPresences
                          )
    .AddApplicationCommands()
    .AddGatewayHandlers(typeof<DiscordBot.GuildCreateHandler>.Assembly)
    .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>()
    //.AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
    //.AddComponentInteractions<ModalInteraction, ModalInteractionContext>() 
    |> ignore

builder.Services.AddDistributedMemoryCache() |> ignore
builder.Services.AddSession(fun opt ->
    opt.IdleTimeout        <- TimeSpan.FromMinutes 10.0
    opt.Cookie.HttpOnly    <- true
    opt.Cookie.IsEssential <- true) |> ignore

// CORS – allow Fable dev server and production origin

let frontendOrigin =
    match Environment.GetEnvironmentVariable "frontendorigin" with
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
        .AddSingleton<SqliteStorage>()
        .AddHostedService<UpdatePriceService>()
        .AddSingleton<TxTrackerService>()
        .AddHostedService<TrackChampCfgService>()
        .AddSingleton<BattleService>()
        .AddHostedService(fun sp -> sp.GetRequiredService<BattleService>())
        .AddHostedService<BackupService>()
        .AddHostedService<GenService>()
        .AddHostedService<RefundInvalidTxService>()
        .AddHostedService<RefundFailedGenService>()
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
builder.Services.AddOptions<DiscordConfiguration>().BindConfiguration("Configuration:Discord") |> ignore
builder.Services.AddOptions<WalletConfiguration>().BindConfiguration("Configuration:Wallet") |> ignore
builder.Services.AddOptions<ChainConfiguration>().BindConfiguration("Configuration:Chain") |> ignore
builder.Services.AddOptions<DbConfiguration>().BindConfiguration("Configuration:Db") |> ignore
builder.Services.AddOptions<GenConfiguration>().BindConfiguration("Configuration:Gen") |> ignore
builder.Services.AddOptions<BackupConfiguration>().BindConfiguration("Configuration:Backup") |> ignore

open DarkChampAscent.Api
open DiscordBot.Commands

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
        Pattern.BattleRoundStatusInfo, battleRoundInfoHandler
        Pattern.BattleStatusInfo, battleInfoHandler

        Pattern.Shop, shopHandler
        Pattern.Storage, storageHandler
        Pattern.StorageUseItem, useItemHandler

        Pattern.Champs, myChampsHandler
        Pattern.ChampsUnderEffects, champsUnderEffectsHandler
        Pattern.ChampsDefeated, champsDefeatedHandler
        Pattern.ChampsLevelUp, lvlUpHandler
        Pattern.ChampsRescan, rescanHandler
        Pattern.ChampsDetail None, champHandler
        Pattern.ChampsNames, champsNamesHandler

        Pattern.Monsters, myMonstersHandler
        Pattern.MonstersDefeated, monstersDefeatedHandler
        Pattern.MonstersRename, renameMonstrHandler
        Pattern.MonstersDetail None, monstrHandler

        Pattern.Requests, myRequestsHandler

        Pattern.LeaderboardChamps, LeaderboardHandlers.champsHandler
        Pattern.LeaderboardMonsters, LeaderboardHandlers.monstersHandler
        Pattern.LeaderboardDonaters, LeaderboardHandlers.donatersHandler

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
let host = wapp

host
    .AddApplicationCommandModule(typeof<WalletModule>)
    .AddApplicationCommandModule(typeof<UserModule>)
    .AddApplicationCommandModule(typeof<BattleModule>)
    .AddApplicationCommandModule(typeof<GeneralModule>)
    .AddComponentInteraction<StringMenuInteractionContext>("actionselect", Func<_,_,_,_>(Interactions.actionselect)) |> ignore

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