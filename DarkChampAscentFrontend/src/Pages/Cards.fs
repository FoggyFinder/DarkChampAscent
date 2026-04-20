module UI.Cards

open GameLogic.Shop
open DarkChampAscent.Api
open UseWallet
open Display

open System

open Feliz
open Types
open Components

let shopItemCard (i: int) (item: ShopItem) (price: decimal) (wallet: IUseWalletResult) (setMsg: string option -> unit) =
    let sir = Display.ShopItemRow(item, price)
    let dStr = if sir.Duration = Int32.MaxValue then "" elif sir.Duration = 0 then "" else $"{sir.Duration} {WebEmoji.Rounds} rounds"
    let vStr = let v = Shop.getValue item in if v <> 0L then $"+{v} {webEmojiFromSITarget sir.Target}" else ""
    let target = DisplayEnum.ShopItemTarget sir.Target
    let itemPrice = Shop.getPrice item

    let meta =
        [ target; vStr; dStr ]
        |> List.filter (fun s -> s <> "")
        |> String.concat "  •  "

    Html.div [
        prop.className "shop-card"
        prop.children [
            Html.div [
                prop.className "shop-card-header"
                prop.children [
                    Html.span [
                        prop.className "shop-card-kind"
                        prop.text $"{i+1}. {DisplayEnum.ShopItemKind sir.Kind}"
                    ]
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
                                    | Error err -> setMsg (Some err)
                                } |> Async.StartImmediate)
                            prop.text "Buy"
                        ]
                    | None -> Html.none
                ]
            ]
            if meta <> "" then
                Html.div [
                    prop.className "shop-card-meta"
                    prop.text meta
                ]
            Html.div [
                prop.className "shop-card-price"
                prop.dangerouslySetInnerHTML $"{Display.toRound6StrD itemPrice} {WebEmoji.USDC} <span class='muted'>(~{Display.toRound6StrD sir.Price} {WebEmoji.DarkCoin} DarkCoins)</span>"
            ]
            match wallet.activeAddress with
            | None ->
                Html.p [ prop.className "notice"; prop.text "Connect wallet on 'Account' page to buy" ]
            | Some _ -> Html.none
        ]
    ]

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


let storageItemCard (i: int) (item: ShopItem) (amount: int) (champs: ChampInfoWithStat list) (selected: string) (setSelected: string -> unit) (setMsg: string option -> unit) (reload: unit -> unit) =
    let sit = Shop.getTarget item
    let vStr = let v = Shop.getValue item in if v <> 0L then $"+{v} {webEmojiFromSITarget sit}" else ""
    let dStr = let dur = Shop.getRoundDuration item in if dur = Int32.MaxValue then "" elif dur = 0 then "" else $"{dur} {WebEmoji.Rounds} rounds"
    
    let target = DisplayEnum.ShopItemTarget (Shop.getTarget item)
    let kind = DisplayEnum.ShopItemKind (Shop.getKind item)

    let meta =
        [ target; vStr; dStr ]
        |> List.filter (fun s -> s <> "")
        |> String.concat "  •  "

    Html.div [
        prop.className "shop-card"
        prop.children [
            Html.div [
                prop.className "shop-card-header"
                prop.children [
                    Html.span [ prop.className "shop-card-kind"; prop.text $"{i+1}. {kind}" ]
                    Html.span [ prop.className "shop-card-amount muted"; prop.text $"×{amount}" ]
                ]
            ]
            if meta <> "" then
                Html.div [ prop.className "shop-card-meta"; prop.text meta ]
            Html.div [
                prop.className "storage-use"
                prop.children [
                    CustomSelectInput
                        selected
                        (fun s -> champs |> List.tryPick (fun rpc -> if (rpc.ID.ToString()) = s then Some (rpc.ID.ToString()) else None) |> Option.defaultValue "" |> setSelected)
                        [ for c in champs -> (string c.ID), c.Name, Some (Links.IPFS + c.IPFS) ]

                    match champs |> List.tryPick (fun rpc -> if (rpc.ID.ToString()) = selected then Some rpc else None) with
                    | Some sChamp -> chTable sChamp.Stat                          
                    | None -> ()

                    Html.div [
                        prop.className "storage-use-footer"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-sm btn-primary"
                                prop.onClick (fun _ ->
                                    match UInt64.TryParse selected with
                                    | true, cid ->
                                        async {
                                            let! r = Api.useItem item cid
                                            match r with
                                            | Ok () -> setMsg (Some "Used!"); reload ()
                                            | Error e -> setMsg (Some ("Error: " + e))
                                        } |> Async.StartImmediate
                                    | _ -> ())
                                prop.text "Use"
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let myChampCard (i: int) (c: ChampShortInfo) (balance: decimal) =
    Html.div [
        prop.className "monster-card"
        prop.children [
            Html.div [ prop.className "monster-card-rank"; prop.text (string (i + 1)) ]
            ipfsImg c.IPFS "monster-card-img"
            Html.div [
                prop.className "monster-card-info"
                prop.children [
                    champLink c.ID c.Name
                    Html.span [
                        prop.className "monster-card-xp"
                        prop.text $"{WebEmoji.Gem} {c.XP} XP"
                    ]
                    Html.span [
                        prop.className "muted"
                        prop.dangerouslySetInnerHTML $"{balance} {WebEmoji.DarkCoin}"
                    ]
                ]
            ]
        ]
    ]

let champEffectCard (i: int) (c: ChampUnderEffect) =
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
                        prop.text $"{DisplayEnum.Effect c.Effect}"
                    ]
                    Html.span [
                        prop.className "muted"
                        prop.text $"{c.RoundsLeft} {WebEmoji.Rounds} rounds left"
                    ]
                ]
            ]
        ]
    ]

let myMonsterCard (i: int) (m: MonsterShortInfo) =
    Html.div [
        prop.className "monster-card"
        prop.children [
            Html.div [ prop.className "monster-card-rank"; prop.text (string (i + 1)) ]
            Html.img [ prop.className "monster-card-img"; Utils.srcMonsterImg m.Pic; prop.alt "" ]
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
                        prop.text $"{WebEmoji.Gem} {m.XP} XP"
                    ]
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
                        prop.text $"{WebEmoji.Gem} {c.XP} XP"
                    ]
                ]
            ]
        ]
    ]

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
                        prop.text $"{WebEmoji.Gem} {m.XP} XP"
                    ]
                ]
            ]
        ]
    ]

let donationCard (i: int) (d: LatestDonationDTO) =
    Html.a [
        prop.className "donation-card"
        prop.href $"https://explorer.perawallet.app/tx/{d.Tx}"
        prop.target.blank
        prop.title d.Tx
        prop.children [
            Html.div [
                prop.className "donation-card-header"
                prop.children [
                    Html.span [ prop.className "donation-card-name"; prop.text $"{i+1}. {d.Donater}" ]
                    Html.span [ prop.className "donation-card-amount"; prop.dangerouslySetInnerHTML $"{d.Amount} {WebEmoji.DarkCoin}" ]
                ]
            ]
            Html.span [
                prop.className "donation-card-tx muted"
                prop.text (d.Tx.[..5] + "…" + d.Tx.[d.Tx.Length-4..])
            ]
        ]
    ]