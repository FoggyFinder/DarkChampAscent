module Pages.NonAuth

open Feliz
open Types
open Components
open DTO
open Display
open GameLogic.Shop
open System

[<ReactComponent>]
let ShopPage () =
    let data, setData = React.useState<Deferred<ShopDTO>> Loading
    let msg, setMsg   = React.useState<string option> None

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
                match d.Balance with
                | Some b -> Html.p [ prop.dangerouslySetInnerHTML (sprintf "Balance: %s %s" (string b) WebEmoji.DarkCoin) ]
                | None -> Html.none
                match msg with
                | Some m -> Html.p [ prop.className "action-msg"; prop.text m ]
                | None -> Html.none
                Html.table [
                    Html.thead [
                        Html.tr [
                            Html.th [prop.text "#"]; Html.th [prop.text "Kind"]; Html.th [prop.text "Price"]
                            Html.th [prop.text "Duration"]; Html.th [prop.text "Effect"]; Html.th [prop.text "Target"]
                            if d.Balance.IsSome then Html.th [prop.text ""]
                        ]
                    ]
                    Html.tbody [
                        for (i, item) in d.Items |> List.indexed ->
                            let sir = Display.ShopItemRow(item, d.Price)
                            let canAfford = d.Balance |> Option.map (fun b -> b >= sir.Price) |> Option.defaultValue false
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
                                if d.Balance.IsSome then
                                    Html.td [
                                        Html.button [
                                            prop.className "btn btn-primary btn-sm"; prop.disabled (not canAfford)
                                            prop.onClick (fun _ ->
                                                async {
                                                    let! r = Api.buyItem item
                                                    match r with
                                                    | Ok ()   -> setMsg (Some "Purchased!")
                                                    | Error e -> setMsg (Some ("Error: " + e))
                                                } |> Async.StartImmediate)
                                            prop.text "Buy"
                                        ]
                                    ]
                            ]
                    ]
                ]
                if d.Balance.IsSome then
                    let p = Page.Storage
                    Html.p [
                        Html.text "Check your "
                        Html.a [ prop.href p.Route; prop.onClick (Nav.navTo p.Route); prop.text "storage" ]
                        Html.text "."
                    ]
            ]
        ])

[<ReactComponent>]
let MonstersEffectsPage () =
    let data, setData = React.useState<Deferred<MonsterUnderEffect list>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getMonstersEffects ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun monsters ->
        Html.div [
            prop.className "monsters-under-effects"
            prop.children [
                if monsters.IsEmpty then
                    Html.p [ prop.text "No monsters under effects." ]
                else
                    Html.table [
                        Html.thead [ Html.tr [ Html.th [prop.text "#"]; Html.th [prop.text "Pic"]; Html.th [prop.text "Name"]; Html.th [prop.text "Effect"]; Html.th [prop.text "Rounds"] ] ]
                        Html.tbody [
                            for (i, m) in monsters |> List.sortByDescending (fun m -> m.RoundsLeft) |> List.indexed ->
                                Html.tr [
                                    Html.td [prop.text (string (i+1))]
                                    Html.td [ 
                                      Html.img [ prop.className "picSmall"; Utils.srcMonsterImg m.Pic; prop.alt "" ] 
                                    ]
                                    Html.td [monsterLink (uint64 m.ID) m.Name]
                                    Html.td [prop.text $"{DisplayEnum.Effect m.Effect}"]
                                    Html.td [prop.text $"{m.RoundsLeft} {WebEmoji.Rounds}"]
                                ]
                        ]
                    ]
            ]
        ])