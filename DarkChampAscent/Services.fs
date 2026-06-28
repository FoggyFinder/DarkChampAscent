module Services

open Db
open Microsoft.Extensions.Hosting
open System.Threading.Tasks
open System
open DTO
open Serilog
open GameLogic.Battle

open GameLogic.Champs
open NetCord.Rest
open NetCord.Gateway
open Microsoft.Extensions.Options
open Blockchain
open GameLogic.Monsters
open Display
open NetCord
open System.IO
open DiscordBot.Components
open Utils
open Helpers
open FSharp.Control

type BackupService(db:SqliteStorage, backupOpt: IOptions<Conf.BackupConfiguration>) =
    inherit BackgroundService()

    override _.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    let now = DateTime.UtcNow
                    let backupIsRequired =
                        match db.GetDateTimeKey(Db.DbKeys.LastTimeBackupPerformed) with
                        | Some dt ->
                            let th = (now - dt).TotalHours
                            let cv = float backupOpt.Value.PeriodHrs
                            th >= cv
                        | None -> true
                    if backupIsRequired then
                        let datasource =
                            let dir = backupOpt.Value.DBFolder
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
                    do! Task.Delay(TimeSpan.FromHours(12), cancellationToken)
                    Log.Error(exn, "BackupService")
        }

type DiscordRoleCheckService(client: GatewayClient, db:SqliteStorage) =
    inherit BackgroundService()
    
    override _.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    // TODO: remove a role if user is no longer hold a Champ NFT
                    for kv in client.Cache.Guilds do
                        let guild = kv.Value
                        let playerRoleO = guild.Roles |> Seq.tryFind(fun r -> r.Value.Name = Channels.DarkAscentPlayerRole)
                        match playerRoleO with
                        | Some pRole ->
                            do! 
                                guild.GetUsersAsync()
                                |> TaskSeq.iterAsync(fun u ->
                                    match db.ConfirmedUserByDiscordId u.Id with
                                    | Ok b ->
                                        if b then
                                            task {
                                                if u.RoleIds |> Seq.exists(fun r -> r = pRole.Key) |> not then
                                                    try
                                                        do! guild.AddUserRoleAsync(u.Id, pRole.Key)
                                                        Log.Information($"Role added to a {u.Id} user")
                                                    with exn ->
                                                        Log.Error(exn, $"Unable to add role to user inside {guild.Name} guild")
                                            }
                                        else task { () }
                                    | Error err ->
                                        task { Log.Error(err) })
                        | _ ->
                            Log.Error($"Unable to find a role to user inside {guild.Name} guild")

                    do! Task.Delay(TimeSpan.FromHours(12), cancellationToken)
                with exn ->
                    Log.Error(exn, "DiscordRoleCheckService")
                    do! Task.Delay(TimeSpan.FromHours(24), cancellationToken)
        }

type ConfirmationService(db:SqliteStorage, client: GatewayClient, options: IOptions<Conf.WalletConfiguration>) =
    inherit BackgroundService()

    override _.ExecuteAsync(cancellationToken) =
        task {
            while cancellationToken.IsCancellationRequested |> not do
                do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
                try
                    let unconfirmedWalletExists = db.UnConfirmedWalletExists()
                    if unconfirmedWalletExists then
                        let dt = DateTime.UtcNow
                        let lpd = db.GetDateTimeKey(DbKeys.LastTimeConfirmationCodeChecked)
                        let confirmations =
                            Blockchain.getNotesForWallet(options.Value.GameWallet, lpd)
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
                                                do! DUtils.addDiscordRole client (uint64 discordId)
                                            | None -> ()
                                            match db.FindUserIdByWallet wallet with
                                            | Some userId ->
                                                noErrors <- noErrors && CommonHelpers.updateChampsForAUser (db, uint64 userId, [wallet]) |> Result.isOk
                                            | None -> ()
                                        else ()
                                    return noErrors
                            }
                        if isOk then
                            db.SetDateTimeKey(DbKeys.LastTimeConfirmationCodeChecked, dt) |> ignore
                        do! Task.Delay(TimeSpan.FromMinutes(3.0), cancellationToken)
                    else
                        do! Task.Delay(TimeSpan.FromMinutes(5.0), cancellationToken)
                with exn ->
                    do! Task.Delay(TimeSpan.FromHours(1), cancellationToken)
                    Log.Error(exn, "confirmationTracker2")
        }

type RescanChampsService(db:SqliteStorage) =
    inherit BackgroundService()

    override _.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(7.0), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    let userAndWallets = db.GetAllConfirmedWallets()
                    for (userId, wallets) in userAndWallets |> List.groupBy(fun (uId, _) -> uId) do
                        let wallets' = wallets |> List.map snd
                        try 
                            let r = CommonHelpers.updateChampsForAUser(db, userId, wallets')
                            match r with
                            | Ok () -> ()
                            | Error err -> Log.Error(err)
                        with err ->
                            Log.Error(err, $"rescan for {userId} failed")
                        do! Task.Delay(TimeSpan.FromMinutes(3.0), cancellationToken)
                with exn ->
                    Log.Error(exn, "rescanChampsService")
                do! Task.Delay(TimeSpan.FromHours(8.0), cancellationToken)
        }

