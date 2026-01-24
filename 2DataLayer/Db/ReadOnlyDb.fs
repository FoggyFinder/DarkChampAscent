namespace Db

open Microsoft.Extensions.Options
open GameLogic.Monsters
open GameLogic.Champs
open GameLogic.Battle
open Microsoft.Data.Sqlite
open Donald
open Serilog
open Db.Sqlite
open System.Text.Json.Serialization
open System
open GameLogic.Effects
open Conf
open Types

type SqliteWebUiStorage(options:IOptions<DbConfiguration>)=
    let cs = options.Value.ConnectionString
    let getUserIdByDiscordId(discordId: uint64) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetUserIdByDiscordId conn
            |> Db.setParams [ "discordId", SqlType.Int64 <| int64 discordId ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
        with exn ->
            Log.Error(exn, $"getUserIdByDiscordId: {discordId}")
            None
    
    let isRegistered(discordId: uint64) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.UserExists conn
            |> Db.setParams [ "discordId", SqlType.Int64 <| int64 discordId ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, $"userExists: {discordId}")
            false

    member _.GetLastRoundId() =
        try 
            use conn = new SqliteConnection(cs)
            
            Db.newCommand SQL.GetLastRound conn
            |> Db.querySingle(fun r -> uint64 <| r.GetInt64(0))
        with exn ->
            Log.Error(exn, "GetLastRoundId")
            None
    
    member _.GetRoundStatus(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            
            Db.newCommand SQL.GetRoundStatus conn
            |> Db.setParams [
                "roundId", SqlType.Int64 <| int64 roundId
            ]
            |> Db.querySingle(fun r -> enum<RoundStatus> <| r.GetInt32(0))
        with exn ->
            Log.Error(exn, "GetRoundStatus")
            None
  
    member _.GetBattleIdForRound(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            
            Db.newCommand SQL.GetBattleIdForRound conn
            |> Db.setParams [
                "roundId", SqlType.Int64 <| int64 roundId
            ]
            |> Db.querySingle(fun r -> uint64 <| r.GetInt64(0))
        with exn ->
            Log.Error(exn, "GetBattleIdForRound")
            None

    member _.GetBattleStatus(battleId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            
            Db.newCommand SQL.GetBattleStatus conn
            |> Db.setParams [
                "battleId", SqlType.Int64 <| int64 battleId
            ]
            |> Db.querySingle(fun r -> enum<BattleStatus> <| r.GetInt32(0))
        with exn ->
            Log.Error(exn, "GetBattleStatus")
            None
    
    member _.GetActiveUserChamps(discordId: uint64, roundId: uint64) =
        match getUserIdByDiscordId discordId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetActiveUserChamps conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query (fun r ->
                   uint64 <| r.GetInt64(0),
                   r.GetString(1),
                   ""
                )
                |> Ok
            with exn ->
                Log.Error(exn, $"GetActiveUserChamps {discordId}")
                Error("Unexpected error")
        | None -> Error("Unable to find user")
    
    member x.GetAvailableUserChamps(userId:uint64): Result<(uint64 * string * string) list, string> =
        match x.GetLastRoundId() with
        | Some roundId ->
            match x.GetRoundStatus roundId with
            | Some status ->
                if status = RoundStatus.Started then
                    x.GetActiveUserChamps(userId, roundId)
                else
                    Error("Please wait until new round is started")
            | None -> Error("Something went wrong - unable to get round status")
        | None -> Error("Something went wrong - unable to get round. Maybe there is no any?")

    member x.GetLastRoundParticipants(): Result<(uint64 * string * string) list, string> =
        match x.GetLastRoundId() with
        | Some roundId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetChampsFromRound conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query (fun r ->
                   uint64 <| r.GetInt64(0),
                   r.GetString(1),
                   r.GetString(2)
                )
                |> Ok
            with exn ->
                Log.Error(exn, "GetLastRoundParticipants")
                Error("Unexpected error")
        | None -> Error("Something went wrong - unable to get round. Maybe there is no any?")

    member _.GetCurrentBattleInfo() : Result<CurrentBattleInfo, string> =
        try
            let options = JsonFSharpOptions().ToJsonSerializerOptions()
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetLastBattleInfo conn
            |> Db.querySingle (fun r ->
                CurrentBattleInfo(
                    uint64 <| r.GetInt64(0),
                    r.GetInt32(1) |> enum<BattleStatus>,
                    {
                        Name = r.GetString(3)
                        Description = r.GetString(4)
                        Picture = System.Text.Json.JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData r 5, options)
                        XP = uint64 <| r.GetInt32(6)
                        Stat = {

                            Health = r.GetInt64(7)
                            Magic = r.GetInt64(8)

                            Accuracy = r.GetInt64(9)
                            Luck = r.GetInt64(10)

                            Attack = r.GetInt64(11)
                            MagicAttack = r.GetInt64(12)

                            Defense = r.GetInt64(13)
                            MagicDefense = r.GetInt64(14)
                        }
                        MType = enum<MonsterType> <| r.GetInt32(15)
                        MSubType = enum<MonsterSubType> <| r.GetInt32(16)
                    },
                    r.GetInt64(2) |> uint64
                )
            )
            |> function
                | Some v -> Ok v
                | None -> Error("Unknown error")
        with exn ->
            Log.Error(exn, "GetCurrentBattleInfo")
            Error("Unexpected error")

    member _.GetBattleHistory(battleId:uint64, monster:string) : Result<(uint64 * (string * PerformedMove * string) list) list, string> =
        try
            let options = JsonFSharpOptions().ToJsonSerializerOptions()
            use conn = new SqliteConnection(cs)
            let chActions =
                Db.newCommand SQL.GetBattleChampActions conn
                |> Db.setParams [
                    "battleId", SqlType.Int64 <| int64 battleId
                ]
                |> Db.query (fun r ->
                   PM.Champ(
                    uint64 <| r.GetInt64(0),
                    r.GetString(3),
                    System.Text.Json.JsonSerializer.Deserialize<PerformedMove>(Utils.getBytesData r 2, options),
                    r.GetDateTime(1)
                   )
                )
            
            let mActions =
                Db.newCommand SQL.GetBattleMonsterActions conn
                |> Db.setParams [
                    "battleId", SqlType.Int64 <| int64 battleId
                ]
                |> Db.query (fun r ->
                   PM.Monster(
                    uint64 <| r.GetInt64(0),
                    System.Text.Json.JsonSerializer.Deserialize<PerformedMove>(Utils.getBytesData r 1, options),
                    if r.IsDBNull(2) then None else Some(r.GetString(2))
                   )
                 )
            let res =
                mActions
                |> List.append chActions
                |> List.groupBy (fun pm -> pm.RoundId)
                |> List.map(fun (id, gr) ->
                    let chActs =
                        gr
                        |> List.choose(fun pm ->
                            match pm with
                            | PM.Champ(_,name, pm, dt) -> (dt, pm, name) |> Some
                            | PM.Monster _ -> None)
                        |> List.sortBy(fun (dt, _, _) -> dt)
                        |> List.map(fun (_, pm, name)  -> name, pm, monster)
                    let mActs =
                        gr
                        |> List.choose(fun pm ->
                            match pm with
                            | PM.Champ _ -> None
                            | PM.Monster(_, pm, target) -> Some(monster, pm, target |> Option.defaultValue ""))
                    let mActionsFirst = mActs |> List.exists(fun (_, pm, _) -> pm.Dmg.IsNone)
                    id, 
                        if mActionsFirst then
                            chActs |> List.append mActs
                        else
                            mActs |> List.append chActs)
                |> List.sortByDescending fst
            Ok res
        with exn ->
            Log.Error(exn, "GetBattleHistory")
            Error("Unexpected error")

    member _.IsRegistered (userId:uint64) =
        isRegistered userId

    member t.PerformAction(raction:RoundActionRecord) =
        try
            use conn = new SqliteConnection(cs)
            let lastRoundId =
                Db.newCommand SQL.GetLastActiveRound conn
                |> Db.scalar (fun v -> tryUnbox<int64> v)
            match lastRoundId with
            | Some roundId ->
                let itemIdO =
                    Db.newCommand SQL.GetEffectItemIdByItem conn
                    |> Db.setParams [
                        "item", SqlType.Int <| int Effect.Death
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                match itemIdO with
                | Some itemId ->
                    let isUnderEffect =
                        Db.newCommand SQL.ChampsIsUnderEffectInRound conn
                        |> Db.setParams [
                            "champId", SqlType.Int64 <| int64 raction.ChampId
                            "roundId", SqlType.Int64 <| roundId
                            "itemId", SqlType.Int64 <| itemId
                        ]
                        |> Db.scalar (fun v -> tryUnbox<int64> v)
                        |> Option.map(fun v -> v > 0)
                    match isUnderEffect with
                    | Some b ->
                        if b then Error("This champ is dead.")
                        else
                            // ToDo: check that champ didn't already participated at this round
                            Db.newCommand SQL.AddAction conn
                            |> Db.setParams [
                                "roundId", SqlType.Int64 <| roundId
                                "champId", SqlType.Int64 <| int64 raction.ChampId
                                "move", SqlType.Int <| int raction.Move
                            ]
                            |> Db.exec
                            Ok(())
                    | None ->
                        Error("Unexpected error. Can't check champ's status")
                           
                | None -> Error("Unexpected error. Can't find effect")
            | None -> Error("Round not found")
        with exn ->
            Log.Error(exn, $"Perform action: {raction}")
            Error("Unexpected error")

    member _.GetChampNameIPFSById(id:uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetChampNameIPFSById conn
            |> Db.setParams [
                "id", SqlType.Int64 <| int64 id
            ]
            |> Db.querySingle (fun r -> r.GetString(0), r.GetString(1))
        with exn ->
            Log.Error(exn, "GetChampNameIPFSById")
            None


    member _.GetStats() =
        try
            use conn = new SqliteConnection(cs)

            let players =
                Db.newCommand SQL.GetTotalUserCount conn
                |> Db.scalar (tryUnbox<int64> >> Option.map uint64)
            
            let confirmedPlayers =
                Db.newCommand SQL.GetConfirmedPlayersCount conn
                |> Db.scalar (tryUnbox<int64> >> Option.map uint64)

            let champs =
                Db.newCommand SQL.GetChampsCount conn
                |> Db.scalar (tryUnbox<int64> >> Option.map uint64)

            let cMonsters =
                Db.newCommand SQL.GetCustomMonstersCount conn
                |> Db.scalar (tryUnbox<int64> >> Option.map uint64)

            let battles =
                Db.newCommand SQL.GetBattlesCount conn
                |> Db.scalar (tryUnbox<int64> >> Option.map uint64)

            let rounds =
                Db.newCommand SQL.GetRoundsCount conn
                |> Db.scalar (tryUnbox<int64> >> Option.map uint64)

            let rewards =
                Db.newCommand SQL.GetSpecialWithdrawalSum conn
                |> Db.query(fun r ->
                    r.GetInt32(0) |> enum<WalletType>,
                    r.GetDecimal(1))
                |> Map.ofSeq

            let playersEarned =
                Db.newCommand SQL.PlayersEarned conn
                |> Db.querySingle (fun r -> r.GetDecimal(0))


            Stats(
                players, confirmedPlayers, champs, cMonsters,
                battles, rounds, playersEarned,
                rewards.TryFind WalletType.Burn, rewards.TryFind WalletType.DAO,
                rewards.TryFind WalletType.Reserve, rewards.TryFind WalletType.Dev,
                rewards.TryFind WalletType.Staking
            ) |> Some
        with exn ->
            Log.Error(exn, "GetStats")
            None