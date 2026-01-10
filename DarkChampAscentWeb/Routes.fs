namespace UI

[<RequireQualifiedAccess>]
module Route =
    let [<Literal>] index = "/"
    let [<Literal>] login = "/login"
    let [<Literal>] traits = "/traits"

    let [<Literal>] denied = "/denied"
    let [<Literal>] signinDiscord = "sdiscord"
    
    let [<Literal>] battle = "/battle"
    let [<Literal>] joinBattle = "/joinBattle"
    
    let [<Literal>] account = "/account"
    let [<Literal>] walletRegister = "/walregister"

    let [<Literal>] faq = "/faq"

    let [<Literal>] monstersUnderEffects = "/monstersundereffects"

    let [<Literal>] mychamps = "/mychamps"
    let [<Literal>] myChampsUnderEffects = "/mychampsundereffects"
    let [<Literal>] mymonsters = "/mymonsters"
    let [<Literal>] myrequests = "/myrequests"
    
    let [<Literal>] monster = "/monster/{id}"
    let [<Literal>] champ = "/champ/{id}"
    let [<Literal>] levelUp = "/levelup"

    let [<Literal>] storage = "/storage"
    let [<Literal>] use' = "/useitem"

    let [<Literal>] shop = "/shop"
    let [<Literal>] buyitem = "/buyitem"

    let [<Literal>] top = "/top"
    let [<Literal>] topChamps = "/top/champs"
    let [<Literal>] topMonsters = "/top/monsters"
    let [<Literal>] topDonaters = "/top/donaters"
    let [<Literal>] topUnknownDonaters = "/top/donaters/unknown"

    let [<Literal>] createMonster = "/createMonster"
    let [<Literal>] renameMonster = "/renameMonster"
    let [<Literal>] renameChamp = "/renameChamp"
    let [<Literal>] rescan = "/rescan"

    let [<Literal>] donate = "/donate"
    let [<Literal>] error = "/error"

    let [<Literal>] logout = "/logout"

[<RequireQualifiedAccess>]
module Uri =
    let champ (champId:uint64) =
         Route.champ.Replace("{id}", champId.ToString())
    let monstr (monstrId:uint64) =
         Route.monster.Replace("{id}", monstrId.ToString())

[<RequireQualifiedAccess>]
module Links =
    let [<Literal>] Github = "https://github.com/FoggyFinder/DarkChampAscent"
    let [<Literal>] Discord = "https://discord.gg/bYPtQhYKwN"
    let [<Literal>] IPFS = "https://ipfs.dark-coin.io/ipfs/"
    let [<Literal>] DarkChampCollection = "https://www.minthol.art/3%3A0_9846/assets/all?listingTypes=BUY&listingTypes=BID"

[<RequireQualifiedAccess>]
module KnownWallets =
    let [<Literal>] DarkChampAscent = "SZYECJF52SCJYMDQG4M3RGGLDTQPEEPTD36SW4PABEGZ6MCA4KH67QKRAU"
    let [<Literal>] DarkChampAscentNFD = "darkchampascent.algo"

open Falco.Markup
open GameLogic.Champs
open Display

[<RequireQualifiedAccess>]
module FileUtils =
    open GameLogic.Monsters
    open System.IO
    let private monstrsDir = "monstrs"
    let private wwwroot = "wwwroot"
    
    let getLocalImg (mpic:MonsterImg) =

        let imgDir = Path.Combine(wwwroot, monstrsDir)

        if Directory.Exists(imgDir) |> not then
            Directory.CreateDirectory(imgDir) |> ignore

        let fullpath = (match mpic with | MonsterImg.File fn -> fn)

        let filename =
            Path.Combine(monstrsDir, Path.GetFileName(fullpath))

        let destFileName = Path.Combine(wwwroot, filename)
        if File.Exists destFileName |> not then
            try
                File.Copy(fullpath, destFileName)
            with
            | _ -> () // Ignore if source file doesn't exist

        if File.Exists destFileName then filename else ""

