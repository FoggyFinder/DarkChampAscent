module LeaderboardView

open Falco.Markup
open Types
open UI

let general =
    Elem.main [
        Attr.class' "leaderboard"
        Attr.role "main"
    ] [
        Elem.section [ 
            Attr.class' "block top-champs"
            Attr.id "top-champs"
            XmlAttribute.KeyValueAttr("aria-labelledby", "hb-user")
        ] [
            Elem.div [ ] [
                Elem.img [ Attr.class' "thumb" ]
                Elem.a [ Attr.href Route.topChamps ] [ Text.raw "Champs" ] 
            ]
        ]

        Elem.section [ 
            Attr.class' "block top-monsters"
            Attr.id "top-monsters"
            XmlAttribute.KeyValueAttr("aria-labelledby", "hb-user")
        ] [
            Elem.div [ ] [
                Elem.img [ Attr.class' "thumb" ]
                Elem.a [ Attr.href Route.topMonsters ] [ Text.raw "Monsters" ] 
            ]
        ]

        Elem.section [ 
            Attr.class' "block top-donaters"
            Attr.id "top-donaters"
            XmlAttribute.KeyValueAttr("aria-labelledby", "hb-user")
        ] [
            Elem.div [ ] [
                Elem.img [ Attr.class' "thumb" ]
                Elem.a [ Attr.href Route.topDonaters ] [ Text.raw "Donaters (players)" ] 
            ]
        ]

        Elem.section [ 
            Attr.class' "block top-udonaters"
            Attr.id "top-udonaters"
            XmlAttribute.KeyValueAttr("aria-labelledby", "hb-user")
        ] [
            Elem.div [ ] [
                Elem.img [ Attr.class' "thumb" ]
                Elem.a [ Attr.href Route.topUnknownDonaters ] [ Text.raw "Donaters (unknown)" ] 
            ]
        ]
    ]

let champs (champs: ChampShortInfo list) =
    Elem.main [
        Attr.class' "leaderboard"
        Attr.role "main"
    ] [
        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "" ]
                Elem.th [] [ Text.raw "Pic" ]
                Elem.th [] [ Text.raw "Name" ]
                Elem.th [] [ Text.raw "Xp" ]
            ]
            for (i, item) in champs |> List.indexed do
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
                ]
        ]
    ]

let monsters (monsters: MonsterShortInfo list) =
    Elem.main [
        Attr.class' "leaderboard"
        Attr.role "main"
    ] [
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
  
let donaters (title:string) (xs: (string * decimal) list) =
    Elem.main [
        Attr.class' "leaderboard"
        Attr.role "main"
    ] [
        Elem.table [] [
            Elem.tr [] [
                Elem.th [] [ Text.raw "" ]
                Elem.th [] [ Text.raw title ]
                Elem.th [] [ Text.raw "Darkcoins" ]
            ]
            for (i, (name, amount)) in xs |> List.indexed do
                Elem.tr [] [
                    Elem.td [] [ Text.raw $"{i + 1}" ]
                    Elem.td [] [ Text.raw $"{name}" ]
                    Elem.td [] [ Text.raw $"{amount}" ]
                ]
        ]
    ]
  