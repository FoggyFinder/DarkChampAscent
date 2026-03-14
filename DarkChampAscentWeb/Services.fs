module Services

open Db
open Microsoft.Extensions.Hosting
open System.Threading.Tasks
open System
open DTO
open Serilog
open System.Threading
open GameLogic.Battle

// TODO: remove it once battle service are moved here
type RoundParticipantsService(rdb:SqliteWebUiStorage) =
    inherit BackgroundService()
    let roundParticipantsChanged = Event<Result<RoundParticipantDTO list, string>>()
    let mutable currentR = None
    let locker = obj()
    let scanLock = new SemaphoreSlim(1, 1)

    let raiseIfChanged (v:Result<RoundParticipantDTO list, string>) =
        lock locker (fun _ ->
            if Some v <> currentR then
                currentR <- Some v
                roundParticipantsChanged.Trigger v
        )

    let scan() =
        if scanLock.Wait(0) then
            try
                rdb.GetLastRoundParticipants ()
                |> Result.map(List.map(fun (cId, name, ipfs) -> RoundParticipantDTO(cId, name, ipfs)))
                |> raiseIfChanged
            with exn ->
                Log.Error(exn, "roundParticipantsTracker.scan")
            scanLock.Release() |> ignore

    member _.RoundParticipantsChanged = roundParticipantsChanged.Publish
    member _.CurrentValue = lock locker (fun _ -> currentR)
    member _.ForceRescan() = scan()

    override _.ExecuteAsync(cancellationToken) =
        task {
            scan()
            while cancellationToken.IsCancellationRequested |> not do
                try
                    do! Task.Delay(TimeSpan.FromSeconds(15.0), cancellationToken)
                    scan()
                with :? OperationCanceledException -> ()
        }

    override _.Dispose() =
        scanLock.Dispose()
        base.Dispose()

type RoundStatusService(db:SqliteStorage) =
    inherit BackgroundService()
    let roundStatusChanged = Event<Result<RoundInfoDTO, string>>()
    let mutable currentR = None
    let locker = obj()
    let scanLock = new SemaphoreSlim(1, 1)

    let raiseIfChanged (v:Result<RoundInfoDTO, string>) =
        lock locker (fun _ ->
            if Some v <> currentR then
                currentR <- Some v
                roundStatusChanged.Trigger v
        )

    let scan() =
        if scanLock.Wait(0) then
            try
                let roundInfo =
                    match db.GetLastRoundId() with
                    | Some roundId ->
                        match db.GetRoundStatus roundId with
                        | Some status ->
                            let tsO =
                                match status with
                                | RoundStatus.Started -> db.GetRoundTimestamp roundId
                                | _ -> None
                            RoundInfoDTO(status, tsO) |> Ok
                        | None -> Error "Unable to get round status info"
                    | None -> Error "Unable to get last round info"
                roundInfo
                |> raiseIfChanged
            with exn ->
                Log.Error(exn, "roundStatusService.scan")
            scanLock.Release() |> ignore

    member _.RoundStatusChanged = roundStatusChanged.Publish
    member _.CurrentValue = lock locker (fun _ -> currentR)
    member _.ForceRescan() = scan()

    override x.ExecuteAsync(cancellationToken) =
        task {
            while cancellationToken.IsCancellationRequested |> not do
                try
                    scan()
                    let current = x.CurrentValue
                    let delay =
                        match current with
                        | Some (Ok rs) ->
                            match rs.Status with
                            | RoundStatus.Finished | RoundStatus.Processing -> TimeSpan.FromMinutes 1.
                            | RoundStatus.Started ->
                                match rs.RoundStarted with
                                | Some start ->
                                    let targetUtc = start + Params.RoundDuration
                                    let isAwaitingPlayers = DateTime.UtcNow > targetUtc
                                    if isAwaitingPlayers then TimeSpan.FromMinutes 1.
                                    else targetUtc - DateTime.UtcNow
                                | None -> TimeSpan.FromMinutes 1.0
                        | _ -> TimeSpan.FromMinutes 1.
                    do! Task.Delay(delay, cancellationToken)
                with :? OperationCanceledException -> ()
        }

    override _.Dispose() =
        scanLock.Dispose()
        base.Dispose()