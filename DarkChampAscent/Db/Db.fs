namespace Db

open GameLogic.Rewards
open GameLogic.Champs
open GameLogic.Monsters
open Db.Sqlite
open Gen

type DbKeys =
    | LastTrackedChampCfg = 0
    | LastProcessedDeposit = 1
    | LastTimePriceIsUpdated = 2
    | LastTimeConfirmationCodeChecked = 3
    | LastTimeBackupPerformed = 4

type DbKeysNum =
    | Rewards = 0
    | Reserve = 1
    | Dev = 2
    | Burn = 3
    | DAO = 4
    | DarkCoinPrice = 5 // => itemPriceDarkCoin = itemPriceUsd / darkCoinPriceUsd
    | Locked = 6

type WalletType =
    | DAO = 0
    | Dev = 1
    | Reserve = 2
    | Burn = 3

type DbKeysBool =
    | InitBalanceIsSet = 0
    | BalanceCheckIsPassed = 1
    | LockedKeyIsSet = 2

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

[<RequireQualifiedAccess>]
type ProcessDepositStatus = 
    | Success = 0
    | Donation = 1
    | Error = 2

open Microsoft.Data.Sqlite
open Donald
open System
open GameLogic.Effects
open GameLogic.Shop
open GameLogic.Battle
open Serilog
open System.Text.Json
open System.Text.Json.Serialization

