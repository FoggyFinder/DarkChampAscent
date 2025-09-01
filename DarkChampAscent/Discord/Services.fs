﻿namespace DiscordBot.Services

open System
open GameLogic.Champs
open Db
open NetCord.Rest
open Microsoft.Extensions.Hosting
open NetCord.Gateway
open DiscordBot
open System.Threading.Tasks
open Serilog
open Microsoft.Extensions.Options
open Blockchain
open GameLogic.Monsters
open Display
open NetCord
open System.IO
open GameLogic.Battle

type BackupService(db:SqliteStorage, options: IOptions<Conf.Configuration>) =
    inherit BackgroundService()

    override this.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    let now = DateTime.UtcNow
                    let backupIsRequired =
                        match db.GetDateTimeKey(Db.DbKeys.LastTimeBackupPerformed) with
                        | Some dt ->
                            let th = (now - dt).TotalHours
                            // ToDo: move to conf
                            th >= 24.
                        | None -> true
                    if backupIsRequired then
                        let datasource =
                            let dir = options.Value.Db.BackupFolder
                            if Directory.Exists(dir) |> not then
                                Directory.CreateDirectory(dir) |> ignore
                            let dbname = now.ToString("yyyyMMddhhmm") + ".sqlite"
                            Path.Combine(dir, dbname)
                        
                        match db.Backup(datasource) with
                        | Ok () ->
                            db.SetDateTimeKey(Db.DbKeys.LastTimeBackupPerformed, now) |> ignore
                        | Error err ->
                            Log.Error(err)
                    do! Task.Delay(TimeSpan.FromHours(Random.Shared.Next(4, 12)), cancellationToken)
                with exn ->
                    Log.Error(exn, "BackupService")
        }

type ConfirmationService(db:SqliteStorage, client: GatewayClient, options: IOptions<Conf.Configuration>) =
    inherit BackgroundService()

    override this.ExecuteAsync(cancellationToken) =
        task {
            while cancellationToken.IsCancellationRequested |> not do
                do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
                try
                    let dt = DateTime.UtcNow
                    let lpd = db.GetDateTimeKey(DbKeys.LastTimeConfirmationCodeChecked)
                    let confirmations =
                        Blockchain.getNotesForWallet(options.Value.Wallet.GameWallet, lpd)
                        |> Seq.choose(fun (wallet, barr) ->
                            try
                                Some(wallet, System.Text.Encoding.UTF8.GetString barr)
                            with _ -> None)
                        |> Seq.toArray
                    let! isOk =
                        match confirmations.Length with
                        | 0 -> task { return true }
                        | _ ->
                            task {
                                let mutable noErrors = true
                                for (wallet, code) in confirmations do
                                    if db.WalletIsConfirmed(wallet) then ()
                                    elif db.ConfirmWallet(wallet, code) then
                                        match db.FindDiscordIdByWallet wallet with
                                        | Some discordId ->
                                            for guild in client.Cache.Guilds do
                                                match guild.Value.Users.TryGetValue(uint64 discordId) with
                                                | true, duser ->
                                                    let rO =
                                                        guild.Value.Roles
                                                        |> Seq.tryFind(fun r -> r.Value.Name = Channels.DarkAscentPlayerRole)
                                                    match rO with
                                                    | Some role ->
                                                        try
                                                            if duser.RoleIds |> Seq.contains role.Key |> not then
                                                                let! v = guild.Value.AddUserRoleAsync(uint64 discordId, role.Key)
                                                                Log.Information("Role added to a user")
                                                        with exn ->
                                                            Log.Error(exn, $"Unable to add role to user inside {guild.Value.Name} guild")
                                                    | None ->
                                                        Log.Error($"Unable to find a role to user inside {guild.Value.Name} guild")
                                                | false, _ -> ()
                                        | None -> ()
                                        match db.FindUserIdByWallet wallet with
                                        | Some userId ->
                                            Blockchain.getChampsForWallet wallet
                                            |> Seq.iter(fun assetId ->
                                                let r = db.ChampExists assetId
                                                let isOk =
                                                    match r with
                                                    | Ok b ->
                                                        if b then
                                                            db.UpdateUserForChamp(uint64 userId, assetId) |> Result.isOk
                                                        else
                                                            Blockchain.tryGetChampInfo assetId
                                                            |> Option.map(fun (trait', ipfs) ->
                                                                db.AddOrInsertChamp ({
                                                                    Name = Blockchain.getAssetName assetId
                                                                    AssetId = assetId
                                                                    IPFS = ipfs
                                                                    UserId = uint64 userId
                                                                    Stats = Champ.generateStats trait'
                                                                    Traits = trait'
                                                                }))
                                                            |> Option.defaultValue false
                                                    | Error _ -> false
                                                noErrors <- noErrors && isOk)
                                        | None -> ()
                                    else ()
                                return noErrors
                            
                        }
                    if isOk then
                        db.SetDateTimeKey(DbKeys.LastTimeConfirmationCodeChecked, dt) |> ignore
                    do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
                with exn ->
                    Log.Error(exn, "confirmationTracker2")
        }

type UpdatePriceService(db:SqliteStorage, gclient:GatewayClient) =
    inherit BackgroundService()

    override this.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(0.5), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    let now = DateTime.UtcNow
                    let isPriceUpToDate =
                        match db.GetDateTimeKey(Db.DbKeys.LastTimePriceIsUpdated) with
                        | Some dt ->
                            let th = (now - dt).TotalHours
                            // ToDo: move to conf
                            th < 12.
                        | None -> false
                    if isPriceUpToDate |> not then
                        match External.API.getDarkCoinPrice() with
                        | Some price ->
                            if db.SetNumKey(Db.DbKeysNum.DarkCoinPrice, price) then
                                db.SetDateTimeKey(Db.DbKeys.LastTimePriceIsUpdated, now) |> ignore
                                let mp = MessageProperties(Content = $"In-game DarkCoin price was updated to {price}")
                                do! Utils.sendMsgToLogChannel gclient mp
                        | None ->
                            Log.Error("UpdatePriceService: Vestige didn't return price")
                    do! Task.Delay(TimeSpan.FromHours(Random.Shared.Next(4, 12)), cancellationToken)
                with exn ->
                    Log.Error(exn, "UpdatePriceService")
        }

