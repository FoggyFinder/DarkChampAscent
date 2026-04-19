module Pages.NonAuth

open Feliz
open Types
open Components
open DTO
open Display
open GameLogic.Shop
open System
open UseWallet
open DarkChampAscent.Api

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
                Html.table [
                    Html.thead [
                        Html.tr [
                            Html.th [prop.text "#"]; Html.th [prop.text "Kind"]; Html.th [prop.text "Price"]
                            Html.th [prop.text "Duration"]; Html.th [prop.text "Effect"]; Html.th [prop.text "Target"]
                        ]
                    ]
                    Html.tbody [
                        for (i, item) in d.Items |> List.indexed ->
                            let sir = Display.ShopItemRow(item, d.Price)
                            let dStr = if sir.Duration = Int32.MaxValue then "" else string sir.Duration
                            let vStr =
                                let v = Shop.getValue item
                                if v <> 0L then $"+ {v}" else ""
                            let target = Shop.getTarget item
                            let price = Shop.getPrice item
                            Html.tr [
                                Html.td [prop.text (string (i+1))]
                                Html.td [prop.text $"{DisplayEnum.ShopItemKind sir.Kind}"]
                                Html.td [prop.dangerouslySetInnerHTML $"{Display.toRound6StrD price} {WebEmoji.USDC} (~{Display.toRound6StrD sir.Price} {WebEmoji.DarkCoin} DarkCoins)"]
                                Html.td [prop.text dStr]
                                Html.td [prop.text vStr]
                                Html.td [prop.text $"{DisplayEnum.ShopItemTarget target}"]
                                
                                Html.td [
                                    match wallet.activeAddress with
                                    | Some awallet ->
                                        Html.button [
                                            prop.className "btn btn-primary btn-sm"
                                            prop.onClick (fun _ ->
                                                async {
                                                    "" |> Some |> setMsg
                                                    let tx = Tx.BuyItem (awallet, item, 1ul)
                                                    let! r = Api.createTx tx
                                                    match r with
                                                    | Ok txnb64 ->
                                                        let! sr =
                                                            UseWallet.signTx (Api.submitTx tx) wallet txnb64
                                                                (fun () -> "Processing request..." |> Some |> setMsg)
                                                        match sr with
                                                        | Ok m -> m
                                                        | Error err -> $"Error: {err}"
                                                        |> Some |> setMsg 
                                                    | Error err ->
                                                        setMsg (Some err)
                                                } |> Async.StartImmediate)
                                            prop.text "Buy"
                                        ]
                                    | None ->
                                        Html.p [ prop.className "notice"; prop.text "Connect wallet on 'Account' page to buy" ]
                                ]
                            ]
                    ]
                ]
            ]
        ])

let defeatedMonsterCard (i: int) (m: MonsterUnderEffect) =
    Html.div [
        prop.className "monster-card"
        prop.children [
            Html.div [ prop.className "monster-card-rank"; prop.text (string (i + 1)) ]
            Html.img [ prop.className "monster-card-img"; Utils.srcMonsterImg m.Pic; prop.alt "" ]
            Html.div [
                prop.className "monster-card-info"
                prop.children [
                    monsterLink (uint64 m.ID) m.Name
                    Html.span [
                        prop.className "monster-card-xp"
                        prop.text $"{m.RoundsLeft} {WebEmoji.Rounds} rounds left"
                    ]
                ]
            ]
        ]
    ]

let defeatedChampCard (i: int) (c: ChampUnderEffect) =
    Html.div [
        prop.className "monster-card"
        prop.children [
            Html.div [ prop.className "monster-card-rank"; prop.text (string (i + 1)) ]
            ipfsImg c.IPFS "monster-card-img"
            Html.div [
                prop.className "monster-card-info"
                prop.children [
                    champLink (uint64 c.ID) c.Name
                    Html.span [
                        prop.className "monster-card-xp"
                        prop.text $"{c.RoundsLeft} {WebEmoji.Rounds} rounds left"
                    ]
                ]
            ]
        ]
    ]

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
                                defeatedMonsterCard i m
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
                                defeatedChampCard i c
                        ]
                    ]
            ]
        ])