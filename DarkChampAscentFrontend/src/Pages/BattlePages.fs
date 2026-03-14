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

let now () = Constructors.Date.now()
let toUnixMs (dt: DateTime) =
    (DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeMilliseconds() |> float)

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
let private JoinSection (dto: BattleDTO) =
    let selChamp, setSelChamp = React.useState None
    let selMove, setSelMove   = React.useState Move.Attack
    let joining, setJoining   = React.useState false
    let joinMsg, setJoinMsg   = React.useState<string option> None

    React.useEffect((fun () ->
        match dto.ChampsRes with
        | Some (Ok (first :: _)) -> setSelChamp (Some first)
        | _ -> ()
    ), [| box dto.ChampsRes |])

    let champsView =
        match dto.ChampsRes with
        | Some (Ok champs) ->
            if champs.IsEmpty then
                Html.p [ prop.text "No champs available." ]
            else
                Html.div [
                    prop.children [
                        Html.h2 [ prop.id "hb-current"; prop.text "My Champs" ]
                        Html.div [
                            prop.className "select-wrap"
                            prop.children [
                                TomSelectInput ""
                                    (selChamp |> Option.map (fun (cId, _, _) -> cId.ToString()) |> Option.defaultValue "")
                                    (fun s -> champs |> List.tryFind (fun (cId, _, _) -> (cId.ToString()) = s) |> setSelChamp)
                                    [ for (id, name, _) in champs -> Html.option [ prop.value (string id); prop.text name ] ]
                            ]
                        ]
                        match selChamp with
                        | Some (_, _, ipfs) -> ipfsImg ipfs "picSmall"
                        | None -> ()
                        Html.div [
                            prop.className "select-wrap"
                            prop.children [
                                TomSelectInput ""
                                    (DisplayEnum.Move selMove)
                                    (fun (s: string) ->
                                        AllEnums.Moves
                                        |> List.tryFind (fun m -> DisplayEnum.Move m = s)
                                        |> Option.iter setSelMove)
                                    [
                                        for m in AllEnums.Moves ->
                                            Html.option [ prop.value (DisplayEnum.Move m); prop.text (DisplayEnum.Move m) ]
                                    ]
                                ]
                            ]
                        
                        Html.button [
                            prop.className "btn btn-primary"
                            prop.disabled joining
                            prop.onClick (fun _ ->
                                match selChamp |> Option.map (fun (cid, _, _) -> cid) with
                                | Some cid ->
                                    setJoining true; setJoinMsg None
                                    async {
                                        let! r = Api.joinBattle cid selMove
                                        match r with
                                        | Ok () -> setJoinMsg (Some "Joined!");
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
        | Some (Error e) -> Html.p [ prop.text e ]
        | None -> Html.p [ prop.text "Sign in to join." ]

    Html.div [
        prop.className "battle-join-inner"
        prop.children [
            champsView
        ]
    ]

[<ReactComponent>]
let BattlePage () =
    let data, setData = React.useState<Deferred<BattleDTO>> Loading
    let participants, setParticipants = React.useState(None)
    let roundInfo, setRoundInfo = React.useState(None)
    let prevStatus, setPrevStatus = React.useState<RoundStatus option>(None)

    useSSE (Api.baseUrl + Pattern.BattleParticipants.Str) (fun data ->
        Decoders.parseParticipants data
        |> Some
        |> setParticipants
    )

    useSSE (Api.baseUrl + Pattern.BattleStatusInfo.Str) (fun data ->
        Decoders.parseResult Decoders.decodeRoundInfoDTO data
        |> Some
        |> setRoundInfo
    )

    let load () =
        async {
            let! r = Api.getBattle ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate

    React.useEffect((fun () -> load ()), [||])

    React.useEffect((fun () ->
        match roundInfo with
        | Some (Ok (ri: RoundInfoDTO)) ->
            match prevStatus with
            | Some ps when ps <> ri.Status ->
                setPrevStatus (Some ri.Status)
                load ()
            | None ->
                setPrevStatus (Some ri.Status)
            | _ -> ()
        | _ -> ()
    ), [| box roundInfo |])
    deferred data (fun dto ->
        let timerBlock =
            let roundChamps =
                match participants with
                | Some r -> match r with Ok c -> c | Error _ -> [] 
                | None -> []
            let hasPlayers  = not roundChamps.IsEmpty
            
            match roundInfo with
            | Some r ->
                match r with
                | Ok roundInfoDTO ->
                    let targetUtcO, titleO, explanation =
                        match roundInfoDTO.Status with
                        | RoundStatus.Started ->
                            match roundInfoDTO.RoundStarted with
                            | Some start ->
                                let targetUtc = start + Params.RoundDuration
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
                | Error err -> Html.p [ prop.text err ]
            | None -> Html.none

        let isChampViewVisible =
            roundInfo
            |> Option.map (fun r -> match r with | Ok ri -> ri.Status = RoundStatus.Started | Error _ -> false)
            |> Option.defaultValue true

        let currentBattleSection =
            Html.section [
                prop.className "block current-battle"
                prop.id "current-battle"
                prop.children [
                    match dto.CurrentBattleInfoR with
                    | Ok cbi ->
                        let rounds = dto.History |> Result.map (fun moves -> moves.Length) |> Result.defaultValue 0
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
                                        Html.div [ prop.className "center"; prop.text $"{WebEmoji.Health} {cbi.Monster.Stat.Health} Health" ]
                                        Html.div [ prop.className "center"; prop.text $"{WebEmoji.Magic} {cbi.Monster.Stat.Magic} Magic" ]
                                    ]
                                ]
                                if isChampViewVisible then
                                    Html.div [
                                        prop.className "battle-join"
                                        prop.children [ JoinSection dto ]
                                    ]
                            ]
                        ]
                    | Error e ->
                        Html.p [ prop.text e ]
                ]
            ]

        let participantsSection =
            Html.aside [
                prop.className "block active-participant"
                prop.id "active-participant"
                prop.children [
                    match participants with
                    | Some r ->
                        match r with
                        | Ok champs ->
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
                        | Error e -> Html.p [ prop.text e ]
                    | None -> Html.none
                ]
            ]

        let historySection =
            Html.section [
                prop.className "block history"
                prop.id "history"
                prop.children [
                    match dto.History with
                    | Ok moves ->
                        let rounds = moves.Length
                        if moves.IsEmpty then
                            Html.p [ prop.text "Waiting for round completion" ]
                        else
                            Html.h2 [ prop.className "center"; prop.text $"{WebEmoji.History} Battle history" ]
                            Html.hr []
                            for (r, roundMoves) in moves |> List.truncate 3 do
                                Html.div [
                                    prop.children [
                                        Html.p [ prop.className "round-header center"; prop.text $"Round {r}" ]
                                        Html.hr []
                                        for (sn, pm, tn) in roundMoves do
                                            Html.p [ prop.className "round-move"; prop.text (Display.performedMoveWebUi pm sn tn) ]
                                    ]
                                ]
                            if rounds > 3 then
                                Html.p [ prop.text $"{rounds - 3} more rounds {WebEmoji.Rounds} omitted for clarity" ]
                    | Error e -> Html.p [ prop.text e ]
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