type DepositService(db:SqliteStorage, gclient:GatewayClient, options: IOptions<Conf.Configuration>) =
    inherit BackgroundService()

    override this.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(0.5), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    let dt = DateTime.UtcNow
                    let lpd = db.GetDateTimeKey(DbKeys.LastProcessedDeposit)
                    let deposits = Blockchain.getDarkCoinDepositForWallet(options.Value.Wallet.GameWallet, lpd) |> Seq.toArray
                    let statuses =
                        deposits
                        |> Array.map(fun (txid, sender, value) -> {|
                                        Info = (txid, sender, value)
                                        Status = db.ProcessDeposit(sender, txid, value)
                                    |})
                    if statuses |> Array.forall(fun s -> s.Status <> ProcessDepositStatus.Error) then
                        db.SetDateTimeKey(DbKeys.LastProcessedDeposit, dt) |> ignore
                    
                    for ar in statuses do
                        match ar.Status with
                        | ProcessDepositStatus.Donation ->
                            let (tx, sender, value) = ar.Info
                            let uri = $"https://allo.info/tx/{tx}"
                            let donationCard =
                                ComponentContainerProperties([
                                    TextDisplayProperties($"{Emoj.Rocket} **[New Donation!]({uri})** {Emoj.Rocket}")
                                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                    TextDisplayProperties($" {value} {Emoj.Coin} added to reward pool ")
                                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                    TextDisplayProperties($"Thank you, {sender}")
                                ])
                            
                            let newDonationMessage =
                                MessageProperties()
                                    .WithComponents([ donationCard ])
                                    .WithFlags(MessageFlags.IsComponentsV2)
                            
                            do! Utils.sendMsgToLogChannel gclient newDonationMessage
                        | _ -> ()                  
                with exn ->
                    Log.Error(exn, "DepositService")

                do! Task.Delay(TimeSpan.FromMinutes(5.0), cancellationToken)
        }

