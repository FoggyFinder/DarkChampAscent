module HomeView

open Falco.Markup
open GameLogic.Battle
open GameLogic.Champs
open GameLogic.Rewards
open Display
open UI

let private getImgSource (filename:string) =
    System.IO.Path.Combine("imgs", filename)

let home (rewards: decimal) (dcPriceO:decimal option) =
    let usdcs =
        match dcPriceO with
        | Some dcPrice ->
            let usdc = dcPrice * rewards
            $"(~{Display.toRound2StrD usdc} {WebEmoji.USDC})"
        | None -> ""
    Elem.main [
        Attr.class' "home"
        Attr.role "main"
    ] [
        Elem.section [ 
            Attr.class' "block rewards-section"
            Attr.id "rewards-section"
        ] [
            Elem.table [] [
                Elem.tr [] [
                    Elem.td [] [ Text.raw "Total rewards pool" ]
                    Elem.td [] [ 
                        Text.raw $"{Display.toRound6StrD rewards} {usdcs}" 
                    ]
                ]

                match dcPriceO with
                | Some dcPrice ->
                    Elem.tr [] [
                        Elem.td [] [ Text.raw $"Current DarkCoin price" ]
                        Elem.td [] [
                            Text.raw $"{dcPrice}"
                        ]
                    ]
                | _ -> ()
            ]
        ]

        Elem.section [ 
            Attr.class' "block rules-section"
            Attr.id "rules-section"
        ] [
            Text.h3 "General"
            Elem.div [ ] [
                Elem.p [ ] [ Text.raw """DarkChampAscent - is a discord game that allows players collect DarkCoins by performing one of
                    few available actions each round. This site is WebUi for it. """ ]
                
                Elem.p [ ] [ 
                    Text.raw "To play one has to hold at least one of "
                    Elem.a [ 
                        Attr.href Links.DarkChampCollection
                        Attr.targetBlank
                    ] [ Text.raw "Dark Coin Champions collection" ]
                    Text.raw " NFTs"
                ]

                Elem.p [ ] [ 
                    Text.raw "To play log-in with discord account and then follow "
                    Elem.a [ 
                        Attr.href Route.account
                    ] [ Text.raw "Account page" ]
                    Text.raw " to register and confirm any Algorand wallet"
                ]
                Elem.p [ ] [ 
                    Text.raw "For every NFT from confirmed wallet one in-game champion is created. Their starting stats are based on current"
                    Elem.a [ Attr.href Route.traits ] [ Text.raw "traits" ]
                ]
                Elem.p [ ] [ 
                    Text.raw "All earned DarkCoins are added to in-game Champion balance and automatically distributed at the end of every battle. No actions required from users."
                ]
            ]

            Text.h3 "Basics"
            Elem.div [ ] [

                Text.p "There are 8 characteristics:"
                Elem.ul [ ] [
                    Elem.li [ ] [ Text.raw "Health" ]
                    Elem.li [ ] [ Text.raw "Magic" ]
                    Elem.li [ ] [ Text.raw "Accuracy (increases chance to take damage)" ]
                    Elem.li [ ] [ Text.raw "Luck (increases chance to hit critical)" ]
                    Elem.li [ ] [ Text.raw "Attack" ]
                    Elem.li [ ] [ Text.raw "MagicAttack" ]
                    Elem.li [ ] [ Text.raw "Defense" ]
                    Elem.li [ ] [ Text.raw "MagicDefense" ]
                ]

                Text.p "In a round champ can perform one of 6 actions:"
                Elem.ul [ ] [
                    Elem.li [ ] [ Text.raw "Attack - takes damage" ]
                    Elem.li [ ] [ Text.raw "MagicAttack - takes damage from the enemy but reduces magic as well" ]
                    Elem.li [ ] [ Text.raw "Shield - increase defense for 1 round" ]
                    Elem.li [ ] [ Text.raw "MagicShield - increase magic defense for 1 round but reduces magic" ]
                    Elem.li [ ] [ Text.raw "Heal - increase health but takes magic" ]
                    Elem.li [ ] [ Text.raw "Meditate - increases magic" ]
                ]
            ]
            Elem.hr [ ]

            Text.h3 "Rewards"
            Elem.div [ ] [

                Text.p "Each round rewards allocated for players splitted as"

                Elem.img [ getImgSource "rewards.jpg" |>  Attr.src ]

                Text.p "in case when no champs used a move those coins returned as rewards for next battles"
            ]
            Elem.hr [ ]

            Text.h3 "Battle params and limits"
            Elem.div [ ] [
                Elem.table [] [
                    Elem.tr [] [
                        Elem.td [] [ Text.raw "Rounds in battle" ]
                        Elem.td [] [ 
                            Text.raw $"{Constants.RoundsInBattle}"
                        ]
                    ]

                    Elem.tr [] [
                        Elem.td [] [ Text.raw "Round duration" ]
                        Elem.td [] [ 
                            Text.raw $"{Battle.RoundDuration}"
                        ]
                    ]

                    Elem.tr [] [
                        Elem.td [] [ Text.raw "XP per level" ]
                        Elem.td [] [ 
                            Text.raw $"{Levels.XPPerLvl}" 
                        ]
                    ]
                ]
            ]
            Elem.hr [ ]
        ]

        Elem.section [ 
            Attr.class' "block tokenomics-section"
            Attr.id "tokenomics-section"
        ] [
            Text.h3 "Tokenomics"
            Elem.div [ ] [
                Text.p "All Darkcoins from donation or purchases (items or premium features) go to total rewards pool."

                Text.b $"All prices are settled in USDC ({WebEmoji.USDC}). DarkCoin price updates periodically every few hours."

                Text.p "For each round rewards are splitted by following logic:"
                Elem.img [ getImgSource "tokenomics.jpg" |>  Attr.src ]

                Text.p "Rewards for specific round is calculated based on total amount as:"

                Elem.p [ ] [
                    Text.raw $"Window = {Window}"
                    Elem.br [ ]
                    Text.raw $"RoundsInBattle = {Constants.RoundsInBattle}"
                    Elem.br [ ]
                    Text.raw "BattleReward = InGameRewardsPool / Window"
                    Elem.br [ ]
                    Text.raw "RoundReward = BattleReward / RoundsInBattle"
                    Elem.br [ ]
                ]

                Elem.p [ ] [
                    Text.raw "In case when no champs used a move those coins return to rewards pool."
                ]
            ]
            Elem.hr [ ]           
        ]
    ]

