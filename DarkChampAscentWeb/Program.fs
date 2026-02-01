// https://www.falcoframework.com/docs/get-started.html
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
open Falco.Markup
open GameLogic.Champs
open GameLogic.Battle
open NetCord
open DiscordBot
open NetCord.Rest
open Serilog
open NetCord.Gateway
open UI
open GameLogic.Shop
open Types
open DarkChampAscent.Account

Log.Logger <-
    (new LoggerConfiguration())
          .Enrich.FromLogContext()
          .WriteTo.Console()
          .WriteTo.File("log.txt", rollingInterval=RollingInterval.Month)
          .CreateLogger()

[<RequireQualifiedAccess>]
module Cookie =
    let name = "DiscordAuth"

let builder = WebApplication.CreateBuilder()

let loginHandler : HttpHandler =
    fun ctx ->
        let props = AuthenticationProperties()
        props.RedirectUri <- Route.account
        props.IsPersistent <- true
        ctx.ChallengeAsync(DiscordAuthenticationDefaults.AuthenticationScheme, props)
    //fun ctx -> task {
    //    let handleAuth (ctx:HttpContext) : HttpHandler =
    //        Response.ofPlainText "hello authenticated user"

    //    return Request.ifAuthenticated "Discord" (handleAuth ctx)
    //}

let getDiscordUser (result:AuthenticateResult) =
    if result.Succeeded then
        let claims = result.Principal.Claims |> Seq.toList
        let nameO =
            claims
            |> List.tryPick(fun claim ->
                match claim.Type with
                | "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" -> Some claim.Value
                | _ -> None)
        let idO =
            claims
            |> List.tryPick(fun claim ->
                match claim.Type with
                | "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ->
                    match UInt64.TryParse claim.Value with
                    | true, v -> Some v
                    | false, _ -> None
                | _ -> None
            )
        let picO =
            claims
            |> List.tryPick(fun claim ->
                match claim.Type with
                | "urn:discord:avatar:hash" -> Some claim.Value
                | _ -> None)

        match idO with
        | Some id ->
            let name' = nameO |> Option.defaultValue "User"
            DiscordUser(name', id, picO) |> Some
        | None ->
            Log.Error("Missing id. Claims")
            claims |> Seq.iter(fun claim -> Log.Error($"{claim.Type} = {claim.Value}"))
            None
        |> Some
    else None

let tryGetFromForm (collection:IFormCollection) (key:string) =
    match collection.TryGetValue key with
    | true, s -> Some s
    | false, _ -> None

let accountHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let dUser = getDiscordUser result
            let html =
                match dUser with
                | Some opt ->
                    match opt with
                    | Some du ->
                        let db = ctx.Plug<SqliteStorage>()
                        let dId = UserId.Discord du.DiscordId
                        // a user may not be registered with discord bot
                        db.TryRegisterDiscordUser du.DiscordId |> ignore
                        match db.GetUserWallets dId, db.GetUserChampsCount dId,
                            db.GetUserMonstersCount dId, db.GetUserBalance dId with
                        | Ok ar, Some champs, Some monsters, Some balance ->
                            let wallets = ar |> List.map(fun ar -> Wallet(ar.Wallet, ar.IsConfirmed, ar.Code))
                            let ua = UserAccount(DarkChampAscent.Account.Account.Discord du, wallets, balance, int champs, int monsters)
                            let dcprice = db.GetNumKey(Db.DbKeysNum.DarkCoinPrice)
                            AccountView.accountView ua dcprice
                        | _ -> Ui.unError "Can't fetch user data, please try again later"
                    | None -> Ui.incompleteResponseError
                | None ->
                    Elem.main [
                        Attr.class' "dashboard"
                        Attr.role "main"
                        XmlAttribute.KeyValueAttr("aria-label", "Account dashboard")
                    ] [
                        Text.raw "Log-in to view account details"
                        Elem.form [
                            Attr.methodGet
                            Attr.action Route.login
                        ] [
                            Elem.input [
                                Attr.class' "btn-primary"
                                Attr.typeSubmit
                                Attr.value "Login"
                            ]
                        ]
                    ]
                |> Ui.layout "Account" dUser.IsSome

            let response = Response.ofHtml html ctx

            return! response
        }

let battleHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let dUser = getDiscordUser result
            let rdb = ctx.Plug<SqliteWebUiStorage>()
            let db = ctx.Plug<SqliteStorage>()
            let currentBattle = rdb.GetCurrentBattleInfo()
            let champsAtRound = rdb.GetLastRoundParticipants()
            let userView =
                match dUser with
                | Some userO ->
                    match userO with
                    | Some user ->
                        let roundInfo = 
                            match db.GetLastRoundId() with
                            | Some roundId ->
                                match db.GetRoundStatus roundId with
                                | Some status ->
                                    (status,
                                        match status with
                                        | RoundStatus.Started ->
                                            match db.GetRoundTimestamp(roundId) with
                                            | Some roundStared -> Some roundStared
                                            | None -> None
                                        | RoundStatus.Processing -> None
                                        | RoundStatus.Finished -> None)
                                    |> Some
                                | None -> None
                            | None -> None
                        let hasPlayers = champsAtRound |> Result.map(fun xs -> xs.IsEmpty |> not) |> Result.defaultValue false
                        db.GetAvailableUserChamps(UserId.Discord user.DiscordId)
                        |> BattleView.joinBattle hasPlayers roundInfo
                    | None -> Ui.incompleteResponseError
                | None ->
                    Elem.div [ ] [
                        Text.raw "Sign-in to join battle"
                        Elem.form [
                            Attr.methodGet
                            Attr.action Route.login
                        ] [
                    
                            Elem.input [
                                Attr.class' "btn btn-primary"
                                Attr.typeSubmit
                                Attr.value "Login"
                            ]
                        ]
                    ]

            let view =
                let history =
                    match currentBattle with
                    | Ok cbo -> rdb.GetBattleHistory(cbo.BattleNum, cbo.Monster.Name)
                    | Error _ -> Error("Unknown error")
                
                BattleView.battleView userView currentBattle history champsAtRound

            let response =
                view
                |> Ui.layout "Battle" dUser.IsSome
                |> fun html -> Response.ofHtml html ctx

            return! response
        }

let joinBattleHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let rdb = ctx.Plug<SqliteWebUiStorage>()
            
            do!
                if result.Succeeded then
                    let champIdO =
                        match ctx.Request.Form.TryGetValue("champ") with
                        | true, s ->
                            match s |> UInt64.TryParse with
                            | true, v -> Some v
                            | false, _ -> None
                        | false, _ -> None
                    let moveO =
                        match ctx.Request.Form.TryGetValue("move") with
                        | true, s ->
                            match s |> Enum.TryParse<Move> with
                            | true, v -> Some v
                            | false, _ -> None
                        | false, _ -> None
                    task {
                        match champIdO, moveO with
                        | Some champId, Some move ->
                            let rar = { ChampId = champId; Move = move }
                            let! _ =
                                match rdb.PerformAction rar with
                                | Ok _ -> 
                                    task {
                                        let client = ctx.Plug<GatewayClient>()
                                        let name, ipfs = rdb.GetChampNameIPFSById rar.ChampId |> Option.defaultValue("", "")
                                        let joinedRoundComponent =
                                            ComponentContainerProperties([
                                                ComponentSectionProperties
                                                    (ComponentSectionThumbnailProperties(
                                                        ComponentMediaProperties($"https://ipfs.dark-coin.io/ipfs/{ipfs}")),
                                                    [
                                                        TextDisplayProperties($"**{name}**")
                                                        TextDisplayProperties("joined round!")
                                                    ])
                                            ])
                                        let mp = MessageProperties().WithComponents([ joinedRoundComponent ]).WithFlags(MessageFlags.IsComponentsV2)
                                    
                                        Utils.sendMsgToLogChannel client mp |> ignore
                                        return ()
                                    }
                                | _ -> task { return () }
                            ()
                        | _, _ ->  ()
                    }
                else
                    task { return () }
            let response = Response.redirectPermanently Route.battle ctx

            return! response
        }

let registerNewWalletHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let dUser = getDiscordUser result
            
            let db = ctx.Plug<SqliteStorage>()
            let route =
                match dUser with
                | Some userO ->
                    match userO with
                    | Some user ->
                        match ctx.Request.Form.TryGetValue("wallet") with
                        | true, s ->
                            let wallet = s.ToString().Trim()
                            if Blockchain.isValidAddress wallet then
                                db.RegisterNewWallet(UserId.Discord user.DiscordId, wallet) |> ignore
                            else
                                try
                                    Log.Error($"{user.DiscordId} attempts to register invalid {wallet} address")
                                with exn ->
                                    Log.Error(exn, $"attempt to register {wallet} address")
                                ()
                        | false, _ -> ()
                    | None -> ()
                    Route.account
                | None -> Route.login
            let response = Response.redirectPermanently route ctx

            return! response
        }

let shopHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let user = getDiscordUser result
            let view =
                let db = ctx.Plug<SqliteStorage>()
                match db.GetShopItems(), db.GetNumKey(Db.DbKeysNum.DarkCoinPrice) with
                | Some items, Some price ->
                    let userBalance =
                        match user with
                        | Some user -> user |> Option.bind(fun u -> UserId.Discord u.DiscordId |> db.GetUserBalance)
                        | None -> None
                    items
                    |> List.map(fun item -> Display.ShopItemRow(item, price))
                    |> ShopView.shop userBalance
                | _ -> Ui.defError
            let response =
                view
                |> Ui.layout "Shop" user.IsSome
                |> fun html -> Response.ofHtml html ctx

            return! response
        }

let buyItemHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let db = ctx.Plug<SqliteStorage>()
            
            let route =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        match ctx.Request.Form.TryGetValue("shopitem") with
                        | true, s ->
                            match s |> Enum.TryParse<ShopItem> with
                            | true, v ->
                                match db.BuyItem(UserId.Discord user.DiscordId, v, 1) with
                                | Ok _ -> Route.storage
                                | Error _ -> Route.error
                            | false, _ -> Route.error
                        | false, _ -> Route.error
                    | None -> Route.error
                | None -> Route.login

            let response = Response.redirectPermanently route ctx

            return! response
        }

let storageHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let dUser = getDiscordUser result
            let response =
                match getDiscordUser result with
                | Some userO ->
                    let db = ctx.Plug<SqliteStorage>()
                    match userO with
                    | Some user ->
                        let uId = UserId.Discord user.DiscordId
                        match db.GetUserStorage uId, db.GetUserChamps uId with
                        | Some items, Some champs ->
                            champs
                            |> List.map(fun ar -> uint64 ar.ID, ar.Name, ar.Ipfs)
                            |> ShopView.storage items
                        | _ -> Ui.defError
                    | _ -> Ui.incompleteResponseError
                    |> Ui.layout "Storage" dUser.IsSome
                    |> fun html -> Response.ofHtml html ctx
                | _ -> Response.redirectPermanently Route.login ctx

            return! response
        }

let useItemHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let db = ctx.Plug<SqliteStorage>()
            
            let route =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        let shopItem =
                            match ctx.Request.Form.TryGetValue("useitem") with
                            | true, s ->
                                match s |> Enum.TryParse<ShopItem> with
                                | true, v -> Some v
                                | false, _ -> None
                            | false, _ -> None
                        let champId =
                            match ctx.Request.Form.TryGetValue("champ") with
                            | true, s ->
                                match s |> UInt64.TryParse with
                                | true, v -> Some v
                                | false, _ -> None
                            | false, _ -> None
                        match shopItem, champId with
                        | Some si, Some cid ->
                            match db.UseItemFromStorage(UserId.Discord user.DiscordId, si, cid) with
                            | Ok _ -> ()
                            | Error _ -> ()
                        | _ -> ()
                    | None -> ()
                    Route.storage
                | None -> Route.login

            let response = Response.redirectPermanently route ctx

            return! response
        }


let myMonstersHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let response =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        let db = ctx.Plug<SqliteStorage>()
                        let uId = UserId.Discord user.DiscordId
                        match db.GetUserMonsters uId, db.GetNumKey(Db.DbKeysNum.DarkCoinPrice) with
                        | Ok monsters, Some dcPrice ->
                            let userBalance = db.GetUserBalance uId
                            MonsterView.monsters monsters userBalance dcPrice
                        | _ -> Ui.defError
                    | _ -> Ui.incompleteResponseError
                    |> Ui.layout "Monsters" true
                    |> fun html -> Response.ofHtml html ctx
                | _ -> Response.redirectPermanently Route.login ctx

            return! response
        }

let myChampsHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let response =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        let db = ctx.Plug<SqliteStorage>()
                        match UserId.Discord user.DiscordId |> db.GetUserChampsInfo with
                        | Some champs -> ChampView.champs champs
                        | _ -> Ui.defError
                    | _ -> Ui.incompleteResponseError
                    |> Ui.layout "Champs" true
                    |> fun html -> Response.ofHtml html ctx
                | _ -> Response.redirectPermanently Route.login ctx

            return! response
        }

let champHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let dUser = getDiscordUser result 
            let isAuth = dUser |> Option.isSome
            let route = Request.getRoute ctx
            let champId = route.GetInt64 "id"
            let view =
                let db = ctx.Plug<SqliteStorage>()
                let userBalance =
                    dUser |> Option.bind(fun user ->
                        user |> Option.bind(fun u ->
                            let uId = UserId.Discord u.DiscordId
                            db.ChampBelongsToAUser(uint64 champId, uId)
                            |> Option.bind(fun b -> 
                                if b then db.GetUserBalance uId
                                else None
                            )))
                    
                match db.GetChampInfoById champId, db.GetNumKey(Db.DbKeysNum.DarkCoinPrice) with
                | Some champ, Some price -> ChampView.champInfo champ userBalance price
                | _ -> Ui.defError

            let response =
                view
                |> Ui.layout "Champ" isAuth
                |> fun html -> Response.ofHtml html ctx

            return! response
        }

let monstrHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let dUser = getDiscordUser result 
            let isAuth = dUser |> Option.isSome
            let route = Request.getRoute ctx
            let mId = route.GetInt64 "id"
            let view =
                let db = ctx.Plug<SqliteStorage>()
                let isUserOwnedMonstr =
                    dUser
                    |> Option.bind(fun user ->
                        user 
                        |> Option.bind(fun u -> db.MonsterBelongsToAUser(uint64 mId, UserId.Discord u.DiscordId)))
                    |> Option.defaultValue false
                match db.GetMonsterById mId with
                | Some monstr -> MonsterView.monstrInfo (uint64 mId) monstr isUserOwnedMonstr
                | _ -> Ui.defError

            let response =
                view
                |> Ui.layout "Monster" isAuth
                |> fun html -> Response.ofHtml html ctx

            return! response
        }

let renameMonstrHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let db = ctx.Plug<SqliteStorage>()
            
            let route =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        let mIdO =
                            tryGetFromForm ctx.Request.Form "mnstrid"
                            |> Option.bind(fun s ->
                                match s |> UInt64.TryParse with
                                | true, v -> Some v
                                | false, _ -> None)
                        let mnstrnameO =
                            tryGetFromForm ctx.Request.Form "mnstrname"
                            |> Option.map(fun s -> s.ToString())
                        match mIdO, mnstrnameO with
                        | Some mId, Some newName ->
                            let uId = UserId.Discord user.DiscordId
                            if db.MonsterBelongsToAUser(mId, uId) |> Option.defaultValue false then
                                match db.RenameUserMonster(uId, newName, int64 mId) with
                                | Ok _ -> Uri.monstr mId
                                | Error _ -> Route.error
                            else Route.error
                        | _, _ -> Route.error
                    | None -> Route.error
                | None -> Route.login

            let response = Response.redirectPermanently route ctx
            return! response
        }

let renameChampHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let db = ctx.Plug<SqliteStorage>()
            
            let route =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        let oldNameO =
                            tryGetFromForm ctx.Request.Form "oldname"
                            |> Option.map(fun s -> s.ToString())
                        let newNameO =
                            tryGetFromForm ctx.Request.Form "newname"
                            |> Option.map(fun s -> s.ToString())
                        let chmpIdO =
                            tryGetFromForm ctx.Request.Form "chmpId"
                            |> Option.bind(fun s ->
                                match s |> UInt64.TryParse with
                                | true, v -> Some v
                                | false, _ -> None)
                        match oldNameO, newNameO, chmpIdO with
                        | Some oldName, Some newName, Some cId ->
                            match db.RenameChamp(UserId.Discord user.DiscordId, oldName, newName) with
                            | Ok _ -> Uri.champ cId
                            | Error _ -> Route.error
                        | _ -> Route.error
                    | None -> Route.error
                | None -> Route.login

            let response = Response.redirectPermanently route ctx
            return! response
        }

let myRequestsHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let dUser = getDiscordUser result
            let response =
                match getDiscordUser result with
                | Some userO ->
                    let db = ctx.Plug<SqliteStorage>()
                    match userO with
                    | Some user ->
                        match UserId.Discord user.DiscordId |> db.GetPendingUserRequests with
                        | Ok requests -> GenView.myRequests requests
                        | _ -> Ui.defError
                    | _ -> Ui.defError
                    |> Ui.layout "Champs" dUser.IsSome
                    |> fun html -> Response.ofHtml html ctx
                | _ -> Response.redirectPermanently Route.login ctx

            return! response
        }

let champsUnderEffectsHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let response =
                match getDiscordUser result with
                | Some userO ->
                    let db = ctx.Plug<SqliteStorage>()
                    match userO with
                    | Some user ->
                        match db.GetLastRoundId() with
                        | Some roundId ->
                            match db.GetUserChampsUnderEffect(UserId.Discord user.DiscordId, roundId) with
                            | Ok champs -> ChampView.champsUnderEffects champs
                            | _ -> Ui.defError
                        | None -> Ui.defError
                    | _ -> Ui.defError
                    |> Ui.layout "Champs under effects" true
                    |> fun html -> Response.ofHtml html ctx
                | _ -> Response.redirectPermanently Route.login ctx

            return! response
        }

let monstersUnderEffectsHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let isAuth = getDiscordUser result |> Option.isSome
            let db = ctx.Plug<SqliteStorage>()
            let view =
                match db.GetLastRoundId() with
                | Some roundId ->
                    match db.GetMonstersUnderEffect(roundId) with
                    | Ok monsters -> MonsterView.monstersUnderEffects monsters
                    | _ -> Ui.defError
                | _ -> Ui.defError

            let response =
                view
                |> Ui.layout "Monsters under effects" isAuth
                |> fun html -> Response.ofHtml html ctx

            return! response
        }

open DiscordBot.Components
open GameLogic.Monsters

let donateHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let db = ctx.Plug<SqliteStorage>()
            
            let route =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        match ctx.Request.Form.TryGetValue("amount") with
                        | true, s ->
                            match s |> Decimal.TryParse with
                            | true, v ->
                                match db.Donate(UserId.Discord user.DiscordId, v) with
                                | Ok _ ->
                                    let client = ctx.Plug<GatewayClient>()
                                    let card = donationCard v user.DiscordId
                                    let newInGameDonationMessage =
                                        MessageProperties()
                                            .WithComponents([ card ])
                                            .WithFlags(MessageFlags.IsComponentsV2)
                                            .WithAllowedMentions(AllowedMentionsProperties.None)

                                    Utils.sendMsgToLogChannel client newInGameDonationMessage |> ignore
                                    Route.topDonaters
                                | Error _ -> Route.account
                            | false, _ -> Route.account
                        | false, _ -> Route.account
                    | None -> Route.account
                | None -> Route.login

            let response = Response.redirectPermanently route ctx

            return! response
        }

let createMonsterHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let db = ctx.Plug<SqliteStorage>()
            
            let route =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        let mtypeO = 
                            tryGetFromForm ctx.Request.Form "mtype"
                            |> Option.bind(fun s ->
                                match s |> Enum.TryParse<MonsterType> with
                                | true, v -> Some v
                                | false, _ -> None)
                        let msubtypeO =
                            tryGetFromForm ctx.Request.Form "msubtype"
                            |> Option.bind(fun s ->
                                match s |> Enum.TryParse<MonsterSubType> with
                                | true, v -> Some v
                                | false, _ -> None)
                        match mtypeO, msubtypeO with
                        | Some mtype, Some msubtype ->
                            match db.CreateGenRequest(UserId.Discord user.DiscordId, mtype, msubtype) with
                            | Ok _ -> Route.myrequests
                            | Error _ -> Route.error
                        | _, _ -> Route.error
                    | None -> Route.error
                | None -> Route.login

            let response = Response.redirectPermanently route ctx
            return! response
        }


let lvlUpHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let db = ctx.Plug<SqliteStorage>()
            
            let route =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        match ctx.Request.Form.TryGetValue("champ"), ctx.Request.Form.TryGetValue("char") with
                        | (true, s1), (true, s2) ->
                            match s1 |> UInt64.TryParse, s2 |> Enum.TryParse<Characteristic> with
                            | (true, cId), (true, ch) ->
                                match db.ChampBelongsToAUser(cId, UserId.Discord user.DiscordId) with
                                | Some b ->
                                    if b then
                                        if db.LevelUp(cId, ch) then
                                            Uri.champ cId
                                        else
                                            Route.account
                                    else Route.error
                                | _ -> Route.error
                            | _ -> Route.error
                        | _ -> Route.error
                    | None -> Route.error
                | None -> Route.login

            let response = Response.redirectPermanently route ctx

            return! response
        }

let rescanHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let db = ctx.Plug<SqliteStorage>()
            
            let route =
                match getDiscordUser result with
                | Some userO ->
                    match userO with
                    | Some user ->
                        match db.GetUserWallets(UserId.Discord user.DiscordId) with
                        | Ok xs ->
                            let xs' = xs |> List.choose(fun ar -> if ar.IsConfirmed then Some ar.Wallet else None)
                            CommonHelpers.updateChamps(db, user.DiscordId, xs')
                            Route.mychamps
                        | Error _ -> Route.error
                    | None -> Route.error
                | None -> Route.login

            let response = Response.redirectPermanently route ctx

            return! response
        }


let logoutHandler : HttpHandler =
    Response.signOutAndRedirect "Cookies" Route.index

