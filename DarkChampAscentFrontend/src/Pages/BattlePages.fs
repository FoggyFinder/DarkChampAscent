module Pages.Battle

open Feliz
open Types
open Display
open Components
open DTO
open System
open GameLogic.Battle
open GameLogic.Champs
open DarkChampAscent.Api
open Fable.Core.JS
open GameLogic.Monsters

let now () = Constructors.Date.now()
let toUnixMs (dt: DateTime) =
    (DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeMilliseconds() |> float)

let private champAvatar (c: RoundParticipantChamp) =
    (ipfsImg c.IPFS "move-actor-img") 

let private monsterAvatar (m: RoundParticipantMonster) =
    (Html.img [ Utils.srcMonsterImg m.Img ; prop.className "move-actor-img" ])

let private actor (avatar: ReactElement) (name: string) (page: Page) =
    Html.a [
        prop.href page.Route
        prop.onClick (Nav.navTo page.Route)
        prop.className "move-actor"
        prop.children [ avatar; Html.text name ]
    ]

let private champActor (c: RoundParticipantChamp) =
    actor (champAvatar c) c.Name (Page.ChampDetail c.ID)

let private monsterActor (m: RoundParticipantMonster) =
    actor (monsterAvatar m) m.Name (Page.MonsterDetail m.ID)

let private performedMove (ri:RoundInfo) (pmd: PMResult) (monster: RoundParticipantMonster) =
    
    let sActor, tActor =
        match pmd.Detail with
        | PMDetail.Monster m ->
            monsterActor monster,
            m.Target |> Option.map champActor |> Option.defaultValue Html.none
        | PMDetail.Champ c ->
            champActor c.Champ,
            monsterActor monster

    let isKiller =
        match pmd.Detail with
        | PMDetail.Monster m ->
            match m.Target with
            | Some cId -> ri.DefeatedChamps |> List.contains cId.ID
            | None -> false
        | PMDetail.Champ c ->
            Some c.Champ.ID = ri.MonsterKiller

    let pm =
        match pmd.Detail with
        | PMDetail.Monster m -> m.PM
        | PMDetail.Champ c -> c.PM

    let attack, mattack, shield, mshield, health, magic =
        WebEmoji.Attack, WebEmoji.MagicAttack, WebEmoji.Shield,
        WebEmoji.MagicShield, WebEmoji.Health, WebEmoji.Magic

    let parts =
        match pm with
        | PerformedMove.Attack dmg ->
            match dmg with
            | Dmg.Critical v | Dmg.Default v ->
                if v > 0UL then 
                    [ sActor; Html.text $" {attack} overpowered "; tActor; Html.text $" protection and took {v} {health}" ]
                else           [ sActor; Html.text " attack but "; tActor; Html.text " defense was too strong "; ]
            | Dmg.Missed ->    [ sActor; Html.text " missed "; tActor; Html.text ". Maybe next time?" ]
        | PerformedMove.MagicAttack(dmg, m) ->
            match dmg with
            | Dmg.Critical v | Dmg.Default v ->
                if v > 0UL then 
                    [ sActor; Html.text $" {mattack} overpowered "; tActor; Html.text $" protection and took {v} {health}" ]
                else           [ sActor; Html.text $" attacked, but "; tActor; Html.text $" was too good and blocked fible attempt, {m} {magic} was taken nevertheless" ]
            | Dmg.Missed ->    [ sActor; Html.text " missed "; tActor; Html.text ". Maybe next time?" ]
        | PerformedMove.Shield v ->
            [ sActor; Html.text $" increased their defense: + {v} to {shield}" ]
        | PerformedMove.MagicShield(v1, v2) ->
            let desc =
                if v1 > 0UL && v2 > 0UL then $" casted magical protection with {v1} {mshield} and spend {v2} {magic}"
                elif v1 = 0UL && v2 > 0UL then $" casted magical protection, used {v2} {magic} but failed to produce anything sustainable"
                else " don't have enough magic power to cast magic shield"
            [ sActor; Html.text desc ]
        | PerformedMove.Heal(v1, v2) ->
            let desc =
                if v1 > 0UL && v2 > 0UL then $" healed {v1} {health} life with {v2} {magic} magic"
                elif v1 = 0UL && v2 > 0UL then $" used {v2} {magic} but failed to heal themself"
                else " don't have enough magic power to heal"
            [ sActor; Html.text desc ]
        | PerformedMove.Meditate v ->
            [ sActor; Html.text $" gained {v} {magic}" ]

    Html.p [ 
        prop.className "round-move";
        prop.children [
            yield! parts
            yield Html.text $" + {pmd.XP} {WebEmoji.Gem} XP"
            match pmd.Rewards with
            | Some r ->
                yield Html.span [ prop.dangerouslySetInnerHTML $"; + {r} {WebEmoji.DarkCoin}" ]
            | _ -> ()
        ]
    ]

