module Pages.NonAuth

open Feliz
open Types
open Components
open DTO
open UI
open UseWallet

[<ReactComponent>]
let ShopPage () =
    let data, setData = React.useState<Deferred<ShopDTO>> Loading
    let msg, setMsg   = React.useState<string option> None
    let wallet = useWallet ()

    React.useEffect((fun () ->
        async {
            let! r = Api.getShop ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])

    deferred data (fun d ->
        Html.div [
            prop.className "shop"
            prop.children [
                match msg with
                | Some m -> Html.p [ prop.className "action-msg"; prop.text m ]
                | None -> Html.none
                Html.div [
                    prop.className "shop-grid"
                    prop.children [
                        for (i, item) in d.Items |> List.indexed ->
                            Cards.shopItemCard i item d.Price wallet setMsg
                    ]
                ]
            ]
        ])

[<ReactComponent>]
let DefeatedMonstersPage () =
    let data, setData = React.useState<Deferred<MonsterUnderEffect list>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getDefeatedMonsters ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun monsters ->
        Html.div [
            prop.className "monsters-under-effects"
            prop.children [
                if monsters.IsEmpty then
                    Html.p [ prop.className "muted"; prop.text "No monsters defeated." ]
                else
                    Html.div [
                        prop.className "monster-leaderboard"
                        prop.children [
                            for (i, m) in monsters |> List.sortByDescending (fun m -> m.RoundsLeft) |> List.indexed ->
                                Cards.defeatedMonsterCard i m
                        ]
                    ]
            ]
        ])

[<ReactComponent>]
let DefeatedChampsPage () =
    let data, setData = React.useState<Deferred<ChampUnderEffect list>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getDefeatedChamps ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun champs ->
        Html.div [
            prop.className "champs-under-effects"
            prop.children [
                if champs.IsEmpty then
                    Html.p [ prop.className "muted"; prop.text "No champs defeated." ]
                else
                    Html.div [
                        prop.className "monster-leaderboard"
                        prop.children [
                            for (i, c) in champs |> List.sortByDescending (fun c -> c.RoundsLeft) |> List.indexed ->
                                Cards.defeatedChampCard i c
                        ]
                    ]
            ]
        ])