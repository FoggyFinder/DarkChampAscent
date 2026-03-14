module Components

open Feliz
open Types
open Display

type private Tab =
    | Battle
    | AccountOrLogin
    | Leaderboard
    | About

let private tabOf (page: Page) =
    match page with
    | Page.Battle | Page.Shop                                                    -> Some Tab.Battle
    | Page.Account | Page.MyChamps | Page.MyChampsEffects
    | Page.MyMonsters | Page.MyRequests | Page.Storage | Page.Login              -> Some Tab.AccountOrLogin
    | Page.TopChamps | Page.TopMonsters | Page.TopDonaters
    | Page.TopUnknownDonaters | Page.TopGeneral                                  -> Some Tab.Leaderboard
    | Page.FAQ | Page.Stats | Page.Traits | Page.Tokenomics                      -> Some Tab.About
    | _                                                                          -> None

[<ReactComponent>]
let private SubBar (items: (Page * string * string) list) (currentPage: Page) =
    Html.div [
        prop.className "sub-bar"
        prop.children [
            for (page, label, icon) in items do
                Html.a [
                    prop.key label
                    prop.href page.Route
                    prop.className (
                        if currentPage = page then "sub-item sub-item--active"
                        else "sub-item")
                    prop.onClick (fun e ->
                        e.preventDefault()
                        Nav.navigateTo page.Route)
                    prop.children [
                        Html.span [ prop.className "sub-icon"; prop.text icon ]
                        Html.span [ prop.className "sub-label"; prop.text label ]
                    ]
                ]
        ]
    ]