type UpdatePriceService(db:SqliteStorage, gclient:GatewayClient) =
    inherit BackgroundService()

    override _.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(0.5), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    let now = DateTime.UtcNow
                    let isPriceUpToDate =
                        match db.GetDateTimeKey(Db.DbKeys.LastTimePriceIsUpdated) with
                        | Some dt ->
                            let th = (now - dt).TotalHours
                            // TODO: move to conf
                            th < 3.
                        | None -> false
                    if isPriceUpToDate |> not then
                        let! priceO = External.API.getDarkCoinPrice()
                        match priceO with
                        | Some price ->
                            if db.UpdateDCPrice price then
                                let mp = MessageProperties(Content = $"In-game DarkCoin price was updated to {price}")
                                do! DUtils.sendMsgToLogChannel gclient mp
                        | None ->
                            Log.Error("UpdatePriceService: Vestige didn't return price")
                    do! Task.Delay(TimeSpan.FromHours(Random.Shared.Next(1, 3)), cancellationToken)
                with exn ->
                    do! Task.Delay(TimeSpan.FromHours(1), cancellationToken)
                    Log.Error(exn, "UpdatePriceService")
        }

type TxTrackerService(db:SqliteStorage, options: IOptions<Conf.WalletConfiguration>) =
    inherit BackgroundService()

    override _.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(0.5), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    let dt = DateTime.UtcNow
                    let lpd = db.GetDateTimeKey(DbKeys.LastTimeTxsAreScanned)
                    let txs = Blockchain.getDarkCoinTxForWallet(options.Value.GameWallet, lpd) |> Seq.toArray
                    // delay before processing so give time to handle tx properly if it was created from the app
                    do! Task.Delay(TimeSpan.FromMinutes(30.0), cancellationToken)
                    
                    let statuses = txs |> Array.map(fun ptx -> db.ProcessParsedRawTx ptx)
                    if statuses |> Array.forall Result.isOk then
                        db.SetDateTimeKey(DbKeys.LastTimeTxsAreScanned, dt) |> ignore
                    
                with exn ->
                    Log.Error(exn, "TxTrackerService")
                    do! Task.Delay(TimeSpan.FromMinutes(30.0), cancellationToken)
        }

type TrackChampCfgService(db:SqliteStorage) =
    inherit BackgroundService()

    override _.ExecuteAsync(cancellationToken) =
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
                    | Some _ ->
                        // no champs registered, no point to spam with requests
                        db.SetDateTimeKey(DbKeys.LastTrackedChampCfg, dt) |> ignore
                        do! Task.Delay(TimeSpan.FromHours(6), cancellationToken)
                    | None ->
                        do! Task.Delay(TimeSpan.FromHours(12), cancellationToken)
                with exn ->
                    do! Task.Delay(TimeSpan.FromHours(12), cancellationToken)
                    Log.Error(exn, "TrackedChampCfg")
        }

open Types
open DarkChampAscent.Account

