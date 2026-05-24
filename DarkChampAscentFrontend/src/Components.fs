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
    | Page.Battle | Page.Shop | Page.DefeatedMonsters | Page.DefeatedChamps      -> Some Tab.Battle
    | Page.Account | Page.MyChamps | Page.MyChampsEffects
    | Page.MyMonsters | Page.MyRequests | Page.Storage | Page.Login              -> Some Tab.AccountOrLogin
    | Page.TopChamps | Page.TopMonsters | Page.TopDonaters
    | Page.TopGeneral                                  -> Some Tab.Leaderboard
    | Page.Stats | Page.Traits | Page.Tokenomics                      -> Some Tab.About
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
                Page.DefeatedChamps, "Defeated Champs", WebEmoji.Champs
                Page.DefeatedMonsters, "Defeated Monsters", WebEmoji.Monsters
                Page.Shop,   "Shop",   WebEmoji.Shop
            ] currentPage |> Some

        | Some Tab.AccountOrLogin ->
            if isLoggedIn then
                SubBar [
                    Page.Account, "Account", WebEmoji.Account
                    Page.MyChamps, "Champs", WebEmoji.Champs
                    Page.MyChampsEffects, "Champs under effects", WebEmoji.Champs
                    Page.MyMonsters, "Monsters", WebEmoji.Monsters
                    Page.MyRequests, "Requests", WebEmoji.MyRequests
                    Page.Storage, "Storage", WebEmoji.MyStorage
                ] currentPage |> Some
            else None

        | Some Tab.Leaderboard ->
            SubBar [
                Page.TopChamps, "Champs", WebEmoji.Champs
                Page.TopMonsters, "Monsters", WebEmoji.Monsters
                Page.TopDonaters, "Donaters",  WebEmoji.TopDonaters
            ] currentPage |> Some

        | Some Tab.About ->
            SubBar [
                Page.Stats, "Stats", WebEmoji.Stats
                Page.Traits, "Traits", WebEmoji.About
                Page.Tokenomics, "Tokenomics", WebEmoji.Tokenomics
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
                                prop.target.blank
                                prop.rel "noopener noreferrer"
                                prop.children [
                                    Html.span [ prop.text WebEmoji.Discord ]
                                    Html.span [ prop.text "Discord" ]
                                ]
                            ]
                            Html.a [
                                prop.href Links.Github
                                prop.className "logo-bar-link"
                                prop.target.blank
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
                    tabLink Tab.About           "About"       WebEmoji.About       Page.Stats.Route
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
open GameLogic.Champs

[<ReactComponent>]
let CustomSelectInput (value: string) (onChange: string -> unit) (options: (string * string * string option) list) =
    let isOpen, setIsOpen = React.useState false
    let controlRef = React.useRef<Browser.Types.HTMLElement option>(None)
    let dropdownRef = React.useRef<Browser.Types.HTMLElement option>(None)
    let dropdownPos, setDropdownPos = React.useState {| top = 0.0; left = 0.0; width = 0.0 |}

    let updatePos () =
        match controlRef.current with
        | Some el ->
            let rect = el.getBoundingClientRect()
            setDropdownPos {| top = rect.bottom; left = rect.left; width = rect.width |}
        | None -> ()

    // Close on outside click
    React.useEffect((fun () ->
        let handler (e: Browser.Types.Event) =
            let target = e.target :?> Browser.Types.Node
            let outsideControl =
                match controlRef.current with
                | Some el -> not (el.contains target)
                | None -> true
            let outsideDropdown =
                match dropdownRef.current with
                | Some el -> not (el.contains target)
                | None -> true
            if outsideControl && outsideDropdown then
                setIsOpen false
        Browser.Dom.document.addEventListener("mousedown", handler)
        { new System.IDisposable with
            member _.Dispose() = Browser.Dom.document.removeEventListener("mousedown", handler) }
    ), [||])

    // Reposition on scroll/resize while open
    React.useEffect((fun () ->
        if isOpen then
            let onScroll _ = updatePos()
            let onResize _ = updatePos()
            // true = capture phase, catches scroll on any ancestor
            Browser.Dom.window.addEventListener("scroll", onScroll, true)
            Browser.Dom.window.addEventListener("resize", onResize)
            { new System.IDisposable with
                member _.Dispose() =
                    Browser.Dom.window.removeEventListener("scroll", onScroll, true)
                    Browser.Dom.window.removeEventListener("resize", onResize) }
        else
            { new System.IDisposable with member _.Dispose() = () }
    ), [| box isOpen |])

    let selectedOpt   = options |> List.tryFind (fun (v, _, _) -> v = value)
    let selectedLabel = selectedOpt |> Option.map (fun (_, l, _) -> l) |> Option.defaultValue ""
    let selectedImg   = selectedOpt |> Option.bind (fun (_, _, img) -> img)

    let dropdown =
        Html.div [
            prop.ref (fun el -> dropdownRef.current <- if isNull (box el) then None else Some (unbox el))
            prop.className "custom-select-dropdown"
            prop.style [
                style.position.fixedRelativeToWindow
                style.top    (int dropdownPos.top)
                style.left   (int dropdownPos.left)
                style.width  (int dropdownPos.width)
                style.zIndex 9999
            ]
            prop.children [
                for (v, label, img) in options do
                    Html.div [
                        prop.className ("custom-select-option" + (if v = value then " selected" else ""))
                        prop.onMouseDown (fun e ->
                            e.preventDefault()
                            e.stopPropagation())
                        prop.onClick (fun e ->
                            e.stopPropagation()
                            onChange v
                            setIsOpen false)
                        prop.children [
                            match img with
                            | Some url -> Html.img [ prop.className "custom-select-img"; prop.src url ]
                            | None -> ()
                            Html.span [ prop.text label ]
                        ]
                    ]
            ]
        ]

    React.Fragment [
        Html.div [
            prop.ref (fun el -> controlRef.current <- if isNull (box el) then None else Some (unbox el))
            prop.className ("custom-select" + (if isOpen then " open" else ""))
            prop.children [
                Html.div [
                    prop.className "custom-select-control"
                    prop.onMouseDown (fun e -> e.preventDefault())
                    prop.onClick (fun _ ->
                        if isOpen then setIsOpen false
                        else
                            updatePos()
                            setIsOpen true)
                    prop.children [
                        match selectedImg with
                        | Some url -> Html.img [ prop.className "custom-select-img"; prop.src url ]
                        | None -> ()
                        Html.span [ prop.className "custom-select-value"; prop.text selectedLabel ]
                        Html.span [ prop.className "custom-select-arrow"; prop.text "▾" ]
                    ]
                ]
            ]
        ]

        if isOpen then
            ReactDOM.createPortal(dropdown, Browser.Dom.document.body)
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

let fullStatRow (WebEmoji: string) (label: string) (value: string) (link: string option) =
    Html.tr [
        Html.td [ Html.span [ prop.text WebEmoji ] ]
        Html.td [
            Html.div [
                prop.className "label-wrap"
                prop.children [
                    match link with
                    | Some l -> Html.a [ prop.href l; prop.target.blank; prop.text label ]
                    | None -> Html.text label
                ]
            ]
        ]
        Html.td [
            prop.className "stat-value"
            prop.text value
        ]
    ]

let statRow (WebEmoji: string) (label: string) (value: string) =
    fullStatRow WebEmoji label value None

let chTable (stat:Stat) =
    Html.table [
        Html.tbody [
            Html.tr [
                Html.td [ prop.className "stat-icon"; prop.text WebEmoji.Health ]
                Html.td [ prop.className "stat-name"; prop.text "Health" ]
                Html.td [ prop.className "stat-val";  prop.text (string stat.Health) ]

                Html.td [ prop.className "stat-sep" ]

                Html.td [ prop.className "stat-icon"; prop.text WebEmoji.Magic ]
                Html.td [ prop.className "stat-name"; prop.text "Magic" ]
                Html.td [ prop.className "stat-val";  prop.text (string stat.Magic) ]
            ]

            Html.tr [
                Html.td [ prop.className "stat-icon"; prop.text WebEmoji.Attack ]
                Html.td [ prop.className "stat-name"; prop.text "Attack" ]
                Html.td [ prop.className "stat-val";  prop.text (string stat.Attack) ]

                Html.td [ prop.className "stat-sep" ]

                Html.td [ prop.className "stat-icon"; prop.text WebEmoji.MagicAttack ]
                Html.td [ prop.className "stat-name"; prop.text "M. Attack" ]
                Html.td [ prop.className "stat-val";  prop.text (string stat.MagicAttack) ]
            ]

            Html.tr [
                Html.td [ prop.className "stat-icon"; prop.text WebEmoji.Shield ]
                Html.td [ prop.className "stat-name"; prop.text "Defense" ]
                Html.td [ prop.className "stat-val";  prop.text (string stat.Defense) ]

                Html.td [ prop.className "stat-sep" ]

                Html.td [ prop.className "stat-icon"; prop.text WebEmoji.MagicShield ]
                Html.td [ prop.className "stat-name"; prop.text "M. Defense" ]
                Html.td [ prop.className "stat-val";  prop.text (string stat.MagicDefense) ]
            ]

            Html.tr [
                Html.td [ prop.className "stat-icon"; prop.text WebEmoji.Luck ]
                Html.td [ prop.className "stat-name"; prop.text "Luck" ]
                Html.td [ prop.className "stat-val";  prop.text (string stat.Luck) ]
                
                Html.td [ prop.className "stat-sep" ]

                Html.td [ prop.className "stat-icon"; prop.text WebEmoji.Accuracy ]
                Html.td [ prop.className "stat-name"; prop.text "Accuracy" ]
                Html.td [ prop.className "stat-val";  prop.text (string stat.Accuracy) ]
            ]
        ]
    ]


let useSSE (url: string) (onMessage: string -> unit) =
    React.useEffect((fun () ->
        let mutable es : obj = null
        let mutable disposed = false

        let rec connect () =
            es <- createNew (Browser.Dom.window :> obj)?EventSource url
            es?onmessage <- fun (e: obj) -> onMessage (string e?data)
            es?onerror <- fun _ ->
                Browser.Dom.console.warn("SSE error on", url)
                es?close()
                if not disposed then
                    Browser.Dom.window.setTimeout((fun () -> connect ()), 3000) |> ignore

        let onVisible _ =
            if Browser.Dom.document.visibilityState = "visible" then
                es?close()
                Browser.Dom.window.setTimeout((fun () ->
                    if not disposed && Browser.Dom.document.visibilityState = "visible" then
                        connect()
                ), 300) |> ignore

        connect()
        Browser.Dom.document.addEventListener("visibilitychange", onVisible)

        { new System.IDisposable with
            member _.Dispose() =
                disposed <- true
                if es <> null then es?close()
                Browser.Dom.document.removeEventListener("visibilitychange", onVisible) }
    ), [| box url |])

let deferred<'T> (state: Deferred<'T>) (render: 'T -> ReactElement) =
    match state with
    | NotStarted | Loading -> spinner ()
    | Failed err -> errorView err
    | Loaded data -> render data

[<RequireQualifiedAccess>]
module Utils =
    let srcMonsterImg (mimg:MonsterImg)=
        match mimg with
        | MonsterImg.File f -> prop.src (Api.baseUrl + "/" + f)

    let formatValue (d:decimal) =
        match d with
        | _ when abs d >= 1_000_000.0M -> sprintf "%.2fM" (d / 1_000_000.0M)
        | _ when abs d >= 1_000.0M     -> sprintf "%.2fK" (d / 1_000.0M)
        | _                            -> sprintf "%.4f" d