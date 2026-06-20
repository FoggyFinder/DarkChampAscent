namespace Db

open GameLogic.Rewards
open GameLogic.Champs
open GameLogic.Monsters
open Db.Sqlite
open Gen
open Types
open DarkChampAscent.Api

type DbKeys =
    | LastTrackedChampCfg = 0
    | LastTimePriceIsUpdated = 2
    | LastTimeConfirmationCodeChecked = 3
    | LastTimeBackupPerformed = 4
    | LastTimeTxsAreScanned = 5

type DbKeysNum =
    | Rewards = 0
    | Reserve = 1
    | Dev = 2
    | Burn = 3
    | DAO = 4
    | DarkCoinPrice = 5 // => itemPriceDarkCoin = itemPriceUsd / darkCoinPriceUsd
    | Staking = 7
    | DarkCoinPriceOld = 8

type WalletType =
    | DAO = 0
    | Dev = 1
    | Reserve = 2
    | Burn = 3
    | Staking = 4

type DbKeysBool =
    | InitBalanceIsSet = 0
    | LockedKeyIsSet = 2
    | StakingKeyIsSet = 3
    | ProcessingKeyIsSet = 4
    | MonsterImgIPFSAdded = 5

type TxType =
    | Unknown = 0
    | Donate = 1
    | BuyItem = 2
    | RenameChamp = 3
    | CreateCustomMonster = 4
    | CreateNFTBasedCustomMonster = 5

[<RequireQualifiedAccess>]
type MonsterImgOld =
    | File of filepath:string
    member x.ToMonsterImg =
        match x with
        | MonsterImgOld.File fp -> MonsterImg.File fp

[<RequireQualifiedAccess>]
module TxType =
    let fromTx (tx:Tx) =
        match tx with
        | Tx.Confirm _ -> TxType.Unknown
        | Tx.RenameChamp _  -> TxType.RenameChamp
        | Tx.Donate _ -> TxType.Donate
        | Tx.CreateCustomMonster _ -> TxType.CreateCustomMonster
        | Tx.BuyItem _ -> TxType.BuyItem
        | Tx.CreateNFTBasedCustomMonster _ -> TxType.CreateNFTBasedCustomMonster

type NewChampDb = {
    Name: string
    AssetId: uint64
    IPFS: string
    UserId: uint64
    Stats: Stat
    Traits: Traits
}

[<RequireQualifiedAccess>]
type BattleStatusError =
    | UnfinishedBattleFound = 0
    | UnfinishedRoundFound = 1
    | NoMosterFound = 2
    | MonsterIsDead = 3
    | GettingRewardError = 4
    | UnknownError = 5
    | NoRewards = 6

[<RequireQualifiedAccess>]
type StartRoundError = 
    | UnfinishedRoundFound = 0
    | NoActiveBattle = 1
    | UnknownError = 2
    | IncorrectArgs = 3
    | MaxRoundsInBattle = 4

open GameLogic.Effects

open Microsoft.Data.Sqlite
open Donald
open System

open GameLogic.Shop
open GameLogic.Battle
open Serilog
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Extensions.Options
open Conf
open DarkChampAscent.Account
open System.Collections.Concurrent
open Blockchain
open GameLogic.Limits
open System.Text
open System.Collections.Generic

