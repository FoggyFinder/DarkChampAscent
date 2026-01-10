module ChampView

open Falco.Markup
open Types
open UI
open GameLogic.Champs
open GameLogic.Shop
open Display
open Db
open System

let champs (champs: (ChampShortInfo * decimal) list) =
    Elem.main [
        Attr.class' "my-champs"
        Attr.role "main"
    ] [
        if champs.IsEmpty then
            Elem.div [ ] [
                Elem.p [ ] [
                    Text.raw "You don't have any DarkCoin Champion NFT in any confirmed wallet. You can buy one "
                    Elem.a [ ] [ Text.raw "here" ]
                ]
                Elem.p [ ] [
                    Text.raw "Once done, please click button below to rescan"
                ]

                Elem.form [
                    Attr.methodPost
                    Attr.action Route.rescan
                ] [
                    Elem.input [
                        Attr.class' "btn btn-primary"
                        Attr.typeSubmit
                        Attr.value "Rescan"
                    ]
                ]
            ]
        else
            Elem.div [ ] [
                Elem.p [ ] [
                    Text.raw "If you don't see all your champs here, you can try to rescan (only confirmed wallets are checked)"
                ]

                Elem.form [
                    Attr.methodPost
                    Attr.action Route.rescan
                ] [
                    Elem.input [
                        Attr.class' "btn btn-primary"
                        Attr.typeSubmit
                        Attr.value "Rescan"
                    ]
                ]

                Elem.table [] [
                    Elem.tr [] [
                        Elem.th [] [ Text.raw "" ]
                        Elem.th [] [ Text.raw "Pic" ]
                        Elem.th [] [ Text.raw "Name" ]
                        Elem.th [] [ Text.raw "Xp" ]
                        Elem.th [] [ Text.raw "Balance" ]
                    ]
                    for (i, (item, balance)) in champs |> List.indexed do
                        Elem.tr [] [
                            Elem.td [] [ Text.raw $"{i + 1}" ]
                            Elem.td [] [ 
                                Elem.img [
                                    Attr.class' "picSmall"
                                    Attr.src (Links.IPFS + item.IPFS)
                                ]
                            ]
                            Elem.td [] [
                                Elem.a [ Attr.href (Uri.champ item.ID) ] [ Text.raw $"{item.Name}" ]
                            ]
                            Elem.td [] [ Text.raw $"{item.XP}" ]
                            Elem.td [] [ Text.raw $"{balance}" ]
                        ]
                ]
            ]
    ]

let champsUnderEffects (champs: ChampUnderEffect list) =
    Elem.main [
        Attr.class' "champs-under-effects"
        Attr.role "main"
    ] [
        if champs.IsEmpty then
            Elem.div [ ] [
                Text.raw "No champs are under effects currently"
            ]
        else
            Elem.table [] [
                Elem.tr [] [
                    Elem.th [] [ Text.raw "" ]
                    Elem.th [] [ Text.raw "Pic" ]
                    Elem.th [] [ Text.raw "Name" ]
                    Elem.th [] [ Text.raw "Effect" ]
                    Elem.th [] [ Text.raw "Rounds left" ]
                ]
                for (i, champ) in champs |> List.sortByDescending(fun cue -> cue.RoundsLeft) |> List.indexed do
                    Elem.tr [] [
                        Elem.td [] [ Text.raw $"{i + 1}" ]
                        Elem.td [] [ 
                            Elem.img [
                                Attr.class' "picSmall"
                                Attr.src (Links.IPFS + champ.IPFS)
                            ]
                        ]
                        Elem.td [] [
                            Elem.a [ Attr.href (Uri.champ (uint64 champ.ID)) ] [ Text.raw $"{champ.Name}" ]
                        ]
                        Elem.td [] [ Text.raw $"{champ.Effect}" ]
                        Elem.td [] [ Text.raw $"{champ.RoundsLeft} {WebEmoji.Rounds}" ]
                    ]
            ]
    ]

