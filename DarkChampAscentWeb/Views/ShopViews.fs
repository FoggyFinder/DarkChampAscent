module ShopView

open Falco.Markup
open UI
open Display
open GameLogic.Shop
open System

let shop (isAuth:bool) (shopItems:ShopItemRow list) =
    Elem.main [
        Attr.class' "shop"
        Attr.role "main"
    ] [
        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "" ]
                Elem.th [] [ Text.raw "Kind" ]
                Elem.th [] [ Text.raw "Price" ]
                Elem.th [] [ Text.raw "Duration" ]
                Elem.th [] [ Text.raw "Effect" ]
                Elem.th [] [ Text.raw "Target" ]
                if isAuth then
                    Elem.th [] [ Text.raw "" ]
            ]
            for (i, item) in shopItems |> List.indexed do
                let price = Shop.getPrice item.Item
                let vStr =
                    let v = Shop.getValue item.Item
                    if v <> 0L then $"+ {v}" else ""
                let dStr = 
                    if item.Duration = Int32.MaxValue then ""
                    else item.Duration.ToString()
                Elem.tr [] [
                    Elem.td [] [ Text.raw $"{i + 1}" ]
                    Elem.td [] [ Text.raw $"{item.Kind}" ]
                    Elem.td [] [ Text.raw $"{Display.toRound6StrD price} {WebEmoji.USDC} (~{Display.toRound6StrD item.Price} DarkCoins)" ]
                    Elem.td [] [ Text.raw $"{dStr}" ]
                    Elem.td [] [ Text.raw $"{vStr}" ]
                    Elem.td [] [ Text.raw $"{item.Target}" ]
                    if isAuth then
                        Elem.td [] [
                            Elem.form [
                                Attr.methodPost
                                Attr.action Route.buyitem
                            ] [
                                Elem.input [
                                    Attr.typeHidden
                                    Attr.name "shopitem"
                                    Attr.value $"{item.Item}"
                                ]

                                Elem.input [
                                    Attr.class' "btn btn-primary"
                                    Attr.typeSubmit
                                    Attr.value "Buy"
                                ]
                            ]
                        ]
                ]
        ]
        if isAuth then
            Elem.div [ Attr.style "float: right" ] [
                Text.raw "You can check your storage "
                Elem.a [ Attr.href Route.storage ] [ Text.raw $"here" ] 
            ]
    ]
 

let storage (shopItems:(ShopItem * int) list) (champs: (uint64 * string * string) list) =
    let champsSelector =
        let champOptions =
            champs |> List.map(fun (id, name, ipfs) ->
                Elem.option [ Attr.value (string id) ] [
                    Text.raw name
                ]
            )
        Elem.div [ Attr.class' "select-wrap"; Attr.style "margin:8px 0;" ] [
            Elem.select [
                Attr.class' "tom-select"
                Attr.id "champ"
                Attr.name "champ"
            ] (champOptions)
        ]

    Elem.main [
        Attr.class' "storage"
        Attr.role "main"
    ] [
        
        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "" ]
                Elem.th [] [ Text.raw "Item" ]
                Elem.th [] [ Text.raw "Duration" ]
                Elem.th [] [ Text.raw "Effect" ]
                Elem.th [] [ Text.raw "Target" ]
                Elem.th [] [ Text.raw "Amount" ]
                Elem.th [] [ Text.raw "" ]
            ]
            for (i, (item, amount)) in shopItems |> List.indexed do
                let vStr =
                    let v = Shop.getValue item
                    if v <> 0L then $"+ {v}" else ""
                let dStr =
                    let dur = Shop.getRoundDuration item
                    if dur = Int32.MaxValue then ""
                    else dur.ToString()
                let target = Shop.getTarget item
                Elem.tr [] [
                    Elem.td [] [ Text.raw $"{i + 1}" ]
                    Elem.td [] [ Text.raw $"{item}" ]
                    Elem.td [] [ Text.raw $"{dStr}" ]
                    Elem.td [] [ Text.raw $"{vStr}" ]
                    Elem.td [] [ Text.raw $"{target}" ]
                    Elem.td [] [ Text.raw $"{amount}" ]
                    Elem.td [] [
                        Elem.form [
                            Attr.methodPost
                            Attr.action Route.use'
                        ] [
                            champsSelector
                            Elem.input [
                                Attr.typeHidden
                                Attr.name "useitem"
                                Attr.value $"{item}"
                            ]
                            Elem.input [
                                Attr.class' "btn btn-primary"
                                Attr.typeSubmit
                                Attr.value "Use"
                            ]
                        ]
                    ]
                ]
        ]

        Elem.div [ Attr.style "float: right" ] [
            Text.raw "You can buy more items "
            Elem.a [ Attr.href Route.shop ] [ Text.raw $"here" ] 
        ]
    ]
   