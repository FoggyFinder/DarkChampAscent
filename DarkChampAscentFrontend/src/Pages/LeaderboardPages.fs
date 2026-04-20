module Pages.Leaderboard

open Feliz
open Types
open Display
open Components
open UI

[<ReactComponent>]
let LeaderboardGeneralPage () =
    let sections = [
        Page.TopChamps, "Champs", WebEmoji.Champs
        Page.TopMonsters, "Monsters", WebEmoji.Monsters
        Page.TopDonaters, "Donaters", WebEmoji.TopDonaters
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
            prop.className "leaderboard monster-leaderboard"
            prop.children [
                for (i, c) in champs |> List.indexed ->
                    Cards.champCard i c
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
            prop.className "leaderboard monster-leaderboard"
            prop.children [
                for (i, m) in monsters |> List.indexed ->
                    Cards.monsterCard i m
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
                        Html.div [
                            prop.className "donation-grid"
                            prop.children [
                                for (i, d) in donaters.Latest |> List.indexed ->
                                    Cards.donationCard i d
                            ]
                        ]
                    ]
                ]
            ]
        ])