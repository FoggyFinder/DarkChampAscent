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
open DarkChampAscent.Api
open UseWallet

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
    let wallet = useWallet ()

    let load () =
        async {
            let! r = Api.getChamp champId
            match r with
            | Ok d  -> setData (Loaded d); setNewName d.ChampInfo.ChampInfo.Name
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate

    React.useEffect((fun () -> load ()), [| box champId |])

    deferred data (fun d ->
        let c = d.ChampInfo.ChampInfo
        let isOwner = d.BelongsToAUser
        let lvl = Levels.getLvlByXp c.XP

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
                            match wallet.activeAddress with
                            | Some awallet ->
                                Html.button [
                                    prop.className "btn btn-secondary btn-sm"
                                    prop.disabled (newName = c.Name || newName = "")
                                    prop.onClick (fun _ ->
                                        async {
                                            "" |> Some |> setMsg
                                            let tx = Tx.RenameChamp (awallet, c.ID, newName) 
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
                                    prop.text "Rename"
                                ]
                            | None ->
                                Html.p [ prop.className "notice"; prop.text "Connect confirmed wallet on 'Account' page to rename" ]
                        ]
                    ]
                
                Html.div [
                    prop.className "champ-body"
                    prop.children [

                        Html.div [
                            prop.className "champ-left"
                            prop.children [
                                ipfsImg c.Ipfs "picNormal"
                                Html.h3 [ prop.text c.Name ]
                                Html.div [
                                    Html.a [
                                        prop.href (Page.UserDetail d.ChampInfo.UserLink.UserRawId).Route
                                        prop.onClick (Nav.navTo (Page.UserDetail d.ChampInfo.UserLink.UserRawId).Route)
                                        prop.target.blank;
                                        prop.text $"Owned by {d.ChampInfo.UserLink.Nickname}" 
                                    ]
                                ]
                                Html.table [
                                    prop.className "stats-table"
                                    prop.children [
                                        Html.tbody [
                                            Html.tr [
                                                Html.td [ Html.span [ prop.dangerouslySetInnerHTML WebEmoji.DarkCoin ]  ]
                                                Html.td [ 
                                                    Html.div [ 
                                                        prop.className "label-wrap" 
                                                        prop.text "Balance"
                                                    ]
                                                ]
                                                Html.td [ 
                                                    
                                                    prop.className "stat-value"
                                                    prop.dangerouslySetInnerHTML $"{toRound2StrD c.Balance} {WebEmoji.DarkCoin}" ]
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
                                            CustomSelectInput (DisplayEnum.Characteristic selChar)
                                                (fun (s: string) ->
                                                    AllEnums.Characteristics
                                                    |> List.tryFind (fun c -> DisplayEnum.Characteristic c = s)
                                                    |> Option.iter setSelChar)
                                                [ for ch in characteristics -> DisplayEnum.Characteristic ch, DisplayEnum.Characteristic ch, None ]
                                            
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
            | Ok d  -> setData (Loaded d); setNewName d.Monster.MonsterInfo.Name
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate

    React.useEffect((fun () -> load ()), [| box monsterId |])

    deferred data (fun d ->
        let m = d.Monster.MonsterInfo
        let lvl = Levels.getLvlByXp(m.XP)

        let bs = Stat.Zero
        let ls = Monster.getMonsterStatsByLvl(m.MType, m.MSubType, lvl)
        let fs = FullStat(m.Stat - ls, bs, ls)
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
                                        let! r = Api.renameMonster m.Id newName
                                        match r with
                                        | Ok ()   -> setMsg (Some "Renamed!"); load ()
                                        | Error e -> setMsg (Some ("Error: " + e))
                                    } |> Async.StartImmediate)
                                prop.text "Rename"
                            ]
                        ]
                    ]
                
                Html.div [
                    prop.className "champ-body"
                    prop.children [
                        Html.div [
                            prop.className "champ-left"
                            prop.children [
                                Html.img [ prop.className "picNormal"; Utils.srcMonsterImg m.Picture; prop.alt m.Name ]

                                Html.h3 [ prop.text m.Name ]
                                match d.Monster.UserLink with
                                | Some ul ->
                                    Html.div [
                                        Html.a [
                                            prop.href (Page.UserDetail ul.UserRawId).Route
                                            prop.onClick (Nav.navTo (Page.UserDetail ul.UserRawId).Route)
                                            prop.target.blank;
                                            prop.text $"Owned by {ul.Nickname}"
                                        ]
                                    ]
                                | None -> ()

                                Html.p [ prop.className "center muted"; prop.text m.Description ]
                                Html.p [ prop.className "center muted"; prop.text (Display.monsterClass(m.MType, m.MSubType)) ]
                                
                                match m.GenType with
                                | MonsterGenType.Generative -> ()
                                | MonsterGenType.NFTBased(assetId, website) ->
                                    Html.div [
                                        prop.className "nft-info"
                                        prop.children [
                                            Html.p [ prop.className "muted"; prop.text "Type: NFT Based" ]
                                            Html.p [
                                                Html.a [
                                                    prop.href $"https://explorer.perawallet.app/asset/{assetId}"
                                                    prop.target.blank
                                                    prop.text $"ASA: {assetId}"
                                                ]
                                            ]
                                            if String.IsNullOrWhiteSpace(website) |> not then
                                                Html.p [
                                                    Html.a [
                                                        prop.href website
                                                        prop.target.blank
                                                        prop.text "Project website"
                                                    ]
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

type private EntityTab =
    | ChampsTab
    | MonstersTab

[<ReactComponent>]
let UserDetailPage (userId: uint64) =
    let data, setData = React.useState<Deferred<UserInfo>> Loading
    let activeTab, setActiveTab = React.useState ChampsTab
    let selectedIdx, setSelectedIdx = React.useState<int option> None

    let load () =
        async {
            let! r = Api.getUserInfo userId
            match r with
            | Ok d  ->
                setData (Loaded d)
                if d.Champs.Length > 0 then setSelectedIdx (Some 0)
                elif d.Monsters.Length > 0 then
                    setActiveTab MonstersTab
                    setSelectedIdx (Some 0)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate

    React.useEffect((fun () -> load ()), [| box userId |])

    let selectTab (tab: EntityTab) (count: int) =
        setActiveTab tab
        setSelectedIdx (if count > 0 then Some 0 else None)

    deferred data (fun d ->
        let champs = d.Champs
        let champsCount = champs.Length

        let monstrs = d.Monsters
        let monstrsCount = monstrs.Length

        let itemCount =
            match activeTab with
            | ChampsTab -> champsCount
            | MonstersTab -> monstrsCount

        Html.div [
            prop.className "user-card"
            prop.children [

                Html.div [
                    prop.className "user-card-header"
                    prop.children [
                        Html.h2 [ prop.text d.Nickname ]
                    ]
                ]

                Html.div [
                    prop.className "entity-tabs"
                    prop.children [
                        Html.button [
                            prop.className (
                                if activeTab = ChampsTab
                                then "entity-tab entity-tab--active"
                                else "entity-tab")
                            prop.onClick (fun _ -> selectTab ChampsTab champsCount)
                            prop.children [
                                Html.span [ prop.className "entity-tab-label"; prop.text "Champs" ]
                                Html.span [ prop.className "entity-tab-count"; prop.text (string champsCount) ]
                            ]
                        ]
                        Html.button [
                            prop.className (
                                if activeTab = MonstersTab
                                then "entity-tab entity-tab--active"
                                else "entity-tab")
                            prop.onClick (fun _ -> selectTab MonstersTab monstrsCount)
                            prop.children [
                                Html.span [ prop.className "entity-tab-label"; prop.text "Monsters" ]
                                Html.span [ prop.className "entity-tab-count"; prop.text (string monstrsCount) ]
                            ]
                        ]
                    ]
                ]

                Html.div [
                    prop.className (
                        if itemCount <= 3
                        then "user-card-body user-card-body--centered"
                        else "user-card-body")
                    prop.children [

                        Html.div [
                            prop.className "user-card-list"
                            prop.children [
                                Html.div [
                                    prop.className "participants-grid"
                                    prop.children [
                                        match activeTab with
                                        | ChampsTab ->
                                            for i, c in champs |> List.indexed do
                                                let isActive =
                                                    selectedIdx |> Option.map ((=) i) |> Option.defaultValue false
                                                Html.div [
                                                    prop.className (
                                                        if isActive
                                                        then "participant-avatar participant-avatar--active"
                                                        else "participant-avatar")
                                                    prop.title c.Name
                                                    prop.onClick (fun _ -> setSelectedIdx (Some i))
                                                    prop.children [
                                                        ipfsImg c.IPFS "participant-img"
                                                    ]
                                                ]
                                        | MonstersTab ->
                                            for i, m in monstrs |> List.indexed do
                                                let isActive =
                                                    selectedIdx |> Option.map ((=) i) |> Option.defaultValue false
                                                Html.div [
                                                    prop.className (
                                                        if isActive
                                                        then "participant-avatar participant-avatar--active"
                                                        else "participant-avatar")
                                                    prop.title m.Name
                                                    prop.onClick (fun _ -> setSelectedIdx (Some i))
                                                    prop.children [
                                                        Html.img [
                                                            prop.className "participant-img"
                                                            Utils.srcMonsterImg m.Picture
                                                            prop.alt m.Name
                                                        ]
                                                    ]
                                                ]
                                    ]
                                ]
                            ]
                        ]

                        Html.div [
                            prop.className "user-card-detail"
                            prop.children [
                                match activeTab with
                                | ChampsTab ->
                                    match selectedIdx |> Option.bind (fun i -> if i >= 0 && i < champsCount then Some (i, champs.[i]) else None) with
                                    | None ->
                                        if champsCount > 0 then
                                            Html.p [ prop.className "muted"; prop.text "Select a champ" ]
                                        else
                                            Html.p [ prop.className "muted"; prop.text "No champs yet" ]
                                    | Some (idx, c) ->
                                        let isNavBackDisabled = idx = 0
                                        let isNavForwardDisabled = idx = champsCount - 1

                                        Html.div [
                                            prop.className "champ-nav"
                                            prop.children [
                                                Html.button [
                                                    prop.className "btn champ-nav-btn"
                                                    prop.disabled isNavBackDisabled
                                                    prop.onClick (fun _ -> setSelectedIdx (Some (idx - 1)))
                                                    prop.text "‹"
                                                ]
                                                Html.a [
                                                    prop.href (Page.ChampDetail c.ID).Route
                                                    prop.onClick (Nav.navTo (Page.ChampDetail c.ID).Route)
                                                    prop.className "champ-nav-name"
                                                    prop.text c.Name
                                                ]
                                                Html.button [
                                                    prop.className "btn champ-nav-btn"
                                                    prop.disabled isNavForwardDisabled
                                                    prop.onClick (fun _ -> setSelectedIdx (Some (idx + 1)))
                                                    prop.text "›"
                                                ]
                                            ]
                                        ]

                                        let lvl = Levels.getLvlByXp c.XP

                                        Html.div [
                                            prop.className "champ-body"
                                            prop.children [
                                                Html.div [
                                                    prop.className "champ-left"
                                                    prop.children [
                                                        ipfsImg c.IPFS "picNormal"
                                                        Html.table [
                                                            prop.className "stats-table"
                                                            prop.children [
                                                                Html.tbody [
                                                                    statRow WebEmoji.Gem "XP" (string c.XP)
                                                                    statRow WebEmoji.Level "Level" (string lvl)
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
                                                                    statRow WebEmoji.Health      "Health"       (string c.Stat.Health)
                                                                    statRow WebEmoji.Magic       "Magic"        (string c.Stat.Magic)
                                                                    statRow WebEmoji.Luck        "Luck"         (string c.Stat.Luck)
                                                                    statRow WebEmoji.Accuracy    "Accuracy"     (string c.Stat.Accuracy)
                                                                    statRow WebEmoji.Attack      "Attack"       (string c.Stat.Attack)
                                                                    statRow WebEmoji.MagicAttack "MagicAttack"  (string c.Stat.MagicAttack)
                                                                    statRow WebEmoji.Shield      "Defense"      (string c.Stat.Defense)
                                                                    statRow WebEmoji.MagicShield "MagicDefense" (string c.Stat.MagicDefense)
                                                                ]
                                                            ]
                                                        ]
                                                    ]
                                                ]
                                            ]
                                        ]
                                | MonstersTab ->
                                    match selectedIdx |> Option.bind (fun i -> if i >= 0 && i < monstrsCount then Some (i, monstrs.[i]) else None) with
                                    | None ->
                                        if monstrsCount > 0 then
                                            Html.p [ prop.className "muted"; prop.text "Select a monster" ]
                                        else
                                            Html.p [ prop.className "muted"; prop.text "No monsters yet" ]
                                    | Some (idx, m) ->
                                        let isNavBackDisabled = idx = 0
                                        let isNavForwardDisabled = idx = monstrsCount - 1

                                        Html.div [
                                            prop.className "champ-nav"
                                            prop.children [
                                                Html.button [
                                                    prop.className "btn champ-nav-btn"
                                                    prop.disabled isNavBackDisabled
                                                    prop.onClick (fun _ -> setSelectedIdx (Some (idx - 1)))
                                                    prop.text "‹"
                                                ]
                                                Html.a [
                                                    prop.href (Page.MonsterDetail m.Id).Route
                                                    prop.onClick (Nav.navTo (Page.MonsterDetail m.Id).Route)
                                                    prop.className "champ-nav-name"
                                                    prop.text m.Name
                                                ]
                                                Html.button [
                                                    prop.className "btn champ-nav-btn"
                                                    prop.disabled isNavForwardDisabled
                                                    prop.onClick (fun _ -> setSelectedIdx (Some (idx + 1)))
                                                    prop.text "›"
                                                ]
                                            ]
                                        ]

                                        let lvl = Levels.getLvlByXp m.XP

                                        Html.div [
                                            prop.className "champ-body"
                                            prop.children [
                                                Html.div [
                                                    prop.className "champ-left"
                                                    prop.children [
                                                        Html.img [
                                                            prop.className "picNormal"
                                                            Utils.srcMonsterImg m.Picture
                                                            prop.alt m.Name
                                                        ]

                                                        Html.p [ prop.className "center muted"; prop.text (Display.monsterClass(m.MType, m.MSubType)) ]
                                
                                                        match m.GenType with
                                                        | MonsterGenType.Generative -> ()
                                                        | MonsterGenType.NFTBased(assetId, website) ->
                                                            Html.div [
                                                                prop.className "nft-info"
                                                                prop.children [
                                                                    Html.p [
                                                                        Html.a [
                                                                            prop.href $"https://explorer.perawallet.app/asset/{assetId}"
                                                                            prop.target.blank
                                                                            prop.text $"ASA: {assetId}"
                                                                        ]
                                                                    ]
                                                                    if String.IsNullOrWhiteSpace(website) |> not then
                                                                        Html.p [
                                                                            Html.a [
                                                                                prop.href website
                                                                                prop.target.blank
                                                                                prop.text "Project website"
                                                                            ]
                                                                        ]
                                                                ]
                                                            ]

                                                        Html.table [
                                                            prop.className "stats-table"
                                                            prop.children [
                                                                Html.tbody [
                                                                    statRow WebEmoji.Gem "XP" (string m.XP)
                                                                    statRow WebEmoji.Level "Level" (string lvl)
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
                                                                    statRow WebEmoji.Health      "Health"       (string m.Stat.Health)
                                                                    statRow WebEmoji.Magic       "Magic"        (string m.Stat.Magic)
                                                                    statRow WebEmoji.Luck        "Luck"         (string m.Stat.Luck)
                                                                    statRow WebEmoji.Accuracy    "Accuracy"     (string m.Stat.Accuracy)
                                                                    statRow WebEmoji.Attack      "Attack"       (string m.Stat.Attack)
                                                                    statRow WebEmoji.MagicAttack "MagicAttack"  (string m.Stat.MagicAttack)
                                                                    statRow WebEmoji.Shield      "Defense"      (string m.Stat.Defense)
                                                                    statRow WebEmoji.MagicShield "MagicDefense" (string m.Stat.MagicDefense)
                                                                ]
                                                            ]
                                                        ]
                                                    ]
                                                ]
                                            ]
                                        ]
                            ]
                        ]
                    ]
                ]
            ]
        ])