type TrackChampCfgService(db:SqliteStorage) =
    inherit BackgroundService()

    override this.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(0.5), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    let dt = DateTime.UtcNow
                    let champs = db.GetChampsCount()
                    match champs with
                    | Some v when v > 0 ->
                        let ltcc = db.GetDateTimeKey(DbKeys.LastTrackedChampCfg)
                        let isOk = 
                            Blockchain.getDCChampAcfgTransactions(ltcc)
                            |> Seq.map(fun (assetId, ipfs, t) ->
                                match db.ChampExists assetId with
                                | Ok b ->
                                    if b then db.UpdateCfgForChamp(assetId, ipfs, t)
                                    else Ok(())
                                | Error err -> Error err)
                            |> Seq.forall Result.isOk
                        if isOk then
                           db.SetDateTimeKey(DbKeys.LastTrackedChampCfg, dt) |> ignore
                        do! Task.Delay(TimeSpan.FromHours(1), cancellationToken)
                    | Some v ->
                        // no champs registered, no point to spam with requests
                        db.SetDateTimeKey(DbKeys.LastTrackedChampCfg, dt) |> ignore
                        do! Task.Delay(TimeSpan.FromHours(6), cancellationToken)
                    | None -> ()
                with exn ->
                    Log.Error(exn, "TrackedChampCfg")
        }