[<RequireQualifiedAccess>]
module LeaderboardHandlers =
    let champsHandler : HttpHandler =
        fun ctx ->
            task {
                let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                let isAuth = getDiscordUser result |> Option.isSome

                let view =
                    let db = ctx.Plug<SqliteStorage>()
                    match db.GetChampLeaderboard() with
                    | Ok xs ->
                        xs
                        |> List.map(fun ar -> ChampShortInfo(uint64 ar.Id, ar.Name, ar.IPFS, uint64 ar.Xp))
                        |> LeaderboardView.champs
                    | _ -> Ui.defError

                let response =
                    view
                    |> Ui.layout "Leaderboard" isAuth
                    |> fun html -> Response.ofHtml html ctx

                return! response
            }

    let monstersHandler : HttpHandler =
        fun ctx ->
            task {
                let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                let isAuth = getDiscordUser result |> Option.isSome
                let view =
                    let db = ctx.Plug<SqliteStorage>()
                    match db.GetMonsterLeaderboard() with
                    | Ok xs -> xs |> LeaderboardView.monsters
                    | _ -> Ui.defError

                let response =
                    view
                    |> Ui.layout "Leaderboard" isAuth
                    |> fun html -> Response.ofHtml html ctx

                return! response
            }

    open System.Collections.Generic
    open System.Threading.Tasks

    let donatersHandler : HttpHandler =
        let names = Dictionary<uint64, string>()
        fun ctx ->
            task {
                let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                let isAuth = getDiscordUser result |> Option.isSome
                let! view =
                    let db = ctx.Plug<SqliteStorage>()
                    let client = ctx.Plug<RestClient>()
                    match db.GetTopInGameDonaters() with
                    | Ok xs ->
                        task {
                            let users =
                                [ for dId in xs |> List.map(fun ar -> uint64 ar.DiscordId) |> List.filter(names.ContainsKey >> not) do 
                                    task { 
                                        return! client.GetUserAsync(dId)
                                    }
                                ]
                            let! us = Task.WhenAll(users)
                            us |> Array.iter(fun u -> names.Add(u.Id, u.GlobalName))
                            let view =
                                xs
                                |> List.map(fun ar ->
                                    let name =
                                        match names.TryGetValue (uint64 ar.DiscordId) with
                                        | true, n -> n
                                        | false, _ -> string ar.DiscordId
                                    name, ar.Amount)
                                |> LeaderboardView.donaters "Name"
                            
                            return view
                        }
                    | _ -> task { return Ui.defError }

                let response =
                    view
                    |> Ui.layout "Leaderboard" isAuth
                    |> fun html -> Response.ofHtml html ctx

                return! response
            }

    let unknownDonatersHandler : HttpHandler =
        fun ctx ->
            task {
                let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                let isAuth = getDiscordUser result |> Option.isSome
                let view =
                    let db = ctx.Plug<SqliteStorage>()
                    match db.GetTopDonaters() with
                    | Ok xs ->
                        xs
                        |> List.map(fun ar -> ar.Wallet, ar.Amount)
                        |> LeaderboardView.donaters "Wallet"
                    | _ -> Ui.defError

                let response =
                    view
                    |> Ui.layout "Leaderboard" isAuth
                    |> fun html -> Response.ofHtml html ctx

                return! response
            }

    let generalHandler : HttpHandler =
        fun ctx ->
            task {
                let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                let isAuth = getDiscordUser result |> Option.isSome

                let response =
                    LeaderboardView.general
                    |> Ui.layout "Leaderboard" isAuth
                    |> fun html -> Response.ofHtml html ctx

                return! response
            }

let homeHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let isAuth = getDiscordUser result |> Option.isSome
            let view =
                let db = ctx.Plug<SqliteStorage>()
                let res = db.GetNumKey DbKeysNum.Rewards
                let darkCoinPriceOpt = db.GetNumKey DbKeysNum.DarkCoinPrice
                match res with
                | Some d -> HomeView.home d darkCoinPriceOpt
                | _ -> Ui.defError

            let response =
                view
                |> Ui.layout "Home" isAuth
                |> fun html -> Response.ofHtml html ctx

            return! response
        }

let traitsHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let isAuth = getDiscordUser result |> Option.isSome
            let view = HomeView.allTraits |> HomeView.traits

            let response =
                view
                |> Ui.layout "Traits" isAuth
                |> fun html -> Response.ofHtml html ctx

            return! response
        }

let faqHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let isAuth = getDiscordUser result |> Option.isSome
            
            let response =
                FAQView.faqView
                |> Ui.layout "FAQ" isAuth
                |> fun html -> Response.ofHtml html ctx

            return! response
        }

let statsHandler : HttpHandler =
    fun ctx ->
        task {
            let! result = ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            let isAuth = getDiscordUser result |> Option.isSome
            
            let view =
                let db = ctx.Plug<SqliteWebUiStorage>()
                match db.GetStats() with
                | Some s -> HomeView.statsPage s
                | _ -> Ui.defError

            let response =
                view
                |> Ui.layout "Stats" isAuth
                |> fun html -> Response.ofHtml html ctx

            return! response
        }

let deniedHandler : HttpHandler =
    Response.ofPlainText "Access Denied"

let onTicketReceived =
    Func<_, _>(fun (ctx:TicketReceivedContext) -> 
            System.Threading.Tasks.Task.CompletedTask
        )