type SqliteStorage(options:IOptions<DbConfiguration>) =
    let jsOptions = JsonFSharpOptions().ToJsonSerializerOptions()
    let updateShop (conn:SqliteConnection) =
        try
            let items = System.Enum.GetValues<ShopItem>() |> Array.map int |> Set.ofArray
            let currentItems =
                Db.newCommand "SELECT Item FROM Shop;" conn
                |> Db.query(fun reader -> reader.GetInt32(0))
                |> Set.ofList
            for item in Set.difference items currentItems do
                Db.newCommand "INSERT INTO Shop(Item) VALUES(@item);" conn
                |> Db.setParams [ "item", SqlType.Int item ]
                |> Db.exec
            true
        with exn ->
            Log.Error(exn, "updateShop")
            false
    
    let updateEffects (conn:SqliteConnection) =
        try
            let items = System.Enum.GetValues<Effect>() |> Array.map int |> Set.ofArray
            let currentItems =
                Db.newCommand "SELECT Item FROM Effect;" conn
                |> Db.query(fun reader -> reader.GetInt32(0))
                |> Set.ofList
            for item in Set.difference items currentItems do
                Db.newCommand "INSERT INTO Effect(Item) VALUES(@item);" conn
                |> Db.setParams [ "item", SqlType.Int item ]
                |> Db.exec
            true
        with exn ->
            Log.Error(exn, "updateEffects")
            false

    let setKeyDateTime (conn:SqliteConnection) (key:DbKeys, dt:DateTime) =
        try
            Db.newCommand SQL.SetKey conn
            |> Db.setParams [
                "key", SqlType.String (key.ToString())
                "value", SqlType.DateTime dt
            ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"setKeyDateTime: {key}={dt}")
            false

    let getKeyDateTime (conn:SqliteConnection) (key:DbKeys) =
        try
            Db.batch (fun tn ->
                let exists =
                    Db.newCommandForTransaction SQL.KeyExists tn
                    |> Db.setParams [ "key", SqlType.String (key.ToString()) ]
                    |> Db.scalar (fun v -> (unbox<int64> v) = 1L)
                if exists then
                    Db.newCommandForTransaction SQL.GetKey tn
                    |> Db.setParams [ "key", SqlType.String (key.ToString()) ]
                    |> Db.querySingle (fun rd -> rd.ReadDateTime "Value")
                else
                    let dt = DateTime.UtcNow
                    Db.newCommand SQL.SetKey conn
                    |> Db.setParams [
                        "key", SqlType.String (key.ToString())
                        "value", SqlType.DateTime dt
                    ]
                    |> Db.exec
                    Some dt) conn
        with exn ->
            Log.Error(exn, $"getKeyDateTime: {key}")
            None

    let getKeyNum(conn:SqliteConnection) (key:DbKeysNum) =
        try
            Db.newCommand SQL.GetKeyNum conn
            |> Db.setParams [ "key", SqlType.String (key.ToString()) ]
            |> Db.querySingle (fun rd -> rd.ReadDecimal "Value")
        with exn ->
            Log.Error(exn, $"getKeyNum: {key}")
            None
    
    let setKeyNum (conn:SqliteConnection) (key:DbKeysNum, d:decimal) =
        try
            Db.newCommand SQL.SetKeyNum conn
            |> Db.setParams [
                "key", SqlType.String (key.ToString())
                "value", SqlType.Decimal d
            ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"setKeyNum: {key}={d}")
            false

    let getKeyBool (conn:SqliteConnection) (key:DbKeysBool) =
        try
            Db.newCommand SQL.GetKeyBool conn
            |> Db.setParams [ "key", SqlType.String (key.ToString()) ]
            |> Db.querySingle (fun rd -> rd.ReadBoolean "Value")
        with exn ->
            Log.Error(exn, $"getKeyBool: {key}")
            None
    
    let setKeyBool(conn:SqliteConnection) (key:DbKeysBool, b:bool) =
        try
            Db.newCommand SQL.SetKeyBool conn
            |> Db.setParams [
                "key", SqlType.String (key.ToString())
                "value", SqlType.Boolean b
            ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"setKeyBool: {key} = {b}")
            false

    let setInitBalance(conn:SqliteConnection) =
        try
            let sql = """
                INSERT INTO KeyValueNum(Key, Value)
                    VALUES
                        (@rewards, 0.000000),
                        (@reserve, 0.000000),
                        (@dev, 0.000000),
                        (@burn, 0.000000),
                        (@dao, 0.000000)
            """
            Db.batch(fun tn ->
                let isInit =
                    Db.newCommandForTransaction SQL.GetKeyBool tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysBool.InitBalanceIsSet.ToString())
                    ]
                    |> Db.querySingle (fun rd -> rd.ReadBoolean "Value")
                    |> Option.defaultValue false
                if isInit |> not then
                    Db.newCommandForTransaction sql tn
                    |> Db.setParams [
                        "rewards", SqlType.String (DbKeysNum.Rewards.ToString())
                        "reserve", SqlType.String (DbKeysNum.Reserve.ToString())
                        "dev", SqlType.String (DbKeysNum.Dev.ToString())
                        "burn", SqlType.String (DbKeysNum.Burn.ToString())
                        "dao", SqlType.String (DbKeysNum.DAO.ToString())
                    ]
                    |> Db.exec

                    Db.newCommandForTransaction SQL.SetKeyBool tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysBool.InitBalanceIsSet.ToString())
                        "value", SqlType.Boolean true
                    ]
                    |> Db.exec
            ) conn
            
            true
        with exn ->
            Log.Error(exn, "SetInitBalance")
            false

    // ToDo: move to setInitBalance
    let setStakingKey(conn:SqliteConnection) =
        try
            let sql = "INSERT INTO KeyValueNum(Key, Value) VALUES (@key, 0.000000)"
            Db.batch(fun tn ->
                let isInit =
                    Db.newCommandForTransaction SQL.GetKeyBool tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysBool.StakingKeyIsSet.ToString())
                    ]
                    |> Db.querySingle (fun rd -> rd.ReadBoolean "Value")
                    |> Option.defaultValue false
                if isInit |> not then
                    Db.newCommandForTransaction sql tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysNum.Staking.ToString())
                    ]
                    |> Db.exec

                    Db.newCommandForTransaction SQL.SetKeyBool tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysBool.StakingKeyIsSet.ToString())
                        "value", SqlType.Boolean true
                    ]
                    |> Db.exec
            ) conn
            
            true
        with exn ->
            Log.Error(exn, "setStakingKey")
            false

    let createNewMonster(cs:string, monster:MonsterRecord) =
        try
            use conn = new SqliteConnection(cs)
            let monsterExists =
                Db.newCommand SQL.MonsterExistsByName conn
                |> Db.setParams [ "name", SqlType.String monster.Name ]
                |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
            if monsterExists |> not then
                let bytes = JsonSerializer.SerializeToUtf8Bytes(monster.Img, jsOptions)
                Db.newCommand SQL.CreateMonster conn
                |> Db.setParams [
                    "name", SqlType.String monster.Name
                    "description", SqlType.String monster.Description
                    "img", SqlType.Bytes bytes
                    "health", SqlType.Int64 <| int64 monster.Stats.Health
                    "magic", SqlType.Int64 <| int64 monster.Stats.Magic
                            
                    "accuracy", SqlType.Int64 <| int64 monster.Stats.Accuracy
                    "luck", SqlType.Int64 <| int64 monster.Stats.Luck
                    "attack", SqlType.Int64 <| int64 monster.Stats.Attack
                    "mattack", SqlType.Int64 <| int64 monster.Stats.MagicAttack
                    "defense", SqlType.Int64 <| int64 monster.Stats.Defense
                    "mdefense", SqlType.Int64 <| int64 monster.Stats.MagicDefense
                    "type", SqlType.Int <| int monster.Monster.MType
                    "subtype", SqlType.Int <| int monster.Monster.MSubType
                ]
                |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"CreateNewMonster: {monster}")
            false

    let addIPFSImg(conn:SqliteConnection) =
        try
            Db.batch(fun tn ->
                let isInit =
                    Db.newCommandForTransaction SQL.GetKeyBool tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysBool.MonsterImgIPFSAdded.ToString())
                    ]
                    |> Db.querySingle (fun rd -> rd.ReadBoolean "Value")
                    |> Option.defaultValue false
                
                if isInit |> not then
                    let dbParams =
                        Db.newCommandForTransaction "SELECT ID, Picture FROM Monster" tn
                        |> Db.query(fun r -> 
                            r.GetInt32(0),
                            JsonSerializer.Deserialize<MonsterImgOld>(Utils.getBytesData r 1, jsOptions))
                        |> List.map(fun (mId, pic) ->
                            let img = pic.ToMonsterImg
                            let bytes = JsonSerializer.SerializeToUtf8Bytes(img, jsOptions)
                            [
                                "img", SqlType.Bytes bytes
                                "mId", SqlType.Int64 mId
                            ] : RawDbParams)

                    Db.newCommandForTransaction "UPDATE Monster SET Picture = @img WHERE ID = @mId" tn
                    |> Db.execMany dbParams

                    Db.newCommandForTransaction SQL.SetKeyBool tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysBool.MonsterImgIPFSAdded.ToString())
                        "value", SqlType.Boolean true
                    ]
                    |> Db.exec
            ) conn
            
            true
        with exn ->
            Log.Error(exn, "addIPFSImg")
            false

    let addNFTMonsterIdColumn(conn:SqliteConnection) =
        let balanceColumnCount = """
                SELECT COUNT(*) FROM
                pragma_table_info('UserMonster')
                WHERE name='NFTMonsterId'
            """
        let sql = """
            PRAGMA foreign_keys = OFF;

            CREATE TABLE IF NOT EXISTS UserMonsterNew (
                ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                MonsterId INTEGER NOT NULL UNIQUE,
                UserId INTEGER NOT NULL,
                RequestId INTEGER,
                NFTMonsterId INTEGER,
                FOREIGN KEY (MonsterId)
                   REFERENCES Monster (ID),
                FOREIGN KEY (UserId)
                   REFERENCES User (ID),
                FOREIGN KEY (RequestId)
                   REFERENCES UserGenMonsterRequest (ID),
                FOREIGN KEY (NFTMonsterId)
                   REFERENCES NFTMonster (ID),
                CHECK (RequestId IS NOT NULL OR NFTMonsterId IS NOT NULL)
            );

            INSERT INTO UserMonsterNew(MonsterId, UserId, RequestId, NFTMonsterId)
            SELECT MonsterId, UserId, RequestId, NULL
            FROM UserMonster;

            DROP TABLE UserMonster;

            ALTER TABLE UserMonsterNew RENAME TO UserMonster;

            PRAGMA foreign_keys = ON;
            """

        Db.newCommand balanceColumnCount conn
        |> Db.scalar(fun v -> tryUnbox<int64> v)
        |> Option.iter(fun i ->
            if i = 0L then
                Db.newCommand sql conn
                |> Db.exec)
    let cs = options.Value.ConnectionString
    do Log.Information("Db is init....")

    let _conn = new SqliteConnection(cs)
    do Db.newCommand SQLTables.createTablesSQL _conn |> Db.exec
    
    do updateShop(_conn) |> ignore
    do updateEffects(_conn) |> ignore
    do setInitBalance(_conn) |> ignore
    do setStakingKey(_conn) |> ignore
    do addIPFSImg(_conn) |> ignore
    do addNFTMonsterIdColumn(_conn) |> ignore

    do _conn.Dispose()

    do Log.Information("Db init is finished")

    do Utils.DefaultData.DefaultsMonsters |> List.iter(fun mr -> createNewMonster(cs, mr) |> ignore)

    let walletExists(wallet: string) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.WalletExists conn
            |> Db.setParams [ "wallet", SqlType.String wallet ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, $"walletExists: {wallet}")
            false

    let dbIdByDiscordId = ConcurrentDictionary<uint64, int64>()
    let dbIdByCustomId = ConcurrentDictionary<uint64, int64>()
    let dbIdByWallet = ConcurrentDictionary<string, int64>()
    let userChampsInfoByRound = ConcurrentDictionary<int64, Dictionary<int64, ChampInfoWithStat list>>()
    let userMonstrsInfoByRound = ConcurrentDictionary<int64, Dictionary<int64, MonsterInfo list>>()

    let getUserIdByWallet(wallet: string) =
        try 
            match dbIdByWallet.TryGetValue wallet with
            | true, dbId -> Some dbId
            | false, _ ->
                let dbIdOpt =
                    use conn = new SqliteConnection(cs)
                    Db.newCommand SQL.GetUserIdByWallet conn
                    |> Db.setParams [ "wallet", SqlType.String <| wallet ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                dbIdOpt |> Option.iter (fun dbId -> dbIdByWallet.TryAdd(wallet, dbId) |> ignore)
                dbIdOpt
        with exn ->
            Log.Error(exn, $"getUserIdByWallet: {wallet}")
            None

    let getUserIdByUserId(uId: UserId) =
        try
            match uId with
            | UserId.Discord discordId ->
                match dbIdByDiscordId.TryGetValue discordId with
                | true, dbId -> Some dbId
                | false, _ ->
                    let dbIdOpt =
                        use conn = new SqliteConnection(cs)
                        Db.newCommand SQL.GetUserIdByDiscordId conn
                        |> Db.setParams [ "discordId", SqlType.Int64 <| int64 discordId ]
                        |> Db.scalar (fun v -> tryUnbox<int64> v)
                    dbIdOpt |> Option.iter (fun dbId -> dbIdByDiscordId.TryAdd(discordId, dbId) |> ignore)
                    dbIdOpt
            | UserId.Custom customId ->
                match dbIdByCustomId.TryGetValue customId with
                | true, dbId -> Some dbId
                | false, _ ->
                    let dbIdOpt =
                        use conn = new SqliteConnection(cs)
                        Db.newCommand SQL.GetUserIdByCustomId conn
                        |> Db.setParams [ "customId", SqlType.Int64 <| int64 customId ]
                        |> Db.scalar (fun v -> tryUnbox<int64> v)
                    dbIdOpt |> Option.iter (fun dbId -> dbIdByCustomId.TryAdd(customId, dbId) |> ignore)
                    dbIdOpt
            | UserId.Web3 uId -> Some (int64 uId)
        with exn ->
            Log.Error(exn, $"getUserIdByUserId: {uId}")
            None

    let discordUserExists(discordId: uint64) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.UserExistsByDiscordId conn
            |> Db.setParams [ "discordId", SqlType.Int64 <| int64 discordId ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, $"userExists: {discordId}")
            false
    
    let customUserExists(customId: uint64) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.UserExistsByCustomId conn
            |> Db.setParams [ "customId", SqlType.Int64 <| int64 customId ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, $"customUserExists: {customId}")
            false

    let userExistsByWeb3Id(uId: uint64) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.UserExistsById conn
            |> Db.setParams [ "id", SqlType.Int64 <| int64 uId ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, $"userExists: {uId}")
            false

    let userExists(uId:UserId) =
        match uId with
        | UserId.Discord discordId -> discordUserExists discordId
        | UserId.Custom inGameId -> customUserExists inGameId
        | UserId.Web3 uId -> userExistsByWeb3Id uId

    let getShopItemIdByItem(shopItem: ShopItem) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetShopItemIdByItem conn
            |> Db.setParams [ "item", SqlType.Int32 <| int shopItem ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
        with exn ->
            Log.Error(exn, $"getShopItemIdByItem: {shopItem}")
            None

    let registerNewDiscordUser(discordId:uint64) =
        if discordUserExists discordId then
            false
        else
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.AddNewDiscordUser conn
                |> Db.setParams [ "discordId", SqlType.Int64 <| int64 discordId ]
                |> Db.exec
                true
            with exn ->
                Log.Error(exn, $"registerNewDiscordUser: {discordId}")
                false
    
    let userNameExists(nickname:string)=
        use conn = new SqliteConnection(cs)
        Db.newCommand SQL.UserNameAlreadyExists conn
        |> Db.setParams [ "name", SqlType.String nickname ]
        |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)

    let registerNewCustomUser(nickname:string, password:string) =
        match Validation.validateNickname nickname, Validation.validateHash password with
        | Ok validNickname, Ok validPassword ->
            try
                use conn = new SqliteConnection(cs)
                let nameExists =
                    Db.newCommand SQL.UserNameAlreadyExists conn
                    |> Db.setParams [ "name", SqlType.String validNickname ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                if nameExists then Error("Nickname already taken, try something else")
                else
                    Db.batch(fun tn ->
                        let customIdO =
                            Db.newCommand SQL.AddNewCustomUser conn
                            |> Db.setParams [ 
                                "nickname", SqlType.String validNickname
                                "password", SqlType.String validPassword
                            ]
                            |> Db.scalar (fun v -> tryUnbox<int64> v)
                        match customIdO with
                        | Some customId ->
                            Db.newCommand SQL.AddCustomUser conn
                            |> Db.setParams [ "customId", SqlType.Int64 <| int64 customId ]
                            |> Db.exec
                            Ok(customId)
                        | None ->
                            tn.Rollback()
                            Error("Unexpected error")

                    ) conn
            with exn ->
                Log.Error(exn, $"registerNewCustomUser: {nickname}")
                Error("Unexpected error")
        | Error nicknameErr, _ -> Error nicknameErr
        | _, Error passwordErr -> Error passwordErr

    let confirmationCodeIsMatchedForWallet(wallet:string, code:string) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.CodeIsMatchedForUnconfirmedWallet conn
            |> Db.setParams [ 
                "wallet", SqlType.String wallet
                "code", SqlType.String code
            ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, $"confirmationCodeIsMatchedForWallet: {wallet}, {code}")
            false      
    
    let updateChampStat (tn:Data.IDbTransaction) (champId: uint64) (stat:Stat) =
        tn
        |> Db.newCommandForTransaction SQL.UpdateChampStat
        |> Db.setParams [
            "health", SqlType.Int64 <| int64 stat.Health
            "magic", SqlType.Int64 <| int64 stat.Magic

            "accuracy", SqlType.Int64 <| int64 stat.Accuracy
            "luck", SqlType.Int64 <| int64 stat.Luck

            "attack", SqlType.Int64 <| int64 stat.Attack
            "mattack", SqlType.Int64 <| int64 stat.MagicAttack

            "defense", SqlType.Int64 <| int64 stat.Defense
            "mdefense", SqlType.Int64 <| int64 stat.MagicDefense
            "champId", SqlType.Int64 <| int64 champId
        ]
        |> Db.exec

    /// includes unprocessed requests
    let monstersByTypeSubtype(userId: int64, mtype:MonsterType, msubtype:MonsterSubType) =
        use conn = new SqliteConnection(cs)

        let monsters = 
            Db.newCommand SQL.CountUserMonsters conn
            |> Db.setParams [
                "userId", SqlType.Int64 userId
                "type", SqlType.Int <| int mtype
                "subtype", SqlType.Int <| int msubtype
            ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
                
        let requests =
            Db.newCommand SQL.CountUserRequests conn
            |> Db.setParams [
                "userId", SqlType.Int64 userId
                "type", SqlType.Int <| int mtype
                "subtype", SqlType.Int <| int msubtype
            ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
                    
        match monsters, requests with
        | Some m, Some r -> (m + r) |> Ok
        | Some _, None -> Error("Unable to count requests")
        | None, Some _ -> Error("Unable to count monsters")
        | None, None -> Error("Unable to count monsters and requests")

    let unfinishedRequestsByUser(userId: int64) =
        use conn = new SqliteConnection(cs)

        let requests =
            Db.newCommand SQL.CountUnfinishedUserRequests conn
            |> Db.setParams [
                "userId", SqlType.Int64 userId
            ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
                    
        match requests with
        | Some m -> m |> Ok
        | None -> Error("Unable to count requests")

    let renameChamp(sender:string, tx:string, note:string, champId:uint64, newName:string, amount:decimal) =        
        try
            use conn = new SqliteConnection(cs)
            Db.batch (fun tn ->
                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal amount
                    "key", SqlType.String (DbKeysNum.Rewards.ToString())
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.RenameChamp
                |> Db.setParams [
                    "champId", SqlType.Int64 (int64 champId)
                    "newName", SqlType.String newName
                ]
                |> Db.exec
                    
                tn
                |> Db.newCommandForTransaction SQL.InsertTx
                |> Db.setParams [
                    "tx", SqlType.String tx
                    "wallet", SqlType.String sender
                    "note", SqlType.String note
                    "amount", SqlType.Decimal amount
                    "type", SqlType.Int <| int TxType.RenameChamp
                    "isValid", SqlType.Boolean true
                    "isFinished", SqlType.Boolean true
                    "comment", SqlType.String ""
                ]
                |> Db.exec

                Ok ()
            ) conn
        with exn ->
            Log.Error(exn, $"Rename champ")
            Error exn.Message

    let donate(sender:string, tx:string, note:string, amount:decimal) =
        try
            use conn = new SqliteConnection(cs)
            Db.batch (fun tn ->
                Db.newCommandForTransaction SQL.InsertTx tn
                |> Db.setParams [
                    "tx", SqlType.String tx
                    "wallet", SqlType.String sender
                    "note", SqlType.String note
                    "amount", SqlType.Decimal amount
                    "type", SqlType.Int <| int TxType.Donate
                    "isValid", SqlType.Boolean true
                    "isFinished", SqlType.Boolean true
                    "comment", SqlType.String ""
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal amount
                    "key", SqlType.String (DbKeysNum.Rewards.ToString())
                ]
                |> Db.exec

                Ok (())
            ) conn
        with exn ->
            Log.Error(exn, $"Donate")
            Error exn.Message

    let createGenRequest(sender:string, tx:string, note:string, amount:decimal, mtype:MonsterType, msubtype:MonsterSubType) =
        try
            use conn = new SqliteConnection(cs)
            let prompt = Prompt.createMonsterNameDesc mtype msubtype
            let payload = GenPayload.TextReqCreated prompt
            let json = JsonSerializer.Serialize(payload, jsOptions)
            
            match getUserIdByWallet sender with
            | Some userId ->
                Db.batch (fun tn ->
                    let requestId =
                        tn
                        |> Db.newCommandForTransaction SQL.InitGenRequest
                        |> Db.setParams [
                            "userId", SqlType.Int64 userId
                            "status", SqlType.Int <| int payload.Status
                            "payload", SqlType.String json
                            "cost", SqlType.Decimal amount
                            "type", SqlType.Int <| int mtype
                            "subtype", SqlType.Int <| int msubtype
                        ]
                        |> Db.scalar (fun v -> unbox<int64> v)
                    
                    let txId =
                        tn
                        |> Db.newCommandForTransaction SQL.InsertTx
                        |> Db.setParams [
                            "tx", SqlType.String tx
                            "wallet", SqlType.String sender
                            "note", SqlType.String note
                            "amount", SqlType.Decimal amount
                            "type", SqlType.Int <| int TxType.CreateCustomMonster
                            "isValid", SqlType.Boolean true
                            "isFinished", SqlType.Boolean true
                            "comment", SqlType.String ""
                        ]
                        |> Db.scalar (fun v -> unbox<int64> v)

                    tn
                    |> Db.newCommandForTransaction SQL.AddGenRequestTx
                    |> Db.setParams [
                        "requestId", SqlType.Int64 requestId
                        "txId", SqlType.Int64 txId
                    ]
                    |> Db.exec
                    Ok()        
                ) conn
            | None -> Error("User not found")
        with exn ->
            Log.Error(exn, $"createGenRequest")
            Error exn.Message

    let buyItem(sender:string, tx:string, note:string, amount:decimal, item:ShopItem, itemsAmount:uint32) =
        match getUserIdByWallet sender with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)

                Db.batch (fun tn ->
                    let itemId =
                        tn
                        |> Db.newCommandForTransaction SQL.GetShopItemIdByItem
                        |> Db.setParams [
                            "item", SqlType.Int <| int item
                        ]
                        |> Db.scalar (fun v -> tryUnbox<int64> v)
                    match itemId with
                    | Some iid ->
                        tn
                        |> Db.newCommandForTransaction SQL.AddToStorage
                        |> Db.setParams [
                            "userId", SqlType.Int64 userId
                            "itemId", SqlType.Int64 iid
                            "amount", SqlType.Int <| int itemsAmount
                        ]
                        |> Db.exec

                        tn
                        |> Db.newCommandForTransaction SQL.AddToKeyNum
                        |> Db.setParams [
                            "amount", SqlType.Decimal amount
                            "key", SqlType.String (DbKeysNum.Rewards.ToString())
                        ]
                        |> Db.exec

                        tn
                        |> Db.newCommandForTransaction SQL.InsertTx
                        |> Db.setParams [
                            "tx", SqlType.String tx
                            "wallet", SqlType.String sender
                            "note", SqlType.String note
                            "amount", SqlType.Decimal amount
                            "type", SqlType.Int <| int TxType.BuyItem
                            "isValid", SqlType.Boolean true
                            "isFinished", SqlType.Boolean true
                            "comment", SqlType.String ""
                        ]
                        |> Db.exec
                       
                        Ok(())
                    | None ->
                        Error("Item not found")
                ) conn
            with exn ->
                Log.Error(exn, $"Buy item: {item}, {amount}")
                Error($"Unexpected error: {exn.Message}")
        | None -> Error("User not found")

    let createNFTBasedMonster(sender:string, tx:string, note:string, amount:decimal, requestId:uint64) =
        match getUserIdByWallet sender with
        | Some userId ->
            try
                try
                    for d in userMonstrsInfoByRound.Values do
                        if d.ContainsKey userId then
                            d.Remove userId |> ignore
                with inner ->
                    Log.Error(inner, $"remove monstrs from cache for {userId}")

                use conn = new SqliteConnection(cs)
                
                Db.batch (fun tn ->
                    tn
                    |> Db.newCommandForTransaction SQL.FinishNFTMonsterCreationRequest
                    |> Db.setParams [
                        "rId", SqlType.Int64 <| int64 requestId
                    ]
                    |> Db.exec

                    let nftMonsterId =
                        tn
                        |> Db.newCommandForTransaction SQL.CreateNFTMonsterFromRequest
                        |> Db.setParams [
                            "rId", SqlType.Int64 <| int64 requestId
                        ]
                        |> Db.scalar (fun v -> unbox<int64> v)
                    
                    let monsterId =
                        let monster = Monster.Universal()
                        let stats = Monster.getStats monster
                        
                        Db.newCommandForTransaction SQL.CreateMonsterWithDataFromRequest tn
                        |> Db.setParams [
                            "rId", SqlType.Int64 <| int64 requestId

                            "health", SqlType.Int64 <| int64 stats.Health
                            "magic", SqlType.Int64 <| int64 stats.Magic
                            
                            "accuracy", SqlType.Int64 <| int64 stats.Accuracy
                            "luck", SqlType.Int64 <| int64 stats.Luck
                            "attack", SqlType.Int64 <| int64 stats.Attack
                            "mattack", SqlType.Int64 <| int64 stats.MagicAttack
                            "defense", SqlType.Int64 <| int64 stats.Defense
                            "mdefense", SqlType.Int64 <| int64 stats.MagicDefense
                            "type", SqlType.Int <| int monster.MType
                            "subtype", SqlType.Int <| int monster.MSubType
                        ]
                        |> Db.scalar (fun v -> unbox<int64> v)
                    
                    tn
                    |> Db.newCommandForTransaction SQL.ConnectMonsterToUser
                    |> Db.setParams [ 
                        "monsterId", SqlType.Int64 monsterId
                        "userId", SqlType.Int64 userId
                        "requestId", SqlType.Null
                        "nftMonsterId", SqlType.Int64 nftMonsterId
                    ]
                    |> Db.exec

                    tn
                    |> Db.newCommandForTransaction SQL.AddToKeyNum
                    |> Db.setParams [
                        "amount", SqlType.Decimal amount
                        "key", SqlType.String (DbKeysNum.Rewards.ToString())
                    ]
                    |> Db.exec

                    tn
                    |> Db.newCommandForTransaction SQL.InsertTx
                    |> Db.setParams [
                        "tx", SqlType.String tx
                        "wallet", SqlType.String sender
                        "note", SqlType.String note
                        "amount", SqlType.Decimal amount
                        "type", SqlType.Int <| int TxType.BuyItem
                        "isValid", SqlType.Boolean true
                        "isFinished", SqlType.Boolean true
                        "comment", SqlType.String ""
                    ]
                    |> Db.exec
                       
                    Ok(())
                ) conn
                
            with exn ->
                Log.Error(exn, $"createNFTBasedMonster {requestId}")
                Error($"Unexpected error: {exn.Message}")
        | None -> Error("User not found")

    member _.FindUserIdByWallet(wallet: string) = getUserIdByWallet wallet
    
    member _.FindUserIdByUserId = getUserIdByUserId

    member _.FindDiscordIdByWallet(wallet: string) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetDiscordIdByWallet conn
            |> Db.setParams [ "wallet", SqlType.String <| wallet ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
        with exn ->
            Log.Error(exn, $"FindDiscordIdByWallet: {wallet}")
            None
    
    member _.TryRegisterDiscordUser(discordId:uint64) = registerNewDiscordUser discordId
    member _.UserNameExists(nickname:string) = userNameExists nickname
    member _.GetCustomUserInfoByNickname(nickname:string) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetCustomUserInfoByNickname conn
            |> Db.setParams [ "name", SqlType.String nickname ]
            |> Db.querySingle(fun r ->
                r.GetInt64(0) |> uint64,
                r.GetString(1))
        with exn ->
            Log.Error(exn, $"GetCustomUserInfoByNickname: {nickname}")
            None
    member _.TryRegisterCustomUser(nickname:string, password:string) =
        registerNewCustomUser(nickname, password)
    member _.UpdatePassword(cId:int64, password:string) =
        if String.IsNullOrWhiteSpace(password) then None
        else
            try 
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.UpdatePassword conn
                |> Db.setParams [
                    "password", SqlType.String <| password
                    "cId", SqlType.Int64 cId
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v)
            with exn ->
                Log.Error(exn, $"UpdatePassword: {cId}")
                None
    member _.TryRegisterWeb3User(wallet:string) =
        try
            use conn = new SqliteConnection(cs)
            let walletExists =
                Db.newCommand SQL.PrimaryWalletAlreadyExists conn
                |> Db.setParams [ "wallet", SqlType.String wallet ]
                |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
            if walletExists then Error("Wallet already registered")
            else
                Db.batch(fun tn ->
                    let uIdO =
                        Db.newCommandForTransaction SQL.AddNewWeb3User tn
                        |> Db.setParams [ 
                            "wallet", SqlType.String wallet
                        ]
                        |> Db.scalar (fun v -> tryUnbox<int64> v)
                    match uIdO with
                    | Some uId ->
                        Db.newCommandForTransaction SQL.RegisterNewWeb3Wallet tn
                        |> Db.setParams [ 
                            "userId", SqlType.Int64 uId
                            "wallet", SqlType.String wallet
                        ]
                        |> Db.exec
                        Ok uId
                    | None ->
                        tn.Rollback()
                        Error("Unexpected error")
                ) conn
        with exn ->
            Log.Error(exn, $"registerNewCustomUser: {wallet}")
            Error("Unexpected error")

    member _.RegisterNewWallet(uId:UserId, wallet:string) =
        let isRegistered =
            match uId with
            | UserId.Discord dId -> userExists uId || registerNewDiscordUser dId
            | UserId.Custom _
            | UserId.Web3 _ -> userExists uId
        if isRegistered then
            let isWalletExists = walletExists wallet
            if isWalletExists then
                Error("This wallet already registered")
            else
                let userId = getUserIdByUserId uId
                match userId with
                | Some id ->
                    try
                        // ToDo: improve
                        let code = System.Random.Shared.NextInt64(10000L, 99999L) |> string
                        use conn = new SqliteConnection(cs)
                        if uId.IsWeb3 |> not then
                            Db.newCommand SQL.RegisterNewWallet conn
                            |> Db.setParams [ 
                                "userId", SqlType.Int64 id
                                "wallet", SqlType.String wallet
                                "code", SqlType.String code
                            ]
                            |> Db.exec
                        Ok(code)
                    with exn ->
                        Log.Error(exn, $"RegisterNewWallet: {userId}, {wallet}")
                        Error("Something went wrong: unable to register wallet")              
                | None -> Error("Can't find user")
        else Error("User doesn't exist")
    
    member _.DeactivateWallet(uId:UserId, wallet:string) =
        let isRegistered =
            match uId with
            | UserId.Discord dId -> userExists uId || registerNewDiscordUser dId
            | UserId.Custom _ -> userExists uId
            | UserId.Web3 _ -> userExists uId
        if isRegistered then
            let isWalletExists = walletExists wallet
            if isWalletExists then
                let userId = getUserIdByUserId uId
                match userId with
                | Some _ ->
                    try
                        use conn = new SqliteConnection(cs)
                        Db.newCommand SQL.DeactivateWallet conn
                        |> Db.setParams [
                            "wallet", SqlType.String wallet
                        ]
                        |> Db.exec
                        Ok()
                    with exn ->
                        Log.Error(exn, $"DeactivateWallet: {userId}, {wallet}")
                        Error("Something went wrong: unable to deactivate wallet")              
                | None -> Error("Can't find user")
            else
                Error("This wallet either not registered or deactivated")
        else Error("User doesn't exist")

    member _.SaveNonce(wallet: string, nonce: string, expiresAt: DateTime) =
        try 
            use conn = new SqliteConnection(cs)
            let expiresUnix = DateTimeOffset(expiresAt).ToUnixTimeSeconds()
            conn
            |> Db.newCommand SQL.SaveNonce
            |> Db.setParams [ 
                "wallet", SqlType.String wallet
                "nonce", SqlType.String nonce
                "expiresAt", SqlType.Int64 expiresUnix 
            ]
            |> Db.exec
            Ok(())
        with exn ->
            Log.Error(exn, "SaveNonce")
            Error("Unexpected error")

    member _.GetNonce(wallet: string) : (string * DateTime) option =
        try 
            use conn = new SqliteConnection(cs)
            conn
            |> Db.newCommand SQL.GetNonceByWallet
            |> Db.setParams [ "wallet", SqlType.String wallet ]
            |> Db.querySingle (fun rd ->
                rd.ReadString "Nonce",
                DateTimeOffset.FromUnixTimeSeconds(rd.ReadInt64 "ExpiresAt").UtcDateTime)
        with exn ->
            Log.Error(exn, "GetNonce")
            None

    member _.DeleteNonce(wallet: string) =
        try 
            use conn = new SqliteConnection(cs)
            conn
            |> Db.newCommand SQL.DeleteNonce
            |> Db.setParams [ "wallet", SqlType.String wallet ]
            |> Db.exec
            Ok(())
        with exn ->
            Log.Error(exn, "DeleteNonce")
            Error("Unexpected error")

    member _.PurgeExpiredNonces() =
        try
            let now = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            use conn = new SqliteConnection(cs)
            conn
            |> Db.newCommand "DELETE FROM Web3Nonces WHERE ExpiresAt < @now"
            |> Db.setParams [ "now", SqlType.Int64 now ]
            |> Db.exec
            Ok(())
        with exn ->
            Log.Error(exn, "PurgeExpiredNonces")
            Error("Unexpected error")

    member _.WalletIsConfirmed(wallet:string) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.ConfirmedWalletExists conn
            |> Db.setParams [ 
                "wallet", SqlType.String wallet
            ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, $"WalletIsConfirmed: {wallet}")
            false 

    member _.UnConfirmedWalletExists() =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.UnConfirmedWalletExists conn
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, "SQL.UnConfirmedWalletExists")
            false

    member _.ConfirmWallet(wallet:string, code:string) =
        if confirmationCodeIsMatchedForWallet(wallet, code) then
            try 
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.ConfirmWallet conn
                |> Db.setParams [ "wallet", SqlType.String wallet ]
                |> Db.exec
                true
            with exn ->
                Log.Error(exn, $"Confirm wallet: {wallet}, {code}")
                false            
        else false

    member _.ConfirmedUserByDiscordId(discordId: uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.ConfirmedActiveWalletExistsByDiscordId conn
            |> Db.setParams [ "discordId", SqlType.Int64 <| int64 discordId ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
            |> Ok
        with exn ->
            Log.Error(exn, $"ConfirmedUserByDiscordId: {discordId}")
            Error("Unexpected error")

    member _.ChampExists(assetId: uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.ChampExists conn
            |> Db.setParams [ "assetId", SqlType.Int64 <| int64 assetId ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
            |> Ok
        with exn ->
            Log.Error(exn, $"ChampExists: {assetId}")
            Error("Unexpected error")

    member _.UpdateUserForChamp(userId:uint64, assetId:uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.UpsertChampAndUser conn
            |> Db.setParams [ 
                "userId", SqlType.Int64 <| int64 userId
                "assetId", SqlType.Int64 <| int64 assetId
            ]
            |> Db.exec
            Ok(())
        with exn ->
            Log.Error(exn, $"UpdateUserForChamp: {userId}, {assetId}")
            Error("Something went wrong: unable to attach champ to user")              
    
    member _.GetUserWallets(uId:UserId) =
        match getUserIdByUserId uId with
        | Some id ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserWallets conn
                |> Db.setParams [
                    "userId", SqlType.Int64 id
                ]
                |> Db.query(fun r -> Wallet(r.GetString(0), r.GetBoolean(1), r.GetString(3), r.GetBoolean(2)))
                |> Ok
            with exn ->
                Log.Error(exn, $"GetUserWallets: {id}")
                Error("Something went wrong")              
        | None -> Error("Can't find user")    
    
    member _.AddOrInsertChamp(champ:NewChampDb) =
        try
            use conn = new SqliteConnection(cs)
            Db.batch (fun tn ->
                let champId = 
                    tn
                    |> Db.newCommandForTransaction SQL.CreateChamp
                    |> Db.setParams [ 
                        "name", SqlType.String champ.Name
                        "assetId", SqlType.Int64 <| int64 champ.AssetId
                        "ipfs", SqlType.String champ.IPFS
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                match champId with
                | Some cid ->
                    tn
                    |> Db.newCommandForTransaction SQL.ConnectChampToUser
                    |> Db.setParams [ 
                        "champId", SqlType.Int64 cid
                        "userId", SqlType.Int64 <| int64 champ.UserId
                    ]
                    |> Db.exec

                    tn
                    |> Db.newCommandForTransaction SQL.CreateChampStat
                    |> Db.setParams [ 
                        "champId", SqlType.Int64 cid

                        "health", SqlType.Int64 <| int64 champ.Stats.Health
                        "magic", SqlType.Int64 <| int64 champ.Stats.Magic
                            
                        "accuracy", SqlType.Int64 <| int64 champ.Stats.Accuracy
                        "luck", SqlType.Int64 <| int64 champ.Stats.Luck
                        "attack", SqlType.Int64 <| int64 champ.Stats.Attack
                        "mattack", SqlType.Int64 <| int64 champ.Stats.MagicAttack
                        "defense", SqlType.Int64 <| int64 champ.Stats.Defense
                        "mdefense", SqlType.Int64 <| int64 champ.Stats.MagicDefense
                    ]
                    |> Db.exec

                    tn
                    |> Db.newCommandForTransaction SQL.UpsertChampTrait
                    |> Db.setParams [
                        "champId", SqlType.Int64 cid
                        "background", SqlType.Int <| int champ.Traits.Background 
                        "skin", SqlType.Int <| int champ.Traits.Skin
                        "weapon", SqlType.Int <| int champ.Traits.Weapon
                        "magic", SqlType.Int <| int champ.Traits.Magic
                        "head", SqlType.Int <| int champ.Traits.Head
                        "armour", SqlType.Int <| int champ.Traits.Armour
                        "extra", SqlType.Int <| int champ.Traits.Extra
                    ]
                    |> Db.exec
                    true
                | None -> tn.Rollback(); false
            ) conn
        with exn ->
            Log.Error(exn, $"AddOrInsertChamp: {champ}")
            false    
    
    member _.CreateNewMonster(monster:MonsterRecord) =
        createNewMonster(cs, monster)

    /// In case when tx are send by mistake or already handled
    member _.ProcessParsedRawTx(ptx:ParsedTx) =
        try
            use conn = new SqliteConnection(cs)
            let txExists =
                Db.newCommand SQL.TxExists conn
                |> Db.setParams [
                    "tx", SqlType.String ptx.TxId
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
            if txExists then Ok(None)
            else
                // edge case: donation, explicit intentions
                let isDonation =
                    let note = ptx.Note
                    note.Contains "donate" || note.Contains "dnt" || note.Contains "donation" ||
                    note.Contains "gift"
                if isDonation then
                    Db.batch (fun tn ->
                        Db.newCommandForTransaction SQL.InsertTx tn
                        |> Db.setParams [
                            "tx", SqlType.String ptx.TxId
                            "wallet", SqlType.String ptx.Sender
                            "note", SqlType.String ptx.Note
                            "amount", SqlType.Decimal ptx.Amount
                            "type", SqlType.Int <| int TxType.Donate
                            "isValid", SqlType.Boolean true
                            "isFinished", SqlType.Boolean true
                            "comment", SqlType.String ""
                        ]
                        |> Db.exec

                        tn
                        |> Db.newCommandForTransaction SQL.AddToKeyNum
                        |> Db.setParams [
                            "amount", SqlType.Decimal ptx.Amount
                            "key", SqlType.String (DbKeysNum.Rewards.ToString())
                        ]
                        |> Db.exec

                        Some TxType.Donate
                    ) conn
                else
                    Db.newCommand SQL.InsertTx conn
                    |> Db.setParams [
                        "tx", SqlType.String ptx.TxId
                        "wallet", SqlType.String ptx.Sender
                        "note", SqlType.String ptx.Note
                        "amount", SqlType.Decimal ptx.Amount
                        "type", SqlType.Int <| int TxType.Unknown
                        "isValid", SqlType.Boolean false
                        "isFinished", SqlType.Boolean false
                        "comment", SqlType.String ""
                    ]
                    |> Db.exec
                    Some TxType.Unknown
                |> Ok
            with exn ->
                Log.Error(exn, $"ProcessParsedValidTx: {ptx}")
                Error "Unexpected error"

    member _.IsTxValid(uId:UserId, tx:Tx, ?amountO:decimal) =
        let validatePrice (conn:SqliteConnection) (price:decimal) =
            match amountO with
            | Some amount ->
                let darkCoinPriceO = getKeyNum conn DbKeysNum.DarkCoinPrice
                let darkCoinPriceOldO =
                    getKeyNum conn DbKeysNum.DarkCoinPriceOld
                    |> Option.orElse darkCoinPriceO
                match darkCoinPriceO, darkCoinPriceOldO with
                | Some cPrice, Some oPrice ->
                    let expectedAmount = Math.Round(price / cPrice, 6)
                    let fallbackAmount = Math.Round(price / oPrice, 6)
                    let isValidAmount =
                        Utils.isCloseEnough expectedAmount amount ||
                        Utils.isCloseEnough fallbackAmount amount

                    if isValidAmount then
                        Ok(())
                    else
                        Error("Amount not valid")
                | _ -> Error("Unexpected error")
            | None -> Ok(())            
        if tx.IsConfirm then Ok (())
        else
            match getUserIdByUserId uId with
            | Some userId ->
                try
                    use conn = new SqliteConnection(cs)
                    let isWalletValid =
                        Db.newCommand SQL.WalletBelongsToAUser conn
                        |> Db.setParams [
                            "wallet", SqlType.String tx.Wallet
                            "userId", SqlType.Int64 userId
                        ]
                        |> Db.scalar (fun v -> (unbox<int64> v) > 0)
                    if isWalletValid then
                        match tx with
                        | Tx.Confirm _ -> Ok (())
                        | Tx.RenameChamp(_, champId, nName) ->
                            if String.IsNullOrWhiteSpace nName then
                                Error ("Name is empty!")
                            elif nName.Trim().Length < 3 then
                                Error ("Name is too short!")
                            else
                                let belongsToAUserO =
                                    Db.newCommand SQL.ChampBelongsToAUser conn
                                    |> Db.setParams [
                                        "champId", SqlType.Int64 <| int64 champId
                                        "userId", SqlType.Int64 <| int64 userId
                                    ]
                                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
                                match belongsToAUserO with
                                | Some b ->
                                    if b then
                                        let isNameAlreadyTaken = 
                                            Db.newCommand SQL.IsChampNameExists conn
                                            |> Db.setParams [
                                                "name", SqlType.String nName
                                            ]
                                            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                                        if isNameAlreadyTaken then Error("Name isn't unique, try something else")
                                        else validatePrice conn Shop.RenamePrice
                                    else Error("Doesn't belong to you")
                                | None -> Error("Unexpected error")
                        | Tx.Donate(_, amount) ->
                            if amount > 0M then Ok(())
                            else Error("Invalid amount")
                        | Tx.CreateCustomMonster(_, mtype, msubtype) ->
                            if mtype = MonsterType.Universal then Error "Unexpected monster type"
                            else
                                let monstersCreatedR = monstersByTypeSubtype(userId, mtype, msubtype)
                                let pendingRequestsR = unfinishedRequestsByUser(userId)
                                match monstersCreatedR, pendingRequestsR with
                                | Ok m, Ok r when m < Limits.CustomMonstersPerTypeSubtype && r < Limits.UnfinishedRequests ->
                                    validatePrice conn Shop.GenMonsterPrice
                                | Ok m, Ok r ->
                                    let sb = StringBuilder()
                    
                                    if m >= Limits.CustomMonstersPerTypeSubtype then
                                        sb.AppendLine $"Max amount of custom monsters for this type and subtype reached: {m} >= {Limits.CustomMonstersPerTypeSubtype}"
                                        |> ignore
                    
                                    if r >= Limits.UnfinishedRequests then
                                        sb.AppendLine $"Max amount of pending requests reached: {r} >= {Limits.UnfinishedRequests}"
                                        |> ignore

                                    Error (sb.ToString())
                                | Error err, _ 
                                | _, Error err -> Error err
                        | Tx.BuyItem _ ->
                            // TODO: validate price
                            Ok (())
                        | Tx.CreateNFTBasedCustomMonster(_, requestId) ->
                            let requestBelongsToAUser =
                                Db.newCommand SQL.NFTMonsterCreationRequestBelongsToAUser conn
                                |> Db.setParams [
                                    "userId", SqlType.Int64 userId
                                    "rId", SqlType.Int64 <| int64 requestId
                                ]
                                |> Db.scalar (fun v -> (unbox<int64> v) = 1L)
                            if requestBelongsToAUser then
                                validatePrice conn Shop.GenMonsterPrice
                            else
                                Error ("Invalid wallet or requestId")
                    else
                        Error("Invalid wallet")
                    with exn ->
                        Log.Error(exn, $"IsTxValid: {uId} | {tx}")
                        Error(exn.Message)
            | None -> Error("Can't find user")

    member _.ProcessParsedValidTx(ptx:ParsedTx) =
        try
            use conn = new SqliteConnection(cs)
            let txExists =
                Db.newCommand SQL.TxExists conn
                |> Db.setParams [
                    "tx", SqlType.String ptx.TxId
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
            if txExists then Ok ()
            else
                match ptx.Tx with
                | Some tx ->
                    let res =
                        match tx with
                        | Tx.Confirm _ -> Error ("Not supported")
                        | Tx.RenameChamp(wallet, champId, nName) ->
                            renameChamp(wallet, ptx.TxId, ptx.Note, champId, nName, ptx.Amount)
                        | Tx.Donate(wallet, amount) ->
                            if Utils.isCloseEnough ptx.Amount amount then
                                donate(wallet, ptx.TxId, ptx.Note, ptx.Amount)
                            else Error($"Tx amount doesn't match: {ptx.Amount} & {amount}")
                        | Tx.CreateCustomMonster(wallet, mtype, msubtype) ->
                            createGenRequest(wallet, ptx.TxId, ptx.Note, ptx.Amount, mtype, msubtype)
                        | Tx.BuyItem(wallet, item, amount) ->
                            buyItem(wallet, ptx.TxId, ptx.Note, ptx.Amount, item, amount)
                        | Tx.CreateNFTBasedCustomMonster(wallet, requestId) ->
                            createNFTBasedMonster(wallet, ptx.TxId, ptx.Note, ptx.Amount, requestId)
                    match res with
                    | Ok () -> res
                    | Error err ->
                        try
                            Db.newCommand SQL.InsertTx conn
                            |> Db.setParams [
                                "tx", SqlType.String ptx.TxId
                                "wallet", SqlType.String ptx.Sender
                                "note", SqlType.String ptx.Note
                                "amount", SqlType.Decimal ptx.Amount
                                "type", SqlType.Int (TxType.fromTx tx |> int)
                                "isValid", SqlType.Boolean false
                                "isFinished", SqlType.Boolean false
                                "comment", SqlType.String err
                            ]
                            |> Db.exec

                            res
                        with exn ->
                            Log.Error(exn, $"ProcessParsedValidTx, error case: {ptx}")
                            Error exn.Message
                | None ->
                    Db.newCommand SQL.InsertTx conn
                    |> Db.setParams [
                        "tx", SqlType.String ptx.TxId
                        "wallet", SqlType.String ptx.Sender
                        "note", SqlType.String ptx.Note
                        "amount", SqlType.Decimal ptx.Amount
                        "type", SqlType.Int <| int TxType.Unknown
                        "isValid", SqlType.Boolean false
                        "isFinished", SqlType.Boolean false
                        "comment", SqlType.String "Empty TX"
                    ]
                    |> Db.exec

                    Ok (())          
            with exn ->
                Log.Error(exn, $"ProcessParsedValidTx: {ptx}")
                Error exn.Message                  

    member _.GetShopItems() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetShopItems conn
            |> Db.query (fun r -> r.GetInt32(0) |> enum<ShopItem>)
            |> Some
        with exn ->
            Log.Error(exn, $"GetShopItem")
            None

    member _.GetEffects() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetEffects conn
            |> Db.query (fun r -> r.GetInt32(0) |> enum<Effect>)
            |> Some
        with exn ->
            Log.Error(exn, $"GetEffects")
            None

    member _.GetChampsCount() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetChampsCount conn
            |> Db.scalar (fun v -> tryUnbox<int64> v)
        with exn ->
            Log.Error(exn, $"GetChampsCount")
            None

    member _.GetUserChampsCount(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserChampsCount conn
                |> Db.setParams [
                    "userId", SqlType.Int64 <| int64 userId
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v)
            with exn ->
                Log.Error(exn, $"GetUserChampsCount: {uId}")
                None
        | None -> None

    member _.GetUserMonstersCount(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserMonstersCount conn
                |> Db.setParams [
                    "userId", SqlType.Int64 <| int64 userId
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v)
            with exn ->
                Log.Error(exn, $"GetUserMonstersCount: {uId}")
                None
        | None -> None

    member _.GetUserRequestsCount(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserRequestsCount conn
                |> Db.setParams [
                    "userId", SqlType.Int64 <| int64 userId
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v)
            with exn ->
                Log.Error(exn, $"GetUserRequestsCount: {uId}")
                None
        | None -> None

    member _.UpdateCfgForChamp(champAssetId:uint64, ipfs:string, t:Traits) =
        try
            use conn = new SqliteConnection(cs)
            let champIdO =
                Db.newCommand SQL.GetChampIdByAssetId conn
                |> Db.setParams [
                    "assetId", SqlType.Int64 <| int64 champAssetId
                ]
                |> Db.querySingle (fun r -> r.GetInt64(0))
            match champIdO with
            | Some champId ->
                Db.batch(fun tn ->
                    Db.newCommandForTransaction SQL.UpdateIpfsByChampId tn
                    |> Db.setParams [
                        "ipfs", SqlType.String ipfs
                        "champId", SqlType.Int64 champId
                    ]
                    |> Db.exec
                    
                    tn
                    |> Db.newCommandForTransaction SQL.UpsertChampTrait
                    |> Db.setParams [
                        "champId", SqlType.Int64 champId
                        "background", SqlType.Int <| int t.Background 
                        "skin", SqlType.Int <| int t.Skin
                        "weapon", SqlType.Int <| int t.Weapon
                        "magic", SqlType.Int <| int t.Magic
                        "head", SqlType.Int <| int t.Head
                        "armour", SqlType.Int <| int t.Armour
                        "extra", SqlType.Int <| int t.Extra
                    ]
                    |> Db.exec
                ) conn
                Ok(())
            | None ->
                Error("Champ doesn't exist ")
        with exn ->
            Log.Error(exn, $"GetChampsCount")
            Error("Unexpected error")

    member _.GetUserStorage(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserStorage conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.query (fun r ->
                    r.GetInt32(0) |> enum<ShopItem>,
                    r.GetInt32(1))
                |> Some
            with exn ->
                Log.Error(exn, $"GetUserStorage: {uId}")
                None
        | None -> None

    member x.GetUserChampsRaw(userId: int64) =
        try
            let getChampsInfo(roundId:int64) =
                use conn = new SqliteConnection(cs)
                let lvls =
                    Db.newCommand SQL.GetLvlsForUserChamps conn
                    |> Db.setParams [
                        "userId", SqlType.Int64 userId
                    ]
                    |> Db.query(fun r -> {|
                        ChampId = uint64 <| r.GetInt64(0)
                        Characteristic = enum<Characteristic> <| r.GetInt32(1)
                    |})
                    |> List.groupBy(fun v -> v.ChampId)
                    |> dict
                    |> Seq.map(fun kv ->
                        kv.Key,
                        kv.Value |> List.countBy(fun l -> l.Characteristic) |> dict |> Levels.statFromCharacteristics)
                    |> Map.ofSeq

                let boosts =
                    Db.newCommand SQL.GetBoostsForUserChampsAtRound conn
                    |> Db.setParams [
                        "userId", SqlType.Int64 userId
                        "roundId", SqlType.Int64 roundId
                    ]
                    |> Db.query(fun r -> {|
                        ChampId = uint64 <| r.GetInt64(0)
                        RoundBoost = {
                            StartRoundId = r.GetInt64(1)
                            Boost = enum<ShopItem> <| r.GetInt32(2)
                            Duration = r.GetInt32(3)
                        }
                    |})
                    |> List.groupBy(fun v -> v.ChampId)
                    |> List.map(fun (k,v) ->
                        k, 
                        v
                        |> List.map(fun r -> r.RoundBoost)
                        |> List.fold(fun stat boost ->
                                Battle.processShopItem stat boost roundId) Stat.Zero)
                    |> Map.ofList

                Db.newCommand SQL.GetUserChamps conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.query (fun r ->
                    let cId = uint64 <| r.GetInt64(0)
                    let bStat =
                        {
                            Health = r.GetInt64(3)
                            Magic = r.GetInt64(4)

                            Accuracy = r.GetInt64(5)
                            Luck = r.GetInt64(6)

                            Attack = r.GetInt64(7)
                            MagicAttack = r.GetInt64(8)

                            Defense = r.GetInt64(9)
                            MagicDefense = r.GetInt64(10)
                        }
                    let xp = uint64 (r.GetInt64(11))
                    let lStat = Map.tryFind cId lvls |> Option.defaultValue (Stat.Zero)
                    let bsStat = Map.tryFind cId boosts |> Option.defaultValue (Stat.Zero)
                    ChampInfoWithStat(cId, r.GetString(1), r.GetString(2), xp, bStat + lStat + bsStat))

            let roundId = x.GetLastRoundId() |> Option.defaultValue 0UL |> int64
            
            // remove all outdated records
            for key in userChampsInfoByRound.Keys |> Seq.filter(fun r -> r < roundId) do
                userChampsInfoByRound.Remove key |> ignore

            match userChampsInfoByRound.TryGetValue roundId with
            | true, dct ->
                match dct.TryGetValue userId with
                | true, champs -> champs
                | false, _ ->
                    let info = getChampsInfo roundId
                    dct.Add (userId, info)
                    info
            | false, _ ->
                let info = getChampsInfo roundId
                let d = Dictionary<int64, ChampInfoWithStat list>()
                d.Add (userId, info)
                userChampsInfoByRound.TryAdd(roundId, d) |> ignore
                info
            |> Ok
        with exn ->
            Log.Error(exn, $"GetUserChampsRaw {userId}")
            Error("Unexpected error")
    
    member x.GetUserChamps(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            x.GetUserChampsRaw userId
        | None -> Error("Unable to find user")

    member x.GetUserMonstrsRaw(userId: int64) =
        try
            let getMonstrsInfo() =
                use conn = new SqliteConnection(cs)
                
                Db.newCommand SQL.GetUserMonstersFull conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.query (fun r ->
                    let gtype =
                        if r.IsDBNull(15) then MonsterGenType.Generative
                        else
                            MonsterGenType.NFTBased(
                                r.GetInt64(15) |> uint64,
                                r.GetString(16))
                    let mi =
                        MonsterInfo(
                            uint64 <| r.GetInt64(0),
                            uint64 <| r.GetInt64(1),
                            r.GetString(2),
                            r.GetString(3),
                            JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData r 4, jsOptions),
                            {
                                Health = r.GetInt64(5)
                                Magic = r.GetInt64(6)

                                Accuracy = r.GetInt64(7)
                                Luck = r.GetInt64(8)

                                Attack = r.GetInt64(9)
                                MagicAttack = r.GetInt64(10)

                                Defense = r.GetInt64(11)
                                MagicDefense = r.GetInt64(12)
                            },
                            enum<MonsterType> <| r.GetInt32(13),
                            enum<MonsterSubType> <| r.GetInt32(14),
                            Some (uint64 userId), gtype)
                    let monsterLvl = Levels.getLvlByXp mi.XP
                    let monsterLvlStats = Monster.getMonsterStatsByLvl(mi.MType, mi.MSubType, monsterLvl)
                    mi.WithStat(mi.Stat + monsterLvlStats))

            let roundId = x.GetLastRoundId() |> Option.defaultValue 0UL |> int64
            
            // remove all outdated records
            for key in userMonstrsInfoByRound.Keys |> Seq.filter(fun r -> r < roundId) do
                userMonstrsInfoByRound.Remove key |> ignore

            match userMonstrsInfoByRound.TryGetValue roundId with
            | true, dct ->
                match dct.TryGetValue userId with
                | true, monstrs -> monstrs
                | false, _ ->
                    let info = getMonstrsInfo()
                    dct.Add (userId, info)
                    info
            | false, _ ->
                let info = getMonstrsInfo()
                let d = Dictionary<int64, MonsterInfo list>()
                d.Add (userId, info)
                userMonstrsInfoByRound.TryAdd(roundId, d) |> ignore
                info
            |> Ok
        with exn ->
            Log.Error(exn, $"GetUserMonstrsRaw {userId}")
            Error("Unexpected error")

    member _.GetUserInfoRaw(userId:int64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetUserInfo conn
            |> Db.setParams [
                "userId", SqlType.Int64 userId
            ]
            |> Db.querySingle(fun r ->
                if r.IsDBNull(0) |> not then
                    UserType.Discord(r.GetInt64(0) |> uint64)
                elif r.IsDBNull(1) |> not then
                    UserType.Custom(r.GetString(1))
                else
                    UserType.Web3(r.GetString(2)))
        with exn ->
            Log.Error(exn, $"GetUserInfoRaw {userId}")
            None

    member _.GetUserChampsInfo(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserChampsInfo conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.query (fun r ->
                    ChampShortInfo(uint64 <| r.GetInt64(0), r.GetString(1), r.GetString(2),
                        uint64 <| r.GetInt64(3)), r.GetDecimal(4))
                |> Some
            with exn ->
                Log.Error(exn, $"GetUserChampsInfo {uId}")
                None
        | None -> None  

    member _.GetActiveUserChamps(uId: UserId, roundId: uint64) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                let roundId' = int64 roundId
                use conn = new SqliteConnection(cs)
                // TODO: cache this for fast lookup
                let lvls =
                    Db.newCommand SQL.GetLvlsForUserChamps conn
                    |> Db.setParams [
                        "userId", SqlType.Int64 <| int64 userId
                    ]
                    |> Db.query(fun r -> {|
                        ChampId = uint64 <| r.GetInt64(0)
                        Characteristic = enum<Characteristic> <| r.GetInt32(1)
                    |})
                    |> List.groupBy(fun v -> v.ChampId)
                    |> dict
                    |> Seq.map(fun kv ->
                        kv.Key,
                        kv.Value |> List.countBy(fun l -> l.Characteristic) |> dict |> Levels.statFromCharacteristics)
                    |> Map.ofSeq

                let boosts =
                    Db.newCommand SQL.GetBoostsForUserChampsAtRound conn
                    |> Db.setParams [
                        "userId", SqlType.Int64 <| int64 userId
                        "roundId", SqlType.Int64 roundId'
                    ]
                    |> Db.query(fun r -> {|
                        ChampId = uint64 <| r.GetInt64(0)
                        RoundBoost = {
                            StartRoundId = r.GetInt64(1)
                            Boost = enum<ShopItem> <| r.GetInt32(2)
                            Duration = r.GetInt32(3)
                        }
                    |})
                    |> List.groupBy(fun v -> v.ChampId)
                    |> List.map(fun (k,v) ->
                        k, 
                        v
                        |> List.map(fun r -> r.RoundBoost)
                        |> List.fold(fun stat boost ->
                                Battle.processShopItem stat boost roundId') Stat.Zero)
                    |> Map.ofList

                Db.newCommand SQL.GetActiveUserChamps conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query (fun r ->
                    let cId = uint64 <| r.GetInt64(0)
                    let bStat =
                        {
                            Health = r.GetInt64(3)
                            Magic = r.GetInt64(4)

                            Accuracy = r.GetInt64(5)
                            Luck = r.GetInt64(6)

                            Attack = r.GetInt64(7)
                            MagicAttack = r.GetInt64(8)

                            Defense = r.GetInt64(9)
                            MagicDefense = r.GetInt64(10)
                        }
                    let xp = uint64 (r.GetInt64(11))
                    let lStat = Map.tryFind cId lvls |> Option.defaultValue (Stat.Zero)
                    let bsStat = Map.tryFind cId boosts |> Option.defaultValue (Stat.Zero)
                    ChampInfoWithStat(cId, r.GetString(1), r.GetString(2), xp, bStat + lStat + bsStat))
                |> Ok
            with exn ->
                Log.Error(exn, $"GetActiveUserChamps {uId}")
                Error("Unexpected error")
        | None -> Error("Unable to find user")

    member x.GetAvailableUserChamps(uId: UserId): Result<ChampInfoWithStat list, string> =
        match x.GetLastRoundId() with
        | Some roundId ->
            match x.GetRoundStatus roundId with
            | Some status ->
                if status = RoundStatus.Started then
                    x.GetActiveUserChamps(uId, roundId)
                else
                    Error("Please wait until new round is started")
            | None -> Error("Something went wrong - unable to get round status")
        | None -> Error("Something went wrong - unable to get round. Maybe there is no any?")

    member _.GetUserChampsUnderEffect(uId: UserId, roundId: uint64) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserChampsUnderEffect  conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query (fun r ->
                   let endsAt = (r.GetInt64(2)) + int64 (r.GetInt32(3))
                   ChampUnderEffect(r.GetInt64(0), r.GetString(1), endsAt, r.GetInt32(4) |> enum<Effect>, endsAt - int64 roundId, r.GetString(5))
                )
                |> Ok
            with exn ->
                Log.Error(exn, $"GetUserChampsUnderEffect {uId} at {roundId}")
                Error("Unexpected error")
        | None -> Error("Unable to find user")

    member _.GetDefeatedChamps(roundId: uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetDefeatedChamps  conn
            |> Db.setParams [
                "roundId", SqlType.Int64 <| int64 roundId
            ]
            |> Db.query (fun r ->
                let endsAt = (r.GetInt64(2)) + int64 (r.GetInt32(3))
                ChampUnderEffect(r.GetInt64(0), r.GetString(1), endsAt, Effect.Death, endsAt - int64 roundId, r.GetString(4))
            )
            |> Ok
        with exn ->
            Log.Error(exn, $"GetDefeatedChamps")
            Error("Unexpected error")

    //member _.GetMonstersUnderEffect(roundId: uint64) =
    //    try
    //        use conn = new SqliteConnection(cs)
    //        Db.newCommand SQL.GetMonstersUnderEffect  conn
    //        |> Db.setParams [
    //            "roundId", SqlType.Int64 <| int64 roundId
    //        ]
    //        |> Db.query (fun r ->
    //            let endsAt = (r.GetInt64(4)) + int64 (r.GetInt32(5))
    //            MonsterUnderEffect(r.GetInt64(0), r.GetString(1),
    //                enum<MonsterType> <| r.GetInt32(2), enum<MonsterSubType> <| r.GetInt32(3),
    //                endsAt, r.GetInt32(6) |> enum<Effect>, endsAt - int64 roundId,
    //                JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData r 7, jsOptions))
    //        )
    //        |> Ok
    //    with exn ->
    //        Log.Error(exn, $"GetMonstersUnderEffect at {roundId}")
    //        Error("Unexpected error")

    member _.GetDefeatedMonsters(roundId: uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetDefeatedMonsters  conn
            |> Db.setParams [
                "roundId", SqlType.Int64 <| int64 roundId
            ]
            |> Db.query (fun r ->
                let endsAt = (r.GetInt64(4)) + int64 (r.GetInt32(5))
                MonsterUnderEffect(r.GetInt64(0), r.GetString(1),
                    enum<MonsterType> <| r.GetInt32(2), enum<MonsterSubType> <| r.GetInt32(3),
                    endsAt, Effect.Death, endsAt - int64 roundId,
                    JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData r 6, jsOptions))
            )
            |> Ok
        with exn ->
            Log.Error(exn, $"GetDefeatedMonsters at {roundId}")
            Error("Unexpected error")

    member _.GetUserChampsWithStats(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserChampsWithStats conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.query (fun r ->
                   {|
                        Name = r.GetString(0)
                        AssetId = r.GetInt64(1)
                        Ipfs = r.GetString(2)
                        Balance = Math.Round(r.GetDecimal(3), 6)
                        XP = r.GetInt64(4) |> uint64

                        Stat = {

                            Health = r.GetInt64(5)
                            Magic = r.GetInt64(6)

                            Accuracy = r.GetInt64(7)
                            Luck = r.GetInt64(8)

                            Attack = r.GetInt64(9)
                            MagicAttack = r.GetInt64(10)

                            Defense = r.GetInt64(11)
                            MagicDefense = r.GetInt64(12)
                        }
                   |}
                )
                |> Some
            with exn ->
                Log.Error(exn, $"GetUserChampsWithStats {uId}")
                None
        | None -> None 

    member _.GetChampInfo(champId: int64): ChampInfo option =
        try
            use conn = new SqliteConnection(cs)
            let lvledChars =
                Db.newCommand SQL.GetLvledCharsForChamp conn
                |> Db.setParams [
                    "champId", SqlType.Int64 champId
                ]
                |> Db.scalar (fun v -> unbox<int64> v)
            let baseChampInfoOpt =
                Db.newCommand SQL.GetChampInfoByID conn
                |> Db.setParams [
                    "champId", SqlType.Int64 champId
                ]
                |> Db.querySingle (fun r ->
                    ChampInfo(
                        uint64 <| r.GetInt64(0),
                        r.GetString(1),
                        r.GetString(2),
                        Math.Round(r.GetDecimal(3), 6),
                        r.GetInt64(4) |> uint64,
                        {
                            Health = r.GetInt64(5)
                            Magic = r.GetInt64(6)

                            Accuracy = r.GetInt64(7)
                            Luck = r.GetInt64(8)

                            Attack = r.GetInt64(9)
                            MagicAttack = r.GetInt64(10)

                            Defense = r.GetInt64(11)
                            MagicDefense = r.GetInt64(12)
                        },
                        {
                            Background = r.GetInt32(13) |> enum<Background>
                            Skin = r.GetInt32(14) |> enum<Skin>
                            Weapon = r.GetInt32(15) |> enum<Weapon>
                            Magic = r.GetInt32(16) |> enum<Magic>
                            Head = r.GetInt32(17) |> enum<Head>
                            Armour = r.GetInt32(18) |> enum<Armour>
                            Extra = r.GetInt32(19) |> enum<Extra>
                        }, None, None, uint64 lvledChars, uint64 <| r.GetInt64(20)))
            
            match baseChampInfoOpt with
            | Some baseChampInfo ->
                Db.newCommand SQL.GetLastRound conn
                |> Db.scalar (fun v -> tryUnbox<int64> v)
                |> Option.map(fun roundId ->
                    let boosts =
                        Db.newCommand SQL.GetBoostsForChampAtRound conn
                        |> Db.setParams [
                            "champId", SqlType.Int64 champId
                            "roundId", SqlType.Int64 <| int64 roundId
                        ]
                        |> Db.query(fun r -> {
                            StartRoundId = r.GetInt64(0)
                            Boost = enum<ShopItem> <| r.GetInt32(1)
                            Duration = r.GetInt32(2)
                            })
                        |> List.fold(fun stat boost ->
                            Battle.processShopItem stat boost (int64 roundId)) Stat.Zero

                    let lvls = 
                        Db.newCommand SQL.GetLvlsForChamp conn
                        |> Db.setParams [
                            "champId", SqlType.Int64 champId
                        ]
                        |> Db.query(fun r -> enum<Characteristic> <| r.GetInt32(0))
                        |> List.countBy(id)
                        |> dict
                        |> Levels.statFromCharacteristics
                    baseChampInfo.WithFullStats(lvls, boosts))
            | None -> None
        with exn ->
            Log.Error(exn, $"GetChampInfo {champId}")
            None

    member _.GetBalances() =
        try
            use conn = new SqliteConnection(cs)
            let poolO = getKeyNum conn DbKeysNum.Rewards
            let reserveO = getKeyNum conn DbKeysNum.Reserve
            let devO = getKeyNum conn DbKeysNum.Dev
            let daoO = getKeyNum conn DbKeysNum.DAO
            let burnO = getKeyNum conn DbKeysNum.Burn
            let champO =
                Db.newCommand SQL.GetChampsBalance conn
                |> Db.querySingle (fun r -> if r.IsDBNull(0) then 0M else r.GetDecimal(0))
            let stakingO = 
                Db.newCommand SQL.GetKeyNum conn
                |> Db.setParams [ "key", SqlType.String (DbKeysNum.Staking.ToString()) ]
                |> Db.querySingle (fun r -> if r.IsDBNull(0) then 0M else r.GetDecimal(0))

            match poolO, reserveO, devO, daoO, burnO, champO, stakingO with
            | Some pool, Some reserve, Some dev, Some dao, Some burn, Some champ, Some staking->
                {
                    DAO = dao
                    Reserve = reserve
                    Dev = dev
                    Burn = burn
                    Rewards = pool
                    Champs = champ
                    Staking = staking
                }
                |> Ok
            | _, _, _, _, _,_,_ ->
                let err = $"rewards: {poolO.IsSome}; Reserve: {reserveO.IsSome}; Dev: {devO.IsSome}; Burn: {burnO.IsSome}; Champ:{champO.IsSome}; Staking:{stakingO.IsSome}"
                Error(err)

        with exn ->
            Log.Error(exn, $"GetBalances")
            Error("Unexpected error")

    member _.GetBattleReward() =
        try
            use conn = new SqliteConnection(cs)
            match getKeyNum conn DbKeysNum.Rewards with
            | Some v -> GameLogic.Rewards.getBattleReward v |> Ok
            | None -> Error("Can't get rewards balance")
        with exn ->
            Log.Error(exn, $"GetBattleReward")
            Error("Unexpected error")

    member _.GetUserEarnings(uId: UserId, startRound:uint64, endRound:uint64) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.UserEarnings conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                    "startRound", SqlType.Int64 <| int64 startRound
                    "endRound", SqlType.Int64 <| int64 endRound
                ]
                |> Db.querySingle (fun r -> if r.IsDBNull(0) then 0M else r.GetDecimal(0))
            with exn ->
                Log.Error(exn, $"GetUserEarnings {uId}: [{startRound}-{endRound}]")
                None
        | None -> None

    member _.GetPendingRewards(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetPendingRewards conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.querySingle (fun r -> if r.IsDBNull(0) then 0M else r.GetDecimal(0))
            with exn ->
                Log.Error(exn, $"GetPendingRewards {uId}")
                None
        | None -> None

    member t.UseItemFromStorage(uId: UserId, item:ShopItem, champId: uint64) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                let itemIdO =
                    Db.newCommand SQL.GetShopItemIdByItem conn
                    |> Db.setParams [
                        "item", SqlType.Int <| int item
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)

                let lastRoundId =
                    Db.newCommand SQL.GetLastRound conn
                    |> Db.scalar (fun v -> tryUnbox<int64> v)

                match itemIdO, lastRoundId with
                | Some itemId, Some roundId ->
                    let amount =
                        Db.newCommand SQL.GetAmountFromStorage conn
                        |> Db.setParams [
                            "userId", SqlType.Int64 userId
                            "itemId", SqlType.Int64 itemId
                        ]
                        |> Db.scalar (fun v -> tryUnbox<int64> v)
                    match amount with
                    | Some v when v > 0 ->
                        let isBoostUsed =
                            Db.newCommand SQL.IsBoostAlreadyUsedAtRound conn
                            |> Db.setParams [
                                "champId", SqlType.Int64 <| int64 champId
                                "itemId", SqlType.Int64 <| int64 itemId
                                "roundId", SqlType.Int64 <| int64 roundId
                            ]
                            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                        if isBoostUsed then
                            Error("You already use this item in this round.")
                        else
                            Db.batch (fun tn ->
                                Db.newCommandForTransaction SQL.AddToExistedStorage tn
                                |> Db.setParams [
                                    "amount", SqlType.Int -1
                                    "userId", SqlType.Int64 userId
                                    "itemId", SqlType.Int64 itemId
                                ]
                                |> Db.exec
                                match item with
                                | ShopItem.RevivalSpell ->
                                    let deathIdO =
                                        Db.newCommandForTransaction SQL.GetEffectItemIdByItem tn
                                        |> Db.setParams [
                                            "item", SqlType.Int <| int Effect.Death
                                        ]
                                        |> Db.scalar (fun v -> tryUnbox<int64> v)
                                    match deathIdO with
                                    | Some deathId ->
                                        let isChampDead =
                                            tn
                                            |> Db.newCommandForTransaction SQL.ChampsIsUnderEffectInRound
                                            |> Db.setParams [
                                                "champId", SqlType.Int64 <| int64 champId
                                                "roundId", SqlType.Int64 <| int64 roundId
                                                "itemId", SqlType.Int64 <| int64 deathId
                                            ]
                                            |> Db.scalar (fun v -> tryUnbox<int64> v)
                                            |> Option.map(fun v -> v > 0)
                                            |> Option.defaultValue false
                                        let isChampDeadAndItemIsRevival = isChampDead && item = ShopItem.RevivalSpell
                                        Db.newCommandForTransaction SQL.InsertToBoosts tn
                                        |> Db.setParams [
                                            "champId", SqlType.Int64 <| int64 champId
                                            "itemId", SqlType.Int64 <| int64 itemId
                                            "roundId", SqlType.Int64 <| int64 roundId
                                            "duration", SqlType.Int (if isChampDeadAndItemIsRevival then 0 else Shop.getRoundDuration(item))
                                        ]
                                        |> Db.exec

                                        if isChampDeadAndItemIsRevival then
                                            tn
                                            |> Db.newCommandForTransaction SQL.MarkEffectsAsPassiveBeforeRound
                                            |> Db.setParams [
                                                "champId", SqlType.Int64 <| int64 champId
                                                "roundId", SqlType.Int64 <| int64 roundId
                                            ]
                                            |> Db.exec
                                        Ok(())
                                    | None ->
                                        tn.Rollback()
                                        Error("Can't find effect itemId")
                                | ShopItem.ElixirOfLife
                                | ShopItem.ElixirOfMagic ->
                                    // get round status - it should be active
                                    match t.GetRoundStatus (uint64 roundId) with
                                    | Some status when status = RoundStatus.Started ->
                                        // get champ's stats
                                        // update champ's stats
                                        Db.newCommandForTransaction SQL.InsertToBoosts tn
                                        |> Db.setParams [
                                            "champId", SqlType.Int64 <| int64 champId
                                            "itemId", SqlType.Int64 <| int64 itemId
                                            "roundId", SqlType.Int64 <| int64 roundId
                                            "duration", SqlType.Int 0
                                        ]
                                        |> Db.exec

                                        let statO =
                                            Db.newCommandForTransaction SQL.GetChampStats tn
                                            |> Db.setParams [
                                                "champId", SqlType.Int64 <| int64 champId
                                            ]
                                            |> Db.querySingle(fun r -> {
                                                Health = r.GetInt64(0)
                                                Magic = r.GetInt64(1)

                                                Accuracy = r.GetInt64(2)
                                                Luck = r.GetInt64(3)

                                                Attack = r.GetInt64(4)
                                                MagicAttack = r.GetInt64(5)

                                                Defense = r.GetInt64(6)
                                                MagicDefense = r.GetInt64(7)
                                            })
                                        match statO with
                                        | Some stat ->
                                            stat |> Shop.applyShopItem item
                                            |> updateChampStat tn (uint64 champId)
                                            Ok()
                                        | None ->
                                            tn.Rollback()
                                            Error("Unable to get stats")
                                    | Some _ ->
                                        tn.Rollback()
                                        Error("Wait while new round starts before using this item")
                                    | None ->
                                        tn.Rollback()
                                        Error("Unable to get round status")
                                | ShopItem.ElixirOfLuck
                                | ShopItem.ElixirOfAccuracy
                                | ShopItem.ElixirOfDamage
                                | ShopItem.ElixirOfMagicalDamage
                                | ShopItem.ElixirOfDefense
                                | ShopItem.ElixirOfMagicalDefense ->
                                    Db.newCommandForTransaction SQL.InsertToBoosts tn
                                    |> Db.setParams [
                                        "champId", SqlType.Int64 <| int64 champId
                                        "itemId", SqlType.Int64 <| int64 itemId
                                        "roundId", SqlType.Int64 <| int64 roundId
                                        "duration", SqlType.Int (Shop.getRoundDuration(item))
                                    ]
                                    |> Db.exec
                                    Ok(())
                        
                            ) conn
                    | Some _ -> Error("You don't have this item in the storage")
                    | None -> Error("Record not found in storage")
                | _, _ ->
                    let sb = new System.Text.StringBuilder();
                    if(itemIdO.IsNone) then sb.Append("Item not found;") |> ignore
                    if(lastRoundId.IsNone) then sb.Append("Round not found; ") |> ignore
                    Error(sb.ToString())
            with exn ->
                Log.Error(exn, $"UseItemFromStorage: {uId}, {item}")
                Error($"Unexpected error: {exn.Message}")        
        | None -> Error("User not found")

    member _.LevelUp(champId:uint64, ch:Characteristic) =
        try
            use conn = new SqliteConnection(cs)
            let lvledChars =
                Db.newCommand SQL.GetLvledCharsForChamp conn
                |> Db.setParams [
                    "champId", SqlType.Int64 <| int64 champId
                ]
                |> Db.scalar (fun v -> unbox<int64> v |> uint64)

            let xp =
                Db.newCommand SQL.GetChampXp conn
                |> Db.setParams [
                    "champId", SqlType.Int64 <| int64 champId
                ]
                |> Db.scalar (fun v -> unbox<int64> v |> uint64)
            let lvl = Levels.getLvlByXp xp
            if lvl > lvledChars then
                Db.newCommand SQL.InsertChampLvl conn
                |> Db.setParams [
                    "champId", SqlType.Int64 <| int64 champId
                    "characteristic", SqlType.Int32 <| int ch
                ]
                |> Db.exec
                Ok (())
            else
                Error("Not enough xp")
        with exn ->
            Log.Error(exn, $"LevelUp: {champId}, {ch}")
            Error(exn.Message)

    // ToDo: optimize if/when monsters > 100
    /// last round is included in selection
    member _.GetAliveMonsters() =
        try
            use conn = new SqliteConnection(cs)
            let lastRoundIdO =
                Db.newCommand SQL.GetLastRound conn
                |> Db.scalar (fun v -> tryUnbox<int64> v)
            match lastRoundIdO with
            | Some roundId ->
                Db.newCommand SQL.GetAliveMonsters conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query (fun r ->
                    let ownerId =
                        if r.IsDBNull(1) then None
                        else r.GetInt64(1) |> uint64 |> Some
                    r.GetInt64(0), ownerId)
                |> Ok
            | None ->
                Db.newCommand SQL.GetMonsters conn
                |> Db.query (fun r -> 
                    let ownerId =
                        if r.IsDBNull(1) then None
                        else r.GetInt64(1) |> uint64 |> Some
                    r.GetInt64(0), ownerId)
                |> Ok
        with exn ->
            Log.Error(exn, "GetAliveMonsters")
            Error("Unexpected error")

    member _.GetMonsters(mtype:MonsterType, msubtype: MonsterSubType) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.FilterMonsters conn
            |> Db.setParams [
                "mtype", SqlType.Int <| int mtype
                "msubtype", SqlType.Int <| int msubtype
            ]
            |> Db.query (fun v -> v.GetInt64(0), v.GetString(1))
            |> Some
        with exn ->
            Log.Error(exn, "GetMonsters")
            None

    member _.RoundsExists() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.RoundExists conn
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
        with exn ->
            Log.Error(exn, "RoundsExists")
            None
    
    member _.AnyChampJoinedRound(roundId:uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.ChampsPlayedInRound conn
            |> Db.setParams [
                "roundId", SqlType.Int64 <| int64 roundId
            ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
        with exn ->
            Log.Error(exn, "AnyChampJoinedRound")
            None

    member _.GetAssetIdByName(name:string) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetAssetIdByName conn
            |> Db.setParams [
                "name", SqlType.String name
            ]
            |> Db.querySingle (fun r -> r.GetInt64(0))
            |> Ok
        with exn ->
            Log.Error(exn, "GetAssetIdByName")
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
    
    member _.PerformAction(uId: UserId, raction:RoundActionRecord) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                let belongsToAUserO =
                    Db.newCommand SQL.ChampBelongsToAUser conn
                    |> Db.setParams [
                        "champId", SqlType.Int64 <| int64 raction.ChampId
                        "userId", SqlType.Int64 <| int64 userId
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
                match belongsToAUserO with
                | Some b ->
                    if b then
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
                                        // TODO: check that champ didn't already participated at this round
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
                    else
                        Error("Doesn't belong to a user")
                | None -> Error("Unexpected error")
            with exn ->
                Log.Error(exn, $"Perform action: {raction}")
                Error("Unexpected error")
        | None -> Error("Unable to find user")

    member t.StartBattle(monsterId: int64) =
        try
            use conn = new SqliteConnection(cs)
            let monsterExists =
                Db.newCommand SQL.MonsterExists conn
                |> Db.setParams [ "monsterId", SqlType.Int64 monsterId ]
                |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
            if monsterExists then
                let unfinishedBattleExists =
                    Db.newCommand SQL.UnfinishedBattleExists conn
                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                if unfinishedBattleExists then
                    Error(BattleStatusError.UnfinishedBattleFound)
                else
                    let unfinishedRoundExists =
                        Db.newCommand SQL.UnfinishedRoundExists conn
                        |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                    
                    if unfinishedRoundExists then
                        Error(BattleStatusError.UnfinishedRoundFound)
                    else
                        match t.GetBattleReward() with
                        | Ok battleReward when battleReward > 0M ->
                            let lastRoundId =
                                Db.newCommand SQL.GetLastRound conn
                                |> Db.scalar (fun v -> tryUnbox<int64> v)
                            let roundsCount =
                                Db.newCommand SQL.GetRoundsCount conn
                                |> Db.scalar (fun v -> unbox<int64> v)
                            if lastRoundId.IsNone && roundsCount <> 0L then
                                Error(BattleStatusError.UnknownError)
                            else
                                let monsterIsDead =
                                    match lastRoundId with
                                    | Some roundId ->
                                        Db.newCommand SQL.MonsterIsDeadAtRound conn
                                        |> Db.setParams [
                                            "monsterId", SqlType.Int64 <| int64 monsterId
                                            "roundId", SqlType.Int64 roundId
                                        ]
                                        |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                                    | None -> false
                                if monsterIsDead then
                                    Error(BattleStatusError.MonsterIsDead)
                                else
                                    let battleId =
                                        Db.newCommand SQL.StartBattle conn
                                        |> Db.setParams [
                                            "monsterId", SqlType.Int64 <| int64 monsterId
                                            "rewards", SqlType.Decimal battleReward
                                        ]
                                        |> Db.scalar (fun v -> unbox<int64> v)
                                    let roundRewards = GameLogic.Rewards.getRoundReward battleReward
                                    let roundId =
                                        Db.newCommand SQL.StartRound conn
                                        |> Db.setParams [
                                            "battleId", SqlType.Int64 <| int64 battleId
                                            "rewards", SqlType.Decimal roundRewards
                                        ]
                                        |> Db.scalar (fun v -> unbox<int64> v)
                                    {|
                                        BattleId = battleId
                                        BattleRewards = battleReward
                                        RoundId = roundId
                                        RoundRewards = roundRewards
                                    |} |> Ok
                        | Ok _ -> BattleStatusError.NoRewards |> Error
                        | Error err -> BattleStatusError.GettingRewardError |> Error
            else
                Error(BattleStatusError.NoMosterFound)
        with exn ->
            Log.Error(exn, $"StartBattle: {monsterId}")
            Error(BattleStatusError.UnknownError)

    member _.NextRound() =
        try
            use conn = new SqliteConnection(cs)
            let unfinishedRoundExists =
                Db.newCommand SQL.UnfinishedRoundExists conn
                |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                    
            if unfinishedRoundExists then
                Error(StartRoundError.UnfinishedRoundFound)
            else
                let lastBattleId =
                    Db.newCommand SQL.GetLastBattle conn
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                match lastBattleId with
                | Some battleId ->
                    let battleIsActive =
                        Db.newCommand SQL.BattleIsActive conn
                        |> Db.setParams [
                            "battleId", SqlType.Int64 battleId
                        ]
                        |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)                                
                    if battleIsActive then
                        let roundsInBattleO =
                            Db.newCommand SQL.RoundsInBattle conn
                            |> Db.setParams [
                                "battleId", SqlType.Int64 <| int64 battleId
                            ]
                            |> Db.querySingle(fun r -> r.GetInt32(0))  
                        match roundsInBattleO with
                        | Some rounds ->
                            if rounds < Constants.RoundsInBattle then
                                let battleRewardsO =
                                    Db.newCommand SQL.GetBattleRewards conn
                                    |> Db.setParams [
                                        "battleId", SqlType.Int64 <| int64 battleId
                                    ]
                                    |> Db.querySingle(fun r -> r.GetDecimal(0))
                                match battleRewardsO with
                                | Some battleRewards ->
                                    let roundRewards = GameLogic.Rewards.getRoundReward battleRewards
                                    Db.newCommand SQL.StartRound conn
                                    |> Db.setParams [
                                        "battleId", SqlType.Int64 <| int64 battleId
                                        "rewards", SqlType.Decimal roundRewards
                                    ]
                                    |> Db.scalar (fun v -> unbox<int64> v)
                                    |> Ok
                                | None ->
                                    Error(StartRoundError.UnknownError)
                            else
                                Db.newCommand SQL.FinishBattle conn
                                |> Db.setParams [
                                    "battleId", SqlType.Int64 <| int64 battleId
                                ]
                                |> Db.exec
                                Error(StartRoundError.MaxRoundsInBattle)
                        | None ->
                            Error(StartRoundError.UnknownError)
                    else
                        Error(StartRoundError.NoActiveBattle)
                | None -> Error(StartRoundError.UnknownError)
        with exn ->
            Log.Error(exn, "Next round")
            Error(StartRoundError.UnknownError)

    member _.ApplyEffect(champId: uint64, item:Effect) =
        try
            use conn = new SqliteConnection(cs)

            let lastRoundId =
                Db.newCommand SQL.GetLastActiveBattle conn
                |> Db.scalar (fun v -> tryUnbox<int64> v)
            match lastRoundId with
            | Some roundId ->
                let itemIdO =
                    Db.newCommand SQL.GetEffectItemIdByItem conn
                    |> Db.setParams [
                        "item", SqlType.Int <| int item
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                
                let duration = Effects.getRoundDuration(item)

                match itemIdO with
                | Some itemId ->
                    Db.newCommand SQL.ApplyEffect conn
                    |> Db.setParams [
                        "champId", SqlType.Int64 <| int64 champId
                        "itemId", SqlType.Int64 <| int64 itemId
                        "roundId", SqlType.Int64 <| int64 roundId
                        "duration", SqlType.Int duration
                    ]
                    |> Db.exec
                    Ok(())
                | None -> Error("No item found")
            | None -> Error("No round found")
        with exn ->
            Log.Error(exn, $"ApplyEffect: {champId}, {item}")
            Error($"Unexpected error: {exn.Message}")   
    
    member _.ApplyEffectToMonster(monsterId: uint64, item:Effect) =
        try
            use conn = new SqliteConnection(cs)

            let lastRoundId =
                Db.newCommand SQL.GetLastActiveBattle conn
                |> Db.scalar (fun v -> tryUnbox<int64> v)
            match lastRoundId with
            | Some roundId ->
                let itemIdO =
                    Db.newCommand SQL.GetEffectItemIdByItem conn
                    |> Db.setParams [
                        "item", SqlType.Int <| int item
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                if item = Effect.Death then failwith "death effect should be handled differently"
                let duration = Effects.getRoundDuration(item)

                match itemIdO with
                | Some itemId ->
                    Db.newCommand SQL.ApplyEffectToMonster conn
                    |> Db.setParams [
                        "monsterId", SqlType.Int64 <| int64 monsterId
                        "itemId", SqlType.Int64 <| int64 itemId
                        "roundId", SqlType.Int64 <| int64 roundId
                        "duration", SqlType.Int duration
                    ]
                    |> Db.exec
                    Ok(())
                | None -> Error("No item found")
            | None -> Error("No round found")
        with exn ->
            Log.Error(exn, $"ApplyEffectToMonster: {monsterId}, {item}")
            Error($"Unexpected error: {exn.Message}")   

    member _.FinishRound() =
        try 
            use conn = new SqliteConnection(cs)
            let lastActiveRound =
                Db.newCommand SQL.GetLastActiveRound conn
                |> Db.querySingle(fun r -> if r.IsDBNull(0) then None else Some (r.GetInt64(0)))
                |> Option.flatten
            match lastActiveRound with
            | Some roundId ->
                let playersO =
                    Db.newCommand SQL.ChampsPlayedInRound conn
                    |> Db.setParams [
                        "roundId", SqlType.Int64 roundId
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
                match playersO with
                | Some b when b ->
                    let dto =
                        Db.newCommand SQL.GetTimestampForRound conn
                        |> Db.setParams [
                            "roundId", SqlType.Int64 roundId
                        ]
                        |> Db.querySingle(fun r -> r.GetDateTime(0))
                    match dto with
                    | Some lastRoundTimestamp ->
                        if lastRoundTimestamp.Add(BattleParams.RoundDuration()) > DateTime.UtcNow then
                            Error("Not enough time passed since prev. round")
                        else
                            Db.newCommand SQL.FinishRound conn
                            |> Db.setParams [
                                "roundId", SqlType.Int64 roundId
                            ]
                            |> Db.exec
                            Ok(())
                    | None -> Error("Unexpected error, can't get timestamp for round")
                | Some _ -> Error("Not enough players joined")
                | None -> Error("Can't get players number")
            | None -> Error("Active round not found")
        with exn ->
            Log.Error(exn, "FinishRound")
            Error($"Unexpected error: {exn.Message}")
  
    member _.FinishBattle() =
        try 
            use conn = new SqliteConnection(cs)
            let lastActiveRound =
                Db.newCommand SQL.GetLastActiveRound conn
                |> Db.querySingle(fun r -> if r.IsDBNull(0) then None else Some (r.GetInt64(0)))
                |> Option.flatten
            match lastActiveRound with
            | Some roundId ->
                let playersO =
                    Db.newCommand SQL.ChampsPlayedInRound conn
                    |> Db.setParams [
                        "roundId", SqlType.Int64 roundId
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
                match playersO with
                | Some b when b ->
                    let dto =
                        Db.newCommand SQL.GetTimestampForRound conn
                        |> Db.setParams [
                            "roundId", SqlType.Int64 roundId
                        ]
                        |> Db.querySingle(fun r -> r.GetDateTime(0))
                    match dto with
                    | Some lastRoundTimestamp ->
                        if lastRoundTimestamp.Add(BattleParams.RoundDuration()) > DateTime.UtcNow then
                            Error("Not enough time passed since prev. round")
                        else
                            Db.newCommand SQL.FinishRound conn
                            |> Db.setParams [
                                "roundId", SqlType.Int64 roundId
                            ]
                            |> Db.exec
                            Ok(())
                    | None -> Error("Unexpected error, can't get timestamp for round")
                | Some _ -> Error("Not enough players joined")
                | None -> Error("Can't get players number")
            | None -> Error("Active round not found")
        with exn ->
            Log.Error(exn, "FinishBattle")
            Error($"Unexpected error: {exn.Message}")
     
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

    member _.GetRoundTimestamp(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            
            Db.newCommand SQL.GetRoundTimestamp conn
            |> Db.setParams [
                "roundId", SqlType.Int64 <| int64 roundId
            ]
            |> Db.querySingle(fun r -> r.GetDateTime(0))
        with exn ->
            Log.Error(exn, "GetRoundTimestamp")
            None

    member _.GetRoundInfo(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            
            Db.newCommand SQL.GetRoundInfo conn
            |> Db.setParams [
                "roundId", SqlType.Int64 <| int64 roundId
            ]
            |> Db.querySingle(fun r ->
                uint64 <| r.GetInt64(0),
                r.GetDateTime(1),
                enum<RoundStatus> <| r.GetInt32(2)
            )
        with exn ->
            Log.Error(exn, "GetRoundInfo")
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

    member _.GetRewardsForFinishedRound(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            let statusAndRewardsO =
                Db.newCommand "SELECT Status, Rewards FROM Round WHERE ID = @roundId" conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.querySingle(fun r ->
                    enum<RoundStatus> <| r.GetInt32(0),
                    r.GetDecimal(1))
            match statusAndRewardsO with
            | Some (status, rewards) when status = RoundStatus.Processing ->
                rewards
                |> Ok
            | Some _ -> Error("Incorrect state")
            | None -> Error("Can't get round status")
        with exn ->
            Log.Error(exn, $"GetRewardsForFinishedRound: {roundId}")
            Error("Unexpected error")
        
    member _.GetActionsForFinishedRound(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            let statusO =
                Db.newCommand SQL.GetRoundStatus conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.querySingle(fun r -> enum<RoundStatus> <| r.GetInt32(0))
            match statusO with
            | Some status when status = RoundStatus.Processing ->
                Db.newCommand SQL.GetActionsForRound conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query(fun r -> {|
                    ChampId = uint64 <| r.GetInt64(0)
                    Timestamp = r.GetDateTime(1)
                    Move = enum<Move> <| r.GetInt32(2)
                |})
                |> Ok
            | Some _ -> Error("Incorrect state")
            | None -> Error("Can't get round status")
        with exn ->
            Log.Error(exn, $"GetActionsForFinishedRound: {roundId}")
            Error("Unexpected error")

    member _.GetEffectsForFinishedRound(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            let statusO =
                Db.newCommand SQL.GetRoundStatus conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.querySingle(fun r -> enum<RoundStatus> <| r.GetInt32(0))
            match statusO with
            | Some status when status = RoundStatus.Processing ->
                Db.newCommand SQL.GetEffectsForRound conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query(fun r -> {|
                    ChampId = uint64 <| r.GetInt64(0)
                    Effect = {
                        StartRoundId = r.GetInt64(1)
                        Item = enum<Effect> <| r.GetInt32(2)
                        Duration = r.GetInt32(3)
                        Val = if r.IsDBNull(4) then None else r.GetInt64(4) |> uint64 |> Some
                    }
                |})
                |> List.groupBy(fun v -> v.ChampId)
                |> List.map(fun (k,v) -> k, v |> List.map(fun r -> r.Effect))
                |> dict
                |> Ok
            | Some _ -> Error("Incorrect state")
            | None -> Error("Can't get round status")
        with exn ->
            Log.Error(exn, $"GetEffectsForFinishedRound: {roundId}")
            Error("Unexpected error")

    member _.GetBoostsForFinishedRound(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            let statusO =
                Db.newCommand SQL.GetRoundStatus conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.querySingle(fun r -> enum<RoundStatus> <| r.GetInt32(0))
            match statusO with
            | Some status when status = RoundStatus.Processing ->
                Db.newCommand SQL.GetBoostsForRound conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query(fun r -> {|
                    ChampId = uint64 <| r.GetInt64(0)
                    RoundBoost = {
                        StartRoundId = r.GetInt64(1)
                        Boost = enum<ShopItem> <| r.GetInt32(2)
                        Duration = r.GetInt32(3)
                    }
                |})
                |> List.groupBy(fun v -> v.ChampId)
                |> List.map(fun (k,v) -> k, v |> List.map(fun r -> r.RoundBoost))
                |> Map.ofList
                |> Ok
            | Some _ -> Error("Incorrect state")
            | None -> Error("Can't get round status")
        with exn ->
            Log.Error(exn, $"GetBoostsForFinishedRound: {roundId}")
            Error("Unexpected error")

    member _.GetLvlsForFinishedRound(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            let statusO =
                Db.newCommand SQL.GetRoundStatus conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.querySingle(fun r -> enum<RoundStatus> <| r.GetInt32(0))
            match statusO with
            | Some status when status = RoundStatus.Processing ->
                Db.newCommand SQL.GetLvlsForRound conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query(fun r -> {|
                    ChampId = uint64 <| r.GetInt64(0)
                    Characteristic = enum<Characteristic> <| r.GetInt32(1)
                |})
                |> List.groupBy(fun v -> v.ChampId)
                |> dict
                |> Ok
            | Some _ -> Error("Incorrect state")
            | None -> Error("Can't get round status")
        with exn ->
            Log.Error(exn, $"GetLvlsForFinishedRound: {roundId}")
            Error("Unexpected error")

    member _.GetUserChampsWithStatsForFinishedRound(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            let statusO =
                Db.newCommand SQL.GetRoundStatus conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.querySingle(fun r -> enum<RoundStatus> <| r.GetInt32(0))
            match statusO with
            | Some status when status = RoundStatus.Processing ->
                Db.newCommand SQL.GetChampStatsForRound conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query(fun r ->
                    uint64 <| r.GetInt64(0),
                    (uint64 <| r.GetInt64(1),
                    {
                        Health = r.GetInt64(2)
                        Magic = r.GetInt64(3)

                        Accuracy = r.GetInt64(4)
                        Luck = r.GetInt64(5)

                        Attack = r.GetInt64(6)
                        MagicAttack = r.GetInt64(7)

                        Defense = r.GetInt64(8)
                        MagicDefense = r.GetInt64(9)
                    },
                    r.GetString(10)))
                |> Map.ofList
                |> Ok
            | Some _ -> Error("Incorrect state")
            | None -> Error("Can't get round status")
        with exn ->
            Log.Error(exn, $"GetUserChampsWithStatsForFinishedRound: {roundId}")
            Error("Unexpected error")
    
    member _.GetMonsterForFinishedRound(roundId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            let statusO =
                Db.newCommand SQL.GetRoundStatus conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.querySingle(fun r -> enum<RoundStatus> <| r.GetInt32(0))
            match statusO with
            | Some status when status = RoundStatus.Processing ->
                let monsterIdO =
                    Db.newCommand SQL.GetMonsterIdForRound conn
                    |> Db.setParams [
                        "roundId", SqlType.Int64 <| int64 roundId
                    ]
                    |> Db.querySingle(fun r -> r.GetInt64(0))
                match monsterIdO with
                | Some monsterId ->
                    let monsterO =
                        Db.newCommand SQL.GetMonsterStats conn
                        |> Db.setParams [
                            "monsterId", SqlType.Int64 monsterId
                        ]
                        |> Db.querySingle(fun r -> {|
                            Xp = r.GetInt64(0)
                            Name = r.GetString(1)
                            Description = r.GetString(2)
                            Img = JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData r 3, jsOptions)
                            Stat = {

                                Health = r.GetInt64(4)
                                Magic = r.GetInt64(5)

                                Accuracy = r.GetInt64(6)
                                Luck = r.GetInt64(7)

                                Attack = r.GetInt64(8)
                                MagicAttack = r.GetInt64(9)

                                Defense = r.GetInt64(10)
                                MagicDefense = r.GetInt64(11)
                            }
                            MType = enum<MonsterType> <| r.GetInt32(12)
                            MSubType = enum<MonsterSubType> <| r.GetInt32(13)
                        |})
                    match monsterO with
                    | Some rdb ->
                        let monsterEffects =
                            Db.newCommand SQL.GetMonsterEffectsForRound conn
                            |> Db.setParams [
                                "roundId", SqlType.Int64 <| int64 roundId
                                "monsterId", SqlType.Int64 monsterId
                            ]
                            |> Db.query(fun r -> {
                                StartRoundId = r.GetInt64(0)
                                Item = enum<Effect> <| r.GetInt32(1)
                                Duration = r.GetInt32(2)
                                Val = if r.IsDBNull(3) then None else r.GetInt64(3) |> uint64 |> Some
                            })
                        {|
                            MonsterId = uint64 monsterId
                            MonsterRecord = MonsterRecord(rdb.Name, rdb.Description,
                                Monster.TryCreate(rdb.MType, rdb.MSubType).Value, rdb.Stat, uint64 rdb.Xp, rdb.Img)
                            MonsterEffects = monsterEffects
                        |}
                        |> Ok
                    | None -> Error("Can't get monster data")
                | None -> Error("Can't get monsterId by roundId")
            | Some _ -> Error("Incorrect state")
            | None -> Error("Can't get round status")
        with exn ->
            Log.Error(exn, $"GetMonsterForFinishedRound: {roundId}")
            Error("Unexpected error")        

    member _.FinalizeRound (bresult:BattleResult) (monsterRevivalTime:uint) (boosts:Map<uint64, RoundBoost list>) =
        try
            use conn = new SqliteConnection(cs)

            let updateRewards (tn:Data.IDbTransaction) (roundId: uint64) (roundRewards: RoundRewardSplit) =
                let sRoundRewards = roundRewards.SRewards
                // insert to round rewards
                tn
                |> Db.newCommandForTransaction SQL.InsertRoundRewards
                |> Db.setParams [ 
                    "roundId", SqlType.Int64 <| int64 roundId
                    "unclaimed", SqlType.Decimal roundRewards.Unclaimed
                    "burn", SqlType.Decimal sRoundRewards.Burn
                    "dao", SqlType.Decimal sRoundRewards.DAO
                    "reserve", SqlType.Decimal sRoundRewards.Reserve
                    "devs", SqlType.Decimal sRoundRewards.Dev
                    "champs", SqlType.Decimal roundRewards.ChampsTotal
                ]
                |> Db.exec

                // update numeric keys
                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal (-roundRewards.Claimed)
                    "key", SqlType.String (DbKeysNum.Rewards.ToString())
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal sRoundRewards.Reserve
                    "key", SqlType.String (DbKeysNum.Reserve.ToString())
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal sRoundRewards.Dev
                    "key", SqlType.String (DbKeysNum.Dev.ToString())
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal sRoundRewards.DAO
                    "key", SqlType.String (DbKeysNum.DAO.ToString())
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal sRoundRewards.Burn
                    "key", SqlType.String (DbKeysNum.Burn.ToString())
                ]
                |> Db.exec

                // rewards data inserted to ChampAction table is handled elsewhere
     
            let updateMonsterStat (tn:Data.IDbTransaction) (monsterId:uint64) (stat:Stat) =
                tn
                |> Db.newCommandForTransaction SQL.UpdateMonsterStat
                |> Db.setParams [
                    "health", SqlType.Int64 <| int64 stat.Health
                    "magic", SqlType.Int64 <| int64 stat.Magic

                    "accuracy", SqlType.Int64 <| int64 stat.Accuracy
                    "luck", SqlType.Int64 <| int64 stat.Luck

                    "attack", SqlType.Int64 <| int64 stat.Attack
                    "mattack", SqlType.Int64 <| int64 stat.MagicAttack

                    "defense", SqlType.Int64 <| int64 stat.Defense
                    "mdefense", SqlType.Int64 <| int64 stat.MagicDefense
                    "monsterId", SqlType.Int64 <| int64 monsterId
                ]
                |> Db.exec

            let addMonsterXp (tn:Data.IDbTransaction) (monsterId:uint64) (xpEarned:uint64) =
                tn
                |> Db.newCommandForTransaction SQL.AddMonsterXp
                |> Db.setParams [
                    "xp", SqlType.Int64 <| int64 xpEarned
                    "monsterId", SqlType.Int64 <| int64 monsterId
                ]
                |> Db.exec
                
            let defeatMonster (tn:Data.IDbTransaction) (champId:uint64) =
                let m = bresult.MonsterChar
                tn
                |> Db.newCommandForTransaction SQL.MonsterDefeat
                |> Db.setParams [
                    "monsterId", SqlType.Int64 <| int64 m.Id
                    "roundId", SqlType.Int64 <| int64 bresult.RoundId
                    "champId", SqlType.Int64 <| int64 champId
                    "revivalDuration", SqlType.Int <| int monsterRevivalTime
                ]
                |> Db.exec
 
                Monster.getStats m.Monster
                |> updateMonsterStat tn m.Id

                let itemIdO =
                    Db.newCommandForTransaction SQL.GetEffectItemIdByItem tn
                    |> Db.setParams [
                        "item", SqlType.Int <| int Effect.Death
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)

                match itemIdO with
                | Some itemId ->
                    let duration = Monster.getRevivalDuration m.Monster
                    tn
                    |> Db.newCommandForTransaction SQL.ApplyEffectToMonster
                    |> Db.setParams [
                        "monsterId", SqlType.Int64 <| int64 m.Id
                        "itemId", SqlType.Int64 itemId
                        "roundId", SqlType.Int64 <| int64 bresult.RoundId
                        "duration", SqlType.Int <| int duration
                    ]
                    |> Db.exec
                | None -> tn.Rollback()

            // ToDo: handle case when rewards is 0
            let updateChampMoves (tn:Data.IDbTransaction) (champId: uint64) (pm:PerformedMove) (xpEarned: uint64) (rewards: decimal) =
                let bytes = JsonSerializer.SerializeToUtf8Bytes(pm, jsOptions)
                tn
                |> Db.newCommandForTransaction SQL.UpdateChampAction
                |> Db.setParams [
                    "moveRes", SqlType.Bytes bytes
                    "xpEarned", SqlType.Int64 <| int64 xpEarned
                    "rewards", SqlType.Decimal rewards
                    "roundId", SqlType.Int64 <| int64 bresult.RoundId
                    "champId", SqlType.Int64 <| int64 champId
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.AddChampXp
                |> Db.setParams [
                    "xp", SqlType.Int64 <| int64 xpEarned
                    "champId", SqlType.Int64 <| int64 champId
                ]
                |> Db.exec
                
                tn
                |> Db.newCommandForTransaction SQL.UpdateChampEarnedRewards
                |> Db.setParams [
                    "rewards", SqlType.Decimal rewards
                    "champId", SqlType.Int64 <| int64 champId
                ]
                |> Db.exec

            let defeatChamp (tn:Data.IDbTransaction) (champId:uint64) =
                // 1. add monster victory
                tn
                |> Db.newCommandForTransaction SQL.MonsterVictory
                |> Db.setParams [
                    "monsterId", SqlType.Int64 <| int64 bresult.MonsterChar.Id
                    "roundId", SqlType.Int64 <| int64 bresult.RoundId
                    "champId", SqlType.Int64 <| int64 champId
                ]
                |> Db.exec
                // 2. get traits
                let traitO =
                    tn
                    |> Db.newCommandForTransaction SQL.GetChampTrait
                    |> Db.setParams [
                        "champId", SqlType.Int64 <| int64 champId
                    ]
                    |> Db.querySingle(fun r ->
                        {
                            Background = r.GetInt32(0) |> enum<Background>
                            Skin = r.GetInt32(1) |> enum<Skin>
                            Weapon = r.GetInt32(2) |> enum<Weapon>
                            Magic = r.GetInt32(3) |> enum<Magic>
                            Head = r.GetInt32(4) |> enum<Head>
                            Armour = r.GetInt32(5) |> enum<Armour>
                            Extra = r.GetInt32(6) |> enum<Extra>
                        })
                match traitO with
                | Some t ->
                    // 3. reset stat to defaults
                    Champ.generateStats t
                    |> updateChampStat tn champId

                    // 4. all effects mark as passive to the round
                    tn
                    |> Db.newCommandForTransaction SQL.MarkEffectsAsPassiveBeforeRound
                    |> Db.setParams [
                        "champId", SqlType.Int64 <| int64 champId
                        "roundId", SqlType.Int64 <| int64 bresult.RoundId
                    ]
                    |> Db.exec
                    
                    let itemIdO =
                        Db.newCommandForTransaction SQL.GetEffectItemIdByItem tn
                        |> Db.setParams [
                            "item", SqlType.Int <| int Effect.Death
                        ]
                        |> Db.scalar (fun v -> tryUnbox<int64> v)

                    match itemIdO with
                    | Some itemId ->
                        let revivalExists =
                            boosts.TryFind champId
                            |> Option.map(fun xs -> xs |> List.exists(fun b -> b.Boost = ShopItem.RevivalSpell))
                            |> Option.defaultValue false
                        
                        // if revivalExists set Duration to 0 and mark as passive
                        let duration = if revivalExists then 0 else Effects.getRoundDuration(Effect.Death)
                        
                        Db.newCommandForTransaction SQL.ApplyEffectWithIsActive tn
                        |> Db.setParams [
                            "champId", SqlType.Int64 <| int64 champId
                            "itemId", SqlType.Int64 <| int64 itemId
                            "roundId", SqlType.Int64 <| int64 bresult.RoundId
                            "duration", SqlType.Int duration
                            "isActive", SqlType.Boolean <| not revivalExists
                        ]
                        |> Db.exec

                        // mark revival as finished
                        Db.newCommandForTransaction SQL.UpdateBoostDuration tn
                        |> Db.setParams [
                            "duration", SqlType.Int duration
                            "champId", SqlType.Int64 <| int64 champId
                            "itemId", SqlType.Int64 <| int64 itemId
                            "roundId", SqlType.Int64 <| int64 bresult.RoundId
                        ]
                        |> Db.exec
                    | None -> tn.Rollback()
                | None -> tn.Rollback()

            let performMonsterAction (tn:Data.IDbTransaction) =
                match bresult.MonsterPM with
                | Some pm ->
                    let bytes = JsonSerializer.SerializeToUtf8Bytes(pm, jsOptions)
                    tn
                    |> Db.newCommandForTransaction SQL.AddMonsterAction
                    |> Db.setParams [
                        "roundId", SqlType.Int64 <| int64 bresult.RoundId
                        "champId", SqlType.Null
                        "move", SqlType.Int <| int pm.Move
                        "moveRes", SqlType.Bytes bytes
                        "xp", SqlType.Int64 1L
                    ]
                    |> Db.exec
                | None ->
                    bresult.MonsterActions
                    |> Map.iter(fun champId (pm, xp) ->
                        let bytes = JsonSerializer.SerializeToUtf8Bytes(pm, jsOptions)
                        tn
                        |> Db.newCommandForTransaction SQL.AddMonsterAction
                        |> Db.setParams [
                            "roundId", SqlType.Int64 <| int64 bresult.RoundId
                            "champId", SqlType.Int64 <| int64 champId
                            "move", SqlType.Int <| int pm.Move
                            "moveRes", SqlType.Bytes bytes
                            "xp", SqlType.Int64 <| int64 xp
                        ]
                        |> Db.exec                        
                    )

                match bresult.MonsterPM with
                | Some _ -> 1UL
                | None ->
                    bresult.MonsterActions
                    |> Seq.sumBy(fun kv ->
                        let (_, xp) = kv.Value
                        xp)
                |> addMonsterXp tn bresult.MonsterChar.Id

            Db.batch (fun tn ->
                updateRewards tn bresult.RoundId bresult.Rewards
                bresult.MonsterDefeater |> Option.iter(defeatMonster tn)
                if bresult.MonsterDefeater.IsNone then
                    bresult.MonsterChar.Stat |> updateMonsterStat tn bresult.MonsterChar.Id
                performMonsterAction tn
               
                bresult.ChampsMoveAndXp |> Map.iter(fun champId (pm, xp) ->
                    bresult.Rewards.Champs
                    |> List.tryPick(fun c -> if c.ChampId = champId then Some c.Reward else None)
                    |> Option.defaultValue 0M
                    |> updateChampMoves tn champId pm xp
                )

                bresult.ChampsFinalStat |> Map.iter(updateChampStat tn)
                bresult.DeadChamps |> List.iter(defeatChamp tn)

                Db.newCommand SQL.FinalizeRound conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 bresult.RoundId
                ]
                |> Db.exec

                if bresult.MonsterDefeater.IsSome then
                    Db.newCommand SQL.FinishBattle conn
                    |> Db.setParams [
                        "battleId", SqlType.Int64 <| int64 bresult.BattleId
                    ]
                    |> Db.exec
                
            ) conn
            Ok(())
        with exn ->
            Log.Error(exn, "FinalizeRound")
            Error($"Unexpected error: {exn.Message}")
    
    member _.GetChampLeaderboard() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetChampsLeaderBoard25 conn
            |> Db.query(fun r ->
                ChampShortInfo(uint64 <| r.GetInt64(4), r.GetString(0), 
                    r.GetString(2), uint64 <| r.GetInt64(3)))
            |> Ok
        with exn ->
            Log.Error(exn, "GetChampLeaderboard")
            Error("Unexpected error")

    member _.GetMonsterLeaderboard() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetMonsterLeaderBoard25 conn
            |> Db.query(fun r ->
                MonsterShortInfo(
                   uint64 <| r.GetInt64(0), r.GetString(1),
                   enum<MonsterType> <| r.GetInt32(2), enum<MonsterSubType> <| r.GetInt32(3),
                   JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData r 4, jsOptions),
                   uint64 <| r.GetInt64(5)
                ))
            |> Ok
        with exn ->
            Log.Error(exn, "GetMonsterLeaderboard")
            Error("Unexpected error")

    member _.GetTopDonaters() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetTopDonaters conn
            |> Db.query(fun r ->
                let donater =
                    if r.IsDBNull(0) |> not then
                        Donater.Discord(r.GetInt64(0) |> uint64)
                    elif r.IsDBNull(1) |> not then
                        Donater.Custom(r.GetInt64(1) |> uint64, r.GetString(2))
                    else
                        let wallet =
                            if r.IsDBNull(3) |> not then
                                r.GetString(3)
                            elif r.IsDBNull(4) |> not then
                                r.GetString(4)
                            else "unknown"
                        Donater.Unknown wallet
                let amount = r.GetDecimal(5)
                Donation(donater, amount))
            |> Ok
        with exn ->
            Log.Error(exn, "GetTopInGameDonaters")
            Error("Unexpected error")

    member _.GetLatestDonations() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.Get5LatestDonations conn
            |> Db.query(fun r ->
                let donater =
                    if r.IsDBNull(0) |> not then
                        Donater.Discord(r.GetInt64(0) |> uint64)
                    elif r.IsDBNull(1) |> not then
                        Donater.Custom(r.GetInt64(1) |> uint64, r.GetString(2))
                    else
                        let wallet =
                            if r.IsDBNull(3) |> not then
                                r.GetString(3)
                            elif r.IsDBNull(4) |> not then
                                r.GetString(4)
                            else "unknown"
                        Donater.Unknown(wallet)
                let amount = r.GetDecimal(5)
                let tx = r.GetString(6)
                LatestDonation(donater, amount, tx)
            )
            |> Ok
        with exn ->
            Log.Error(exn, "GetLatestDonations")
            Error("Unexpected error")

    member _.Backup(datasource:string) =
        try
            use conn = new SqliteConnection(cs)
            let backupcs = $"Data Source={datasource}; Cache=Shared;Foreign Keys = True"
            use bconn = new SqliteConnection(backupcs)
            conn.Open()
            bconn.Open()
            conn.BackupDatabase(bconn)
            Ok(())
        with exn ->
            Log.Error(exn, "Backup")
            Error("Unexpected error")

    member _.GetMonsterById(monsterId:int64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetMonsterStats conn
            |> Db.setParams [
                "monsterId", SqlType.Int64 monsterId
            ]
            |> Db.querySingle(fun r ->
                let ownerId =
                    if r.IsDBNull(14) then None
                    else r.GetInt64(14) |> uint64 |> Some
                let gtype =
                    if r.IsDBNull(15) then MonsterGenType.Generative
                    else
                        MonsterGenType.NFTBased(
                            r.GetInt64(15) |> uint64,
                            r.GetString(16))
                MonsterInfo(
                    uint64 <| monsterId,
                    uint64 <| r.GetInt64(0),
                    r.GetString(1),
                    r.GetString(2),
                    JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData r 3, jsOptions),
                    {
                        Health = r.GetInt64(4)
                        Magic = r.GetInt64(5)

                        Accuracy = r.GetInt64(6)
                        Luck = r.GetInt64(7)

                        Attack = r.GetInt64(8)
                        MagicAttack = r.GetInt64(9)

                        Defense = r.GetInt64(10)
                        MagicDefense = r.GetInt64(11)
                    },
                    enum<MonsterType> <| r.GetInt32(12),
                    enum<MonsterSubType> <| r.GetInt32(13),
                    ownerId, gtype))
                |> function
                    | Some monster ->
                        let monsterLvl = Levels.getLvlByXp monster.XP
                        let monsterLvlStats = Monster.getMonsterStatsByLvl(monster.MType, monster.MSubType, monsterLvl)
                        monster.WithStat(monster.Stat + monsterLvlStats)
                        |> Some
                    | None -> None
        with exn ->
            Log.Error(exn, $"GetMonsterById: {monsterId}")
            None
            
    member x.GetMonsterInfoByRequestId(requestId:int64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetUserMonsterInfoByRequestId conn
            |> Db.setParams [
                "rId", SqlType.Int64 requestId
            ]
            |> Db.scalar(fun v -> unbox<int64> v)
            |> x.GetMonsterById     
        with exn ->
            Log.Error(exn, $"GetMonsterInfoByRequestId: {requestId}")
            None   

    member _.GetChampWithBalances() =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetChampWithBalances conn
            |> Db.query(fun r ->
                {|
                    ID = uint64 <| r.GetInt64(0)
                    AssetId = uint64 <| r.GetInt64(1)
                    Balance = r.GetDecimal(2)
                    Name = r.GetString(3)
                |}
            )
            |> Ok

        with exn ->
            Log.Error(exn, "GetChampWithBalances")
            Error("Unable to get champs with balances")

    member _.FinalizeBattle(battleId:uint64) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.FinalizeBattle conn
            |> Db.setParams [
                "battleId", SqlType.Int64 <| int64 battleId
            ]
            |> Db.exec
            true           

        with exn ->
            Log.Error(exn, "FinalizeBattle")
            false

    member _.SendToWallet(champs:(uint64 * decimal) list, battleId:uint64, send:(unit -> (string * bool) option)) =
        try 
            use conn = new SqliteConnection(cs)
            Db.batch(fun tn ->
                for (champId, _) in champs do
                    Db.newCommandForTransaction SQL.WinthdrawFromBalance tn
                    |> Db.setParams [
                        "champId", SqlType.Int64 <| int64 champId
                    ]
                    |> Db.exec
                let r = send()
                match r with
                // ToDo: fix unconfirmed case
                | Some (tx, b) ->
                    for (champId, balance) in champs do
                        Db.newCommandForTransaction SQL.InsertRewardsPayed tn
                        |> Db.setParams [
                            "champId", SqlType.Int64 <| int64 champId
                            "battleId", SqlType.Int64 <| int64 battleId
                            "tx", SqlType.String <| tx
                            "rewards", SqlType.Decimal balance
                        ]
                        |> Db.exec

                        Db.newCommandForTransaction SQL.SetChampActionRewardsStatusToSend tn
                        |> Db.setParams [
                            "champId", SqlType.Int64 <| int64 champId
                            "battleId", SqlType.Int64 <| int64 battleId
                            "tx", SqlType.String <| tx
                            "rewards", SqlType.Decimal balance
                        ]
                        |> Db.exec
                    Some(tx)
                | None ->
                    tn.Rollback()
                    None) conn

        with exn ->
            let errors = StringBuilder()
            errors.AppendLine($"SendToWallet {battleId}") |> ignore
            for (champId, balance) in champs do
                errors.AppendLine($"{champId} -> {balance}") |> ignore
            Log.Error(exn, errors.ToString())
            None

    member _.SendToSpecialWallet(wt:WalletType, battleId:uint64, send:(decimal -> (string * bool) option)) =
        try 
            let numKey =
                match wt with
                | WalletType.DAO -> DbKeysNum.DAO
                | WalletType.Dev -> DbKeysNum.Dev
                | WalletType.Reserve -> DbKeysNum.Reserve
                | WalletType.Burn -> DbKeysNum.Burn
                | WalletType.Staking -> DbKeysNum.Staking
            use conn = new SqliteConnection(cs)
            match getKeyNum conn numKey with
            | Some v when v > 0M ->
                Db.batch(fun tn ->
                    Db.newCommandForTransaction SQL.SetKeyNum tn
                    |> Db.setParams [
                        "key", SqlType.String (numKey.ToString())
                        "value", SqlType.Decimal 0M
                    ]
                    |> Db.exec
                    let r = send v
                    match r with
                    // ToDo: fix unconfirmed case
                    | Some (tx, _) ->
                        Db.newCommandForTransaction SQL.InsertSpecialWithdrawal tn
                        |> Db.setParams [
                            "walletType", SqlType.Int32 <| int wt
                            "battleId", SqlType.Int64 <| int64 battleId
                            "tx", SqlType.String tx
                            "amount", SqlType.Decimal v
                        ]
                        |> Db.exec
                        Some(tx, v)
                    | None ->
                        tn.Rollback()
                        Log.Error("Unable to send tx")
                        None
                ) conn
            | Some _ ->
                Log.Information($"0 rewards for {wt} at {battleId}")
                None
            | None -> 
                Log.Error($"Unable to get {numKey}")
                None
        with exn ->
            Log.Error(exn, "SendToSpecialWallet")
            None

    member _.GetAllUnfinishedGenRequests() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.SelectUnfinishedRequests conn
            |> Db.query(fun r ->
                try
                    {|
                        ID = r.GetInt64(0)
                        UserId = r.GetInt64(1)
                        Status = r.GetInt32(2) |> enum<GenStatus>
                        Payload = JsonSerializer.Deserialize<GenPayload>(r.GetString(3), jsOptions)
                        Cost = r.GetDecimal(4)
                        MType = r.GetInt32(5) |> enum<MonsterType>
                        MSubType = r.GetInt32(6) |> enum<MonsterSubType>
                    |}
                    |> Some
                with exn ->
                    Log.Error(exn, "GetAllUnfinishedGenRequests.Query")
                    None
            )
            |> List.choose id
        with exn ->
            Log.Error(exn, "GetAllUnfinishedGenRequests")
            []

    member _.UpdateGenRequest(rId:int64, payload:GenPayload, userId:int64) =
        try
            let json = JsonSerializer.Serialize(payload, jsOptions)
            let status = payload.Status
            let isFinished = payload.IsFinished
            use conn = new SqliteConnection(cs)
            Db.batch (fun tn ->
                Db.newCommandForTransaction SQL.UpdateGenRequest tn
                |> Db.setParams [
                    "status", SqlType.Int <| int status
                    "payload", SqlType.String json
                    "isFinished", SqlType.Boolean isFinished
                    "id", SqlType.Int64 rId
                ]
                |> Db.exec

                if payload.IsFinalError then
                    tn
                    |> Db.newCommandForTransaction SQL.AddUserGenRequestRefund
                    |> Db.setParams [
                        "userId", SqlType.Int64 userId
                        "requestId", SqlType.Int64 rId
                    ]
                    |> Db.exec
                    true
                else
                    true
            ) conn
        with exn ->
            Log.Error(exn, $"UpdateGenRequest: {rId}")
            false

    member _.CreateCustomMonster(monster:MonsterRecord, mi:MonsterImg, reqId:int64, uid:int64, cost:decimal) =
        try
            use conn = new SqliteConnection(cs)
            Db.batch (fun tn ->
                let bytes = JsonSerializer.SerializeToUtf8Bytes(mi, jsOptions)

                let isMonsterNameExists =
                    Db.newCommandForTransaction SQL.IsMonsterNameExists tn
                    |> Db.setParams [
                        "name", SqlType.String monster.Name
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)

                let name' = 
                    if isMonsterNameExists then
                        let count = 
                            Db.newCommandForTransaction SQL.CountMonster tn
                            |> Db.scalar (fun v ->
                                tryUnbox<int64> v
                                |> Option.map(fun v -> v.ToString())
                                |> Option.defaultValue (Guid.NewGuid().ToString()))
                        $"{monster.Name} {count}"
                    else monster.Name
                // - create monster
                let monsterIdO = 
                    Db.newCommandForTransaction SQL.CreateMonster tn
                    |> Db.setParams [
                        "name", SqlType.String name'
                        "description", SqlType.String monster.Description
                        "img", SqlType.Bytes bytes
                        "health", SqlType.Int64 <| int64 monster.Stats.Health
                        "magic", SqlType.Int64 <| int64 monster.Stats.Magic
                            
                        "accuracy", SqlType.Int64 <| int64 monster.Stats.Accuracy
                        "luck", SqlType.Int64 <| int64 monster.Stats.Luck
                        "attack", SqlType.Int64 <| int64 monster.Stats.Attack
                        "mattack", SqlType.Int64 <| int64 monster.Stats.MagicAttack
                        "defense", SqlType.Int64 <| int64 monster.Stats.Defense
                        "mdefense", SqlType.Int64 <| int64 monster.Stats.MagicDefense
                        "type", SqlType.Int <| int monster.Monster.MType
                        "subtype", SqlType.Int <| int monster.Monster.MSubType
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                
                // - connect monster and user
                match monsterIdO with
                | Some mid ->
                    tn
                    |> Db.newCommandForTransaction SQL.ConnectMonsterToUser
                    |> Db.setParams [ 
                        "monsterId", SqlType.Int64 mid
                        "userId", SqlType.Int64 uid
                        "requestId", SqlType.Int64 reqId
                        "nftMonsterId", SqlType.Null
                    ]
                    |> Db.exec
                    
                    // add coins to rewards
                    tn
                    |> Db.newCommandForTransaction SQL.AddToKeyNum
                    |> Db.setParams [
                        "amount", SqlType.Decimal cost
                        "key", SqlType.String (DbKeysNum.Rewards.ToString())
                    ]
                    |> Db.exec
                    
                    // - change status to success and mark as finished
                    let payload = GenPayload.Success
                    let json = JsonSerializer.Serialize(payload, jsOptions)
                    let status = payload.Status
                    Db.newCommandForTransaction SQL.UpdateGenRequest tn
                    |> Db.setParams [
                        "status", SqlType.Int <| int status
                        "payload", SqlType.String json
                        "isFinished", SqlType.Boolean true
                        "id", SqlType.Int64 reqId
                    ]
                    |> Db.exec
                    Ok mid
                | None ->
                    tn.Rollback()
                    Error "Unexpected error"
            ) conn
        with exn ->
            Log.Error(exn, $"CreateNewMonster: {monster}")
            Error "Unexpected error"
    
    member _.IsMonsterDescriptionExists(description:string) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.IsMonsterDescriptionExists conn
            |> Db.setParams [
                "description", SqlType.String description
            ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)

        with exn ->
            Log.Error(exn, $"IsMonsterDescriptionExists: {description}")
            false

     member _.GetPendingUserRequests(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try 
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetPendingUserRequests conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.query (fun v -> GenRequest(v.GetInt64(0), v.GetDateTime(1), v.GetInt32(2) |> enum<GenStatus>))
                |> Ok
            with exn ->
                Log.Error(exn, $"GetPendingUserRequests: {userId}")
                Error("Unexpected error")
        | None -> Error("Unable to find user") 

     member _.GetUserMonsters(uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserMonsters conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.query (fun v ->
                    MonsterShortInfo(uint64 <| v.GetInt64(0), v.GetString(1), 
                        v.GetInt32(2) |> enum<MonsterType>, v.GetInt32(3) |> enum<MonsterSubType>,
                        JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData v 4, jsOptions), 
                        uint64 <| v.GetInt64(5)))
                |> Ok
            with exn ->
                Log.Error(exn, $"GetUserMonsters: {userId}")
                Error("Unexpected error")
        | None -> Error("Unable to find user")         

     member _.FilterUserMonsters(uId: UserId, mtype:MonsterType, msubtype: MonsterSubType) =
        match getUserIdByUserId uId with
        | Some userId ->
            try 
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.FilterUserMonsters conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                    "type", SqlType.Int <| int mtype
                    "subtype", SqlType.Int <| int msubtype
                ]
                |> Db.query (fun v -> v.GetInt64(0), v.GetString(1))
                |> Ok
            with exn ->
                Log.Error(exn, $"FilterUserMonsters: {userId} | {mtype} | {msubtype}")
                Error("Unexpected error")
        | None -> Error("Unable to find user")

     member _.RenameUserMonster(uId: UserId, newName:string, mid:int64) =
        match getUserIdByUserId uId with
        | Some userId ->
            try 
                use conn = new SqliteConnection(cs)
                let isMonsterNameExists =
                    Db.newCommand SQL.IsMonsterNameExists conn
                    |> Db.setParams [
                        "name", SqlType.String newName
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                if isMonsterNameExists then
                    Error("This name already taken")
                else
                    // ToDo: validate that user did create this monster
                    Db.newCommand SQL.RenameMonster conn
                    |> Db.setParams [
                        "userId", SqlType.Int64 userId
                        "newName", SqlType.String newName
                        "id", SqlType.Int64 mid
                    ]
                    |> Db.exec
                    |> Ok
            with exn ->
                Log.Error(exn, $"RenameUserMonster: {userId} | {newName} | {mid}")
                Error("Unexpected error")
        | None -> Error("Unable to find user")

    member _.ChampBelongsToAUser(champId:uint64, uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.ChampBelongsToAUser conn
                |> Db.setParams [
                    "champId", SqlType.Int64 <| int64 champId
                    "userId", SqlType.Int64 <| int64 userId
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
            with exn ->
                Log.Error(exn, "ChampBelongsToAUser")
                None
        | None -> None

    member _.ChampBelongsToAUserRaw(champId:int64, userId: uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.ChampBelongsToAUser conn
            |> Db.setParams [
                "champId", SqlType.Int64 champId
                "userId", SqlType.Int64 <| int64 userId
            ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
        with exn ->
            Log.Error(exn, "ChampBelongsToAUserRaw")
            None

    member _.MonsterBelongsToAUser(monsterId:uint64, uId: UserId) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.MonsterBelongsToAUser conn
                |> Db.setParams [
                    "monsterId", SqlType.Int64 <| int64 monsterId
                    "userId", SqlType.Int64 <| int64 userId
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
            with exn ->
                Log.Error(exn, "MonsterBelongsToAUser")
                None
        | None -> None

    member x.GetLastRoundParticipants(): Result<RoundParticipantChamp list, string> =
        match x.GetLastRoundId() with
        | Some roundId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetChampsFromRound conn
                |> Db.setParams [
                    "roundId", SqlType.Int64 <| int64 roundId
                ]
                |> Db.query (fun r -> RoundParticipantChamp(uint64 <| r.GetInt64(0), r.GetString(1), r.GetString(2)))
                |> Ok
            with exn ->
                Log.Error(exn, "GetLastRoundParticipants")
                Error("Unexpected error")
        | None -> Error("Something went wrong - unable to get round. Maybe there is no any?")
    
    /// includes stats from the level
    member _.GetCurrentBattleInfo() : Result<CurrentFullBattleInfo, string> =
        try
            use conn = new SqliteConnection(cs)

            let cbi =
                Db.newCommand SQL.GetLastBattleInfo conn
                |> Db.querySingle (fun r ->
                    let ownerId =
                        if r.IsDBNull(17) then None
                        else r.GetInt64(17) |> uint64 |> Some
                    let gtype =
                        if r.IsDBNull(18) then MonsterGenType.Generative
                        else
                            MonsterGenType.NFTBased(
                                r.GetInt64(18) |> uint64,
                                r.GetString(19))
                    CurrentBattleInfo(
                        uint64 <| r.GetInt64(0),
                        r.GetInt32(1) |> enum<BattleStatus>,
                        MonsterInfo(
                            uint64 <| r.GetInt32(2),
                            uint64 <| r.GetInt32(6),
                            r.GetString(3),
                            r.GetString(4),
                            JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData r 5, jsOptions),
                            {
                                Health = r.GetInt64(7)
                                Magic = r.GetInt64(8)

                                Accuracy = r.GetInt64(9)
                                Luck = r.GetInt64(10)

                                Attack = r.GetInt64(11)
                                MagicAttack = r.GetInt64(12)

                                Defense = r.GetInt64(13)
                                MagicDefense = r.GetInt64(14)
                            },
                            enum<MonsterType> <| r.GetInt32(15),
                            enum<MonsterSubType> <| r.GetInt32(16),
                            ownerId, gtype)))
                |> function
                    | Some v ->
                        let monster = v.Monster
                        let monsterLvl = Levels.getLvlByXp monster.XP
                        let monsterLvlStats = Monster.getMonsterStatsByLvl(monster.MType, monster.MSubType, monsterLvl)
                        v.WithMonsterInfo(v.Monster.WithStat(monster.Stat + monsterLvlStats))
                        |> Ok
                    | None -> Error("Unknown error")
            match cbi with
            | Ok cb ->
                let roundsInBattleO =
                    Db.newCommand SQL.RoundInfoByBattleId conn
                    |> Db.setParams [
                        "battleId", SqlType.Int64 <| int64 cb.BattleNum
                    ]
                    |> Db.querySingle(fun r ->
                        let count = r.GetInt32(0)
                        let timestamp = if r.IsDBNull(1) then DateTime.UtcNow else DateTime.SpecifyKind(r.GetDateTime(1), DateTimeKind.Utc)
                        let rewards = if r.IsDBNull(2) then 0M else r.GetDecimal(2)
                        CurrentRoundInfo(count, timestamp, rewards))
                match roundsInBattleO with
                | Some cri -> CurrentFullBattleInfo(cb, cri) |> Ok
                | None -> Error "Unable to read current round info"
            | Error err -> Error err
        with exn ->
            Log.Error(exn, "GetCurrentBattleInfo")
            Error("Unexpected error")

    member _.GetBattleHistory(battleId:uint64) : Result<RoundInfo list, string> =
        try
            use conn = new SqliteConnection(cs)
            let chActions =
                Db.newCommand SQL.GetBattleChampActions conn
                |> Db.setParams [
                    "battleId", SqlType.Int64 <| int64 battleId
                ]
                |> Db.query (fun r ->
                    uint64 <| r.GetInt64(0),
                    PMResult(
                        PMDetail.Champ(PMChamp(
                            System.Text.Json.JsonSerializer.Deserialize<PerformedMove>(Utils.getBytesData r 2, jsOptions),
                            RoundParticipantChamp(uint64 <| r.GetInt64(3), r.GetString(4), r.GetString(5)),
                            r.GetDateTime(1))),
                        r.GetInt64(6) |> uint64,
                        r.GetDecimal(7) |> Some)
                )
            
            let mActions =
                Db.newCommand SQL.GetBattleMonsterActions conn
                |> Db.setParams [
                    "battleId", SqlType.Int64 <| int64 battleId
                ]
                |> Db.query (fun r ->
                    uint64 <| r.GetInt64(0),
                    PMResult(
                        PMDetail.Monster (PMMonster(
                            System.Text.Json.JsonSerializer.Deserialize<PerformedMove>(Utils.getBytesData r 1, jsOptions),
                            if r.IsDBNull(2) then None
                            else Some(RoundParticipantChamp(uint64 <| r.GetInt64(2), r.GetString(3), r.GetString(4))))
                   ), r.GetInt64(5) |> uint64, None))
          
            let mRewards =
                Db.newCommand SQL.GetRewardsForBattle conn
                |> Db.setParams [
                    "battleId", SqlType.Int64 <| int64 battleId
                ]
                |> Db.query (fun r ->
                    uint64 <| r.GetInt64(0),
                    RoundReward(SpecialReward(r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(1), r.GetDecimal(4)), r.GetDecimal(5)))
                |> readOnlyDict

            let defeatedChamps =
                Db.newCommand SQL.GetListOfDefeatedChamps conn
                |> Db.setParams [
                    "battleId", SqlType.Int64 <| int64 battleId
                ]
                |> Db.query (fun r ->
                    uint64 <| r.GetInt64(0),
                    uint64 <| r.GetInt64(1))
                |> List.groupBy fst
                |> List.map(fun (k, gr) -> k, gr |> List.map snd)
                |> readOnlyDict

            let mDefeater =
                Db.newCommand SQL.GetMonsterDefeater conn
                |> Db.setParams [
                    "battleId", SqlType.Int64 <| int64 battleId
                ]
                |> Db.querySingle (fun r -> uint64 <| r.GetInt64(0))

            let res:RoundInfo list =
                mActions
                |> List.append chActions
                |> List.groupBy fst
                |> List.map(fun (rId, gr) ->
                    let chActs =
                        gr
                        |> List.choose(fun (_, pmr) ->
                            match pmr.Detail with
                            | PMDetail.Champ c -> (c.Timestamp, pmr) |> Some
                            | PMDetail.Monster _ -> None)
                        |> List.sortBy(fun (dt, _) -> dt)
                        |> List.map snd
                    let mActs =
                        gr
                        |> List.choose(fun (_, pmr) ->
                            match pmr.Detail with
                            | PMDetail.Champ _ -> None
                            | PMDetail.Monster _ -> Some pmr)
                    let mActionsFirst =
                        mActs
                        |> List.exists(fun pmr -> 
                            match pmr.Detail with 
                            | PMDetail.Monster m -> m.PM.Dmg.IsNone
                            | _ -> false)

                    let details =
                        if mActionsFirst then
                            chActs |> List.append mActs
                        else
                            mActs |> List.append chActs
                    let rRewards =
                        match mRewards.TryGetValue rId with
                        | true, r -> r
                        | false, _ -> RoundReward(SpecialReward(0M, 0M, 0M, 0M), 0M)

                    let dChamps =
                        match defeatedChamps.TryGetValue rId with
                        | true, xs -> xs
                        | false, _ -> []
                    RoundInfo(rId, details, rRewards, dChamps, mDefeater))
                |> List.sortByDescending (fun ri -> ri.RoundId)
            Ok res
        with exn ->
            Log.Error(exn, "GetBattleHistory")
            Error("Unexpected error")
    
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

            (GameStats(players, confirmedPlayers, champs, cMonsters, battles, rounds),
                playersEarned, rewards)
            |> Some
        with exn ->
            Log.Error(exn, "GetStats")
            None

    member _.UpdateDCPrice(nPrice:decimal) =
        try
            use conn = new SqliteConnection(cs)
            Db.batch (fun tn ->
                let key = DbKeys.LastTimePriceIsUpdated
                let cPrice =
                    Db.newCommandForTransaction SQL.GetKeyNum tn
                    |> Db.setParams [ "key", SqlType.String (DbKeysNum.DarkCoinPrice.ToString()) ]
                    |> Db.querySingle (fun rd -> rd.ReadDecimal "Value")
                    // if prev price doesn't exists set to current
                    |> Option.defaultValue nPrice
                
                Db.newCommandForTransaction SQL.SetKeyNum tn
                |> Db.setParams [
                    "key", SqlType.String (DbKeysNum.DarkCoinPrice.ToString())
                    "value", SqlType.Decimal nPrice
                ]
                |> Db.exec

                Db.newCommandForTransaction SQL.SetKeyNum tn
                |> Db.setParams [
                    "key", SqlType.String (DbKeysNum.DarkCoinPriceOld.ToString())
                    "value", SqlType.Decimal cPrice
                ]
                |> Db.exec

                Db.newCommandForTransaction SQL.SetKey tn
                |> Db.setParams [
                    "key", SqlType.String (key.ToString())
                    "value", SqlType.DateTime DateTime.UtcNow
                ]
                |> Db.exec

                true
            ) conn
        with exn ->
            Log.Error(exn, "UpdateDCPrice")
            false

    member _.GetFailedGen() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetPendingRefunds conn
            |> Db.query(fun r -> {|
                GenRequestRefundId = r.GetInt64(0)
                Amount = r.GetDecimal(1)
                Sender = r.GetString(2)
            |})
        with exn ->
            Log.Error(exn, "GetFailedGen")
            []

    member _.CloseFailedGen(rId:int64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.FinishUserGenRequestRefund conn
            |> Db.setParams [
                "id", SqlType.Int64 rId
            ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"CloseFailedGen: {rId}")
            false

    member _.SetOutputTxForUserGenRequestRefund(rId:int64, tx:string) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.SetOutputTxForUserGenRequestRefund conn
            |> Db.setParams [
                "tx", SqlType.String tx
                "id", SqlType.Int64 rId
            ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"SetOutputTxForUserGenRequestRefund: {rId} | {tx}")
            false

    member _.ReopenFailedGen(rId:int64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.ReOpenUserGenRequestRefund conn
            |> Db.setParams [
                "id", SqlType.Int64 rId
            ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"ReopenFailedGen: {rId}")
            false

    member _.GetPendingTxRefunds() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetPendingTxRefunds conn
            |> Db.query(fun r -> {|
                TxId   = r.GetInt64(0)
                Wallet = r.GetString(1)
                Amount = r.GetDecimal(2)
            |})
        with exn ->
            Log.Error(exn, "GetPendingTxRefunds")
            []

    member _.ClosePendingTxRefund(txId: int64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.ClosePendingTxRefund conn
            |> Db.setParams [ "id", SqlType.Int64 txId ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"ClosePendingTxRefund: {txId}")
            false

    member _.ReopenPendingTxRefund(txId: int64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.ReopenPendingTxRefund conn
            |> Db.setParams [ "id", SqlType.Int64 txId ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"ReopenPendingTxRefund: {txId}")
            false

    member _.AddTxRevertHistory(txId: int64, outTx: string) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.AddTxRevertHistory conn
            |> Db.setParams [
                "txId",  SqlType.Int64  txId
                "outTx", SqlType.String outTx
            ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"AddTxRevertHistory: {txId} | {outTx}")
            false

    member _.GetChampIdByAssetId(assetId:uint64) =
        try
            use conn = new SqliteConnection(cs)
            
            Db.newCommand SQL.GetChampIdByAssetId conn
            |> Db.setParams [
                "assetId", SqlType.Int64 <| int64 assetId
            ]
            |> Db.querySingle (fun r -> r.GetInt64(0))
        with exn ->
            Log.Error(exn, $"GetChampIdByAssetId : {assetId}")
            None

    member _.GetAllConfirmedWallets(): (uint64 * string) list =
        try
            use conn = new SqliteConnection(cs)
            
            Db.newCommand SQL.GetConfirmedWallets conn
            |> Db.query(fun r -> uint64 <| r.GetInt64(0), r.GetString(1))
        with exn ->
            Log.Error(exn, $"GetAllConfirmedWallets")
            []

    member _.AddCreateNFTBasedMonsterRequest(uId:UserId, assetId: uint64, name: string, description:string, ipfs:string, eLink:string) =
        match getUserIdByUserId uId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                
                // if there is a Monster with this AssetId - return Error
                // if there is a request from another user - return Error
                // if there is a request from the same user - update

                let monsterExists =
                    Db.newCommand SQL.NFTBasedMonsterExists conn
                    |> Db.setParams [ "assetId",  SqlType.Int64 <| int64 assetId ]
                    |> Db.scalar (fun v -> (unbox<int64> v) = 1L)

                if monsterExists then Error("Monster based on this NFT already exists")
                else
                    let requestExists =
                        Db.newCommand SQL.NFTBasedMonsterExists conn
                        |> Db.setParams [ 
                            "assetId",  SqlType.Int64 <| int64 assetId
                            "userId",  SqlType.Int64 <| int64 userId
                        ]
                        |> Db.scalar (fun v -> (unbox<int64> v) = 1L)                    

                    if requestExists then Error("There is pending request to create a monster based on this NFT")
                    else
                        let userRequestID =
                            Db.newCommand SQL.GetUserNFTBasedMonsterRequestID conn
                            |> Db.setParams [ 
                                "assetId",  SqlType.Int64 <| int64 assetId
                                "userId",  SqlType.Int64 <| int64 userId
                            ]
                            |> Db.querySingle (fun r ->
                                if r.IsDBNull(0) then None else Some(r.GetInt64(0)))
                            |> Option.flatten
                        let bytes = JsonSerializer.SerializeToUtf8Bytes(MonsterImg.Ipfs ipfs, jsOptions)
                        match userRequestID with
                        | Some rId ->
                            Db.newCommand SQL.UpdateNFTMonsterCreationRequest conn
                            |> Db.setParams [
                                "name", SqlType.String name
                                "description", SqlType.String description
                                "picture", SqlType.Bytes bytes
                                "eLink", SqlType.String eLink
                                "rId",  SqlType.Int64 rId
                            ]
                            |> Db.exec
                            rId
                        | None ->
                            Db.newCommand SQL.CreateNFTMonsterCreationRequest conn
                            |> Db.setParams [
                                "userId",  SqlType.Int64 <| int64 userId
                                "assetId",  SqlType.Int64 <| int64 assetId
                                "name", SqlType.String name
                                "description", SqlType.String description
                                "picture", SqlType.Bytes bytes
                                "eLink", SqlType.String eLink
                            ]
                            |> Db.scalar (fun v -> unbox<int64> v)
                        |> Ok
            with exn ->
                Log.Error(exn, $"AddCreateNFTBasedMonsterRequest")
                Error exn.Message
        | None -> Error("Unable to find user")
        
    member _.GetDateTimeKey(key:DbKeys) =
        use conn = new SqliteConnection(cs)
        getKeyDateTime conn key
    member _.SetDateTimeKey(key:DbKeys, dt:DateTime) =
        use conn = new SqliteConnection(cs)
        setKeyDateTime conn (key, dt)

    member _.GetNumKey(key:DbKeysNum) =
        use conn = new SqliteConnection(cs)
        getKeyNum conn key
    member _.SetNumKey(key:DbKeysNum, d:decimal) =
        use conn = new SqliteConnection(cs)
        setKeyNum conn (key, d)
    
    member _.GetBoolKey(key:DbKeysBool) =
        use conn = new SqliteConnection(cs)
        getKeyBool conn key
    member _.SetBoolKey(key:DbKeysBool, d:bool) =
        use conn = new SqliteConnection(cs)
        setKeyBool conn (key, d)