type BattleService(db:SqliteStorage, gclient:GatewayClient, options: IOptions<Conf.Configuration>) =
    inherit BackgroundService()

    let wallets = options.Value.Wallet
    let keys = options.Value.Chain.GameWalletKeys
    let toLong (d:decimal) = uint64 (d * Algo6Decimals)

    let balanceCorrectness() =
        // sum of burn / dev / rewards / reserve / user balances / champs balances
        // must be <= wallet holdings
        let b = db.GetBalances()
        let balance = Blockchain.getDarkCoinBalance(options.Value.Wallet.GameWallet)
        match b, balance with
        | Ok bal, Some walletBalance ->
            if bal.Total <= walletBalance then
                db.SetBoolKey(Db.DbKeysBool.BalanceCheckIsPassed, true) |> ignore
                Log.Information($"Balance check is passed: {bal.Total} <= {walletBalance}")
                Ok(())
            else
                let err = $"Balance doesn't match {bal.Total} >= {walletBalance}"
                Log.Error(err)
                // if diff isn't significant then log error and allow game to continue
                if bal.Total <= walletBalance + 1000M then
                    Ok(())
                else Error(err)
        | _, _ ->
            db.SetBoolKey(Db.DbKeysBool.BalanceCheckIsPassed, false) |> ignore
            let err = $"Unable to read balance: db = {b}; balance = {balance}"
            Log.Error(err)
            Error(err)

    let finalizeBattle(battleId:uint64) = task {
        let send (wallet:string) (d:decimal) =
            let uv = toLong d
            async {
                let! r =
                    Blockchain.sendTx(
                        keys, wallet, uv,
                        Blockchain.DarkCoinAssetId,
                        $"DarkChampAscent: {battleId} battle"
                    ) |> Async.AwaitTask
                do! Async.Sleep(TimeSpan.FromSeconds(5L))
                match r with
                | Ok (tx, _) -> return Some(tx)
                | Error exn ->
                    Log.Error(exn, $"unable to send to {wallet}")
                    return None
            } |> Async.RunSynchronously
        match db.GetChampWithBalances() with
        | Ok champs ->
            for ar in champs do
                match Blockchain.getAssetHolder ar.AssetId with
                | Ok wallet ->
                    match db.SendToWallet(ar.ID, ar.Balance, battleId, send wallet) with
                    | Some tx ->
                        let tnComponent = Components.tnSend $"{ar.Balance} {Emoj.Coin} was send to {ar.Name} ({ar.AssetId})" tx
                        let tnMessage =
                            MessageProperties()
                                .WithComponents([ tnComponent ])
                                .WithFlags(MessageFlags.IsComponentsV2)
                                                
                        do! Utils.sendMsgToLogChannel gclient tnMessage
                    | None -> () 
                | Error err -> Log.Error(err)
        | Error _ -> ()
            
        for wt in Enum.GetValues<WalletType>() do
            let wallet =
                match wt with
                | WalletType.DAO -> wallets.DAOWallet
                | WalletType.Dev -> wallets.DevsWallet
                | WalletType.Reserve -> wallets.ReserveWallet
                | WalletType.Burn -> wallets.BurnWallet
            match db.SendToSpecialWallet(wt, battleId, send wallet) with
            | Some (tx, v) ->
                let tnComponent = Components.tnSend $"{v} {Emoj.Coin} was send to {wt}" tx
                let tnMessage =
                    MessageProperties()
                        .WithComponents([ tnComponent ])
                        .WithFlags(MessageFlags.IsComponentsV2)
                                                
                do! Utils.sendMsgToLogChannel gclient tnMessage
            | None -> ()
        
        // change status to finalized
        if db.FinalizeBattle battleId then
            let battleStartMessage = MessageProperties(Content = $"Battle {battleId} is completed.")
            do! Utils.sendMsgToLogChannel gclient battleStartMessage
    }

    let startBattle() = task {
        match balanceCorrectness() with
        | Ok v ->
            // start new round
            let monsterIdR = db.GetRandomMonsterId()
            match monsterIdR with
            | Ok monsterId ->
                let sbr = db.StartBattle(monsterId)
                match sbr with
                | Ok ar ->
                    match db.GetMonsterById(monsterId) with
                    | Some monster ->
                        let createMP() =
                            let battleCard =
                                ComponentContainerProperties([
                                    TextDisplayProperties($"Battle {ar.BattleId} has started!")
                                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                    TextDisplayProperties($" {Emoj.Coin} Rewards: {ar.BattleRewards} {Emoj.Coin}")
                                ])
                
                            let monsterCard = Components.monsterComponent monster
                        
                            MessageProperties()
                                .WithAttachments([Components.monsterAttachnment monster])
                                .WithComponents([
                                    battleCard
                                    monsterCard
                                ])
                                .WithFlags(MessageFlags.IsComponentsV2)
                                                
                        do! Utils.createAndSendMsgToChannel Channels.BattleChannel gclient createMP true

                        let roundCard =
                            ComponentContainerProperties([
                                TextDisplayProperties($"Round {ar.RoundId} has started!")
                                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                TextDisplayProperties($" {Emoj.Coin} Rewards: {ar.RoundRewards} {Emoj.Coin}")
                            ])

                        let roundStartMessage = MessageProperties().WithComponents([ roundCard ]).WithFlags(MessageFlags.IsComponentsV2)
                        do! Utils.sendMsgToLogChannel gclient roundStartMessage
                    | _ -> ()
                | Error err ->
                    Log.Error($"StartBattleError: {err}")
                    do! Task.Delay(TimeSpan.FromMinutes(5.0))
            | Error _ -> ()   
        | Error err ->
            let mp = MessageProperties(Content = $"Unable to start new battle - {err}")
            do! Utils.sendMsgToLogChannel gclient mp
            do! Task.Delay(TimeSpan.FromHours(12))
    }

    let startRound() = task {
        match balanceCorrectness() with
        | Ok v ->
            // start new round
            let roundIdO = db.NextRound()
            match roundIdO with
            | Ok roundId ->
                let mp = MessageProperties(Content = $"Round {roundId} has started!")
                do! Utils.sendMsgToBattleChannel gclient mp
                do! Utils.sendMsgToLogChannel gclient mp
            | Error err ->
                match err with
                | StartRoundError.MaxRoundsInBattle ->
                    let mp = MessageProperties(Content = $"Battle is finished - max amount of rounds ({Constants.RoundsInBattle}) is reached!")
                    do! Utils.sendMsgToBattleChannelSilently gclient mp  
                | _ ->
                    Log.Error($"StartRoundError: {err}")
                    do! Task.Delay(TimeSpan.FromMinutes(5.0))
        | Error err ->
            let mp = MessageProperties(Content = $"Unable to start new battle - {err}")
            do! Utils.sendMsgToLogChannel gclient mp
            do! Task.Delay(TimeSpan.FromHours(1))
    }

    let finishRound(roundId:uint64) = task {
        // start new round
        let roundIdO = db.FinishRound()
        match roundIdO with
        | Ok _ ->
            let mp = MessageProperties(Content = $"Round {roundId} is closed. Processing...")
            do! Utils.sendMsgToLogChannel gclient mp
        | Error _ -> ()    
    }

    let finalizeRound(roundId:uint64, battleId:uint64) = task {
        let rewardsR = db.GetRewardsForFinishedRound roundId
        let champsR = db.GetUserChampsWithStatsForFinishedRound roundId
        let actionsR = db.GetActionsForFinishedRound roundId
        let boostsR = db.GetBoostsForFinishedRound roundId
        let lvlsR = db.GetLvlsForFinishedRound roundId
        let monsterR = db.GetMonsterForFinishedRound roundId
        match rewardsR, champsR, actionsR, boostsR, lvlsR, monsterR with
        | Ok rewards, Ok champs, Ok actions, Ok boosts, Ok lvls, Ok monster ->
            let levels =
                lvls
                |> Seq.map(fun kv ->
                    kv.Key,
                    kv.Value |> List.countBy(fun l -> l.Characteristic) |> dict |> Levels.statFromCharacteristics)
                |> Map.ofSeq
            let roundMoves =
                actions
                |> List.map(fun ar ->
                    let xp, stat, name = champs[ar.ChampId]
                    {
                        Move = ar.Move
                        Timestamp = ar.Timestamp
                        ChampId = ar.ChampId
                        ChampName = name
                        Stat = stat
                        ChampLvl = Levels.getLvlByXp xp
                    })

            let champNames = champs |> Map.map(fun k (_,_,name) -> name)

            let monsterChar = MonsterChar(monster.MonsterId, monster.MonsterRecord.Monster, monster.MonsterRecord.Stats, monster.MonsterRecord.Xp, monster.MonsterRecord.Name)

            match Battle.fight(roundId, battleId, roundMoves, boosts, levels, monsterChar, rewards) with
            | Ok bres ->
                let revivalTime = Monster.getRevivalDuration monsterChar.Monster
                match db.FinalizeRound bres revivalTime boosts with
                | Ok _ ->
                    for msg in Components.battleResults bres champNames do
                        let mp = MessageProperties().WithComponents([ msg ]).WithFlags(MessageFlags.IsComponentsV2)
                        do! Utils.sendMsgToBattleChannelSilently gclient mp

                    let mp = MessageProperties(Content = $"Round {roundId} is completed.")
                    do! Utils.sendMsgToLogChannel gclient mp
                | Error _ -> ()
            | Error err ->
                Log.Error($"Error during fight: {err}")
            
        | _ ->
            printfn "Errors: champs = %A; actions = %A; boosts = %A; lvls = %A; monster = %A"
                champsR.IsOk actionsR.IsOk boostsR.IsOk lvlsR.IsOk monsterR.IsOk
    }

    override this.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(0.5), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    let roundExists = db.RoundsExists()
                    match roundExists with
                    | Some b ->
                        if b then
                            match db.GetLastRoundId() with
                            | Some roundId ->
                                match db.GetRoundInfo roundId with
                                | Some (battleId, timestamp, status) ->
                                    match status with
                                    | RoundStatus.Started ->
                                        let dt = DateTime.UtcNow
                                        let duration = dt - timestamp
                                        if duration < Battle.RoundDuration then
                                            Log.Information($"Delay for {(Battle.RoundDuration - duration).TotalMinutes} minutes")
                                            do! Task.Delay(Battle.RoundDuration - duration)
                                            do! Task.Delay(TimeSpan.FromMinutes(0.5))
                                        
                                        while db.AnyChampJoinedRound roundId |> Option.defaultValue false |> not do
                                            Log.Information("waiting for players...")
                                            do! Task.Delay(TimeSpan.FromMinutes(1.0))

                                        do! finishRound(roundId)
                                    | RoundStatus.Processing ->
                                        do! finalizeRound(roundId, battleId)
                                    | RoundStatus.Finished ->
                                        // get battle status
                                        let battleStatusO = db.GetBattleStatus battleId
                                        match battleStatusO with
                                        | Some battleStatus ->
                                            match battleStatus with
                                            | BattleStatus.Started ->
                                                do! startRound()
                                            | BattleStatus.Processing ->
                                                do! finalizeBattle(battleId)
                                            | BattleStatus.Finished ->
                                                do! startBattle()
                                        | None -> ()
                                | None -> ()
                            | None -> ()
                        else
                            do! startBattle()
                    | None -> ()
                    do! Task.Delay(TimeSpan.FromMinutes(0.5), cancellationToken)
                    
                with exn ->
                    Log.Error(exn, "BattleService")
        }