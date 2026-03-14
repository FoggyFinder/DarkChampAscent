module Pages.Details

open Feliz
open Types
open Components
open DTO
open Display
open GameLogic.Champs
open GameLogic.Shop
open System
open GameLogic.Monsters

let private columnsFromStat (stat: Stat) =
    [
        Html.td [ prop.text (stat.Health     |> Display.withPlusOrEmpty) ]
        Html.td [ prop.text (stat.Magic      |> Display.withPlusOrEmpty) ]
        Html.td [ prop.text (stat.Luck       |> Display.withPlusOrEmpty) ]
        Html.td [ prop.text (stat.Accuracy   |> Display.withPlusOrEmpty) ]
        Html.td [ prop.text (stat.Attack     |> Display.withPlusOrEmpty) ]
        Html.td [ prop.text (stat.MagicAttack|> Display.withPlusOrEmpty) ]
        Html.td [ prop.text (stat.Defense    |> Display.withPlusOrEmpty) ]
        Html.td [ prop.text (stat.MagicDefense |> Display.withPlusOrEmpty) ]
    ]

[<ReactComponent>]
let ChampDetailPage (champId: uint64) =
    let data, setData     = React.useState<Deferred<ChampDTO>> Loading
    let newName, setNewName = React.useState ""
    let msg, setMsg       = React.useState<string option> None
    let selChar, setSelChar = React.useState Characteristic.Health
    let characteristics   = AllEnums.Characteristics

    let load () =
        async {
            let! r = Api.getChamp champId
            match r with
            | Ok d  -> setData (Loaded d); setNewName d.ChampInfo.Name
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate

    React.useEffect((fun () -> load ()), [| box champId |])

    deferred data (fun d ->
        let c = d.ChampInfo
        let isOwner = d.Balance.IsSome
        let lvl = c.XP / 1000UL

        let bs = c.BoostStat |> Option.defaultValue Stat.Zero
        let ls = c.LevelsStat |> Option.defaultValue Stat.Zero
        let fs = FullStat(c.Stat, bs, ls)
        Html.div [
            prop.className "champ-card"
            prop.children [
                match msg with
                | Some msg -> Html.p [ prop.className "action-msg"; prop.text msg ]
                | None -> Html.none
                if isOwner then
                    let amount = Math.Round(Shop.RenamePrice / d.Price, 6)
                    Html.div [
                        prop.className "rename-section"
                        prop.children [
                            Html.div [
                                prop.dangerouslySetInnerHTML $"Premium feature - {Shop.RenamePrice} {WebEmoji.USDC} (~ {amount} DarkCoins {WebEmoji.DarkCoin})"
                            ]
                            Html.input [ prop.type' "text"; prop.value newName; prop.onChange setNewName ]
                            Html.button [
                                prop.className "btn btn-secondary btn-sm"
                                prop.disabled (newName = c.Name || newName = "")
                                prop.onClick (fun _ ->
                                    async {
                                        let! r = Api.renameChamp c.Name newName c.ID
                                        match r with
                                        | Ok ()   -> setMsg (Some "Renamed!"); load ()
                                        | Error e -> setMsg (Some ("Error: " + e))
                                    } |> Async.StartImmediate)
                                prop.text "Rename"
                            ]
                        ]
                    ]
                else
                    Html.h2 [ prop.text c.Name ]

               // ── two-column body ──────────────────────────────────────
                Html.div [
                    prop.className "champ-body"
                    prop.children [

                        Html.div [
                            prop.className "champ-left"
                            prop.children [
                                ipfsImg c.Ipfs "picNormal"
                                Html.table [
                                    prop.className "stats-table"
                                    prop.children [
                                        Html.tbody [
                                            Html.tr [
                                                Html.td [ Html.div [ prop.dangerouslySetInnerHTML $"{WebEmoji.DarkCoin} Balance" ] ]
                                                Html.td [ prop.dangerouslySetInnerHTML $"{toRound2StrD c.Balance} {WebEmoji.DarkCoin}" ]
                                            ]
                                            statRow WebEmoji.Gem "XP" (string c.XP)
                                            statRow WebEmoji.Level "Level" (string lvl)
                                        ]
                                    ]
                                ]
                    
                                let freePoints = int lvl - int c.LeveledChars
                                if isOwner && freePoints > 0 then
                                    Html.div [
                                        prop.className "levelup-section"
                                        prop.children [
                                            Html.p [ prop.text (sprintf "Free points: %d" freePoints) ]
                                            TomSelectInput "" (DisplayEnum.Characteristic selChar)
                                                (fun (s: string) ->
                                                    AllEnums.Characteristics
                                                    |> List.tryFind (fun c -> DisplayEnum.Characteristic c = s)
                                                    |> Option.iter setSelChar)
                                                [ for ch in characteristics -> Html.option [ prop.value (DisplayEnum.Characteristic ch); prop.text (DisplayEnum.Characteristic ch) ] ]
                                            
                                            Html.button [
                                                prop.className "btn btn-primary btn-sm"
                                                prop.onClick (fun _ ->
                                                    async {
                                                        let! r = Api.levelUp c.ID selChar
                                                        match r with
                                                        | Ok ()   -> setMsg (Some "Leveled up!"); load ()
                                                        | Error e -> setMsg (Some ("Error: " + e))
                                                    } |> Async.StartImmediate)
                                                prop.text "Level Up!"
                                            ]
                                        ]
                                    ]
                            ]
                        ]

                        Html.div [
                            prop.className "champ-right"
                            prop.children [
                                Html.table [
                                    prop.className "stats-table"
                                    prop.children [
                                        Html.tbody [
                                            statRow WebEmoji.Health "Health" (fs.GetValue Characteristic.Health)
                                            statRow WebEmoji.Magic "Magic" (fs.GetValue Characteristic.Magic)
                                            statRow WebEmoji.Luck "Luck" (fs.GetValue Characteristic.Luck)
                                            statRow WebEmoji.Accuracy "Accuracy" (fs.GetValue Characteristic.Accuracy)
                                            statRow WebEmoji.Attack "Attack" (fs.GetValue Characteristic.Attack)
                                            statRow WebEmoji.MagicAttack "MagicAttack" (fs.GetValue Characteristic.MagicAttack)
                                            statRow WebEmoji.Shield "Defense" (fs.GetValue Characteristic.Defense)
                                            statRow WebEmoji.MagicShield "MagicDefense" (fs.GetValue Characteristic.MagicDefense)
                                        ]
                                    ]
                                ]
                                // footnotes
                                if bs <> Stat.Zero || ls <> Stat.Zero then
                                    Html.hr []
                                    if bs <> Stat.Zero && ls <> Stat.Zero then
                                        Html.p [ prop.className "muted"; prop.text "(*) - values gained from items bought in the shop" ]
                                        Html.p [ prop.className "muted"; prop.text "(**) - values gained from levels up" ]
                                    elif bs <> Stat.Zero then
                                        Html.p [ prop.className "muted"; prop.text "(*) - boosted gained from items bought in the shop" ]
                                    elif ls <> Stat.Zero then
                                        Html.p [ prop.className "muted"; prop.text "(*) - values gained from levels up" ]
                                        Html.hr []
                            ]
                        ]
                    ]
                ]

                Html.hr []

                Html.table [
                    Html.tbody [
                        Html.tr [
                            Html.th [ prop.text "Trait" ]
                            Html.th [ prop.text "" ]
                            Html.th [ prop.text WebEmoji.Health ]
                            Html.th [ prop.text WebEmoji.Magic ]
                            Html.th [ prop.text WebEmoji.Luck ]
                            Html.th [ prop.text WebEmoji.Accuracy ]
                            Html.th [ prop.text WebEmoji.Attack ]
                            Html.th [ prop.text WebEmoji.MagicAttack ]
                            Html.th [ prop.text WebEmoji.Shield ]
                            Html.th [ prop.text WebEmoji.MagicShield ]
                        ]
                        Html.tr [
                            let stat = Champ.fromBackground c.Traits.Background
                            yield Html.td [ prop.text $"{WebEmoji.Background} {nameof Trait.Background}" ]
                            yield Html.td [ prop.text (DisplayEnum.Background c.Traits.Background) ]
                            yield! columnsFromStat stat
                        ]
                        Html.tr [
                            let stat = Champ.fromSkin c.Traits.Skin
                            yield Html.td [ prop.text $"{WebEmoji.Skin} {nameof Trait.Skin}" ]
                            yield Html.td [ prop.text (DisplayEnum.Skin c.Traits.Skin) ]
                            yield! columnsFromStat stat
                        ]
                        Html.tr [
                            let stat = Champ.fromWeapon c.Traits.Weapon
                            yield Html.td [ prop.text $"{WebEmoji.Weapon} {nameof Trait.Weapon}" ]
                            yield Html.td [ prop.text (DisplayEnum.Weapon c.Traits.Weapon) ]
                            yield! columnsFromStat stat
                        ]
                        Html.tr [
                            let stat = Champ.fromMagic c.Traits.Magic
                            yield Html.td [ prop.text $"{WebEmoji.Magic} {nameof Trait.Magic}" ]
                            yield Html.td [ prop.text (DisplayEnum.Magic c.Traits.Magic) ]
                            yield! columnsFromStat stat
                        ]
                        Html.tr [
                            let stat = Champ.fromHead c.Traits.Head
                            yield Html.td [ prop.text $"{WebEmoji.Head} {nameof Trait.Head}" ]
                            yield Html.td [ prop.text (DisplayEnum.Head c.Traits.Head) ]
                            yield! columnsFromStat stat
                        ]
                        Html.tr [
                            let stat = Champ.fromArmour c.Traits.Armour
                            yield Html.td [ prop.text $"{WebEmoji.Armour} {nameof Trait.Armour}" ]
                            yield Html.td [ prop.text (DisplayEnum.Armour c.Traits.Armour) ]
                            yield! columnsFromStat stat
                        ]
                        Html.tr [
                            let stat = Champ.fromExtra c.Traits.Extra
                            yield Html.td [ prop.text $"{WebEmoji.Extra} {nameof Trait.Extra}" ]
                            yield Html.td [ prop.text (DisplayEnum.Extra c.Traits.Extra) ]
                            yield! columnsFromStat stat
                        ]
                    ]
                ]
            ]
        ])

[<ReactComponent>]
let MonsterDetailPage (monsterId: uint64) =
    let data, setData     = React.useState<Deferred<MonsterDTO>> Loading
    let newName, setNewName = React.useState ""
    let msg, setMsg       = React.useState<string option> None

    let load () =
        async {
            let! r = Api.getMonster monsterId
            match r with
            | Ok d  -> setData (Loaded d); setNewName d.Monster.Name
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate

    React.useEffect((fun () -> load ()), [| box monsterId |])

    deferred data (fun d ->
        let m = d.Monster
        let lvl = m.XP / 1000UL

        let bs = Stat.Zero
        let ls = Monster.getMonsterStatsByLvl(m.MType, m.MSubType, lvl)
        let fs = FullStat(m.Stat, bs, ls)
        Html.div [
            prop.className "monstr-card"
            prop.children [
                match msg with
                | Some msg -> Html.p [ prop.className "action-msg"; prop.text msg ]
                | None -> Html.none

                if d.IsOwned then
                    Html.div [
                        prop.className "rename-section"
                        prop.children [
                            Html.input [ prop.type' "text"; prop.value newName; prop.onChange setNewName; prop.minLength 5 ]
                            Html.button [
                                prop.className "btn btn-secondary btn-sm"
                                prop.disabled (newName = m.Name || newName.Length < 5)
                                prop.onClick (fun _ ->
                                    async {
                                        let! r = Api.renameMonster d.ID newName
                                        match r with
                                        | Ok ()   -> setMsg (Some "Renamed!"); load ()
                                        | Error e -> setMsg (Some ("Error: " + e))
                                    } |> Async.StartImmediate)
                                prop.text "Rename"
                            ]
                        ]
                    ]
                else
                    Html.h2 [ prop.text m.Name ]

                Html.div [
                    prop.className "champ-body"
                    prop.children [
                        Html.div [
                            prop.className "champ-left"
                            prop.children [
                                Html.img [ prop.className "picNormal"; Utils.srcMonsterImg m.Picture; prop.alt m.Name ]
                                Html.p [ prop.className "center muted"; prop.text m.Description ]
                                Html.p [ prop.className "center muted"; prop.text (Display.monsterClass(m.MType, m.MSubType)) ]
                            ]
                        ]
                        Html.div [
                            prop.className "champ-right"
                            prop.children [
                                Html.table [
                                    prop.className "stats-table"
                                    prop.children [
                                        Html.tbody [
                                            statRow WebEmoji.Gem "XP" (string m.XP)
                                            statRow WebEmoji.Level "Level" (string lvl)
                                            statRow WebEmoji.Health "Health" (fs.GetValue Characteristic.Health)
                                            statRow WebEmoji.Magic "Magic" (fs.GetValue Characteristic.Magic)
                                            statRow WebEmoji.Luck "Luck" (fs.GetValue Characteristic.Luck)
                                            statRow WebEmoji.Accuracy "Accuracy" (fs.GetValue Characteristic.Accuracy)
                                            statRow WebEmoji.Attack "Attack" (fs.GetValue Characteristic.Attack)
                                            statRow WebEmoji.MagicAttack "MagicAttack" (fs.GetValue Characteristic.MagicAttack)
                                            statRow WebEmoji.Shield "Defense" (fs.GetValue Characteristic.Defense)
                                            statRow WebEmoji.MagicShield "MagicDefense" (fs.GetValue Characteristic.MagicDefense)
                                        ]
                                    ]
                                ]
                                if ls <> Stat.Zero then
                                    Html.p [ prop.className "muted"; prop.text "(*) - values gained from levels up" ]
                            ]
                        ]
                    ]
                ]
            ]
        ])
