module BattleView

open Falco.Markup
open GameLogic.Champs
open System
open Types
open UI
open Display
open GameLogic.Battle

let joinBattle (hasPlayers:bool) (roundInfo:(RoundStatus * DateTime option) option) (champsRes: Result<(uint64 * string * string) list, string>) = 
    let toNextRoundO =
        match roundInfo with
        | Some(status, startVO) ->
            Elem.div [ ] [
                let targetUtcO, titleO, explanation = 
                    match status with
                    | RoundStatus.Started ->
                        match startVO with
                        | Some start ->
                            let targetUtc = start + Battle.RoundDuration
                            let isAwaitingPlayers = DateTime.UtcNow > targetUtc
                        
                            if isAwaitingPlayers then
                                if hasPlayers then
                                    Some (DateTime.UtcNow.AddMinutes(1)), None, "Round closes any second"
                                else
                                    None, None, "Round starts within 1 minute as any player join it"
                            else
                                Some targetUtc, Some "Time to round update:", ""
                        | None -> None, None, ""
                    | RoundStatus.Processing ->
                        Some (DateTime.UtcNow.AddMinutes(1)), Some "Round is processing:", ""
                    | RoundStatus.Finished ->
                        Some (DateTime.UtcNow.AddMinutes(1)), Some "Time to round update:", ""
                Elem.div [ ] [
                    Text.raw explanation
                    match titleO with
                    | Some title ->
                        Elem.h3 [] [
                            Text.raw title
                        ]
                    | None -> ()
                    match targetUtcO with
                    | Some targetUtc ->
                        let iso = targetUtc.ToString("o") // ISO 8601
                        Elem.div [
                            Attr.class' "countdown-ms"
                            XmlAttribute.KeyValueAttr("data-target", iso)
                            XmlAttribute.KeyValueAttr("role", "timer")
                            XmlAttribute.KeyValueAttr("aria-live", "polite")
                            XmlAttribute.KeyValueAttr("aria-atomic", "true")
                        ] [
                            Elem.span [ Attr.class' "countdown-unit minutes" ] [ Text.raw "00" ]
                            Elem.span [ Attr.class' "countdown-sep" ] [ Text.raw ":" ]
                            Elem.span [ Attr.class' "countdown-unit seconds" ] [ Text.raw "00" ]

                            Elem.time [
                                XmlAttribute.KeyValueAttr("datetime", iso)
                                Attr.class' "visually-hidden"
                            ] [ Text.raw iso ]
                        ]

                        Elem.script [
                            XmlAttribute.KeyValueAttr("src", "countdown.js")
                            XmlAttribute.KeyValueAttr("defer", "defer")
                        ] []
                    | None -> ()
                ]
            ]
            |> Some
        | None -> None

    let champsView =
        Elem.div [ ] [
            Elem.h2 [ Attr.id "hb-current" ] [
                Text.raw "My Champs"
            ]
            match champsRes with
            | Ok champs ->
                let champOptions =
                    champs |> List.map(fun (id, name, ipfs) ->
                        Elem.option [ Attr.value (string id) ] [
                            Text.raw name
                        ]
                    )
    
                let moveOptions =
                    Enum.GetNames<Move>()
                    |> Array.map(fun s ->
                        Elem.option [ Attr.value s ] [
                                Text.raw s
                            ])
                    |> Array.toList

        
                if champs.IsEmpty then
                    Text.raw "None left"
                else
                    Elem.form [
                        Attr.methodPost
                        Attr.action Route.joinBattle
                    ] [
                        Elem.div [ Attr.class' "select-wrap" ] [
                            Elem.select [
                              Attr.class' "tom-select"
                              Attr.id "champ"
                              Attr.name "champ"
                            ] (champOptions)
                          ]

                        Elem.div [ Attr.class' "select-wrap" ] [
                            Elem.select [
                              Attr.class' "tom-select"
                              Attr.id "move"
                              Attr.name "move"
                            ] moveOptions
                          ]

                        Elem.input [
                            Attr.class' "btn btn-primary"
                            Attr.typeSubmit
                            Attr.value "Join round"
                        ]
                    ]
       
            | Error err ->
                Text.raw err
        ]
    
    let isChampViewVis =
        roundInfo
        |> Option.map(fun (status, _) -> status = RoundStatus.Started)
        |> Option.defaultValue true

    Elem.section [ 
        Attr.class' "block my-champs"
        Attr.id "my-champs"
        XmlAttribute.KeyValueAttr("aria-labelledby", "hb-champs")
    ] [
        match toNextRoundO with
        | Some v -> v
        | None -> ()

        if isChampViewVis then
            champsView
    ]

let currentBattleInfo(cbir:Result<CurrentBattleInfo, string>) =
    Elem.section [ 
        Attr.class' "block current-battle"
        Attr.id "current-battle"
        XmlAttribute.KeyValueAttr("aria-labelledby", "hb-current")
    ] [
        match cbir with
        | Ok cbi ->
            Elem.h2 [ Attr.id "hb-current" ] [
                Text.raw $"Current battle: {cbi.BattleNum} ({cbi.BattleStatus})"
            ]
            let src = FileUtils.getLocalImg cbi.Monster.Picture
            Elem.table [] [
                Elem.tr [] [
                    Elem.td [] [ 
                        Elem.img [
                            Attr.class' "picSmall"
                            Attr.src $"/{src}"
                        ]
                    ]
                    Elem.td [] [ Text.raw $"{cbi.Monster.Name}" ]
                ]

                Elem.tr [] [
                    Elem.td [ Attr.colspan "2" ] [ 
                        Text.raw $"{cbi.Monster.MType} ({cbi.Monster.MSubType})" 
                    ]
                ]

                Elem.tr [] [
                    Elem.td [ Attr.colspan "2" ] [ 
                        Text.raw $"{WebEmoji.Gem} {cbi.Monster.XP} XP ({WebEmoji.Level} {Levels.getLvlByXp cbi.Monster.XP} lvl)" 
                    ]
                ]
            ]
        | Error err ->
            Text.raw err
    ]

let historyView (history: Result<(uint64 * (string * PerformedMove * string) list) list, string>) =
    Elem.section [
        Attr.class' "block history"
        Attr.id "history"
        XmlAttribute.KeyValueAttr("aria-labelledby", "hb-history")
    ] [
        match history with
        | Ok moves ->
            if moves.IsEmpty then
                Text.p "Waiting for round completion"
            else
                Text.h2 "Battle history"
                Elem.hr []
                let items =
                    moves |> List.map(fun (r, moves) ->
                        Elem.div [ ] [
                            Elem.p [ Attr.class' "round-header center" ] [ Text.raw $"Round {r}" ]
                            Elem.hr []
                            yield! moves |> List.map(fun (sn, pm, tn) ->
                                Elem.p [ Attr.class' "round-move" ] [ Text.raw $"{Display.performedMoveWebUi pm sn tn}" ]
                            )
                        ]
                )
                yield! items
        | Error err ->
            Text.p err
    ]

let roundParticipantsView (champsRes: Result<(uint64 * string * string) list, string>) =
    Elem.aside [
        Attr.class' "block active-participant"
        Attr.id "active-participant"
        XmlAttribute.KeyValueAttr("aria-labelledby", "hb-active")
        Attr.role "complementary"
    ] [
        match champsRes with
        | Ok champs ->
            if champs.IsEmpty then
                Text.p "Waiting for players"
            else
                Text.h2 "Current round participants: "
                Elem.hr []
                yield! champs |> List.map(fun (id, name, ipfs) ->
                    Elem.div [ ] [
                        Elem.img [
                            Attr.class' "picSmall"
                            Attr.src (Links.IPFS + ipfs)
                        ]
                        Elem.a [ 
                            Attr.href (Uri.champ id)
                            Attr.class' "center"
                        ] [ Text.raw name ]
                    ]
                )
        | Error err ->
            Text.p err
    ]

let battleView
    (userView:XmlNode)
    (cb:Result<CurrentBattleInfo, string>)
    (history: Result<(uint64 * (string * PerformedMove * string) list) list, string>)
    (roundChamps: Result<(uint64 * string * string) list, string>) =
    Elem.main [
        Attr.class' "dashboard"
        Attr.role "main"
        XmlAttribute.KeyValueAttr("aria-label", "Battle dashboard")
    ] [
        userView
        currentBattleInfo cb
        roundParticipantsView roundChamps
        historyView history
    ]