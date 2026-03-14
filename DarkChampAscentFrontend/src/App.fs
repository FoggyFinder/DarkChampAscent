module App

open Feliz
open Types
open Components
open DarkChampAscent.Account
open Browser.Dom
open Browser.Types
open Pages.About
open Pages.Leaderboard
open Pages.User
open Pages.Details
open Pages.NonAuth
open Pages.Login
open Pages.Battle

let private parseHash () : Page =
    let hash = window.location.hash
    let path = hash.TrimStart('#').TrimStart('/')
    let segments =
        if path = "" then []
        else path.Split('/') |> Array.toList |> List.filter (fun s -> s <> "")
    match segments with
    | []                               -> Page.Home
    | [ "login" ]                      -> Page.Login
    | [ "account" ]                    -> Page.Account
    | [ "battle" ]                     -> Page.Battle
    | [ "shop" ]                       -> Page.Shop
    | [ "storage" ]                    -> Page.Storage
    | [ "mychamps" ]                   -> Page.MyChamps
    | [ "mychampsundereffects" ]       -> Page.MyChampsEffects
    | [ "mymonsters" ]                 -> Page.MyMonsters
    | [ "myrequests" ]                 -> Page.MyRequests
    | [ "monstersundereffects" ]       -> Page.MonstersEffects
    | [ "champ"; id ]                  ->
        match System.UInt64.TryParse id with true, v -> Page.ChampDetail v | _ -> Page.NotFound
    | [ "monster"; id ]                ->
        match System.UInt64.TryParse id with true, v -> Page.MonsterDetail v | _ -> Page.NotFound
    | [ "top" ]                        -> Page.TopGeneral
    | [ "top"; "champs" ]              -> Page.TopChamps
    | [ "top"; "monsters" ]            -> Page.TopMonsters
    | [ "top"; "donaters" ]            -> Page.TopDonaters
    | [ "top"; "donaters"; "unknown" ] -> Page.TopUnknownDonaters
    | [ "traits" ]                     -> Page.Traits
    | [ "faq" ]                        -> Page.FAQ
    | [ "stats" ]                      -> Page.Stats
    | [ "tokenomics" ]                 -> Page.Tokenomics
    | _                                -> Page.NotFound

[<ReactComponent>]
let App () =
    let currentPage, setPage        = React.useState (parseHash ())
    let authUser, setAuthUser       = React.useState<Account option> None
    let authChecked, setAuthChecked = React.useState false

    React.useEffect((fun () ->
        let handler = fun (_: Event) -> setPage (parseHash ())
        window.addEventListener("hashchange", handler)
        { new System.IDisposable with
            member _.Dispose() = window.removeEventListener("hashchange", handler) }
    ), [||])

    React.useEffect((fun () ->
        async {
            let! r = Api.getMe ()
            setAuthUser (r |> Result.toOption)
            setAuthChecked true
        } |> Async.StartImmediate
    ), [||])

    let onLogin (user: Account) =
        setAuthUser (Some user)
        Nav.navigateTo Page.Account.Route

    let onLogout () =
        setAuthUser None
        Nav.navigateTo Page.Home.Route

    let isLoggedIn = authUser.IsSome

    let pageContent =
        match currentPage with
        | Page.Home               -> HomePage ()
        | Page.Login              -> LoginPage onLogin
        | Page.Account            -> if isLoggedIn then AccountPage onLogout else LoginPage onLogin
        | Page.Battle             -> BattlePage ()
        | Page.Shop               -> ShopPage ()
        | Page.Storage            -> if isLoggedIn then StoragePage () else LoginPage onLogin
        | Page.MyChamps           -> if isLoggedIn then MyChampsPage () else LoginPage onLogin
        | Page.MyChampsEffects    -> if isLoggedIn then ChampsEffectsPage () else LoginPage onLogin
        | Page.MyMonsters         -> if isLoggedIn then MyMonstersPage () else LoginPage onLogin
        | Page.MyRequests         -> if isLoggedIn then MyRequestsPage () else LoginPage onLogin
        | Page.MonstersEffects    -> MonstersEffectsPage ()
        | Page.ChampDetail id     -> ChampDetailPage id
        | Page.MonsterDetail id   -> MonsterDetailPage id
        | Page.TopGeneral         -> LeaderboardGeneralPage ()
        | Page.TopChamps          -> LeaderboardChampsPage ()
        | Page.TopMonsters        -> LeaderboardMonstersPage ()
        | Page.TopDonaters        -> LeaderboardDonatersPage ()
        | Page.TopUnknownDonaters -> LeaderboardUnknownDonatersPage ()
        | Page.Traits             -> TraitsPage ()
        | Page.FAQ                -> FAQPage ()
        | Page.Stats              -> StatsPage ()
        | Page.Tokenomics         -> TokenomicsPage ()
        | Page.NotFound           -> Html.div [ prop.text "404 - Page not found" ]
        

    if not authChecked then spinner ()
    else Layout currentPage.Title isLoggedIn currentPage pageContent

open UseWallet
let manager = UseWallet.WalletManager {|
    wallets = [| "pera"; "defly"; "lute" |]
    defaultNetwork = "mainnet"
|}

let root = ReactDOM.createRoot (document.getElementById "root")
root.render (
    UseWallet.WalletProvider(manager, App())
)