type BattleService(db:SqliteStorage, gclient:GatewayClient, options: IOptions<Conf.Configuration>) =
    inherit BackgroundService()
    let toUnixSeconds (dt: DateTime) =
        DateTimeOffset(dt).ToUnixTimeSeconds()
    let wallets = options.Value.Wallet
    let keys = options.Value.Chain.GameWalletKeys

    let roundParticipants = Signal<RoundParticipantChamp list>([])
    let roundStatus = Signal<RoundInfoDTO>(RoundInfoDTO(RoundStatus.Processing, None, 0UL))
    let battleStatus = Signal<BattleInfoDTO option>(None)
    
    let updateBattleStatus() = 
        task {
            match db.GetCurrentBattleInfo() with
            | Ok cfbi ->
                let! createdBy =
                    match cfbi.CurrentBattleInfo.Monster.OwnerId with
                    | Some ownerId -> Cache.tryGetUserLinkByRawUserId db gclient.Rest (int64 ownerId)                     
                    | None -> task { return None }
                let createdByStr =
                    match createdBy with
                    | Some uLink -> $"Created by [{uLink.Nickname}]({Links.userProfile uLink.UserRawId})" |> Some
                    | None -> None
                let cbi = cfbi.CurrentBattleInfo
                let cri = cfbi.CurrentRoundInfo
                for guild in gclient.Cache.Guilds do
                    match guild.Value.Channels |> Seq.tryFind(fun c -> c.Value.Name = Channels.EntryChannel) with
                    | Some channel ->
                        // TODO: cache message for each guild and use it instead of requesting pinned message every time
                        let! pinnedMsgs = gclient.Rest.GetPinnedMessagesAsync(channel.Key)
                        match pinnedMsgs.Count with
                        | 0 -> Log.Error($"No pinned messages in {Channels.EntryChannel} channel")
                        | 1 ->
                            let! _ = pinnedMsgs.[0].ModifyAsync(fun options ->
                                let name = "image.png"
                                let uri = 
                                    match cbi.Monster.Picture with
                                    | MonsterImg.File _ -> $"attachment://{name}"
                                    | MonsterImg.Ipfs ipfs -> "https://mainnet.api.perawallet.app/v1/ipfs-thumbnails/" + ipfs
                                let components =
                                    ComponentContainerProperties([
                                        TextDisplayProperties($"{Emoj.Battle} Battle # {cbi.BattleNum}")
                                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                        TextDisplayProperties($"{Emoj.Rounds} Round # {cri.Rounds} / {Constants.RoundsInBattle}")
                                        TextDisplayProperties($"{Emoj.Trophy} Round Rewards: {cri.Rewards} {Emoj.Coin}")
                                        TextDisplayProperties($"{Emoj.Rounds} Next round: <t:{toUnixSeconds (cri.RoundStarted + BattleParams.RoundDuration())}:R>")
                                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                        yield! MonstersComponent.monsterComponents cbi.Monster "" uri createdByStr
                                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                        TextDisplayProperties($"To send single Champ use `/battle` command or [webApp]({Links.frontendOrigin})")
                                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                        ActionRowProperties([ 
                                            ButtonProperties($"sendgroup", "Send group", ButtonStyle.Primary)
                                            ButtonProperties($"sendall", "Send all", ButtonStyle.Primary)
                                            ButtonProperties($"pendingrewards", "Pending rewards", ButtonStyle.Success)
                                            ButtonProperties($"info", "Info", ButtonStyle.Success)
                                            ButtonProperties($"register", "Register", ButtonStyle.Danger)
                                        ])
                                    ])
     
                                options.Components <- [components]
                                match cbi.Monster.Picture with
                                | MonsterImg.File filepath ->
                                    options.Attachments <- [MonstersComponent.monsterAttachnment name filepath]
                                | MonsterImg.Ipfs _ -> ()
                            )
                            ()
                        | _ -> Log.Error($"{pinnedMsgs.Count} pinned messages in {Channels.EntryChannel} channel")
                    | None ->
                        Log.Error($"Can't find channel in guild {guild}")
                match db.GetBattleHistory(cbi.BattleNum) with
                | Ok bh ->
                    let bidto = 
                        BattleInfoDTO(cfbi, BattleHistory(bh,
                            RoundParticipantMonster(cbi.Monster.Id, cbi.Monster.Name, cbi.Monster.Picture)))
                    battleStatus.Set(Some bidto)
                | _ -> battleStatus.Set None
            | _ -> battleStatus.Set None
        }

    let balanceCorrectness() =
        // sum of burn / dev / rewards / reserve / champs balances
        // must be <= wallet holdings
        let b = db.GetBalances()
        match b with
        | Ok bal when bal.Rewards > 0M ->
            match Blockchain.getDarkCoinBalance(options.Value.Wallet.GameWallet) with
            | Ok walletBalance ->
                if Math.Round(bal.Total, 6) <= walletBalance then
                    Log.Information($"Balance check is passed: {bal.Total} <= {walletBalance}")
                    Ok(())
                else
                    let err = $"Balance doesn't match {bal.Total} >= {walletBalance}"
                    Log.Error(err)
                    // if diff isn't significant then log error and allow game to continue
                    // TODO: reduce
                    if bal.Total <= walletBalance + 5000M then
                        Ok(())
                    else Error(err)
            | Error err ->
                Log.Error(err)
                Error("Unable to read balance from blockchain API")
        | Ok _ ->
            Log.Information("No rewards")
            Ok(())
        | Error err ->
            let err = $"Unable to read balance: {err}"
            Log.Error(err)
            Error("Unable to read balance")

    let finalizeBattle(battleId:uint64) = task {
        let send (wallet:string) (d:decimal) =
            let uv = Blockchain.toLong (d, Algo6Decimals)
            async {
                let! r =
                    Blockchain.sendTx(
                        keys, wallet, uv,
                        Blockchain.DarkCoinAssetId,
                        $"DarkChampAscent: {battleId} battle"
                    ) |> Async.AwaitTask
                do! Async.Sleep(TimeSpan.FromSeconds(10L))
                match r with
                | TxStatus.Confirmed(tx, _) ->
                    return Some(tx, true)
                | TxStatus.Unconfirmed tx ->
                    Log.Error $"Unconfirmed tx: {tx}"
                    return Some(tx, false)
                | TxStatus.Error exn ->
                    Log.Error(exn, $"unable to send to {wallet}")
                    return None
            } |> Async.RunSynchronously
        match db.GetChampWithBalances() with
        | Ok champs ->
            let champsAndWallet =
                champs
                |> List.choose (fun ar -> 
                    match Blockchain.getAssetHolder ar.AssetId with
                    | Ok wallet -> {| Wallet = wallet; ChampId = ar.ID; Balance = ar.Balance |} |> Some
                    | Error err ->
                        Log.Error err
                        None)
                |> List.groupBy (fun ar -> ar.Wallet)
            for (wallet, champs) in champsAndWallet do
                let champs' = champs |> List.map(fun ar -> ar.ChampId, ar.Balance)
                let sum = champs |> List.sumBy(fun ar -> ar.Balance)
                match db.SendToWallet(champs', battleId, fun () -> send wallet sum) with
                | Some tx ->
                    let tnComponent = ChainComponent.tnSend $"{sum} {Emoj.Coin} was sent to {wallet.Substring(0, 5)}..." tx
                    let mp = DUtils.v2ComponentMessage [tnComponent]
                                                
                    do! DUtils.sendMsgToLogChannel gclient mp
                | None -> () 

        | Error _ -> ()

        match db.SendToMonsterOwnerWallet(battleId, send) with
        | Some (tx, wallet, dark) ->
            let tnComponent = ChainComponent.tnSend $"{dark} {Emoj.Coin} was sent to {wallet.Substring(0, 5)}... as monster owner" tx
            let mp = DUtils.v2ComponentMessage [tnComponent]
                                                
            do! DUtils.sendMsgToLogChannel gclient mp
        | _ -> ()
        
        for wt in Enum.GetValues<WalletType>() do
            let wallet =
                match wt with
                | WalletType.DAO -> wallets.DAOWallet
                | WalletType.Dev -> wallets.DevsWallet
                | WalletType.Reserve -> wallets.ReserveWallet
                | WalletType.Burn -> wallets.BurnWallet
                | WalletType.Staking -> wallets.StakingWallet
            match db.SendToSpecialWallet(wt, battleId, send wallet) with
            | Some (tx, v) ->
                let tnComponent = ChainComponent.tnSend $"{v} {Emoj.Coin} was send to {wt}" tx
                let mp = DUtils.v2ComponentMessage [tnComponent]
                                                
                do! DUtils.sendMsgToLogChannel gclient mp
            | None -> ()
        
        // change status to finalized
        if db.FinalizeBattle battleId then
            let battleStartMessage = MessageProperties(Content = $"Battle {battleId} is completed.")
            do! DUtils.sendMsgToLogChannel gclient battleStartMessage
    }

    let startBattle() = task {
        match balanceCorrectness() with
        | Ok () ->
            // start new round
            match db.GetAliveMonsters() with
            | Ok [] ->
                Log.Error($"StartBattleError - no alive monsters")
                do! Task.Delay(TimeSpan.FromHours(1.0))
            | Ok monsters ->
                let uMonsters, _ = monsters |> List.partition (snd >> Option.isSome)
                let monsterId, ownerIdO =
                    if uMonsters.IsEmpty then monsters
                    else uMonsters
                    |> List.randomChoice
                let dt = DateTime.UtcNow
                let sbr = db.StartBattle(monsterId)
                match sbr with
                | Ok ar ->
                    roundStatus.Set(RoundInfoDTO(RoundStatus.Started, Some dt, uint64 ar.RoundId))
                    roundParticipants.Set([])
                    let! createdBy =
                        match ownerIdO with
                        | Some ownerId -> Cache.tryGetUserLinkByRawUserId db gclient.Rest (int64 ownerId)                     
                        | None -> task { return None }
                    let createdByStr =
                        match createdBy with
                        | Some uLink -> $"Created by [{uLink.Nickname}]({Links.userProfile uLink.UserRawId})" |> Some
                        | None -> None
                    do! updateBattleStatus()
                    match db.GetMonsterById(monsterId) with
                    | Some monster ->
                        let createMP() =
                            let battleCard =
                                ComponentContainerProperties([
                                    TextDisplayProperties($"Battle {ar.BattleId} has started!")
                                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                    TextDisplayProperties($" {Emoj.Coin} Rewards: {ar.BattleRewards} {Emoj.Coin}")
                                ])
                            
                            let name = "image.png"
                            let uri = 
                                match monster.Picture with
                                | MonsterImg.File _ -> $"attachment://{name}"
                                | MonsterImg.Ipfs ipfs -> "https://mainnet.api.perawallet.app/v1/ipfs-thumbnails/" + ipfs
                            let monsterCard = MonstersComponent.monsterComponent monster "Info" uri createdByStr
                        
                            let mp =
                                MessageProperties()
                                  .WithComponents([battleCard; monsterCard])
                                  .WithFlags(MessageFlags.IsComponentsV2)

                            match monster.Picture with
                            | MonsterImg.File filepath ->
                                mp.WithAttachments([MonstersComponent.monsterAttachnment name filepath])
                            | MonsterImg.Ipfs _ -> mp          
                        do! DUtils.createAndSendMsgToChannel Channels.LogChannel gclient createMP true

                        let roundCard = BattleComponent.roundCard ar.RoundId ar.RoundRewards
                        let roundStartMessage = DUtils.v2ComponentMessage [roundCard]
                        do! DUtils.sendMsgToLogChannel gclient roundStartMessage
                    | _ -> ()
                | Error err ->
                    Log.Error($"StartBattleError: {err}")
                    do! Task.Delay(TimeSpan.FromMinutes(5.0))
            | Error _ ->
                do! Task.Delay(TimeSpan.FromHours(1))
        | Error err ->
            let mp = MessageProperties(Content = $"Unable to start new battle - {err}")
            do! DUtils.sendMsgToLogChannel gclient mp
            do! Task.Delay(TimeSpan.FromHours(1))
    }

    let startRound() = task {
        match balanceCorrectness() with
        | Ok () ->
            // start new round
            let dt = DateTime.UtcNow
            let roundIdO = db.NextRound()
            match roundIdO with
            | Ok roundId ->
                roundStatus.Set(RoundInfoDTO(RoundStatus.Started, Some dt, uint64 roundId))
                roundParticipants.Set([])
                do! updateBattleStatus()
                let mp = MessageProperties(Content = $"Round {roundId} has started!")
                do! DUtils.sendMsgToLogChannel gclient mp
            | Error err ->
                match err with
                | StartRoundError.MaxRoundsInBattle ->
                    let mp = MessageProperties(Content = $"Battle is finished - max amount of rounds ({Constants.RoundsInBattle}) is reached!")
                    do! DUtils.sendMsgToLogChannel gclient mp  
                | _ ->
                    Log.Error($"StartRoundError: {err}")
                    do! Task.Delay(TimeSpan.FromMinutes(5.0))
        | Error err ->
            let mp = MessageProperties(Content = $"Unable to start new round - {err}")
            do! DUtils.sendMsgToLogChannel gclient mp
            do! Task.Delay(TimeSpan.FromHours(1))
    }

    let finishRound(roundId:uint64) = task {
        // start new round
        let roundIdO = db.FinishRound()
        match roundIdO with
        | Ok _ ->
            roundStatus.Set(RoundInfoDTO(RoundStatus.Processing, None, roundId))
            // TODO: wrap into try-with and move out
            for guild in gclient.Cache.Guilds do
                match guild.Value.Channels |> Seq.tryFind(fun c -> c.Value.Name = Channels.EntryChannel) with
                | Some channel ->
                    let! pinnedMsgs = gclient.Rest.GetPinnedMessagesAsync(channel.Key)
                    match pinnedMsgs.Count with
                    | 0 -> Log.Error($"No pinned messages in {Channels.EntryChannel} channel")
                    | 1 ->
                        let! _ = pinnedMsgs.[0].ModifyAsync(fun options ->
                            options.Components <- [
                                ComponentContainerProperties([
                                    TextDisplayProperties($"** Round is processing, please wait for a few minutes **")
                                    ActionRowProperties([
                                        ButtonProperties($"pendingrewards", "Pending rewards", ButtonStyle.Success)
                                        ButtonProperties($"info", "Info", ButtonStyle.Success)
                                        ButtonProperties($"register", "Register", ButtonStyle.Danger)
                                    ])
                                ])
                            ])
                        ()
                    | _ -> Log.Error($"{pinnedMsgs.Count} pinned messages in {Channels.EntryChannel} channel")
                | None ->
                    Log.Error($"Can't find channel in guild {guild}")
            let mp = MessageProperties(Content = $"Round {roundId} is closed. Processing...")
            do! DUtils.sendMsgToLogChannel gclient mp
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

            let champNames = champs |> Map.map(fun _ (_,_,name) -> name)

            let monsterChar = MonsterChar(monster.MonsterId, monster.MonsterRecord.Monster, monster.MonsterRecord.Stats, monster.MonsterRecord.Xp, monster.MonsterRecord.Name)
            
            match Battle.fight(roundId, battleId, roundMoves, boosts, levels, monsterChar, monster.IsDefaultMonster, rewards) with
            | Ok bres ->
                let revivalTime =
                    let baseRevival = Monster.getRevivalDuration monsterChar.Monster
                    let lvlRevival = Levels.getLvlByXp monster.MonsterRecord.Xp
                    baseRevival + 3u * uint lvlRevival
                match db.FinalizeRound bres revivalTime boosts with
                | Ok _ ->
                    roundStatus.Set(RoundInfoDTO(RoundStatus.Finished, None, roundId))
                    let boostsStat =
                        let nextRound = roundId + 1UL |> int64
                        boosts
                        |> Map.map(fun _ v -> 
                            v |> List.fold(fun stat boost ->
                                Battle.processShopItem stat boost nextRound) Stat.Zero)
                    for msg in BattleComponent.battleResults bres champNames levels boostsStat do
                        let mp = DUtils.v2ComponentMessage([ msg ])
                        do! DUtils.sendMsgToLogChannel gclient mp

                    let mp = MessageProperties(Content = $"Round {roundId} is completed.")
                    do! DUtils.sendMsgToLogChannel gclient mp
                | Error _ -> ()
            | Error err ->
                Log.Error($"Error during fight: {err}")
            
        | _ ->
            Log.Error (sprintf "Errors: champs = %A; actions = %A; boosts = %A; lvls = %A; monster = %A"
                champsR.IsOk actionsR.IsOk boostsR.IsOk lvlsR.IsOk monsterR.IsOk)
    }

    let joinRound(userId:UserId, rar:RoundActionRecord, sendDiscordMsg:bool) =
        let t (rar:RoundActionRecord) =
            // TODO: add link to a champ's owner
            task {
                let name, ipfs = db.GetChampNameIPFSById rar.ChampId |> Option.defaultValue ("", "")
                let mp = 
                    [ BattleComponent.champJoinRoundComponent name ipfs rar.ChampId ]
                    |> DUtils.v2ComponentMessage
                DUtils.sendMsgToLogChannel gclient mp |> ignore
            }
        if roundParticipants.Value |> List.exists (fun rp -> rp.ID = rar.ChampId) then
            Error "Already joined"
        else
            match db.PerformAction (userId, rar) with
            | Ok () ->
                match db.GetChampNameIPFSById(rar.ChampId) with
                | Some (name, ipfs) ->
                    roundParticipants.Set(RoundParticipantChamp(rar.ChampId, name, ipfs)::roundParticipants.Value)
                | None -> ()
                if sendDiscordMsg then
                    t rar |> ignore
                Ok ()
            | Error err -> Error err

    let sendGroup(userId:UserId) =
        match roundStatus.Value.Status with
        | RoundStatus.Started ->
            let cRound = roundStatus.Value.Round
            match db.GetActiveUserChamps(userId, cRound) with
            | Ok champs when champs.Length > 0 ->
                let moves = champs |> List.map(fun r -> r.ID, r.Stat) |> ActionSelector.selectActions
                let results = moves |> List.map(fun rar -> joinRound(userId, rar, moves.Length = 1))
                if results |> List.forall(fun r -> r.IsOk) then
                    Ok (moves.Length)
                else Error("Unexpected error")
            | Ok _ -> Error "No available (alive) champs!"
            | Error err -> Error err
        | RoundStatus.Processing -> Error "Wait for a new round!"
        | RoundStatus.Finished -> Error "Wait for a new round!"

    let sendAll (userId:UserId) =
        match roundStatus.Value.Status with
        | RoundStatus.Started ->
            let cRound = roundStatus.Value.Round
            let! r = sendGroup(userId)
            match r with
            | Ok v ->
                match db.GetActiveUserChamps(userId, cRound) with
                | Ok champs ->
                    if champs.Length > 0 then
                        let moves =
                            champs
                            |> List.map(fun c ->
                                let move =
                                    if c.Stat.Health > 20 && c.Stat.Magic > 5 then Move.Heal
                                    elif c.Stat.Health > 50 && c.Stat.Magic < 25 then Move.Meditate
                                    else Move.Attack
                                { Move = move; ChampId = c.ID })
                        let results =
                            moves |> List.map(fun rar -> joinRound(userId, rar, moves.Length = 1))
                        if results |> List.forall(fun r -> r.IsOk) then
                            Ok (champs.Length + v)
                        else Error("Unexpected error")
                    else Ok v
                | Error err -> Error err
            | Error err -> Error err
        | RoundStatus.Processing -> Error "Wait for a new round!"
        | RoundStatus.Finished -> Error "Wait for a new round!"

    member _.RoundParticipants: IReadOnlySignal<RoundParticipantChamp list> = roundParticipants
    member _.RoundStatus:IReadOnlySignal<RoundInfoDTO> = roundStatus
    member _.BattleStatus:IReadOnlySignal<BattleInfoDTO option> = battleStatus

    member _.JoinRound(userId:UserId, rar:RoundActionRecord) =
        joinRound(userId, rar, true)

    member _.SendGroup(userId:UserId) = 
        try
            match sendGroup(userId) with
            | Ok c ->
                if c > 1 then
                    task {
                        try
                            let! uLink = Cache.tryGetUserLinkByUserId db gclient.Rest userId
                            let msg =
                                match uLink with
                                | Some u -> $"[{u.Nickname}]({Links.userProfile u.UserRawId}) send {c} champs to join round!"
                                | None -> $"{c} champs joined round!"
                            let mp = 
                                [ TextDisplayProperties(msg) :> IMessageComponentProperties ]
                                |> DUtils.v2ComponentMessage
                            do! DUtils.sendMsgToLogChannel gclient mp
                        with e ->
                            Log.Error(e, e.Message)
                    } |> ignore
                Ok c
            | Error err ->
                Error err
        with e ->
            Log.Error(e, e.Message)
            Error e.Message

    member _.SendAll(userId:UserId) =    
        match sendAll(userId) with
        | Ok c ->
            if c > 1 then
                task {
                    try
                        let! uLink = Cache.tryGetUserLinkByUserId db gclient.Rest userId
                        let msg =
                            match uLink with
                            | Some u -> $"[{u.Nickname}]({Links.userProfile u.UserRawId}) send {c} champs to join round!"
                            | None -> $"{c} champs joined round!"
                        let mp = 
                            [ TextDisplayProperties(msg) :> IMessageComponentProperties ]
                            |> DUtils.v2ComponentMessage
                        do! DUtils.sendMsgToLogChannel gclient mp
                    with e ->
                        Log.Error(e, e.Message)
                } |> ignore
            Ok c
        | Error err ->
            Error err
     
    override _.ExecuteAsync(cancellationToken) =
        task {
            db.GetLastRoundParticipants()
            |> Result.iter roundParticipants.Set

            do! updateBattleStatus()
            
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
                                        roundStatus.Set(RoundInfoDTO(status, Some timestamp, roundId))
                                        let dt = DateTime.UtcNow
                                        let duration = dt - timestamp
                                        if duration < BattleParams.RoundDuration() then
                                            Log.Information($"Delay for {(BattleParams.RoundDuration() - duration).TotalMinutes} minutes")
                                            do! Task.Delay(BattleParams.RoundDuration() - duration)
                                            do! Task.Delay(TimeSpan.FromMinutes(0.5))
                                        
                                        while db.AnyChampJoinedRound roundId |> Option.defaultValue false |> not do
                                            do! Task.Delay(TimeSpan.FromMinutes(1.0))

                                        do! finishRound(roundId)
                                    | RoundStatus.Processing ->
                                        roundStatus.Set(RoundInfoDTO(status, None, roundId))
                                        do! finalizeRound(roundId, battleId)
                                    | RoundStatus.Finished ->
                                        roundStatus.Set(RoundInfoDTO(status, None, roundId))
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
                    do! Task.Delay(TimeSpan.FromHours(1), cancellationToken)
        }

type RefundFailedGenService(db:SqliteStorage, chainOpt: IOptions<Conf.ChainConfiguration>, walletOpt: IOptions<Conf.WalletConfiguration>) =
    inherit BackgroundService()
    
    let keys = chainOpt.Value.GameWalletKeys

    override _.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                try
                    for ar in db.GetFailedGen() do
                        Log.Information($"Processing {ar.GenRequestRefundId}")
                        match Blockchain.getDarkCoinBalance(walletOpt.Value.GameWallet), db.GetBalances() with
                        | Ok walletBalance, Ok dbBalances ->
                            if walletBalance + 5000M > dbBalances.Total + ar.Amount then
                                if db.CloseFailedGen ar.GenRequestRefundId then
                                    Log.Information($"Sending {ar.Amount} to {ar.Sender}")
                                    let amount = Blockchain.toLong (ar.Amount, Algo6Decimals)
                                    Log.Information($"Exact amount = {amount}")
                                    let! r =
                                        Blockchain.sendTx(
                                            keys, ar.Sender, amount,
                                            Blockchain.DarkCoinAssetId,
                                            $"Gen failed, please try again later"
                                        )

                                    match r with
                                    | TxStatus.Error err -> Log.Error($"Unable to send = {err}")
                                    | _ -> ()

                                    let! _ =
                                        Helpers.retry (fun () ->
                                            match r with
                                            | TxStatus.Error _ ->
                                                db.ReopenFailedGen ar.GenRequestRefundId
                                            | TxStatus.Confirmed(tx, _)
                                            | TxStatus.Unconfirmed tx ->
                                                db.SetOutputTxForUserGenRequestRefund(ar.GenRequestRefundId, tx)
                                            ) 5 (TimeSpan.FromMinutes(1.))
                                    ()

                                do! Task.Delay(TimeSpan.FromMinutes(1.), cancellationToken)
                            else
                                let err = $"Balance doesn't match {dbBalances.Total} >= {walletBalance}"
                                Log.Error(err)
                                do! Task.Delay(TimeSpan.FromHours(24.), cancellationToken)  
                        | _ ->
                            Log.Error("Unable to either read balance from chain or db")
                            do! Task.Delay(TimeSpan.FromMinutes(15.), cancellationToken)
                    do! Task.Delay(TimeSpan.FromHours(Random.Shared.Next(4, 12)), cancellationToken)

                with exn ->
                    do! Task.Delay(TimeSpan.FromHours(12), cancellationToken)
                    Log.Error(exn, "RefundFailedGenService")
        }