[<ReactComponent>]
let NavBar (isLoggedIn: bool) (currentPage: Page) =
    let activeTab = tabOf currentPage

    let tabLink (tab: Tab) (label: string) (icon: string) (route: string) =
        Html.a [
            prop.key label
            prop.href route
            prop.className (
                if activeTab = Some tab then "tab-item tab-item--active"
                else "tab-item")
            prop.onClick (fun e ->
                e.preventDefault()
                Nav.navigateTo route)
            prop.children [
                Html.span [ prop.className "tab-icon"; prop.text icon ]
                Html.span [ prop.className "tab-label"; prop.text label ]
            ]
        ]

    let subBar =
        match activeTab with
        | Some Tab.Battle ->
            SubBar [
                Page.Battle, "Battle", WebEmoji.Battle
                Page.Shop,   "Shop",   WebEmoji.Shop
            ] currentPage |> Some

        | Some Tab.AccountOrLogin ->
            if isLoggedIn then
                SubBar [
                    Page.Account,         "Account",              WebEmoji.Account
                    Page.MyChamps,        "Champs",               WebEmoji.Champs
                    Page.MyChampsEffects, "Champs under effects", WebEmoji.Champs
                    Page.MyMonsters,      "Monsters",             WebEmoji.Monsters
                    Page.MyRequests,      "Requests",             WebEmoji.MyRequests
                    Page.Storage,         "Storage",              WebEmoji.MyStorage
                ] currentPage |> Some
            else None

        | Some Tab.Leaderboard ->
            SubBar [
                Page.TopChamps,          "Champs",              WebEmoji.Champs
                Page.TopMonsters,        "Monsters",            WebEmoji.Monsters
                Page.TopDonaters,        "Donaters (players)",  WebEmoji.TopDonaters
                Page.TopUnknownDonaters, "Donaters (wallets)",  WebEmoji.TopUnknownDonaters
            ] currentPage |> Some

        | Some Tab.About ->
            SubBar [
                Page.FAQ,    "FAQ",    WebEmoji.FAQ
                Page.Stats,  "Stats",  WebEmoji.Stats
                Page.Traits, "Traits", WebEmoji.About
                Page.Tokenomics, "Tokenomics", WebEmoji.Stats
            ] currentPage |> Some

        | None -> None

    Html.header [
        prop.className "site-header"
        prop.children [
            Html.div [
                prop.className "site-logo-bar"
                prop.children [
                    Html.a [
                        prop.href Page.Home.Route
                        prop.className "site-logo"
                        prop.onClick (fun e ->
                            e.preventDefault()
                            Nav.navigateTo Page.Home.Route)
                        prop.children [
                            Html.span [ prop.className "logo-icon"; prop.text "🌑" ]
                            Html.span [ prop.className "logo-text"; prop.text "DarkChampAscent" ]
                        ]
                    ]
                    Html.div [
                        prop.className "logo-bar-links"
                        prop.children [
                            Html.a [
                                prop.href Links.Discord
                                prop.className "logo-bar-link"
                                prop.target "_blank"
                                prop.rel "noopener noreferrer"
                                prop.children [
                                    Html.span [ prop.text WebEmoji.Discord ]
                                    Html.span [ prop.text "Discord" ]
                                ]
                            ]
                            Html.a [
                                prop.href Links.Github
                                prop.className "logo-bar-link"
                                prop.target "_blank"
                                prop.rel "noopener noreferrer"
                                prop.children [
                                    Html.span [ prop.text WebEmoji.SourceCode ]
                                    Html.span [ prop.text "GitHub" ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]

            Html.nav [
                prop.className "tab-bar"
                prop.ariaLabel "Main navigation"
                prop.children [
                    tabLink Tab.Battle          "Battle"      WebEmoji.Battle      Page.Battle.Route
                    (if isLoggedIn
                     then tabLink Tab.AccountOrLogin "Account" WebEmoji.Account    Page.Account.Route
                     else tabLink Tab.AccountOrLogin "Log in"  WebEmoji.LogIn      Page.Login.Route)
                    tabLink Tab.Leaderboard     "Leaderboard" WebEmoji.Leaderboard Page.TopChamps.Route
                    tabLink Tab.About           "About"       WebEmoji.About       Page.FAQ.Route
                ]
            ]

            match subBar with
            | Some bar -> bar
            | None     -> Html.none
        ]
    ]

[<ReactComponent>]
let Layout (title: string) (isLoggedIn: bool) (currentPage: Page) (content: ReactElement) =
    React.useEffect((fun () ->
        Browser.Dom.document.title <- title), [| box title |])

    Html.div [
        prop.className "app-layout"
        prop.children [
            NavBar isLoggedIn currentPage
            Html.main [
                prop.role "main"
                prop.className "page-content"
                prop.children [ content ]
            ]
        ]
    ]

let spinner () =
    Html.div [
        prop.className "spinner"
        prop.ariaLabel "Loading..."
        prop.children [ Html.span [ prop.className "sr-only"; prop.text "Loading…" ] ]
    ]

let errorView (msg: string) =
    Html.div [
        prop.className "error-block"
        prop.children [
            Html.p [ prop.className "error-text"; prop.text msg ]
        ]
    ]

let defError = errorView "Unexpected error. Please, try again later."

let pageHeader (text: string) =
    Html.h1 [ prop.className "page-header"; prop.text text ]

let statBadge (WebEmoji: string) (label: string) (value: string) =
    Html.div [
        prop.className "stat-badge"
        prop.children [
            Html.span [ prop.className "stat-WebEmoji"; prop.text WebEmoji ]
            Html.span [ prop.className "stat-label"; prop.text label ]
            Html.span [ prop.className "stat-value"; prop.text value ]
        ]
    ]

let ipfsImg (hash: string) (cls: string) =
    Html.img [
        prop.className cls
        prop.src (Links.IPFS + hash)
        prop.alt ""
    ]

let champLink (id: uint64) (name: string) =
    let p = Page.ChampDetail id
    Html.a [
        prop.href p.Route
        prop.onClick (fun e -> e.preventDefault(); Nav.navigateTo p.Route)
        prop.text name
    ]

let monsterLink (id: uint64) (name: string) =
    let p = Page.MonsterDetail id
    Html.a [
        prop.href p.Route
        prop.onClick (fun e -> e.preventDefault(); Nav.navigateTo p.Route)
        prop.text name
    ]

open Fable.Core.JsInterop
open GameLogic.Monsters

[<ReactComponent>]
let TomSelectInput (className: string) (value: string) (onChange: string -> unit) (children: ReactElement list) =
    
    Html.select [
        prop.className (className + " form-select tom-select")
        prop.value value
        prop.onChange onChange
        prop.children children
    ]

[<ReactComponent>]
let WalletAddress (addr: string) =
    let copied, setCopied = React.useState false

    Html.span [
        prop.className "wallet-address-wrap"
        prop.children [
            Html.span [ prop.className "wallet-address"; prop.text addr ]
            Html.button [
                prop.className "copy-btn"
                prop.title "Copy address"
                prop.ariaLabel "Copy wallet address"
                prop.onClick (fun _ ->
                    emitJsStatement (addr) "navigator.clipboard.writeText($0)"
                    setCopied true
                    Browser.Dom.window.setTimeout((fun () -> setCopied false), 1500) |> ignore)
                prop.children [
                    Html.span [ prop.text (if copied then "✓" else "📋") ]
                ]
            ]
        ]
    ]

let howToDepositBlock () =
    Html.div [
        prop.className "deposit-info"
        prop.children [
            Html.span [
                prop.dangerouslySetInnerHTML $"To deposit DarkCoins {WebEmoji.DarkCoin} send tokens to "
            ]
            WalletAddress KnownWallets.DarkChampAscent
            Html.text $" ({KnownWallets.DarkChampAscentNFD}). "
            Html.br []
            Html.text "Within 5-7 minutes your balance should be updated."
        ]
    ]

let statRow (WebEmoji: string) (label: string) (value: string) =
    Html.tr [
        Html.td [
            Html.div [
                prop.className "label-wrap"
                prop.children [ Html.text $"{WebEmoji} {label}" ]
            ]
        ]
        Html.td [ Html.text value ]
    ]

let useSSE (url: string) (onMessage: string -> unit) =
    React.useEffect((fun () ->
        let es : obj = createNew (Browser.Dom.window :> obj)?EventSource url
        es?onmessage <- fun (e: obj) -> onMessage (string e?data)
        es?onerror <- fun _ -> Browser.Dom.console.warn("SSE error on", url)
        { new System.IDisposable with
            member _.Dispose() = es?close() }
    ), [| box url |])

let deferred<'T> (state: Deferred<'T>) (render: 'T -> ReactElement) =
    match state with
    | NotStarted | Loading -> spinner ()
    | Failed err -> errorView err
    | Loaded data -> render data

[<RequireQualifiedAccess>]
module Utils =
    let srcMonsterImg(mimg:MonsterImg)=
        match mimg with
        | MonsterImg.File f -> prop.src (Api.baseUrl + "/" + f)