open System
open Types

let private getTraitTable<'a when 'a : (new : unit-> 'a) and 'a :> Enum and 'a :struct> (trt:Trait) (fromTrait:'a -> Stat) =
    let chs = TraitCharacteristic.impact.[trt]
    Elem.div [ ] [
        Text.h2 $"{trt}"
        Elem.hr [ ]
        Text.h3 "Affects:"
        Elem.ul [ ] [
            for ch in chs do
                Elem.li [ ] [
                    Text.raw $"{Display.webEmojiFromChar ch} {ch}"
                ]
        ]
        Elem.hr [ ]
        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "" ]
                for ch in chs do
                    Elem.th [] [ Text.raw $"{Display.webEmojiFromChar ch}" ]
            ]

            for b in Enum.GetValues<'a>() do
                Elem.tr [] [
                    let stat = fromTrait b
                    yield Elem.th [] [ Text.raw ($"{b}" |> splitCamel) ]
                    for ch in chs do
                        yield Elem.th [] [ Text.raw $"{stat.GetValueBy ch}" ]
                ]
        ]
        Elem.hr [ ]
    ]


let allTraits =
    [
        getTraitTable<Background> Trait.Background Champ.fromBackground
        getTraitTable<Skin> Trait.Skin Champ.fromSkin
        getTraitTable<Weapon> Trait.Weapon Champ.fromWeapon
        getTraitTable<Head> Trait.Head Champ.fromHead
        getTraitTable<Armour> Trait.Armour Champ.fromArmour
        getTraitTable<Magic> Trait.Magic Champ.fromMagic
        getTraitTable<Extra> Trait.Extra Champ.fromExtra
    ]