// TODO: handle valid tx differently?
type RefundInvalidTxService(db: SqliteStorage, chainOpt: IOptions<Conf.ChainConfiguration>, walletOpt: IOptions<Conf.WalletConfiguration>) =
    inherit BackgroundService()

    let keys = chainOpt.Value.GameWalletKeys

    override _.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
            while not cancellationToken.IsCancellationRequested do
                try
                    for tx in db.GetPendingTxRefunds() do
                        Log.Information($"Processing invalid tx refund {tx.TxId}")
                        match Blockchain.getDarkCoinBalance(walletOpt.Value.GameWallet), db.GetBalances() with
                        | Ok walletBalance, Ok dbBalances ->
                            if walletBalance + 5000M > dbBalances.Total + tx.Amount then
                                if db.ClosePendingTxRefund tx.TxId then
                                    let amount = Blockchain.toLong(tx.Amount, Algo6Decimals)
                                    Log.Information($"Sending {tx.Amount} (exact: {amount}) to {tx.Wallet}")
                                    let! r =
                                        Blockchain.sendTx(
                                            keys, tx.Wallet, amount,
                                            Blockchain.DarkCoinAssetId,
                                            ""
                                        )

                                    match r with
                                    | TxStatus.Error err -> Log.Error($"Refund for tx {tx.TxId} failed: {err}")
                                    | _ -> ()
                                        
                                    let! _ =
                                        Helpers.retry (fun () ->
                                            match r with
                                            | TxStatus.Error _ ->
                                                db.ReopenPendingTxRefund tx.TxId
                                            | TxStatus.Confirmed(outTx, _)
                                            | TxStatus.Unconfirmed outTx ->
                                                db.AddTxRevertHistory(tx.TxId, outTx)
                                            ) 5 (TimeSpan.FromMinutes(1.))
                                    ()

                                do! Task.Delay(TimeSpan.FromMinutes(1.), cancellationToken)
                            else
                                let err = $"Balance doesn't match {dbBalances.Total} >= {walletBalance}"
                                Log.Error(err)
                                do! Task.Delay(TimeSpan.FromHours(24.), cancellationToken) 
                        | _ ->
                            Log.Error("Unable to either read balance from chain or db")
                            do! Task.Delay(TimeSpan.FromMinutes(15.), cancellationToken)
                    do! Task.Delay(TimeSpan.FromHours(Random.Shared.Next(4, 12)), cancellationToken)

                with exn ->
                    Log.Error(exn, "RefundInvalidTxService")
                    do! Task.Delay(TimeSpan.FromHours(12), cancellationToken)
        }

