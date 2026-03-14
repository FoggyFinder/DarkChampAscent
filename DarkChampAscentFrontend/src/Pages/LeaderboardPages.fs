module Pages.Leaderboard

open Feliz
open Types
open Display
open Components

[<ReactComponent>]
let LeaderboardGeneralPage () =
    let sections = [
        Page.TopChamps,          "Champs",            WebEmoji.Champs
        Page.TopMonsters,        "Monsters",          WebEmoji.Monsters
        Page.TopDonaters,        "Donaters (players)", WebEmoji.TopDonaters
        Page.TopUnknownDonaters, "Donaters (wallets)", WebEmoji.TopUnknownDonaters
    ]
    Html.div [
        prop.className "leaderboard"
        prop.children [
            for (page, label, _icon) in sections ->
                let href = page.Route
                Html.section [
                    prop.className "block"
                    prop.children [
                        Html.a [ prop.href href; prop.onClick (Nav.navTo href); prop.text label ]
                    ]
                ]
        ]
    ]

[<ReactComponent>]
let LeaderboardChampsPage () =
    let data, setData = React.useState<Deferred<ChampShortInfo list>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getTopChamps ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun champs ->
        Html.div [
            prop.className "leaderboard"
            prop.children [
                Html.table [
                    Html.thead [ Html.tr [ Html.th [prop.text "#"]; Html.th [prop.text "Pic"]; Html.th [prop.text "Name"]; Html.th [prop.text "XP"] ] ]
                    Html.tbody [
                        for (i, c) in champs |> List.indexed ->
                            Html.tr [
                                Html.td [prop.text (string (i+1))]
                                Html.td [ipfsImg c.IPFS "picSmall"]
                                Html.td [champLink c.ID c.Name]
                                Html.td [prop.text (string c.XP)]
                            ]
                    ]
                ]
            ]
        ])

[<ReactComponent>]
let LeaderboardMonstersPage () =
    let data, setData = React.useState<Deferred<MonsterShortInfo list>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getTopMonsters ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun monsters ->
        Html.div [
            prop.className "leaderboard"
            prop.children [
                Html.table [
                    Html.thead [ Html.tr [ Html.th [prop.text "#"]; Html.th [prop.text "Pic"]; Html.th [prop.text "Name"]; Html.th [prop.text "Class"]; Html.th [prop.text "XP"] ] ]
                    Html.tbody [
                        for (i, m) in monsters |> List.indexed ->
                            Html.tr [
                                Html.td [prop.text (string (i+1))]
                                Html.td [ 
                                    Html.img [ prop.className "picSmall"; Utils.srcMonsterImg m.Pic; prop.alt "" ]
                                ]
                                Html.td [monsterLink m.ID m.Name]
                                Html.td [prop.text (Display.monsterClass (m.MType, m.MSubType)) ]
                                Html.td [prop.text (string m.XP)]
                            ]
                    ]
                ]
            ]
        ])

[<ReactComponent>]
let LeaderboardDonatersPage () =
    let data, setData = React.useState<Deferred<{| name: string; amount: decimal |} list>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getTopDonaters ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun donaters ->
        Html.div [
            prop.className "leaderboard"
            prop.children [
                Html.div [
                    prop.className "wallet-table"
                    prop.children [
                        Html.table [
                            Html.thead [ Html.tr [ Html.th [prop.text "#"]; Html.th [prop.text "Name"]; Html.th [prop.text "DarkCoins"] ] ]
                            Html.tbody [
                                for (i, d) in donaters |> List.indexed ->
                                    Html.tr [
                                        Html.td [prop.text (string (i+1))]
                                        Html.td [prop.text d.name]
                                        Html.td [prop.dangerouslySetInnerHTML $"{d.amount} {WebEmoji.DarkCoin}"]
                                    ]
                            ]
                        ]
                    ]
                ]
            ]
        ])

[<ReactComponent>]
let LeaderboardUnknownDonatersPage () =
    let data, setData = React.useState<Deferred<Donation list>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getTopUnknownDonaters ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun donaters ->
        Html.div [
            prop.className "leaderboard"
            prop.children [
                Html.div [
                    prop.className "wallet-table"
                    prop.children [
                        Html.table [
                            Html.thead [ Html.tr [ Html.th [prop.text "#"]; Html.th [prop.text "Wallet"]; Html.th [prop.text "DarkCoins"] ] ]
                            Html.tbody [
                                for (i, d) in donaters |> List.indexed ->
                                    let dStr =
                                        match d.Donater with
                                        | Donater.Unknown wallet -> wallet
                                        | _ -> ""
                                    Html.tr [
                                        Html.td [prop.text (string (i+1))]
                                        Html.td [prop.text dStr]
                                        Html.td [prop.dangerouslySetInnerHTML $"{d.Amount} {WebEmoji.DarkCoin}"]
                                    ]
                            ]
                        ]
                    ]
                ]
            ]
        ])
