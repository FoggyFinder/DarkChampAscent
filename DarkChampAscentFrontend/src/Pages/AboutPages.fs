module Pages.About

open Feliz
open Types
open Display
open Components
open GameLogic.Champs
open GameLogic.Battle
open GameLogic.Rewards
open DTO
open System

let private getTraitContent (trt: Trait) (fromTrait: 'a -> Stat) (values: 'a list) (toStr: 'a -> string) =
    let chs = TraitCharacteristic.impact.[trt]
    Html.div [
        prop.className "trait-section"
        prop.children [
            Html.h2 [ prop.text $"{trt}" ]
            Html.p [ prop.text "Affects:" ]
            Html.ul [
                for ch in chs ->
                    Html.li [ prop.text $"{Display.webEmojiFromChar ch} {DisplayEnum.Characteristic ch}" ]
            ]
            Html.table [
                Html.thead [
                    Html.tr [
                        Html.th [ prop.text "" ]
                        for ch in chs do Html.th [ prop.text $"{Display.webEmojiFromChar ch}" ]
                    ]
                ]
                Html.tbody [
                    for b in values do
                        Html.tr [
                            let stat = fromTrait b
                            yield Html.th [ prop.text (toStr b) ]
                            for ch in chs do
                                yield Html.td [ prop.text $"{stat.GetValueBy ch}" ]
                        ]
                ]
            ]
        ]
    ]

[<ReactComponent>]
let TraitsPage () =
    let selected, setSelected = React.useState Trait.Background

    let content =
        match selected with
        | Trait.Background -> getTraitContent Trait.Background Champ.fromBackground AllEnums.Backgrounds DisplayEnum.Background
        | Trait.Skin       -> getTraitContent Trait.Skin Champ.fromSkin AllEnums.Skins DisplayEnum.Skin
        | Trait.Weapon     -> getTraitContent Trait.Weapon Champ.fromWeapon AllEnums.Weapons DisplayEnum.Weapon
        | Trait.Head       -> getTraitContent Trait.Head Champ.fromHead AllEnums.Heads DisplayEnum.Head
        | Trait.Armour     -> getTraitContent Trait.Armour Champ.fromArmour AllEnums.Armours DisplayEnum.Armour
        | Trait.Magic      -> getTraitContent Trait.Magic Champ.fromMagic AllEnums.Magic DisplayEnum.Magic
        | Trait.Extra      -> getTraitContent Trait.Extra Champ.fromExtra AllEnums.Extras DisplayEnum.Extra

    Html.div [
        prop.className "traits"
        prop.children [
            Html.div [
                prop.className "traits-tabs"
                prop.children [
                for trt in AllEnums.Traits ->
                    Html.button [
                        prop.className (if trt = selected then "traits-tab active" else "traits-tab")
                        prop.custom("data-icon", Display.webEmojiFromTrait trt)
                        prop.onClick (fun _ -> setSelected trt)
                        prop.children [
                            Html.span [ prop.className "traits-tab-label"; prop.text $"{trt}" ]
                        ]
                    ]
                ]
            ]
            content
        ]
    ]

[<ReactComponent>]
let StatsPage () =
    let data, setData = React.useState<Deferred<Stats>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getStats ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    let optRow label WebEmoji (v: 'a option) =
        match v with
        | Some x -> statRow WebEmoji label (string x)
        | None -> Html.none
    let optTRow label WebEmoji (v: WalletValue option) =
        let link (w:string) =
            $"https://explorer.perawallet.app/transactions/?transaction_list_address={w}&transaction_list_sender={KnownWallets.DarkChampAscent}"
            // $"https://allo.info/account/{w}/txns?sender={KnownWallets.DarkChampAscent}"
            |> Some
        match v with
        | Some x -> fullStatRow WebEmoji label (string x.Value) (link x.Wallet)
        | None -> Html.none
    deferred data (fun s ->
        Html.div [
            prop.className "stats"
            prop.children [
                Html.h2 [ prop.text $"{WebEmoji.Stats} Statistics {WebEmoji.Stats}" ]
                let gs = s.GameStats
                Html.table [
                    prop.className "stats-table"
                    prop.children [
                        Html.tbody [
                            optRow "Players registered" WebEmoji.Account gs.Players
                            optRow "Confirmed players" WebEmoji.CheckMark gs.ConfirmedPlayers
                            optRow "Champions" WebEmoji.Champs gs.Champs
                            optRow "Custom monsters" WebEmoji.Monsters gs.CustomMonsters
                            optRow "Battles" WebEmoji.Battle gs.Battles
                            optRow "Rounds" WebEmoji.Rounds gs.Rounds
                        ]
                    ]
                ]
                Html.h2 [ prop.text $"{WebEmoji.MoneyBag} Rewards {WebEmoji.MoneyBag}"]
                Html.table [
                    prop.className "stats-table"
                    prop.children [
                        Html.tbody [
                            optRow "Players earned" "🤖" s.Rewards
                            optTRow "DAO" WebEmoji.DAO s.TStats.Dao
                            optTRow "Devs" WebEmoji.Dev s.TStats.Devs
                            optTRow "Reserve" WebEmoji.Reserve s.TStats.Reserve
                            optTRow "Burnt" WebEmoji.Fire s.TStats.Burnt
                            optTRow "Staking" WebEmoji.Staking s.TStats.Staking
                        ]
                    ]
                ]
            ]
        ])

[<ReactComponent>]
let HomePage () =
    let data, setData = React.useState<Deferred<RewardsPriceDTO>> Loading

    React.useEffect((fun () ->
        async {
            let! r = Api.getHome ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])

    deferred data (fun d ->
        let usdcs =
            d.Price
            |> Option.map (fun p -> sprintf "(~%s %s)" (string (Math.Round(p * d.Rewards, 2))) WebEmoji.USDC)
            |> Option.defaultValue ""
        

        Html.main [
            prop.className "home"
            prop.role "main"
            prop.children [

                Html.section [
                    prop.className "block rewards-section"
                    prop.children [
                        Html.table [
                            Html.tbody [
                                Html.tr [
                                    Html.td [prop.text "Total rewards pool"]
                                    Html.td [prop.dangerouslySetInnerHTML (sprintf "%s %s %s" (string d.Rewards) WebEmoji.DarkCoin usdcs)]
                                ]
                                match d.Price with
                                | Some p ->
                                    Html.tr [
                                        Html.td [prop.text "DarkCoin price"]
                                        Html.td [prop.text ((string p) + WebEmoji.USDC)]
                                    ]
                                | None -> Html.none
                                Html.tr [
                                    Html.td [ prop.text "Rounds in battle" ]
                                    Html.td [ prop.text (string Constants.RoundsInBattle) ]
                                ]
                                Html.tr [
                                    Html.td [ prop.text "Round duration" ]
                                    Html.td [ prop.text (string (BattleParams.RoundDuration())) ]
                                ]
                                Html.tr [
                                    Html.td [ prop.text "XP per level" ]
                                    Html.td [ prop.text (string Levels.XPPerLvl) ]
                                ]
                            ]
                        ]
                    ]
                ]

                Html.section [
                    prop.className "block rules-section"
                    prop.id "rules-section"
                    prop.children [

                        Html.h3 [prop.text "General"]
                        Html.p [prop.dangerouslySetInnerHTML (sprintf "DarkChampAscent started as an experiment - a discord bot where players collect DarkCoins (%s) by performing actions each round." WebEmoji.DarkCoin)]
                        
                        Html.p [
                            Html.text "To play, hold at least one NFT from"
                            Html.a [prop.href Links.DarkChampCollection; prop.target.blank; prop.text "Dark Coin Champions"]
                            Html.text " collection."
                        ]

                        Html.p [ 
                            prop.dangerouslySetInnerHTML $"All earned DarkCoins ({WebEmoji.DarkCoin}) are added to in-game Champion balance and automatically distributed at the end of every battle. No actions required from users."
                        ]
                        Html.h3 [prop.text "Characteristics"]
                        Html.ul [
                            for ch in AllEnums.Characteristics ->
                                Html.li [prop.text $"{Display.webEmojiFromChar ch} {DisplayEnum.Characteristic ch}"]
                        ]
                        Html.h3 [prop.text "Round actions"]
                        Html.ul [
                            for m in AllEnums.RoundActions ->
                                Html.li [prop.text $"{DisplayEnum.Move m}"]
                        ]
                    ]
                ]
            ]
        ])


[<ReactComponent>]
let TokenomicsPage () =
    Html.main [
        prop.className "tokenomics"
        prop.role "main"
        prop.children [

            Html.section [
                prop.className "block tokenomics-section"
                prop.id "tokenomics-section"
                prop.children [
                    Html.h3 [ prop.text "Tokenomics" ]

                    Html.p [ prop.className "notice"; prop.text "Project is in early stage of development, all numbers are subject to change without prior notice." ]

                    Html.p [ prop.text "All Darkcoins from donation or purchases (items or premium features) go to total rewards pool." ]
                    Html.b [ prop.dangerouslySetInnerHTML $"All prices are settled in USDC ({WebEmoji.USDC}). DarkCoin price updates periodically every few hours." ]
                    
                    Html.p [ prop.text "In case when no champs used a move those coins return to rewards pool." ]
                    Html.hr []

                    Html.h3 [ prop.text "Rewards" ]
                    
                    Html.div [
                        prop.className "charts-row"
                        prop.children [
                            Html.div [
                                prop.className "chart-item"
                                prop.children [
                                    Html.p [ prop.text "For each round rewards are splitted by following logic:" ]
                                    Charts.PieChart "champsChart" 420 420
                                        [ "Players"; "DAO"; "Devs"; "Reserve"; "Staking"; "Burn" ]
                                        [ 75.0; 10.0; 8.0; 5.0; 1.0; 1.0 ]
                                        [ "#74c67a"; "#e64a2b"; "#ffd60a"; "#00d6d6"; "#ff73d6"; "#3d7ef0" ]
                                ]
                            ]
                            Html.div [
                                prop.className "chart-item"
                                prop.children [
                                    Html.p [ prop.text "Each round rewards allocated for players splitted as:" ]
                                    Charts.PieChart "rewardsChart" 420 420
                                        [ "Damage / TotalDamage"; "Shield"; "MagicShield"; "Heal"; "Meditate"; "Attack"; "MagicAttack" ]
                                        [ 20.0; 11.0; 11.0; 11.0; 11.0; 5.5; 5.5 ]
                                        [ "#19c7f1"; "#5d8db3"; "#3ef24a"; "#b16bf0"; "#ff7f3f"; "#e64a2b"; "#d7ef2c" ]
                                ]
                            ]
                        ]
                    ]

                    Html.p [ prop.text "Rewards for specific round is calculated based on total amount as:" ]

                    Html.pre [
                        prop.className "formula-block"
                        prop.children [
                            Html.code [
                                prop.text (sprintf "Window = %d\nRoundsInBattle = %d\nBattleReward = InGameRewardsPool / Window\nRoundReward = BattleReward / RoundsInBattle"
                                    Window Constants.RoundsInBattle)
                            ]
                        ]
                    ]
                    Html.p [ prop.text "In case when no champs used a move those coins returned as rewards for next battles." ]
                    Html.hr []
                ]
            ]
        ]
    ]