let dAuth (options:OAuthOptions) =
    let discord = builder.Configuration.GetSection("Configuration:Discord").Get<DiscordConfiguration>()
    options.ClientId <- discord.ClientId
    options.ClientSecret <- discord.ClientSecret
    options.CallbackPath <- PathString discord.CallBack
    options.SaveTokens <- true
    options.CorrelationCookie.SameSite <- SameSiteMode.Lax
    options.CorrelationCookie.SecurePolicy <- CookieSecurePolicy.Always
    options.Scope.Add("identify")
    let evts = new OAuthEvents(OnTicketReceived = onTicketReceived)
    options.Events <- evts

open NetCord.Hosting.Gateway
open Conf

builder.Logging.AddSerilog(dispose=true) |> ignore
builder.Services.AddDiscordGateway() |> ignore

builder
    .Services
        .AddSingleton<SqliteWebUiStorage>()
        .AddSingleton<SqliteStorage>()
        .AddAuthorization()
        .AddAuthentication(fun options ->
            options.DefaultScheme <- CookieAuthenticationDefaults.AuthenticationScheme
            options.DefaultChallengeScheme <- DiscordAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(fun opt ->
            opt.Cookie.Name <- Cookie.name
            opt.LoginPath <- PathString("/login")
            opt.LogoutPath <- PathString("/logout")
            opt.ExpireTimeSpan <- TimeSpan.FromDays(7)
            opt.Cookie.SameSite <- SameSiteMode.Lax
            opt.Cookie.SecurePolicy <- CookieSecurePolicy.Always
            opt.Cookie.MaxAge <- TimeSpan.FromDays(7)
        )
        .AddDiscord(dAuth)
    |> ignore

builder.Services.AddOptions<Configuration>().BindConfiguration(nameof Configuration) |> ignore
builder.Services.AddOptions<DbConfiguration>().BindConfiguration("Configuration:Db") |> ignore
builder.Services.AddOptions<DiscordConfiguration>().BindConfiguration("Configuration:Discord") |> ignore

let endpoints =
    [
        get Route.index homeHandler
        get Route.traits traitsHandler

        get Route.login loginHandler

        get Route.account accountHandler
        post Route.walletRegister registerNewWalletHandler

        get Route.battle battleHandler
        get Route.denied deniedHandler
        get Route.signinDiscord (fun ctx ->
            Elem.main [] []
            |> Ui.layout "Signed-in" true
            |> fun html -> Response.ofHtml html ctx)

        post Route.joinBattle joinBattleHandler

        get Route.shop shopHandler
        post Route.buyitem buyItemHandler

        get Route.storage storageHandler
        post Route.use' useItemHandler

        get Route.topChamps LeaderboardHandlers.champsHandler
        get Route.topMonsters LeaderboardHandlers.monstersHandler
        get Route.topDonaters LeaderboardHandlers.donatersHandler
        get Route.topUnknownDonaters LeaderboardHandlers.unknownDonatersHandler
        get Route.top LeaderboardHandlers.generalHandler

        get Route.mychamps myChampsHandler
        get Route.mymonsters myMonstersHandler
        get Route.myrequests myRequestsHandler

        post Route.donate donateHandler
        post Route.createMonster createMonsterHandler
        post Route.renameMonster renameMonstrHandler
        post Route.renameChamp renameChampHandler
        post Route.levelUp lvlUpHandler
        post Route.rescan rescanHandler 
        post Route.logout logoutHandler

        get Route.champ champHandler
        get Route.monster monstrHandler

        get Route.monstersUnderEffects monstersUnderEffectsHandler
        get Route.myChampsUnderEffects champsUnderEffectsHandler

        get Route.faq faqHandler
        get Route.stats statsHandler
    ]


let wapp = builder.Build()
wapp.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = (ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto))) |> ignore
wapp.UseHsts() |> ignore

wapp.UseStaticFiles() |> ignore

wapp.UseRouting() |> ignore

wapp.UseAuthorization() |> ignore
wapp.UseAuthentication() |> ignore

wapp
    .UseFalco(endpoints)
    // ^-- activate Falco endpoint source
    .Run()