[<ReactComponent>]
let private Countdown (targetMs: float) =
    let timeLeft, setTimeLeft = React.useState (targetMs - now())
    React.useEffect((fun () ->
        let timer = Browser.Dom.window.setInterval((fun () ->
            setTimeLeft (targetMs - now())
        ), 1000)
        { new System.IDisposable with member _.Dispose() = Browser.Dom.window.clearInterval timer }
    ), [| box targetMs |])
    let total = int (timeLeft / 1000.0)
    let mins = total / 60
    let secs = total % 60
    Html.div [
        prop.className "countdown-ms"
        prop.role "timer"
        prop.children [
            Html.span [ prop.className "countdown-unit minutes"; prop.text (sprintf "%02d" (max 0 mins)) ]
            Html.span [ prop.className "countdown-sep"; prop.text ":" ]
            Html.span [ prop.className "countdown-unit seconds"; prop.text (sprintf "%02d" (max 0 secs)) ]
        ]
    ]

[<ReactComponent>]
let private JoinSection (dto: ChampInfoWithStat list) (onJoined: unit -> unit) =
    let selChamp, setSelChamp = React.useState None
    let selMove, setSelMove   = React.useState Move.Attack
    let joining, setJoining   = React.useState false
    let joinMsg, setJoinMsg   = React.useState<string option> None

    React.useEffect((fun () ->
        match dto with
        | first :: _ ->
            setSelChamp (Some first)
            setSelMove Move.Attack
        | _ -> ()
    ), [| box dto |])

    let champsView =
        match dto with
        | champs ->
            if champs.IsEmpty then
                Html.p [ prop.text "No champs available." ]
            else
                Html.div [
                    prop.children [
                        Html.h2 [ prop.id "hb-current"; prop.text "My Champs" ]
                        Html.div [
                            prop.className "select-wrap"
                            prop.children [
                                CustomSelectInput
                                    (selChamp |> Option.map (fun rpc -> rpc.ID.ToString()) |> Option.defaultValue "")
                                    (fun s -> champs |> List.tryFind (fun rpc -> (rpc.ID.ToString()) = s) |> setSelChamp)
                                    [ for rpc in champs -> rpc.ID.ToString(), rpc.Name, Some (Links.IPFS + rpc.IPFS) ]
                            ]
                        ]

                        match selChamp with
                        | Some sChamp -> chTable sChamp.Stat                          
                        | None -> ()
                        
                        Html.div [
                            prop.className "select-wrap"
                            prop.children [
                                CustomSelectInput
                                    (DisplayEnum.Move selMove)
                                    (fun (s: string) ->
                                        AllEnums.Moves
                                        |> List.tryFind (fun m -> DisplayEnum.Move m = s)
                                        |> Option.iter setSelMove)
                                    [ for m in AllEnums.Moves -> DisplayEnum.Move m, DisplayEnum.Move m, None ]
                                ]
                        ]

                        Html.button [
                            prop.className "btn btn-join"
                            prop.disabled joining
                            prop.onClick (fun _ ->
                                match selChamp |> Option.map (fun rpc -> rpc.ID) with
                                | Some cid ->
                                    setJoining true; setJoinMsg None
                                    async {
                                        let! r = Api.joinBattle cid selMove
                                        match r with
                                        | Ok () -> onJoined ()
                                        | Error e -> setJoinMsg (Some ("Error: " + e))
                                        setJoining false
                                    } |> Async.StartImmediate
                                | None -> setJoinMsg (Some "Error: no champ selected"))
                            prop.text (if joining then "Joining..." else "Join round")
                        ]
                        match joinMsg with
                        | Some m -> Html.p [ prop.className "action-msg"; prop.text m ]
                        | None -> Html.none
                    ]
                ] 

    Html.div [
        prop.className "battle-join-inner"
        prop.children [
            champsView
        ]
    ]

