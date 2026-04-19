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
        Page.TopDonaters,        "Donaters", WebEmoji.TopDonaters
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

let champCard (i: int) (c: ChampShortInfo) =
    Html.div [
        prop.className "monster-card"
        prop.children [
            Html.div [
                prop.className "monster-card-rank"
                prop.text (string (i + 1))
            ]
            ipfsImg c.IPFS "monster-card-img"
            Html.div [
                prop.className "monster-card-info"
                prop.children [
                    champLink c.ID c.Name
                    Html.span [
                        prop.className "monster-card-xp"
                        prop.text $"{c.XP} XP"
                    ]
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
            prop.className "leaderboard monster-leaderboard"
            prop.children [
                for (i, c) in champs |> List.indexed ->
                    champCard i c
            ]
        ])

let monsterCard (i: int) (m: MonsterShortInfo) =
    Html.div [
        prop.className "monster-card"
        prop.children [
            Html.div [
                prop.className "monster-card-rank"
                prop.text (string (i + 1))
            ]
            Html.img [ 
                prop.className "monster-card-img"
                Utils.srcMonsterImg m.Pic
                prop.alt ""
            ]
            Html.div [
                prop.className "monster-card-info"
                prop.children [
                    monsterLink m.ID m.Name
                    Html.span [
                        prop.className "monster-card-class muted"
                        prop.text (Display.monsterClass (m.MType, m.MSubType))
                    ]
                    Html.span [
                        prop.className "monster-card-xp"
                        prop.text $"{m.XP} XP"
                    ]
                ]
            ]
        ]
    ]

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
            prop.className "leaderboard monster-leaderboard"
            prop.children [
                for (i, m) in monsters |> List.indexed ->
                    monsterCard i m
            ]
        ])

[<ReactComponent>]
let LeaderboardDonatersPage () =
    let data, setData = React.useState<Deferred<TopDonatersDTO>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getTopDonaters ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun donaters ->
        Html.div [
            prop.className "leaderboard donaters-layout"
            prop.children [

                Html.section [
                    prop.className "block donaters-top"
                    prop.children [
                        Html.h2 [ prop.text $"{WebEmoji.Leaderboard} All time top" ]
                        Html.table [
                            Html.thead [ Html.tr [ Html.th [ prop.text "#" ]; Html.th [ prop.text "Name" ]; Html.th [ prop.dangerouslySetInnerHTML $"{WebEmoji.DarkCoin}" ] ] ]
                            Html.tbody [
                                for (i, d) in donaters.Top |> List.indexed ->
                                    Html.tr [
                                        Html.td [ prop.text (string (i + 1)) ]
                                        Html.td [ prop.text d.Donater ]
                                        Html.td [ prop.dangerouslySetInnerHTML $"{d.Amount} {WebEmoji.DarkCoin}" ]
                                    ]
                            ]
                        ]
                    ]
                ]

                Html.section [
                    prop.className "block donaters-recent"
                    prop.children [
                        Html.h2 [ prop.text $"{WebEmoji.TopDonaters} Recent" ]
                        Html.table [
                            Html.thead [ Html.tr [ Html.th [ prop.text "#" ]; Html.th [ prop.text "Name" ]; Html.th [ prop.className "col-tx"; prop.text "Tx" ]; Html.th [ prop.dangerouslySetInnerHTML $"{WebEmoji.DarkCoin}" ] ] ]
                            Html.tbody [
                                for (i, d) in donaters.Latest |> List.indexed ->
                                    Html.tr [
                                        Html.td [
                                            Html.a [
                                                prop.href $"https://explorer.perawallet.app/tx/{d.Tx}"
                                                prop.target "_blank"
                                                prop.title d.Tx
                                                prop.text (string (i + 1))
                                            ]
                                        ]
                                        Html.td [ prop.text d.Donater ]
                                        Html.td [
                                            prop.className "col-tx"
                                            prop.children [
                                                Html.a [
                                                    prop.href $"https://explorer.perawallet.app/tx/{d.Tx}"
                                                    prop.target "_blank"
                                                    prop.title d.Tx
                                                    prop.text (d.Tx.[..5] + "…" + d.Tx.[d.Tx.Length-4..])
                                                ]
                                            ]
                                        ]
                                        Html.td [ prop.dangerouslySetInnerHTML $"{d.Amount} {WebEmoji.DarkCoin}" ]
                                    ]
                            ]
                        ]
                    ]
                ]
            ]
        ])