module NavBar =
    type NavMode = Top | Side

    let private menuItem href text icon =
        Elem.li [] [
            Elem.a [ Attr.href href; Attr.class' "menu-link" ] [
                // icon can be an inline SVG or a span with a class for a font icon
                Elem.span [ Attr.class' "menu-icon" ] [ Text.raw icon ]
                Elem.span [ Attr.class' "menu-text" ] [ Text.raw text ]
            ]
        ]

    let private submenu title icon items =
        Elem.li [ Attr.class' "has-submenu"; Attr.create "data-has-submenu" "true" ] [
            Elem.a [ Attr.href "#"; Attr.class' "menu-link parent-link"; Attr.create "aria-expanded" "false" ] [
                Elem.span [ Attr.class' "menu-icon" ] [ Text.raw icon ]
                Elem.span [ Attr.class' "menu-text" ] [ Text.raw title ]
                Elem.span [ Attr.class' "submenu-caret" ] [ Text.raw "▾" ]
            ]
            Elem.ul [ Attr.class' "submenu" ] (items |> List.map (fun (h,t,i) -> 
                Elem.li [] [ Elem.a [ Attr.href h; Attr.class' "menu-link" ] [
                    Elem.span [ Attr.class' "menu-icon" ] [ Text.raw i ]
                    Elem.span [ Attr.class' "menu-text" ] [ Text.raw t ]
                ]]))
        ]

    let navBar (mode:NavMode) (isLoggedIn:bool) =
        let modeClass = match mode with Top -> "nav--top" | Side -> "nav--side"
        Elem.nav [ Attr.class' (sprintf "site-nav %s" modeClass) ] [
            // toggle button (visible in sidebar mode)
            Elem.button [ Attr.class' "nav-toggle"; Attr.create "aria-pressed" "false"; Attr.id "nav-toggle" ] [
                Elem.span [ Attr.class' "visually-hidden" ] [ Text.raw "Toggle navigation" ]
                Elem.span [ Attr.class' "toggle-icon" ] [ Text.raw $"{WebEmoji.Toggle}" ]
            ]

            Elem.ul [ Attr.class' "menu-list" ] [
                menuItem Route.index "Home" WebEmoji.Home
                if isLoggedIn then
                    menuItem Route.account "Account" WebEmoji.Account
                    submenu "Game" WebEmoji.GameAccountOptions [
                        (Route.storage, "My storage", WebEmoji.MyStorage)
                        (Route.mychamps, "My champs", WebEmoji.Champs)
                        (Route.myChampsUnderEffects, $"Effects {WebEmoji.Champs}", WebEmoji.ChampsUnderEffects)
                        (Route.mymonsters, "My monsters", WebEmoji.Monsters)
                        (Route.myrequests, "My requests", WebEmoji.MyRequests)
                    ]
                else
                    menuItem Route.account "Log-In" WebEmoji.LogIn
                menuItem Route.battle "Battle" WebEmoji.Battle

                menuItem Route.shop "Shop" WebEmoji.Shop
                menuItem Route.monstersUnderEffects $"Monsters {WebEmoji.Monsters}" WebEmoji.MonstersUnderEffects

                submenu "Leaderboard" WebEmoji.Leaderboard [
                    (Route.topChamps, "Champs", WebEmoji.Champs)
                    (Route.topMonsters, "Monsters", WebEmoji.Monsters)
                    (Route.topDonaters, "Donaters (players)", WebEmoji.TopDonaters)
                    (Route.topUnknownDonaters, "Donaters (wallets)", WebEmoji.TopUnknownDonaters)
                ]

                menuItem Route.faq "FAQ" WebEmoji.FAQ
                Elem.li [] [ Elem.a [ Attr.href Links.Github; Attr.class' "menu-link"; Attr.targetBlank ] [
                    Elem.span [ Attr.class' "menu-icon" ] [ Text.raw WebEmoji.SourceCode ]
                    Elem.span [ Attr.class' "menu-text" ] [ Text.raw "Source code" ]
                ] ]
                
                Elem.li [] [ Elem.a [ Attr.href Links.Discord; Attr.class' "menu-link"; Attr.targetBlank ] [
                    Elem.span [ Attr.class' "menu-icon" ] [ Text.raw WebEmoji.Discord ]
                    Elem.span [ Attr.class' "menu-text" ] [ Text.raw "Discord" ]
                ] ]
            ]
        ]

module Ui =
    let unError (err:string) =
        Elem.main [
            Attr.class' "error"
            Attr.role "main"
        ] [
            Elem.p [ Attr.class' "tErr" ] [
                Text.raw $"{err}"
            ]
        ]

    let defError = unError "Unexpected error. Please, try again later"

    let layout (title:string) (isLoggedIn:bool) (content : XmlNode) =
        Elem.html [ Attr.lang "en"; ] [
            Elem.head [] [
                Elem.meta  [ Attr.charset "UTF-8" ]
                Elem.meta  [ Attr.httpEquiv "X-UA-Compatible"; Attr.content "IE=edge, chrome=1" ]
                Elem.meta  [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
            
                Elem.title [] [ Text.raw title ]

                Elem.link [ Attr.href "/styles.css"; Attr.rel "stylesheet" ]
                Elem.link [ Attr.href "https://cdn.jsdelivr.net/npm/tom-select@2.2.2/dist/css/tom-select.bootstrap5.min.css"; Attr.rel "stylesheet" ]

                Elem.script [ Attr.src "https://cdn.jsdelivr.net/npm/tom-select@2.2.2/dist/js/tom-select.complete.min.js"; Attr.defer ] [ ]
                Elem.script [ Attr.src "/custom-select.js"; Attr.defer ] [ ]             
                Elem.script [ Attr.src "/nav.js"; Attr.defer ] [ ]
                Elem.link [ Attr.rel "stylesheet"; Attr.href "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.1/css/all.min.css"; Attr.crossorigin "anonymous" ]

                Elem.link [ Attr.rel "shortcut icon"; Attr.href "/favicon.ico"; Attr.type' "image/x-icon" ]

                Elem.link [ Attr.rel "icon"; Attr.href "/favicon.ico"; Attr.type' "image/x-icon" ]
            ]

            Elem.body [ ] [ 
                NavBar.navBar NavBar.NavMode.Side isLoggedIn
                content
            ]
        ]

    let columnsFromStat (stat:Stat) =
        [
            Elem.td [] [ Text.raw $"{stat.Health |> Display.withPlusOrEmpty}" ]
            Elem.td [] [ Text.raw $"{stat.Magic |> Display.withPlusOrEmpty}" ]
            Elem.td [] [ Text.raw $"{stat.Luck |> Display.withPlusOrEmpty}" ]
            Elem.td [] [ Text.raw $"{stat.Accuracy |> Display.withPlusOrEmpty}" ]

            Elem.td [] [ Text.raw $"{stat.Attack |> Display.withPlusOrEmpty}" ]
            Elem.td [] [ Text.raw $"{stat.MagicAttack |> Display.withPlusOrEmpty}" ]
            Elem.td [] [ Text.raw $"{stat.Defense |> Display.withPlusOrEmpty}" ]
            Elem.td [] [ Text.raw $"{stat.MagicDefense |> Display.withPlusOrEmpty}" ]
        ]

    let howToDeposit =
        Elem.div [] [
            Text.raw "To deposit DarkCoins to your account send tokens to "
            
            Elem.span [
                Attr.create "class" "wallet-address"
                Attr.create "data-wallet" KnownWallets.DarkChampAscent
                Attr.create "title" KnownWallets.DarkChampAscent
            ] [
                Text.raw KnownWallets.DarkChampAscent
            ]
            
            Elem.button [
                Attr.create "type" "button"
                Attr.create "class" "copy-btn"
                Attr.create "data-copy" KnownWallets.DarkChampAscent
                Attr.create "aria-label" "Copy wallet address"
                Attr.create "title" "Copy address"
            ] [
                Text.raw "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' fill='none' aria-hidden='true'><rect x='9' y='9' width='13' height='13' rx='2'></rect><path d='M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1'></path></svg>"
            ]
            Text.raw $" ({KnownWallets.DarkChampAscentNFD}) from any confirmed wallet. "
            Elem.br [ ]
            Text.raw "After 5-7 minutes your balance should be updated."

            Elem.script [
                XmlAttribute.KeyValueAttr("src", "copy-wallet.js")
                XmlAttribute.KeyValueAttr("defer", "defer")
            ] []
        ]