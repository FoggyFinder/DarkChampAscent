namespace GameLogic.Battle

open System
open GameLogic.Effects
open GameLogic.Champs
open GameLogic.Monsters
open GameLogic.Rewards

[<RequireQualifiedAccess>]
type BattleStatus =
    | Started = 0
    | Processing = 1
    | Finished = 2

[<RequireQualifiedAccess>]
type RoundStatus =
    | Started = 0
    | Processing = 1
    | Finished = 2

type RoundActionRecord = {
    Move: Move
    ChampId: uint64
}

type RoundAction = {
    Move: Move
    Timestamp: DateTime
    ChampId: uint64
    ChampName: string
    Stat: Stat
    ChampLvl: uint64
}

// effect is something that already took into account boosts and other effects
type RoundEffect = {
    StartRoundId: int64
    Item : Effect
    Duration : int
    Val : uint64 option
}

type BattleResult = {
    RoundId: uint64
    BattleId: uint64
    MonsterChar: MonsterChar

    Rewards: RoundRewardSplit
            
    ChampsMoveAndXp: Map<uint64, PerformedMove * uint64>
    ChampsFinalStat: Map<uint64, Stat>
    DeadChamps: uint64 list

    MonsterDefeater: uint64 option
    MonsterPM: PerformedMove option
    MonsterActions: Map<uint64, PerformedMove * uint64>
}

[<RequireQualifiedAccess>]
module Params =
    let RoundDuration = TimeSpan.FromMinutes(30.0)