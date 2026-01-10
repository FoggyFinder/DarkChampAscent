module MonsterView

open Falco.Markup
open UI
open Types
open GameLogic.Shop
open System
open GameLogic.Monsters
open GameLogic.Champs
open Display
open Db

let monsters (monsters: MonsterShortInfo list) (isAuth:bool) (dcPrice:decimal)=
    Elem.main [
        Attr.class' "my-monsters"
        Attr.role "main"
    ] [
        if isAuth then
            let amount = Math.Round(Shop.GenMonsterPrice / dcPrice, 6)
            let mtOptions =
                Enum.GetNames<MonsterType>()
                |> Array.map(fun s ->
                    Elem.option [ Attr.value s ] [
                            Text.raw s
                        ])
                |> Array.toList
            let mstOptions =
                Enum.GetNames<MonsterSubType>()
                |> Array.map(fun s ->
                    Elem.option [ Attr.value s ] [
                            Text.raw s
                        ])
                |> Array.toList
            Elem.div [ ] [
                Text.raw $"Premium feature, you can create your own monster with {amount} DarkCoins (~{Shop.GenMonsterPrice} USDC)"
                
                Elem.form [
                    Attr.methodPost
                    Attr.action Route.createMonster
                ] [
                    Elem.div [ Attr.class' "select-wrap" ] [
                        Elem.select [
                          Attr.class' "tom-select"
                          Attr.id "mtype"
                          Attr.name "mtype"
                        ] mtOptions
                     ]

                    Elem.div [ Attr.class' "select-wrap" ] [
                        Elem.select [
                          Attr.class' "tom-select"
                          Attr.id "msubtype"
                          Attr.name "msubtype"
                        ] mstOptions
                     ]

                    Elem.input [
                        Attr.class' "btn btn-primary"
                        Attr.typeSubmit
                        Attr.value "Create monster"
                    ]
                ]
            ]
        if monsters.IsEmpty then
            Elem.div [ ] [
                Text.raw "You don't have any monsters yet"
            ]
        else
            Elem.table [] [
                Elem.tr [] [
                    Elem.th [] [ Text.raw "" ]
                    Elem.th [] [ Text.raw "Pic" ]

                    Elem.th [] [ Text.raw "Name" ]
                    Elem.th [] [ Text.raw "Class" ]
                    Elem.th [] [ Text.raw "Xp" ]
                ]
                for (i, item) in monsters |> List.indexed do
                    let src = FileUtils.getLocalImg item.Pic
                    Elem.tr [] [
                        Elem.td [] [ Text.raw $"{i + 1}" ]
                        Elem.td [] [ 
                            Elem.img [
                                Attr.class' "picSmall"
                                Attr.src $"/{src}"
                            ]
                        ]
                        Elem.td [] [ Elem.a [ Attr.href (Uri.monstr item.ID) ] [ Text.raw $"{item.Name}" ] ]
                        Elem.td [] [ Text.raw $"{Display.monsterClass (item.MType, item.MSubType)}" ]
                        Elem.td [] [ Text.raw $"{item.XP}" ]
                    ]
            ]
    ]

let monstersUnderEffects (monsters: MonsterUnderEffect list) =
    Elem.main [
        Attr.class' "monsters-under-effects"
        Attr.role "main"
    ] [
        if monsters.IsEmpty then
            Elem.div [ ] [
                Text.raw "No monsters are under effects currently"
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
                for (i, monster) in monsters |> List.sortByDescending(fun mue -> mue.RoundsLeft) |> List.indexed do
                    let src = FileUtils.getLocalImg monster.Pic
                    Elem.tr [] [
                        Elem.td [] [ Text.raw $"{i + 1}" ]
                        Elem.td [] [ 
                            Elem.img [
                                Attr.class' "picSmall"
                                Attr.src $"/{src}"
                            ]
                        ]
                        Elem.td [] [
                            Elem.a [ Attr.href (Uri.monstr (uint64 monster.ID)) ] [ Text.raw $"{monster.Name}" ]
                        ]
                        Elem.td [] [ Text.raw $"{monster.Effect}" ]
                        Elem.td [] [ Text.raw $"{monster.RoundsLeft}" ]
                    ]
            ]
    ]

let monstrInfo (mId:uint64) (monstr:MonsterInfo) (isOwnerAuth:bool) =
    
    let lvl = Levels.getLvlByXp monstr.XP
    
    let bs = Stat.Zero
    let ls = Monster.getMonsterStatsByLvl(monstr.MType, monstr.MSubType, lvl)
    let fs = FullStat(monstr.Stat, bs, ls)

    let src = FileUtils.getLocalImg monstr.Picture

    Elem.main [
        Attr.class' "monstr-card"
        Attr.role "main"
    ] [
        if isOwnerAuth then
            Elem.div [ ] [
                Text.h2 $"{monstr.Name}"
                Elem.form [
                    Attr.methodPost
                    Attr.action Route.renameMonster
                ] [
                    Elem.input [
                        Attr.typeText
                        Attr.minlength "5"
                        Attr.value monstr.Name
                        Attr.id "mnstrname"
                        Attr.name "mnstrname" ]

                    Elem.input [
                        Attr.typeHidden
                        Attr.name "mnstrid"
                        Attr.value $"{mId}"
                    ]

                    Elem.input [
                        Attr.class' "btn btn-primary"
                        Attr.typeSubmit
                        Attr.value "Rename"
                    ]
                ]
            ]
        else
            Text.h2 $"{monstr.Name}"
        Elem.hr []

        Elem.hr []
        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "Description" ]
                Elem.th [] [ Text.raw "Img" ]
            ]
            Elem.tr [] [
                Elem.td [] [ Text.raw $"{monstr.Description}" ]
                Elem.td [] [ 
                    Elem.img [
                        Attr.class' "picNormal"
                        Attr.src $"/{src}"
                    ]
                ]
            ]
        ]

        Elem.hr []
        Elem.table [] [
            Elem.tr [] [
                Elem.td [] [ Text.raw "Type" ]
                Elem.td [] [ 
                    Text.raw "SubType"
                ]
            ]
            Elem.tr [] [
                Elem.td [] [ Text.raw $"{monstr.MType}" ]
                Elem.td [] [ 
                    Text.raw $"{monstr.MSubType}"
                ]
            ]
        ]

        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "" ]
                Elem.th [] [ Text.raw "" ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"{WebEmoji.Gem} XP" ]
                Elem.td [] [ Text.raw $"{monstr.XP}" ]
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

        if ls <> Stat.Zero then
           Elem.hr []
           Text.raw "(*) - values gained from levels up"
           Elem.hr []
    ]