[<ReactComponent>]
let BattlePage () =
    let data, setData = React.useState<Deferred<BattleInfoDTO>> Loading
    let participants, setParticipants = React.useState(None)
    let roundInfo, setRoundInfo = React.useState(None)
    let activeChamps, setActiveChamps = React.useState<Deferred<ChampInfoWithStat list option>> Loading

    let load () =
        async {
            let! r = Api.getActiveUserChamps ()
            match r with | Ok d  -> Loaded d | Error e -> Failed e
            |> setActiveChamps
        } |> Async.StartImmediate
   
    useSSE (Api.baseUrl + Pattern.BattleStatusInfo.Str) (fun data ->
        match Decoders.decodeBattleInfoDTO data with
        | Some bi -> setData (Loaded bi)
        | None -> setData (Failed "Unable to parse json")
    )

    useSSE (Api.baseUrl + Pattern.BattleParticipants.Str) (fun data ->
        Decoders.parseParticipants data
        |> setParticipants
    )

    useSSE (Api.baseUrl + Pattern.BattleRoundStatusInfo.Str) (fun data ->
        load()
        match Decoders.decodeRoundInfoDTO data with
        | Some ri ->
            match ri.Status with
            | RoundStatus.Started -> load()
            | _ -> Some([]) |> Loaded |> setActiveChamps
            Some ri |> setRoundInfo
        | None ->
            Some([]) |> Loaded |> setActiveChamps
            None |> setRoundInfo
    )

    deferred data (fun dto ->
        let timerBlock =
            let roundChamps =
                match participants with
                | Some r -> r
                | None -> []
            let hasPlayers  = not roundChamps.IsEmpty
            
            match roundInfo with
            | Some roundInfoDTO ->
                let targetUtcO, titleO, explanation =
                    match roundInfoDTO.Status with
                    | RoundStatus.Started ->
                        match roundInfoDTO.RoundStarted with
                        | Some start ->
                            let targetUtc = start + BattleParams.RoundDuration()
                            let isAwaitingPlayers = now() > (toUnixMs targetUtc)
                            if isAwaitingPlayers then
                                if hasPlayers then None, None, "Round closes any second"
                                else None, None, "Round starts within 1 minute as any player joins it"
                            else Some targetUtc, Some "Time to round update:", ""
                        | None -> None, None, ""
                    | RoundStatus.Processing -> None, Some "Round is processing:", ""
                    | RoundStatus.Finished   -> None, Some "Round is finished, waiting...", ""
                    | _ -> None, None, ""
                Html.div [
                    prop.className "timer-group"
                    prop.children [
                        Html.text explanation
                        match titleO with
                        | Some title -> Html.h3 [ prop.text title ]
                        | None -> Html.none
                        match targetUtcO with
                        | Some t -> Countdown (toUnixMs t)
                        | None -> Html.none
                    ]
                ]
            | None -> Html.none

        let isChampViewVisible =
            roundInfo
            |> Option.map (fun r -> r.Status = RoundStatus.Started)
            |> Option.defaultValue true

        let currentBattleSection =
            Html.section [
                prop.className "block current-battle"
                prop.id "current-battle"
                prop.children [
                    let cbi = dto.CurrentBattleInfo
                    let rounds = dto.History.Rounds.Length
                    Html.h2 [ prop.text $"Current battle: {cbi.BattleNum} ({DisplayEnum.BattleStatus cbi.BattleStatus})" ]
                        
                    Html.div [
                        prop.className "progress-row"
                        prop.children [
                            Html.progress [ prop.className "progress-bar"; prop.value (float rounds / float Constants.RoundsInBattle) ]
                            Html.span [ prop.className "round-count"; prop.text $"{rounds} / {Constants.RoundsInBattle} rounds {WebEmoji.Rounds}" ]
                            timerBlock
                        ]
                    ]

                    Html.div [
                        prop.className "battle-body"
                        prop.children [
                            Html.div [
                                prop.className "battle-monster"
                                prop.children [
                                    Html.img [ prop.className "picNormal"; Utils.srcMonsterImg cbi.Monster.Picture ]
                                    Html.div [ prop.className "center"; prop.children [ monsterLink (uint64 cbi.MonsterId) cbi.Monster.Name ] ]
                                    Html.div [ prop.className "center muted"; prop.text (Display.monsterClass(cbi.Monster.MType, cbi.Monster.MSubType)) ]
                                    Html.div [ prop.className "center"; prop.text $"{WebEmoji.Gem} {cbi.Monster.XP} XP ({WebEmoji.Level} {Levels.getLvlByXp cbi.Monster.XP} lvl)" ]
                       
                                    chTable cbi.Monster.Stat
                                ]
                            ]
                            
                            if isChampViewVisible then
                                Html.div [
                                    prop.className "battle-join"
                                    prop.children [ 
                                        match activeChamps with
                                        | Deferred.Loaded xsO ->
                                            match xsO with
                                            | Some xs -> JoinSection xs load
                                            | None -> Html.p [ prop.text "Sign in to join." ]
                                        | Deferred.Loading | Deferred.NotStarted ->
                                            Html.p [ prop.text "loading..." ]
                                        | Deferred.Failed e ->
                                            Html.p [ prop.text e ]
                                    ]
                                ]
                        ]
                    ]
                ]
            ]

        let participantsSection =
            Html.aside [
                prop.className "block active-participant"
                prop.id "active-participant"
                prop.children [
                    match participants with
                    | Some champs ->
                        if champs.IsEmpty then
                            Html.p [ prop.text "Waiting for players" ]
                        else
                            Html.h2 [ prop.text $"Participants ({champs.Length}):" ]
                            Html.div [
                                prop.className "participants-grid"
                                prop.children [
                                    for p in champs do
                                        Html.a [
                                            prop.href (Page.ChampDetail p.ID).Route
                                            prop.onClick (Nav.navTo (Page.ChampDetail p.ID).Route)
                                            prop.className "participant-avatar"
                                            prop.title p.Name
                                            prop.children [
                                                ipfsImg p.IPFS "participant-img"
                                            ]
                                        ]
                                ]
                            ]
                    | None -> Html.none
                ]
            ]

        let historySection =
            Html.section [
                prop.className "block history"
                prop.id "history"
                prop.children [
                    let bh = dto.History
                    let rounds = bh.Rounds.Length
                    if bh.Rounds.IsEmpty then
                        Html.p [ prop.text "Waiting for round completion" ]
                    else
                        Html.h2 [ prop.className "center"; prop.text $"{WebEmoji.History} Battle history" ]
                        Html.hr []
                        for ri in bh.Rounds |> List.truncate 3 do
                            let srewards = ri.Rewards
                            Html.div [
                                prop.children [
                                    Html.p [ prop.className "round-header center"; prop.text $"Round {ri.RoundId}" ]
                                    Html.hr []
                                    for pmd in ri.Details do
                                        performedMove ri pmd bh.Monster
                                    Html.hr []

                                    Html.p [ prop.className "rewards-subheader center"; prop.text $"Rewards" ]
                                    Html.table [
                                        Html.tbody [
                                            Html.tr [
                                                Html.td [ prop.text $"{WebEmoji.Champs} Players {WebEmoji.Champs}" ]
                                                Html.td [ prop.dangerouslySetInnerHTML $"{Display.toRound6StrD(srewards.Champs)} {WebEmoji.DarkCoin}" ]
                                            ]
                                            Html.tr [
                                                Html.td [ prop.text $"{WebEmoji.DAO} DAO {WebEmoji.DAO}" ]
                                                Html.td [ prop.dangerouslySetInnerHTML $"{Display.toRound6StrD(srewards.DAO)} {WebEmoji.DarkCoin}" ]
                                            ]
                                            Html.tr [
                                                Html.td [ prop.text $"{WebEmoji.Dev} Dev {WebEmoji.Dev}" ]
                                                Html.td [ prop.dangerouslySetInnerHTML $"{Display.toRound6StrD(srewards.Dev)} {WebEmoji.DarkCoin}" ]
                                            ]
                                            Html.tr [
                                                Html.td [ prop.text $"{WebEmoji.Reserve} Reserve {WebEmoji.Reserve}" ]
                                                Html.td [ prop.dangerouslySetInnerHTML $"{Display.toRound6StrD(srewards.Reserve)} {WebEmoji.DarkCoin}" ]
                                            ]
                                            Html.tr [
                                                Html.td [ prop.text $"{WebEmoji.Staking} Staking {WebEmoji.Staking}" ]
                                                Html.td [ prop.dangerouslySetInnerHTML $"{Display.toRound6StrD(srewards.Staking)} {WebEmoji.DarkCoin}" ]
                                            ]
                                            Html.tr [
                                                Html.td [ prop.text $"{WebEmoji.Fire} Burn {WebEmoji.Fire}" ]
                                                Html.td [ prop.dangerouslySetInnerHTML $"{Display.toRound6StrD(srewards.Burn)} {WebEmoji.DarkCoin}" ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        if rounds > 3 then
                            Html.p [ prop.text $"{rounds - 3} more rounds {WebEmoji.Rounds} omitted for clarity" ]
                ]
            ]

        Html.div [
            prop.className "dashboard"
            prop.children [
                currentBattleSection
                participantsSection
                historySection
            ]
        ]
    )