open Gen
open GenAIPG
open System.Collections.Immutable

type GenService(db:SqliteStorage, gclient:GatewayClient, options:IOptions<Conf.GenConfiguration>) =
    inherit BackgroundService()
    let MaxRetry = 5
    let mutable errors = ImmutableDictionary.Create<int64, int>()
    let aipgGen = GenAIPG.AipgGen(options)

    let getTextRequest(prompt:string) =
        GenerateTextRequest(prompt, [| "grid/llama-3.3-70b-versatile" |], TextRequestParams(512, 512, 0.7, 0.9))

    let getImgRequest (mfulltype:string) (tp:TextPayload) =
        GenerateRequest(prompt = $"{mfulltype}. {tp.Description}",
            models = [| "flux.2 klein 4b fp8" |],
            parameters = Params(height = 1024, width = 1024, samplerName = "k_dpmpp_2m"))

    override _.ExecuteAsync(cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromMinutes(0.5), cancellationToken)
            while cancellationToken.IsCancellationRequested |> not do
                // get all request that aren't finished
                let requests = db.GetAllUnfinishedGenRequests()
                for req in requests do
                    try
                        match req.Payload with
                        | GenPayload.TextReqCreated prompt ->
                            let! res =
                                prompt
                                |> getTextRequest
                                |> aipgGen.GenerateTextAsync
                            let newPayload = 
                                match res with
                                | Ok id -> GenPayload.TextReqReceived id
                                | Error err ->
                                    Log.Error($"GEN Err [TextReqCreated] : {err}")
                                    GenFailure.Repeat(req.Payload)
                                    |> GenPayload.Failure
                            db.UpdateGenRequest(req.ID, newPayload, req.UserId) |> ignore
                            do! Task.Delay(TimeSpan.FromSeconds(5.0), cancellationToken)
                        | GenPayload.TextReqReceived id ->
                            let! res = aipgGen.GetGeneratedTextAsync id
                            let res' =
                                match res with
                                | Ok json ->
                                    try
                                        deserialize<TextPayload> json
                                        |> Some
                                    with exn ->
                                        Log.Error(exn, $"deserialize {json}")
                                        None
                                | Error err ->
                                    Log.Error($"GEN Err [TextReqReceived] : {err}")
                                    None
                            let newPayload = 
                                match res' with
                                | Some tp ->
                                    // rare case: duplicate description
                                    if db.IsMonsterDescriptionExists tp.Description then
                                        Log.Error($"Duplicate description: {tp.Description}")
                                        Prompt.createMonsterNameDesc req.MType req.MSubType
                                        |> GenPayload.TextReqCreated
                                    else
                                        GenPayload.TextPayloadReceived tp
                                | None ->
                                    GenFailure.Repeat(req.Payload)
                                    |> GenPayload.Failure
                            db.UpdateGenRequest(req.ID, newPayload, req.UserId) |> ignore
                            do! Task.Delay(TimeSpan.FromSeconds(5.0), cancellationToken)
                        | GenPayload.TextPayloadReceived tp ->
                            let subType = match req.MSubType with | MonsterSubType.None -> "" | _ -> $"({req.MSubType})"
                            let mfulltype = $"{subType} {req.MType}"
                            let! res =
                                getImgRequest mfulltype tp
                                |> aipgGen.GenerateImageAsync
                            let newPayload = 
                                match res with
                                | Ok id -> GenPayload.ImgReqReceived(id, tp)
                                | Error err ->
                                    Log.Error($"GEN Err [TextPayloadReceived] : {err}")
                                    GenFailure.Repeat(req.Payload)
                                    |> GenPayload.Failure
                            db.UpdateGenRequest(req.ID, newPayload, req.UserId) |> ignore
                            do! Task.Delay(TimeSpan.FromMinutes(1.0), cancellationToken)
                        | GenPayload.ImgReqReceived (id, tp) ->
                            // fetch img and save locally
                            let! res = aipgGen.FetchCompleteResponseAsync id
                            match res with
                            | Ok bytes ->
                                let dir = options.Value.ImgFolder
                                if Directory.Exists(dir) |> not then
                                    Directory.CreateDirectory(dir) |> ignore
                                let filename =
                                    req.MType.ToString().ToLower() + "_" + req.MSubType.ToString().ToLower() + "_" + req.ID.ToString() + ".png"
                                let filepath = Path.Combine(dir, filename)
                                // save img locally
                                System.IO.File.WriteAllBytes(filepath, bytes)
                                let mi = MonsterImg.File filepath
                                let! createdBy = Cache.tryGetUserLinkByRawUserId db gclient.Rest req.UserId                     
                                let createdByStr =
                                    match createdBy with
                                    | Some uLink -> $"Created by [{uLink.Nickname}]({Links.userProfile uLink.UserRawId})" |> Some
                                    | None -> None
                                match Monster.TryCreate(req.MType, req.MSubType) with
                                | Some monster ->
                                    let monsterRecord = MonsterRecord(tp.Name, tp.Description, monster, Monster.getStats(monster), 0UL, mi)
                                    match db.CreateCustomMonster(monsterRecord, mi, req.ID, req.UserId, req.Cost) with
                                    | Ok mId ->
                                        try
                                           let minfo = MonsterInfo(uint64 mId, monsterRecord.Xp, monsterRecord.Name, monsterRecord.Description,
                                                mi, monsterRecord.Stats, monsterRecord.Monster.MType, monsterRecord.Monster.MSubType, Some (uint64 req.UserId), MonsterGenType.Generative)

                                           let name = "image.png"
                                           let uri = 
                                                match minfo.Picture with
                                                | MonsterImg.File _ -> $"attachment://{name}"
                                                | MonsterImg.Ipfs _ -> ""
                                           let monsterCard = MonstersComponent.monsterComponent minfo $"is {Format.createMsg monster.MType}!" uri createdByStr
                        
                                           let createMP() =
                                              let mp = MessageProperties().WithComponents([monsterCard]).WithFlags(MessageFlags.IsComponentsV2)
                                              match minfo.Picture with
                                              | MonsterImg.File filepath ->
                                                mp.WithAttachments([MonstersComponent.monsterAttachnment name filepath])
                                              | MonsterImg.Ipfs _ -> mp   
                                           do! DUtils.createAndSendMsgToChannel Channels.LogChannel gclient createMP false

                                        with ex ->
                                            Log.Error(ex, "CreateCustomMonster msg")
                                    | Error _ ->
                                        let newPayload = GenFailure.Final("Db error") |> GenPayload.Failure
                                        db.UpdateGenRequest(req.ID, newPayload, req.UserId) |> ignore                                        
                                | None ->
                                    let newPayload = GenFailure.Final("Invalid monster") |> GenPayload.Failure
                                    db.UpdateGenRequest(req.ID, newPayload, req.UserId) |> ignore
                            | Error err ->
                                Log.Error($"GEN Err [ImgReqReceived] : {err}")
                                let newPayload = GenFailure.Repeat(req.Payload) |> GenPayload.Failure
                                db.UpdateGenRequest(req.ID, newPayload, req.UserId) |> ignore
                        | GenPayload.Failure genFailure ->
                            match genFailure with
                            | GenFailure.Final _ ->
                                Log.Error($"Invalid case")
                                db.UpdateGenRequest(req.ID, req.Payload, req.UserId) |> ignore
                            | GenFailure.Repeat prevPayload ->
                                if errors.ContainsKey req.ID |> not then
                                    errors <- errors.Add(req.ID, 0)
                                let count = errors.[req.ID]
                                Log.Information($"Retry...{req.ID} ({prevPayload.Status}); attempts = {count}")
                                if count <= MaxRetry then
                                    match prevPayload with
                                    | GenPayload.Success
                                    | GenPayload.Failure _ ->
                                        Log.Error($"Invalid case")
                                        None
                                    | GenPayload.TextReqCreated prompt ->
                                        GenPayload.TextReqCreated prompt
                                        |> Some
                                    | GenPayload.TextReqReceived _ ->
                                        Prompt.createMonsterNameDesc req.MType req.MSubType
                                        |> GenPayload.TextReqCreated
                                        |> Some
                                    | GenPayload.TextPayloadReceived tp ->
                                        prevPayload |> Some
                                    | GenPayload.ImgReqReceived (_, tp) ->
                                        GenPayload.TextPayloadReceived tp |> Some
                                else
                                    GenFailure.Final "Max retry reached"
                                    |> GenPayload.Failure
                                    |> Some
                                |> Option.iter(fun newPayload ->
                                    if db.UpdateGenRequest(req.ID, newPayload, req.UserId) then
                                        errors <- errors.SetItem(req.ID, count + 1))
                        | GenPayload.Success ->
                            Log.Error($"GenPayload is Success but status is unfinished: {req.ID}")
                            db.UpdateGenRequest(req.ID, req.Payload, req.UserId) |> ignore
                    with exn ->
                        Log.Error(exn, "GenService")
                        do! Task.Delay(TimeSpan.FromMinutes(1.), cancellationToken)
                do! Task.Delay(TimeSpan.FromMinutes(1.), cancellationToken)
        }