type SqliteStorage(cs: string)=
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

    let getKeyDateTime (conn:SqliteConnection) (key:DbKeys) =
        try
            Db.newCommand SQL.GetKey conn
            |> Db.setParams [ "key", SqlType.String (key.ToString()) ]
            |> Db.querySingle (fun rd -> rd.ReadDateTime "Value")
        with exn ->
            Log.Error(exn, $"getKeyDateTime: {key}")
            None
    
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
                        "value", SqlType.Boolean false
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
    let setLockedKey(conn:SqliteConnection) =
        try
            let sql = "INSERT INTO KeyValueNum(Key, Value) VALUES (@key, 0.000000)"
            Db.batch(fun tn ->
                let isInit =
                    Db.newCommandForTransaction SQL.GetKeyBool tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysBool.LockedKeyIsSet.ToString())
                        "value", SqlType.Boolean false
                    ]
                    |> Db.querySingle (fun rd -> rd.ReadBoolean "Value")
                    |> Option.defaultValue false
                if isInit |> not then
                    Db.newCommandForTransaction sql tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysNum.Locked.ToString())
                    ]
                    |> Db.exec

                    Db.newCommandForTransaction SQL.SetKeyBool tn
                    |> Db.setParams [
                        "key", SqlType.String (DbKeysBool.LockedKeyIsSet.ToString())
                        "value", SqlType.Boolean true
                    ]
                    |> Db.exec
            ) conn
            
            true
        with exn ->
            Log.Error(exn, "setLockedKey")
            false

    do Log.Information("Db is init....")

    let _conn = new SqliteConnection(cs)
    do Db.newCommand SQL.createTablesSQL _conn |> Db.exec
    
    do updateShop(_conn) |> ignore
    do updateEffects(_conn) |> ignore
    do setInitBalance(_conn) |> ignore
    do setLockedKey(_conn) |> ignore

    do _conn.Dispose()
    do Log.Information("Db init is finished")

    let userExists(discordId: uint64) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.UserExists conn
            |> Db.setParams [ "discordId", SqlType.Int64 <| int64 discordId ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, $"userExists: {discordId}")
            false

    let walletExists(wallet: string) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.WalletExists conn
            |> Db.setParams [ "wallet", SqlType.String wallet ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
        with exn ->
            Log.Error(exn, $"walletExists: {wallet}")
            false

    let getUserIdByDiscordId(discordId: uint64) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetUserIdByDiscordId conn
            |> Db.setParams [ "discordId", SqlType.Int64 <| int64 discordId ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
        with exn ->
            Log.Error(exn, $"getUserIdByDiscordId: {discordId}")
            None

    let getShopItemIdByItem(shopItem: ShopItem) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetShopItemIdByItem conn
            |> Db.setParams [ "item", SqlType.Int32 <| int shopItem ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
        with exn ->
            Log.Error(exn, $"getShopItemIdByItem: {shopItem}")
            None

    let registerNewUser(discordId:uint64) =
        if userExists discordId then
            false
        else
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.AddNewUser conn
                |> Db.setParams [ "discordId", SqlType.Int64 <| int64 discordId ]
                |> Db.exec
                true
            with exn ->
                Log.Error(exn, $"registerNewUser: {discordId}")
                false
             
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

    member _.FindUserIdByWallet(wallet: string) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetUserIdByWallet conn
            |> Db.setParams [ "wallet", SqlType.String <| wallet ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
        with exn ->
            Log.Error(exn, $"FindUserIdByWallet: {wallet}")
            None
    
    member _.FindUserIdByDiscordId = getUserIdByDiscordId

    member _.FindDiscordIdByWallet(wallet: string) =
        try 
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetDiscordIdByWallet conn
            |> Db.setParams [ "wallet", SqlType.String <| wallet ]
            |> Db.scalar (fun v -> tryUnbox<int64> v)
        with exn ->
            Log.Error(exn, $"FindDiscordIdByWallet: {wallet}")
            None

    member _.RegisterNewWallet(discordId:uint64, wallet:string) =
        let isRegistered = 
            userExists discordId || registerNewUser discordId
        if isRegistered then
            let isWalletExists = walletExists wallet
            if isWalletExists then
                Error("This wallet already registered")
            else
                let userId = getUserIdByDiscordId discordId
                match userId with
                | Some id ->
                    try
                        // ToDo: improve
                        let code = System.Random.Shared.NextInt64(10000L, 99999L) |> string
                        use conn = new SqliteConnection(cs)
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
    
    member _.DeactivateWallet(discordId:uint64, wallet:string) =
        let isRegistered = 
            userExists discordId || registerNewUser discordId
        if isRegistered then
            let isWalletExists = walletExists wallet
            if isWalletExists then
                let userId = getUserIdByDiscordId discordId
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

    member _.UpdateUserForChamp(discordId:uint64, assetId:uint64) =
        let userId = getUserIdByDiscordId discordId
        match userId with
        | Some id ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.UpsertChampAndUser conn
                |> Db.setParams [ 
                    "userId", SqlType.Int64 id
                    "assetId", SqlType.Int64 <| int64 assetId
                ]
                |> Db.exec
                Ok(())
            with exn ->
                Log.Error(exn, $"UpdateUserForChamp: {discordId}, {assetId}")
                Error("Something went wrong: unable to attach champ to user")              
        | None -> Error("Can't find user")    
    
    member _.GetUserWallets(discordId:uint64) =
        let userId = getUserIdByDiscordId discordId
        match userId with
        | Some id ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserWallets conn
                |> Db.setParams [ 
                    "userId", SqlType.Int64 id
                ]
                |> Db.query(fun r ->
                    {|
                        Wallet = r.GetString(0)
                        IsConfirmed = r.GetBoolean(1)
                        IsActive = r.GetBoolean(2)
                        Code = r.GetString(3)
                    |}
                )
                |> Ok
            with exn ->
                Log.Error(exn, $"GetUserWallets: {discordId}")
                Error("Something went wrong")              
        | None -> Error("Can't find user")    
    
    member t.Donate(discordId: uint64, amount:decimal) =
        match getUserIdByDiscordId discordId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                let balance =
                    Db.newCommand SQL.GetUserBalance conn
                    |> Db.setParams [
                        "userId", SqlType.Int64 userId
                    ]
                    |> Db.querySingle (fun r -> r.GetDecimal(0))
                match balance with
                | Some b ->
                    if b >= amount then
                        Db.batch(fun tn ->
                            let amount' = Math.Round(amount, 6)
                            let newBalance = b - amount'
                            tn
                            |> Db.newCommandForTransaction SQL.UpdateUserBalance
                            |> Db.setParams [
                                "balance", SqlType.Decimal newBalance
                                "userId", SqlType.Int64 userId
                            ]
                            |> Db.exec

                            tn
                            |> Db.newCommandForTransaction SQL.InsertInGameDonation
                            |> Db.setParams [ 
                                "userId", SqlType.Int64 userId
                                "amount", SqlType.Decimal amount'
                            ]
                            |> Db.exec

                            tn
                            |> Db.newCommandForTransaction SQL.AddToKeyNum
                            |> Db.setParams [
                                "amount", SqlType.Decimal amount'
                                "key", SqlType.String (DbKeysNum.Rewards.ToString())
                            ]
                            |> Db.exec
                    
                            Ok(())
                        ) conn
                    else Error($"Well, you have only {b} on your balance")
                | None ->
                    Error("Unexpected error")
            with exn ->
                Log.Error(exn, $"Donate: {discordId}")
                Error("Unexpected error")
        | None -> Error("Unexpected error")

    member t.AddOrInsertChamp(champ:NewChampDb) =
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
        try
            use conn = new SqliteConnection(cs)
            let monsterExists =
                Db.newCommand SQL.MonsterExistsByName conn
                |> Db.setParams [ "name", SqlType.String monster.Name ]
                |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
            if monsterExists |> not then
                let img = MonsterImg.DefaultFile monster.Monster
                let options = JsonFSharpOptions().ToJsonSerializerOptions()
                let bytes = JsonSerializer.SerializeToUtf8Bytes(img, options)
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

    member _.ProcessDeposit(wallet:string, tx:string, amount:decimal) =
        try
            use conn = new SqliteConnection(cs)

            Db.batch (fun tn ->
                let userId = 
                    tn
                    |> Db.newCommandForTransaction SQL.GetUserIdByWallet
                    |> Db.setParams [ 
                        "wallet", SqlType.String wallet
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                let donationExists =
                    tn
                    |> Db.newCommandForTransaction SQL.DonationExists
                    |> Db.setParams [
                        "tx", SqlType.String tx
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                match userId with
                | Some uid ->
                    let depositExists =
                        tn
                        |> Db.newCommandForTransaction SQL.DepositExists
                        |> Db.setParams [
                            "tx", SqlType.String tx
                        ]
                        |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)
                    if depositExists || donationExists then ProcessDepositStatus.Success
                    else
                        tn
                        |> Db.newCommandForTransaction SQL.InsertDeposit
                        |> Db.setParams [ 
                            "wallet", SqlType.String wallet
                            "tx", SqlType.String tx
                            "amount", SqlType.Decimal amount
                        ]
                        |> Db.exec
                        
                        let balance =
                            tn
                            |> Db.newCommandForTransaction SQL.GetUserBalance
                            |> Db.setParams [
                                "userId", SqlType.Int64 uid 
                            ]
                            |> Db.querySingle (fun r -> r.GetDecimal(0))
                       
                        match balance with
                        | Some b ->
                            let newBalance = b + amount
                            tn
                            |> Db.newCommandForTransaction SQL.UpdateUserBalance
                            |> Db.setParams [
                                "balance", SqlType.Decimal newBalance
                                "userId", SqlType.Int64 uid
                            ]
                            |> Db.exec
                            ProcessDepositStatus.Success
                        | None -> tn.Rollback(); ProcessDepositStatus.Error
                | None ->
                    if donationExists then ProcessDepositStatus.Success
                    else
                        tn
                        |> Db.newCommandForTransaction SQL.InsertDonation
                        |> Db.setParams [ 
                            "wallet", SqlType.String wallet
                            "tx", SqlType.String tx
                            "amount", SqlType.Decimal amount
                        ]
                        |> Db.exec

                        tn
                        |> Db.newCommandForTransaction SQL.AddToKeyNum
                        |> Db.setParams [
                            "amount", SqlType.Decimal amount
                            "key", SqlType.String (DbKeysNum.Rewards.ToString())
                        ]
                        |> Db.exec
                        ProcessDepositStatus.Donation
            ) conn
        with exn ->
            Log.Error(exn, $"ProcessDeposit: {wallet}, {tx}, {amount}")
            ProcessDepositStatus.Error

    member _.GetUserBalance(discordId: uint64) =
        match getUserIdByDiscordId discordId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserBalance conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.querySingle (fun r -> r.GetDecimal(0))
            with exn ->
                Log.Error(exn, $"GetUserBalance: {discordId}")
                None
        | None -> None

    member _.BuyItem(discordId:uint64, item:ShopItem, amount:int) =
        try
            use conn = new SqliteConnection(cs)

            Db.batch (fun tn ->
                let userId = 
                    tn
                    |> Db.newCommandForTransaction SQL.GetUserIdByDiscordId
                    |> Db.setParams [ 
                        "discordId", SqlType.Int64 <| int64 discordId
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                let itemId =
                    tn
                    |> Db.newCommandForTransaction SQL.GetShopItemIdByItem
                    |> Db.setParams [
                        "item", SqlType.Int <| int item
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                match userId, itemId with
                | Some uid, Some iid ->
                    let balance =
                        tn
                        |> Db.newCommandForTransaction SQL.GetUserBalance
                        |> Db.setParams [
                            "userId", SqlType.Int64 uid 
                        ]
                        |> Db.querySingle (fun r -> r.GetDecimal(0))
                    let darkCoinPrice = getKeyNum conn DbKeysNum.DarkCoinPrice
                    match balance, darkCoinPrice with
                    | Some b, Some dcPrice ->
                        let price = Math.Round(Shop.getPrice item / dcPrice, 6)
                        let subs = Math.Round(decimal amount * price, 6)
                        if subs > b then Error($"Your balance is {b} while {subs} is required")
                        else
                            let newBalance = Math.Round(b - subs, 6)
                            tn
                            |> Db.newCommandForTransaction SQL.UpdateUserBalance
                            |> Db.setParams [
                                "balance", SqlType.Decimal newBalance
                                "userId", SqlType.Int64 uid
                            ]
                            |> Db.exec

                            tn
                            |> Db.newCommandForTransaction SQL.PurchaseItem
                            |> Db.setParams [
                                "userId", SqlType.Int64 uid
                                "itemId", SqlType.Int64 iid
                                "price", SqlType.Decimal dcPrice
                                "amount", SqlType.Int amount
                            ]
                            |> Db.exec
                            
                            tn
                            |> Db.newCommandForTransaction SQL.AddToStorage
                            |> Db.setParams [
                                "userId", SqlType.Int64 uid
                                "itemId", SqlType.Int64 iid
                                "amount", SqlType.Int amount
                            ]
                            |> Db.exec

                            tn
                            |> Db.newCommandForTransaction SQL.AddToKeyNum
                            |> Db.setParams [
                                "amount", SqlType.Decimal subs
                                "key", SqlType.String (DbKeysNum.Rewards.ToString())
                            ]
                            |> Db.exec
                            Ok(())
                    | None, Some _ -> Error("Can't fetch balance. Try again later")
                    | Some _, None -> Error("Can't fetch price. Try again later")
                    | _, _ -> Error("Can't fetch balance and price")
                | Some _, None ->
                    Error("Item not found")
                | None, Some _ ->
                    Error("User not found")
                | None, None -> Error("User and Item not found")
            ) conn
        with exn ->
            Log.Error(exn, $"Buy item: {discordId}, {item}, {amount}")
            Error($"Unexpected error: {exn.Message}")
    
    member _.RenameChamp(discordId:uint64, champName:string, name:string) =
        try
            use conn = new SqliteConnection(cs)

            Db.batch (fun tn ->
                let userId = 
                    tn
                    |> Db.newCommandForTransaction SQL.GetUserIdByDiscordId
                    |> Db.setParams [ 
                        "discordId", SqlType.Int64 <| int64 discordId
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                match userId with
                | Some uid ->
                    let champIdO =
                        Db.newCommandForTransaction SQL.GetChampIdByName tn
                        |> Db.setParams [
                            "name", SqlType.String champName
                        ]
                        |> Db.querySingle (fun r -> r.GetInt64(0))
                    match champIdO with
                    | Some champId ->
                        let champsIds =
                            Db.newCommandForTransaction SQL.GetChampIdsForUser tn
                            |> Db.setParams [
                                "userId", SqlType.Int64 uid
                            ]
                            |> Db.query (fun r -> r.GetInt64(0))
                            |> Set.ofList
                        if champsIds.Contains champId then
                            let balance =
                                tn
                                |> Db.newCommandForTransaction SQL.GetUserBalance
                                |> Db.setParams [
                                    "userId", SqlType.Int64 uid 
                                ]
                                |> Db.querySingle (fun r -> r.GetDecimal(0))
                            let darkCoinPrice = getKeyNum conn DbKeysNum.DarkCoinPrice
                            match balance, darkCoinPrice with
                            | Some b, Some dcPrice ->
                                let subs = Math.Round(Shop.RenamePrice / dcPrice, 6)
                                if subs > b then Error($"Your balance is {b} while {subs} is required")
                                else
                                    let newBalance = Math.Round(b - subs, 6)
                                    tn
                                    |> Db.newCommandForTransaction SQL.UpdateUserBalance
                                    |> Db.setParams [
                                        "balance", SqlType.Decimal newBalance
                                        "userId", SqlType.Int64 uid
                                    ]
                                    |> Db.exec

                                    tn
                                    |> Db.newCommandForTransaction SQL.AddToKeyNum
                                    |> Db.setParams [
                                        "amount", SqlType.Decimal subs
                                        "key", SqlType.String (DbKeysNum.Rewards.ToString())
                                    ]
                                    |> Db.exec

                                    tn
                                    |> Db.newCommandForTransaction SQL.RenameChamp
                                    |> Db.setParams [
                                        "newName", SqlType.String name
                                        "oldName", SqlType.String champName
                                    ]
                                    |> Db.exec

                                    tn
                                    |> Db.newCommandForTransaction SQL.InsertNewOldNames
                                    |> Db.setParams [
                                        "champId", SqlType.Int64 <| int64 champId
                                        "userId", SqlType.Int64 <| int64 uid
                                        "price", SqlType.Decimal subs
                                        "oldName", SqlType.String champName
                                        "name", SqlType.String name
                                    ]
                                    |> Db.exec 
                                    Ok(())
                            | None, Some _ -> Error("Can't fetch balance. Try again later")
                            | Some _, None -> Error("Can't fetch price. Try again later")
                            | _, _ -> Error("Can't fetch balance and price")
                        else
                            Error("This champ doesn't belong to you")
                    | None -> Error($"Champ with name = {champName} not found")
                | None ->
                    Error("User not found")
            ) conn
        with exn ->
            Log.Error(exn, $"Rename champ: {discordId}, {champName}")
            Error($"Unexpected error: {exn.Message}")

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

    member _.GetUserStorage(discordId:uint64) =
        match getUserIdByDiscordId discordId with
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
                Log.Error(exn, $"GetUserStorage: {discordId}")
                None
        | None -> None

    member _.GetUserChamps(discordId: uint64) =
        match getUserIdByDiscordId discordId with
        | Some userId ->
            try
                use conn = new SqliteConnection(cs)
                Db.newCommand SQL.GetUserChamps conn
                |> Db.setParams [
                    "userId", SqlType.Int64 userId
                ]
                |> Db.query (fun r ->
                   {|
                        ID = r.GetInt64(0)
                        Name = r.GetString(1)
                        AssetId = r.GetInt64(2)
                        Ipfs = r.GetString(3)
                        Balance = Math.Round(r.GetDecimal(4), 6)
                   |}
                )
                |> Some
            with exn ->
                Log.Error(exn, $"GetUserChamps {discordId}")
                None
        | None -> None        

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
                   {|
                        Id = r.GetInt64(0)
                        Name = r.GetString(1)
                   |}
                )
                |> Ok
            with exn ->
                Log.Error(exn, $"GetActiveUserChamps {discordId}")
                Error("Unexpected error")
        | None -> Error("Unable to find user")

    member _.GetUserChampsUnderEffect(discordId: uint64, roundId: uint64) =
        match getUserIdByDiscordId discordId with
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
                   {|
                        Id = r.GetInt64(0)
                        Name = r.GetString(1)
                        EndsAt = endsAt
                        Item = r.GetInt32(4) |> enum<Effect>
                        RoundsLeft = endsAt - int64 roundId
                   |}
                )
                |> Ok
            with exn ->
                Log.Error(exn, $"GetUserChampsUnderEffect {discordId} at {roundId}")
                Error("Unexpected error")
        | None -> Error("Unable to find user")

    member _.GetMonstersUnderEffect(roundId: uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetMonstersUnderEffect  conn
            |> Db.setParams [
                "roundId", SqlType.Int64 <| int64 roundId
            ]
            |> Db.query (fun r ->
                let endsAt = (r.GetInt64(3)) + int64 (r.GetInt32(4))
                {|
                    Name = r.GetString(0)
                    MType = enum<MonsterType> <| r.GetInt32(1)
                    MSubType = enum<MonsterSubType> <| r.GetInt32(2)
                    EndsAt = endsAt
                    Item = r.GetInt32(5) |> enum<Effect>
                    RoundsLeft = endsAt - int64 roundId
                |}
            )
            |> Ok
        with exn ->
            Log.Error(exn, $"GetMonstersUnderEffect at {roundId}")
            Error("Unexpected error")

    member _.GetUserChampsWithStats(discordId: uint64) =
        match getUserIdByDiscordId discordId with
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
                Log.Error(exn, $"GetUserChampsWithStats {discordId}")
                None
        | None -> None 

    member _.GetChampInfo(assetId: uint64): ChampInfo option =
        try
            use conn = new SqliteConnection(cs)
            let champIdO =
                Db.newCommand SQL.GetChampIdByAssetId conn
                |> Db.setParams [
                    "assetId", SqlType.Int64 <| int64 assetId
                ]
                |> Db.querySingle (fun r -> r.GetInt64(0))
            match champIdO with
            | Some champId ->
                let lvledChars =
                    Db.newCommand SQL.GetLvledCharsForChamp conn
                    |> Db.setParams [
                        "champId", SqlType.Int64 champId
                    ]
                    |> Db.scalar (fun v -> unbox<int64> v)
                let baseChampInfoOpt =
                    Db.newCommand SQL.GetChampInfo conn
                    |> Db.setParams [
                        "assetId", SqlType.Int64 <| int64 assetId
                    ]
                    |> Db.querySingle (fun r ->
                    {
                        ID = uint64 <| r.GetInt64(0)
                        Name = r.GetString(1)
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

                        Traits = {
                            Background = r.GetInt32(13) |> enum<Background>
                            Skin = r.GetInt32(14) |> enum<Skin>
                            Weapon = r.GetInt32(15) |> enum<Weapon>
                            Magic = r.GetInt32(16) |> enum<Magic>
                            Head = r.GetInt32(17) |> enum<Head>
                            Armour = r.GetInt32(18) |> enum<Armour>
                            Extra = r.GetInt32(19) |> enum<Extra>
                        }

                        BoostStat = None
                        LevelsStat = None
                        LeveledChars = uint64 lvledChars
                    })
            
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
                
                        {
                            baseChampInfo with
                                BoostStat = Some boosts
                                LevelsStat = Some lvls
                        })
                | None -> None
            | None ->
                Log.Error($"Unable to find champ with {assetId}")
                None
        with exn ->
            Log.Error(exn, $"GetChampInfo {assetId}")
            None

    member _.GetBalances() =
        try
            use conn = new SqliteConnection(cs)
            let poolO = getKeyNum conn DbKeysNum.Rewards
            let reserveO = getKeyNum conn DbKeysNum.Reserve
            let devO = getKeyNum conn DbKeysNum.Dev
            let daoO = getKeyNum conn DbKeysNum.DAO
            let burnO = getKeyNum conn DbKeysNum.Burn
            let userO =
                Db.newCommand SQL.GetUsersBalance conn
                |> Db.querySingle (fun r -> if r.IsDBNull(0) then 0M else r.GetDecimal(0))
            let champO =
                Db.newCommand SQL.GetChampsBalance conn
                |> Db.querySingle (fun r -> if r.IsDBNull(0) then 0M else r.GetDecimal(0))
            let lockedO = 
                Db.newCommand SQL.GetKeyNum conn
                |> Db.setParams [ "key", SqlType.String (DbKeysNum.Locked.ToString()) ]
                |> Db.querySingle (fun r -> if r.IsDBNull(0) then 0M else r.GetDecimal(0))

            match poolO, reserveO, devO, daoO, burnO, userO, champO, lockedO with
            | Some pool, Some reserve, Some dev, Some dao, Some burn, Some user, Some champ, Some locked ->
                {
                    DAO = dao
                    Reserve = reserve
                    Dev = dev
                    Burn = burn
                    Rewards = pool
                    Users = user
                    Champs = champ
                    Locked = locked
                }
                |> Ok
            | _, _, _, _, _,_,_,_ ->
                let err = $"rewards: {poolO.IsSome}; Reserve: {reserveO.IsSome}; Dev: {devO.IsSome}; Burn: {burnO.IsSome}; User: {userO.IsSome}; Champ:{champO.IsSome}; Locked:{lockedO.IsSome}"
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

    member _.GetUserEarnings(discordId: uint64, startRound:uint64, endRound:uint64) =
        match getUserIdByDiscordId discordId with
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
                Log.Error(exn, $"GetUserEarnings {discordId}: [{startRound}-{endRound}]")
                None
        | None -> None 

    member t.UseItemFromStorage(discordId: uint64, item:ShopItem, champId: uint64) =
        try
            use conn = new SqliteConnection(cs)
            let userIdOpt = 
                Db.newCommand SQL.GetUserIdByDiscordId conn
                |> Db.setParams [ 
                    "discordId", SqlType.Int64 <| int64 discordId
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v)
            let itemIdO =
                Db.newCommand SQL.GetShopItemIdByItem conn
                |> Db.setParams [
                    "item", SqlType.Int <| int item
                ]
                |> Db.scalar (fun v -> tryUnbox<int64> v)

            let lastRoundId =
                Db.newCommand SQL.GetLastRound conn
                |> Db.scalar (fun v -> tryUnbox<int64> v)

            match userIdOpt, itemIdO, lastRoundId with
            | Some userId, Some itemId, Some roundId ->
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
                                    Db.newCommand SQL.GetEffectItemIdByItem conn
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
                | Some v -> Error("You don't have this item in the storage")
                | None -> Error("Record not found in storage")
            | _, _, _ ->
                let sb = new System.Text.StringBuilder();
                if(userIdOpt.IsNone) then sb.Append("User not found; ") |> ignore
                if(itemIdO.IsNone) then sb.Append("Item not found;") |> ignore
                if(lastRoundId.IsNone) then sb.Append("Round not found; ") |> ignore
                Error(sb.ToString())
        with exn ->
            Log.Error(exn, $"UseItemFromStorage: {discordId}, {item}")
            Error($"Unexpected error: {exn.Message}")        
    
    member _.LevelUp(champId:uint64, ch:Characteristic) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.InsertChampLvl conn
            |> Db.setParams [
                "champId", SqlType.Int64 <| int64 champId
                "characteristic", SqlType.Int32 <| int ch
            ]
            |> Db.exec
            true
        with exn ->
            Log.Error(exn, $"LevelUp: {champId}, {ch}")
            false

    // ToDo: optimize if/when monsters > 100
    member _.GetRandomMonsterId() =
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
                |> Db.query (fun v -> v.GetInt64(0))
                |> List.randomChoice
                |> Ok
            | None ->
                Db.newCommand SQL.GetMonsters conn
                |> Db.query (fun v -> v.GetInt64(0))
                |> List.randomChoice
                |> Ok
        with exn ->
            Log.Error(exn, "GetRandomMonster")
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
    
    member t.RoundsExists() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.RoundExists conn
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
        with exn ->
            Log.Error(exn, "RoundsExists")
            None
    
    member t.AnyChampJoinedRound(roundId:uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.ChampsPlayedInRound conn
            |> Db.setParams [
                "roundId", SqlType.Int64 <| int64 roundId
            ]
            |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0))
        with exn ->
            Log.Error(exn, "RoundsExists")
            None

    member t.GetAssetIdByName(name:string) =
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

    member t.GetChampNameById(id:uint64) =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetChampNameById conn
            |> Db.setParams [
                "id", SqlType.Int64 <| int64 id
            ]
            |> Db.querySingle (fun r -> r.GetString(0))
        with exn ->
            Log.Error(exn, "GetAssetIdByName")
            None

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
                        if lastRoundTimestamp.Add(Battle.RoundDuration) > DateTime.UtcNow then
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
                        if lastRoundTimestamp.Add(Battle.RoundDuration) > DateTime.UtcNow then
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
            | Some status when status = RoundStatus.Processing->
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
                                Monster.TryCreate(rdb.MType, rdb.MSubType).Value, rdb.Stat, uint64 rdb.Xp)
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

    member x.FinalizeRound (bresult:BattleResult) (monsterRevivalTime:uint) (boosts:Map<uint64, RoundBoost list>) =
        try
            use conn = new SqliteConnection(cs)

            let updateRewards (tn:Data.IDbTransaction) (roundId: uint64) (roundRewards: RoundRewardSplit) =
                // insert to round rewards
                tn
                |> Db.newCommandForTransaction SQL.InsertRoundRewards
                |> Db.setParams [ 
                    "roundId", SqlType.Int64 <| int64 roundId
                    "unclaimed", SqlType.Decimal roundRewards.Unclaimed
                    "burn", SqlType.Decimal roundRewards.Burn
                    "dao", SqlType.Decimal roundRewards.DAO
                    "reserve", SqlType.Decimal roundRewards.Reserve
                    "devs", SqlType.Decimal roundRewards.Dev
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
                    "amount", SqlType.Decimal roundRewards.Reserve
                    "key", SqlType.String (DbKeysNum.Reserve.ToString())
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal roundRewards.Dev
                    "key", SqlType.String (DbKeysNum.Dev.ToString())
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal roundRewards.DAO
                    "key", SqlType.String (DbKeysNum.DAO.ToString())
                ]
                |> Db.exec

                tn
                |> Db.newCommandForTransaction SQL.AddToKeyNum
                |> Db.setParams [
                    "amount", SqlType.Decimal roundRewards.Burn
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
                let options = JsonFSharpOptions().ToJsonSerializerOptions()
                let bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(pm, options)
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
                    let options = JsonFSharpOptions().ToJsonSerializerOptions()
                    let bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(pm, options)
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
                        let options = JsonFSharpOptions().ToJsonSerializerOptions()
                        let bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(pm, options)
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
            Db.newCommand SQL.GetChampsLeaderBoard10 conn
            |> Db.query(fun r ->
                {|
                    Name = r.GetString(0)
                    AssetId = r.GetInt64(1)
                    IPFS = r.GetString(2)
                    Xp = r.GetInt64(3)
                |}
            )
            |> Ok
        with exn ->
            Log.Error(exn, "GetChampLeaderboard")
            Error("Unexpected error")

    member _.GetMonsterLeaderboard() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetMonsterLeaderBoard10 conn
            |> Db.query(fun r ->
                {|
                    Name = r.GetString(0)
                    MType = enum<MonsterType> <| r.GetInt32(1)
                    MSubType = enum<MonsterSubType> <| r.GetInt32(2)
                    Xp = r.GetInt64(3)
                |}
            )
            |> Ok
        with exn ->
            Log.Error(exn, "GetMonsterLeaderboard")
            Error("Unexpected error")

    member _.GetTopInGameDonaters() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetTopInGameDonaters conn
            |> Db.query(fun r ->
                {|
                    DiscordId = r.GetInt64(0)
                    Amount = r.GetDecimal(1)
                |}
            )
            |> Ok
        with exn ->
            Log.Error(exn, "GetTopInGameDonaters")
            Error("Unexpected error")

    member _.GetTopDonaters() =
        try
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetTopDonaters conn
            |> Db.query(fun r ->
                {|
                    Wallet = r.GetString(0)
                    Amount = r.GetDecimal(1)
                |}
            )
            |> Ok
        with exn ->
            Log.Error(exn, "GetTopInGameDonaters")
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
            let options = JsonFSharpOptions().ToJsonSerializerOptions()
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.GetMonsterStats conn
            |> Db.setParams [
                "monsterId", SqlType.Int64 monsterId
            ]
            |> Db.querySingle(fun r -> {
                XP = uint64 <| r.GetInt64(0)
                Name = r.GetString(1)
                Description = r.GetString(2)
                Picture = System.Text.Json.JsonSerializer.Deserialize<MonsterImg>(Utils.getBytesData r 3, options)
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
            })
        with exn ->
            Log.Error(exn, $"GetMonsterById: {monsterId}")
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

    member _.SendToWallet(champId:uint64, balance: decimal, battleId:uint64, send:(decimal -> string option)) =
        try 
            use conn = new SqliteConnection(cs)
            Db.batch(fun tn ->
                Db.newCommandForTransaction SQL.WinthdrawFromBalance tn
                |> Db.setParams [
                    "champId", SqlType.Int64 <| int64 champId
                ]
                |> Db.exec
                let r = send balance
                match r with
                | Some tx ->
                    Db.newCommandForTransaction SQL.InsertRewardsPayed tn
                    |> Db.setParams [
                        "champId", SqlType.Int64 <| int64 champId
                        "battleId", SqlType.Int64 <| int64 battleId
                        "tx", SqlType.String <| string tx
                        "rewards", SqlType.Decimal balance
                    ]
                    |> Db.exec

                    Db.newCommandForTransaction SQL.SetChampActionRewardsStatusToSend tn
                    |> Db.setParams [
                        "champId", SqlType.Int64 <| int64 champId
                        "battleId", SqlType.Int64 <| int64 battleId
                        "tx", SqlType.String <| string tx
                        "rewards", SqlType.Decimal balance
                    ]
                    |> Db.exec
                    Some(tx)
                | None ->
                    tn.Rollback()
                    None
            ) conn

        with exn ->
            Log.Error(exn, "SendToWallet")
            None

    member _.SendToSpecialWallet(wt:WalletType, battleId:uint64, send:(decimal -> string option)) =
        try 
            let numKey =
                match wt with
                | WalletType.DAO -> DbKeysNum.DAO
                | WalletType.Dev -> DbKeysNum.Dev
                | WalletType.Reserve -> DbKeysNum.Reserve
                | WalletType.Burn -> DbKeysNum.Burn
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
                    | Some tx ->
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

    member _.CreateGenRequest(discordId: uint64, mtype:MonsterType, msubtype:MonsterSubType) =
        try
            use conn = new SqliteConnection(cs)
            Db.batch (fun tn ->
                let userId = 
                    tn
                    |> Db.newCommandForTransaction SQL.GetUserIdByDiscordId
                    |> Db.setParams [ 
                        "discordId", SqlType.Int64 <| int64 discordId
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v)
                match userId with
                | Some uid ->
                    let balance =
                        tn
                        |> Db.newCommandForTransaction SQL.GetUserBalance
                        |> Db.setParams [
                            "userId", SqlType.Int64 uid 
                        ]
                        |> Db.querySingle (fun r -> r.GetDecimal(0))
                    let darkCoinPrice = getKeyNum conn DbKeysNum.DarkCoinPrice
                    match balance, darkCoinPrice with
                    | Some b, Some dcPrice ->
                        let subs = Math.Round(Shop.GenMonsterPrice / dcPrice, 6)
                        if subs > b then Error($"Your balance is {b} while {subs} is required")
                        else
                            let newBalance = Math.Round(b - subs, 6)
                            tn
                            |> Db.newCommandForTransaction SQL.UpdateUserBalance
                            |> Db.setParams [
                                "balance", SqlType.Decimal newBalance
                                "userId", SqlType.Int64 uid
                            ]
                            |> Db.exec

                            tn
                            |> Db.newCommandForTransaction SQL.AddToKeyNum
                            |> Db.setParams [
                                "amount", SqlType.Decimal subs
                                "key", SqlType.String (DbKeysNum.Locked.ToString())
                            ]
                            |> Db.exec

                            let prompt = Prompt.createMonsterNameDesc mtype msubtype
                            let payload = GenPayload.TextReqCreated prompt
                            let options = JsonFSharpOptions().ToJsonSerializerOptions()
                            let json = JsonSerializer.Serialize(payload, options)

                            tn
                            |> Db.newCommandForTransaction SQL.InitGenRequest
                            |> Db.setParams [
                                "userId", SqlType.Int64 uid
                                "status", SqlType.Int <| int payload.Status
                                "payload", SqlType.String json
                                "cost", SqlType.Decimal subs
                                "type", SqlType.Int <| int mtype
                                "subtype", SqlType.Int <| int msubtype
                            ]
                            |> Db.exec
                            Ok("Request received. Please wait while it processing. It may take a while")
                    | None, Some _ -> Error("Can't fetch balance. Try again later")
                    | Some _, None -> Error("Can't fetch price. Try again later")
                    | _, _ -> Error("Can't fetch balance and price")         
                | None ->
                    Error("User not found")) conn
        with exn ->
            Log.Error(exn, $"CreateGenRequest {discordId}: {mtype}, {msubtype}")
            Error("Unexpected error")

    member _.GetAllUnfinishedGenRequests() =
        try
            let options = JsonFSharpOptions().ToJsonSerializerOptions()
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.SelectUnfinishedRequests conn
            |> Db.query(fun r -> {|
                    ID = r.GetInt64(0)
                    UserId = r.GetInt64(1)
                    Status = r.GetInt32(2) |> enum<GenStatus>
                    Payload = JsonSerializer.Deserialize<GenPayload>(r.GetString(3), options)
                    Cost = r.GetDecimal(4)
                    MType = r.GetInt32(5) |> enum<MonsterType>
                    MSubType = r.GetInt32(6) |> enum<MonsterSubType>
                |}
            )
        with exn ->
            Log.Error(exn, "GetAllUnfinishedGenRequests")
            []

    member _.UpdateGenRequest(rId:int64, payload:GenPayload) =
        try
            let options = JsonFSharpOptions().ToJsonSerializerOptions()
            let json = JsonSerializer.Serialize(payload, options)
            let status = payload.Status
            let isFinished = payload.IsFinished
            use conn = new SqliteConnection(cs)
            Db.newCommand SQL.UpdateGenRequest conn
            |> Db.setParams [
                "status", SqlType.Int <| int status
                "payload", SqlType.String json
                "isFinished", SqlType.Boolean isFinished
                "id", SqlType.Int64 rId
            ]
            |> Db.exec
            // ToDo: return coins to a user in case of final failure
            true
        with exn ->
            Log.Error(exn, $"UpdateGenRequest: {rId}")
            false

    member _.CreateCustomMonster(monster:MonsterRecord, mi:MonsterImg, reqId:int64, uid:int64, cost:decimal) =
        try
            use conn = new SqliteConnection(cs)
            Db.batch (fun tn ->
                let options = JsonFSharpOptions().ToJsonSerializerOptions()
                let bytes = JsonSerializer.SerializeToUtf8Bytes(mi, options)

                let isMonsterNameExists =
                    Db.newCommandForTransaction SQL.IsMonsterNameExists tn
                    |> Db.setParams [
                        "name", SqlType.String monster.Name
                    ]
                    |> Db.scalar (fun v -> tryUnbox<int64> v |> Option.map(fun v -> v > 0) |> Option.defaultValue false)

                let name' = 
                    if isMonsterNameExists then
                        let count = 
                            Db.newCommandForTransaction SQL.CountMonsterByTypes tn
                            |> Db.setParams [
                                "type", SqlType.Int <| int monster.Monster.MType
                                "subtype", SqlType.Int <| int monster.Monster.MSubType
                            ]
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
                    ]
                    |> Db.exec
                    
                    // - remove coins from locked
                    tn
                    |> Db.newCommandForTransaction SQL.AddToKeyNum
                    |> Db.setParams [
                        "amount", SqlType.Decimal -cost
                        "key", SqlType.String (DbKeysNum.Locked.ToString())
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
                    let json = JsonSerializer.Serialize(payload, options)
                    let status = payload.Status
                    Db.newCommandForTransaction SQL.UpdateGenRequest tn
                    |> Db.setParams [
                        "status", SqlType.Int <| int status
                        "payload", SqlType.String json
                        "isFinished", SqlType.Boolean true
                        "id", SqlType.Int64 reqId
                    ]
                    |> Db.exec
                    true
                | None ->
                    tn.Rollback()
                    false
            ) conn
        with exn ->
            Log.Error(exn, $"CreateNewMonster: {monster}")
            false

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