let traits tables =
    Elem.main [
        Attr.class' "home"
        Attr.role "traits"
    ] tables

let statsPage (stats:Stats) =
    Elem.main [
        Attr.class' "stats"
        Attr.role "stats"
    ] [
        Elem.div [ ] [
            Text.h2 $"{WebEmoji.Stats} Statistics {WebEmoji.Stats}"

            Elem.div [ Attr.class' "center" ] [
                Elem.table [ Attr.class' "stats-table" ] [
                    if stats.Players.IsSome then
                        Elem.tr [] [
                            Elem.td [] [ 
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.Account} Players registered"
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Players.Value}"
                            ]
                        ]

                    if stats.ConfirmedPlayers.IsSome then
                        Elem.tr [] [
                            Elem.td [] [ 
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.CheckMark} Confirmed players"
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.ConfirmedPlayers.Value}"
                            ]
                        ]

                    if stats.Champs.IsSome then
                        Elem.tr [] [
                            Elem.td [] [
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.Champs} Champions"
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Champs.Value}"
                            ]
                        ]

                    if stats.Champs.IsSome then
                        Elem.tr [] [
                            Elem.td [] [
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.Monsters} Custom monsters" ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.CustomMonsters.Value}"
                            ]
                        ]

                    if stats.Battles.IsSome then
                        Elem.tr [] [
                            Elem.td [] [ 
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.Battle} Battles"
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Battles.Value}"
                            ]
                        ]
                
                    if stats.Rounds.IsSome then
                        Elem.tr [] [
                            Elem.td [] [
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.Rounds} Rounds" 
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Rounds.Value}"
                            ]
                        ]
                ]
            ]
            
            Text.h2 $"{WebEmoji.MoneyBag} Rewards {WebEmoji.MoneyBag}"

            Elem.div [ Attr.class' "center" ] [
                Elem.table [ Attr.class' "stats-table" ] [
                    if stats.Rewards.IsSome then
                        Elem.tr [] [
                            Elem.td [] [ 
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"🤖 Players earned"
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Rewards.Value} {WebEmoji.Coin}"
                            ]
                        ]

                    
                    if stats.Dao.IsSome then
                        Elem.tr [] [
                            Elem.td [] [ 
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.DAO} DAO"
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Dao.Value} {WebEmoji.Coin}"
                            ]
                        ]

                    if stats.Devs.IsSome then
                        Elem.tr [] [
                            Elem.td [] [
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.Dev} Devs" 
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Devs.Value} {WebEmoji.Coin}"
                            ]
                        ]

                    if stats.Reserve.IsSome then
                        Elem.tr [] [
                            Elem.td [] [ 
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.Reserve} Reserve"
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Reserve.Value} {WebEmoji.Coin}"
                            ]
                        ]

                    if stats.Burnt.IsSome then
                        Elem.tr [] [
                            Elem.td [] [ 
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.Fire} Burnt"
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Burnt.Value} {WebEmoji.Coin}"
                            ]
                        ]
                    

                    if stats.Staking.IsSome then
                        Elem.tr [] [
                            Elem.td [] [
                                Elem.div [ Attr.class' "label-wrap" ] [
                                    Text.raw $"{WebEmoji.Staking} Staking"
                                ]
                            ]
                            Elem.td [] [ 
                                Text.raw $"{stats.Staking.Value} {WebEmoji.Coin}"
                            ]
                        ]
                ]
            ]
        ]
    ]