let champInfo (champ:ChampInfo) (isOwnerAuth:bool) =

    let lvl = Levels.getLvlByXp champ.XP
    let freePoints = lvl - champ.LeveledChars
    let hasFreePoints = freePoints > 0UL
    
    let bs = champ.BoostStat |> Option.defaultValue Stat.Zero
    let ls = champ.LevelsStat |> Option.defaultValue Stat.Zero
    let fs = FullStat(champ.Stat, bs, ls)

    Elem.main [
        Attr.class' "champ-card"
        Attr.role "main"
    ] [
        if isOwnerAuth then
            Elem.div [ ] [
                // TODO: show darkcoin price
                // TODO: add confirmation
                Text.h2 $"{champ.Name}"
                Text.b $"Premium feature ({Shop.RenamePrice} {WebEmoji.USDC})"
                Elem.form [
                    Attr.methodPost
                    Attr.action Route.renameChamp
                ] [
                    Elem.input [
                        Attr.typeText
                        Attr.minlength "1"
                        Attr.value champ.Name
                        Attr.id "newname"
                        Attr.name "newname" ]

                    Elem.input [
                        Attr.typeHidden
                        Attr.name "oldname"
                        Attr.value $"{champ.Name}"
                    ]

                    Elem.input [
                        Attr.typeHidden
                        Attr.name "chmpId"
                        Attr.value $"{champ.ID}"
                    ]

                    Elem.input [
                        Attr.class' "btn btn-primary"
                        Attr.typeSubmit
                        Attr.value "Rename"
                    ]
                ]
            ]
        else
            Text.h2 $"{champ.Name}"
        
        Elem.hr []
        Elem.img [
            Attr.class' "picNormal"
            Attr.src (Links.IPFS + champ.Ipfs)
        ]
        
        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "" ]
                Elem.th [] [ Text.raw "" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.Gem} XP" ]
                Elem.td [] [ Text.raw $"{champ.XP}" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.Level} Level" ]
                Elem.td [] [ Text.raw $"{lvl}" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.Health} Health" ]
                Elem.td [] [ Text.raw $"{fs.GetValue Characteristic.Health}" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.Magic} Magic" ]
                Elem.td [] [ Text.raw $"{fs.GetValue Characteristic.Magic}" ]
            ]
        ]

        if isOwnerAuth && hasFreePoints then
            let chrsOptions =
                Enum.GetNames<Characteristic>()
                |> Array.map(fun s ->
                    Elem.option [ Attr.value s ] [
                            Text.raw s
                        ])
                |> Array.toList
            Elem.div [ ] [
                Elem.form [
                    Attr.methodPost
                    Attr.action Route.levelUp
                ] [
                    Elem.input [
                        Attr.typeHidden
                        Attr.name "champ"
                        Attr.value $"{champ.ID}"
                    ]

                    Elem.div [ Attr.class' "select-wrap" ] [
                        Elem.select [
                          Attr.class' "tom-select"
                          Attr.id "char"
                          Attr.name "char"
                        ] chrsOptions
                     ]

                    Elem.input [
                        Attr.class' "btn btn-primary"
                        Attr.typeSubmit
                        Attr.value "Level Up!"
                    ]
                ]
            ]

        Elem.hr []
        Elem.table [] [
            Elem.tr [] [
                Elem.td [] [ Text.raw "Balance" ]
                Elem.td [] [ Text.raw $"{toRound2StrD champ.Balance}" ]
            ]
        ]

        Elem.hr []
        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "" ]
                Elem.th [] [ Text.raw "" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.Luck} {nameof Characteristic.Luck}" ]
                Elem.td [] [ Text.raw $"{fs.GetValue Characteristic.Luck}" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.Accuracy} {nameof Characteristic.Accuracy}" ]
                Elem.td [] [ Text.raw $"{fs.GetValue Characteristic.Accuracy}" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.Attack} {nameof Characteristic.Attack}" ]
                Elem.td [] [ Text.raw $"{fs.GetValue Characteristic.Attack}" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.MagicAttack} {nameof Characteristic.MagicAttack}" ]
                Elem.td [] [ Text.raw $"{fs.GetValue Characteristic.MagicAttack}" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.Shield} {nameof Characteristic.Defense}" ]
                Elem.td [] [ Text.raw $"{fs.GetValue Characteristic.Defense}" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.MagicShield} {nameof Characteristic.MagicDefense}" ]
                Elem.td [] [ Text.raw $"{fs.GetValue Characteristic.MagicDefense}" ]
            ]
        ]

        if bs <> Stat.Zero || ls <> Stat.Zero then
           Elem.hr []
           if bs <> Stat.Zero && ls <> Stat.Zero then
               Text.raw "(*) - values gained from items bought in the shop"
               Text.raw "(**) - values gained from levels up"
           elif bs <> Stat.Zero then
               Text.raw "(*) - boosted gained from items bought in the shop"
           elif ls <> Stat.Zero then
               Text.raw "(*) - values gained from levels up"
           else ()
           Elem.hr []

        Elem.hr []
        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "Trait" ]
                Elem.th [] [ Text.raw "" ]

                Elem.th [] [ Text.raw $"{WebEmoji.Health}" ]
                Elem.th [] [ Text.raw $"{WebEmoji.Magic}" ]
                Elem.th [] [ Text.raw $"{WebEmoji.Luck}" ]
                Elem.th [] [ Text.raw $"{WebEmoji.Accuracy}" ]

                Elem.th [] [ Text.raw $"{WebEmoji.Attack}" ]
                Elem.th [] [ Text.raw $"{WebEmoji.MagicAttack}" ]
                Elem.th [] [ Text.raw $"{WebEmoji.Shield}" ]
                Elem.th [] [ Text.raw $"{WebEmoji.MagicShield}" ]
            ]

            Elem.tr [] [
                let stat = Champ.fromBackground champ.Traits.Background
                
                Elem.td [] [ Text.raw $"{WebEmoji.Background} {nameof Trait.Background}" ]
                Elem.td [] [ Text.raw ($"{champ.Traits.Background}" |> splitCamel) ]

                yield! Ui.columnsFromStat stat
            ]

            Elem.tr [] [
                let stat = Champ.fromSkin champ.Traits.Skin

                Elem.td [] [ Text.raw $"{WebEmoji.Skin} {nameof Trait.Skin}" ]
                Elem.td [] [ Text.raw ($"{champ.Traits.Skin}" |> splitCamel) ]

                yield! Ui.columnsFromStat stat
            ]

            Elem.tr [] [
                let stat = Champ.fromWeapon champ.Traits.Weapon

                Elem.td [] [ Text.raw $"{WebEmoji.Weapon} {nameof Trait.Weapon}" ]
                Elem.td [] [ Text.raw ($"{champ.Traits.Weapon}" |> splitCamel) ]

                yield! Ui.columnsFromStat stat
            ]

            Elem.tr [] [
                let stat = Champ.fromMagic champ.Traits.Magic

                Elem.td [] [ Text.raw $"{WebEmoji.Magic} {nameof Trait.Magic}" ]
                Elem.td [] [ Text.raw ($"{champ.Traits.Magic}" |> splitCamel) ]

                yield! Ui.columnsFromStat stat
            ]
            
            Elem.tr [] [
                let stat = Champ.fromHead champ.Traits.Head

                Elem.td [] [ Text.raw $"{WebEmoji.Head} {nameof Trait.Head}" ]
                Elem.td [] [ Text.raw ($"{champ.Traits.Head}" |> splitCamel) ]

                yield! Ui.columnsFromStat stat
            ] 

            Elem.tr [] [
                let stat = Champ.fromArmour champ.Traits.Armour

                Elem.td [] [ Text.raw $"{WebEmoji.Armour} {nameof Trait.Armour}" ]
                Elem.td [] [ Text.raw ($"{champ.Traits.Armour}" |> splitCamel) ]

                yield! Ui.columnsFromStat stat
            ] 

            Elem.tr [] [
                let stat = Champ.fromExtra champ.Traits.Extra

                Elem.td [] [ Text.raw $"{WebEmoji.Extra} {nameof Trait.Extra}" ]
                Elem.td [] [ Text.raw ($"{champ.Traits.Extra}" |> splitCamel) ]

                yield! Ui.columnsFromStat stat
            ] 
        ]
    ]