namespace Db.Sqlite


[<RequireQualifiedAccess>]
module internal SQL =

    let createTablesSQL = """
        CREATE TABLE IF NOT EXISTS CustomUser (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Nickname TEXT NOT NULL UNIQUE,
            Password TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS User (
	        ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            DiscordId INTEGER UNIQUE,
            CustomUserId INTEGER UNIQUE,
            PrimaryWallet TEXT UNIQUE,
            FOREIGN KEY (CustomUserId)
            REFERENCES CustomUser (ID),
            CHECK (CustomUserId IS NOT NULL 
                  OR DiscordId IS NOT NULL
                  OR PrimaryWallet IS NOT NULL)
        );

        CREATE TABLE IF NOT EXISTS Web3Nonces (
            Wallet      TEXT        NOT NULL PRIMARY KEY,
            Nonce       TEXT        NOT NULL,
            ExpiresAt  INTEGER     NOT NULL
        );

        CREATE INDEX IF NOT EXISTS IX_Web3Nonces_ExpiresAt ON Web3Nonces (ExpiresAt);

        CREATE TABLE IF NOT EXISTS Wallet (
	        ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Address TEXT NOT NULL UNIQUE,
            IsConfirmed BOOL NOT NULL,
            ConfirmationCode TEXT NOT NULL,
            IsActive BOOL NOT NULL,
            FOREIGN KEY (UserId)
               REFERENCES User (ID)
        );

        CREATE TABLE IF NOT EXISTS TxHistory (
	        ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            TX TEXT NOT NULL UNIQUE,
            Wallet TEXT NOT NULL,
            Note TEXT,
            Amount NUMERIC NOT NULL,
            Type INT NOT NULL,
            IsValid BOOL NOT NULL,
            IsFinished BOOL NOT NULL,
            Comment TEXT,
            CHECK (Amount >= 0)
        );

        CREATE TABLE IF NOT EXISTS TxRevertHistory (
	        ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            TxId INTEGER NOT NULL UNIQUE,
            OutTx TEXT NOT NULL UNIQUE,
            FOREIGN KEY (TxId)
               REFERENCES TxHistory (ID)
        );
        
        CREATE TABLE IF NOT EXISTS Deposit (
	        ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            WalletId INTEGER NOT NULL,
            TX TEXT NOT NULL UNIQUE,
            Amount INT NOT NULL,
            FOREIGN KEY (WalletId)
               REFERENCES Wallet (ID),
            CHECK (Amount >= 0)
        );

        CREATE TABLE IF NOT EXISTS Champ (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            AssetId INTEGER NOT NULL UNIQUE,
            IPFS TEXT,
            Balance NUMERIC NOT NULL,
            TotalEarned NUMERIC NOT NULL,
            Withdrawn NUMERIC NOT NULL,
            CHECK (Balance >= 0)
        );

        CREATE TABLE IF NOT EXISTS UserChamp (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ChampId INTEGER NOT NULL UNIQUE,
            UserId INTEGER NOT NULL,
            UNIQUE(ChampId, UserId),
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID),
            FOREIGN KEY (UserId)
               REFERENCES User (ID)
        );

        CREATE TABLE IF NOT EXISTS ChampStat (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ChampId INTEGER NOT NULL UNIQUE,
            Xp INTEGER NOT NULL,
            Health INTEGER NOT NULL,
            Magic INTEGER NOT NULL,
            Accuracy INTEGER NOT NULL,
            Luck INTEGER NOT NULL,
            Attack INTEGER NOT NULL,
            MagicAttack INTEGER NOT NULL,
            Defense INTEGER NOT NULL,
            MagicDefense INTEGER NOT NULL,
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID)
        );

        CREATE TABLE IF NOT EXISTS ChampTrait (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ChampId INTEGER NOT NULL UNIQUE,
            Background INTEGER NOT NULL,
            Skin INTEGER NOT NULL,
            Weapon INTEGER NOT NULL,
            Magic INTEGER NOT NULL,
            Head INTEGER NOT NULL,
            Armour INTEGER NOT NULL,
            Extra INTEGER NOT NULL,
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID)
        );

        CREATE TABLE IF NOT EXISTS Monster (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            Description TEXT NOT NULL UNIQUE,
            Picture BLOB NOT NULL,
            Xp INTEGER NOT NULL,
            Health INTEGER NOT NULL,
            Magic INTEGER NOT NULL,
            Accuracy INTEGER NOT NULL,
            Luck INTEGER NOT NULL,
            Attack INTEGER NOT NULL,
            MagicAttack INTEGER NOT NULL,
            Defense INTEGER NOT NULL,
            MagicDefense INTEGER NOT NULL,
            Type INTEGER NOT NULL,
            SubType INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS UserGenMonsterRequest (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Timestamp DATETIME NOT NULL,
            Status INT NOT NULL,
            Payload TEXT,
            Cost NUMERIC NOT NULL,
            IsFinished BOOL NOT NULL,
            Type INTEGER NOT NULL,
            SubType INTEGER NOT NULL,
            FOREIGN KEY (UserId)
               REFERENCES User (ID),
            CHECK (Cost > 0)
        );

        CREATE TABLE IF NOT EXISTS GenRequestTx (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            TxId INTEGER NOT NULL UNIQUE,
            RequestId INTEGER NOT NULL UNIQUE,
            FOREIGN KEY (TxId)
               REFERENCES TxHistory (ID),
            FOREIGN KEY (RequestId)
               REFERENCES UserGenMonsterRequest (ID)
        );

        CREATE TABLE IF NOT EXISTS UserGenRequestRefunds (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            RequestId INTEGER NOT NULL UNIQUE,
            IsFinished BOOL NOT NULL,
            OutTx TEXT UNIQUE,
            FOREIGN KEY (UserId)
               REFERENCES User (ID),
            FOREIGN KEY (RequestId)
               REFERENCES UserGenMonsterRequest (ID)
        );

        CREATE TABLE IF NOT EXISTS UserMonster (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            MonsterId INTEGER NOT NULL UNIQUE,
            UserId INTEGER NOT NULL,
            RequestId INTEGER NOT NULL,
            FOREIGN KEY (MonsterId)
               REFERENCES Monster (ID),
            FOREIGN KEY (UserId)
               REFERENCES User (ID),
            FOREIGN KEY (RequestId)
               REFERENCES UserGenMonsterRequest (ID)
        );

        CREATE TABLE IF NOT EXISTS Battle (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            MonsterId INT NOT NULL,
            Timestamp DATETIME NOT NULL,
            Status INT NOT NULL,
            Rewards NUMERIC NOT NULL,
            FOREIGN KEY (MonsterId)
               REFERENCES Monster (ID),
            CHECK (Rewards > 0)
        );

        CREATE TABLE IF NOT EXISTS Round (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            BattleId INT NOT NULL,
            Timestamp DATETIME NOT NULL,
            Rewards NUMERIC NOT NULL,
            Status INT NOT NULL,
            FOREIGN KEY (BattleId)
               REFERENCES Battle (ID),
            CHECK (Rewards > 0)
        );

        CREATE TABLE IF NOT EXISTS ChampLevel (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ChampId INTEGER NOT NULL,
            Characteristic INTEGER NULL,
            Timestamp DATETIME NOT NULL,
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID)
        );

        CREATE TABLE IF NOT EXISTS Action (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            RoundId INT NOT NULL,
            ChampId INT NOT NULL,
            Timestamp DATETIME NOT NULL,
            Move INT NOT NULL,
            MoveRes BLOB,
            XpEarned INTEGER,
            Rewards NUMERIC,
            RewardsStatus INT NOT NULL,
            UNIQUE(ChampId, RoundId),
            FOREIGN KEY (RoundId)
               REFERENCES Round (ID),
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID),
            CHECK (Rewards >= 0)
        );

        CREATE TABLE IF NOT EXISTS MonsterAction (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            RoundId INT NOT NULL,
            ChampId INT,
            Move INT NOT NULL,
            MoveRes BLOB,
            XpEarned INTEGER,
            UNIQUE(ChampId, RoundId),
            FOREIGN KEY (RoundId)
               REFERENCES Round (ID),
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID)
        );

        CREATE TABLE IF NOT EXISTS MonsterDefeats (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            MonsterId INT NOT NULL,
            RoundId INT NOT NULL,
            ChampId INT NOT NULL,
            RevivalDuration INT NOT NULL,
            UNIQUE(MonsterId, RoundId),
            FOREIGN KEY (MonsterId)
               REFERENCES Monster (ID),
            FOREIGN KEY (RoundId)
               REFERENCES Round (ID),
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID)
        );

        CREATE TABLE IF NOT EXISTS MonsterVictories (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            MonsterId INT NOT NULL,
            RoundId INT NOT NULL,
            ChampId INT NOT NULL,
            UNIQUE(MonsterId, ChampId, RoundId),
            FOREIGN KEY (MonsterId)
               REFERENCES Monster (ID),
            FOREIGN KEY (RoundId)
               REFERENCES Round (ID),
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID)
        );

        CREATE TABLE IF NOT EXISTS RewardsHistory (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            RoundId INT NOT NULL,
            Unclaimed NUMERIC NOT NULL,
            Burn NUMERIC NOT NULL,
            DAO NUMETIC NOT NULL,
            Reserve NUMERIC NOT NULL,
            Devs NUMERIC NOT NULL,
            Champs NUMERIC NOT NULL,
            Staking NUMERIC NOT NULL,
            FOREIGN KEY (RoundId)
               REFERENCES Round (ID),
            CHECK (
                Unclaimed > -0.0001 AND
                Burn >= 0 AND
                DAO >= 0 AND
                Reserve >= 0 AND
                Devs >= 0 AND
                Champs >= 0 AND
                Staking >= 0 
            )
        );

        CREATE TABLE IF NOT EXISTS RewardsPayed (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ChampId INT NOT NULL,
            BattleId INT NOT NULL,
            Tx TEXT NOT NULL,
            Rewards NUMERIC NOT NULL,
            UNIQUE(ChampId, BattleId),
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID),
            FOREIGN KEY (BattleId)
               REFERENCES Battle (ID),
            CHECK (Rewards > 0)
        );

        CREATE TABLE IF NOT EXISTS SpecialWithdrawal (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            BattleId INT NOT NULL,
            WalletType INT NOT NULL,
            Tx TEXT NOT NULL UNIQUE,
            Amount NUMERIC NOT NULL,
            FOREIGN KEY (BattleId)
               REFERENCES Battle (ID),
            CHECK (Amount > 0)
        );

        CREATE TABLE IF NOT EXISTS Shop (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Item INT NOT NULL
        );

        -- Items that aren't activated yet
        CREATE TABLE IF NOT EXISTS Storage (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            UserId INT NOT NULL,
            ItemId INT NOT NULL,
            Amount INT NOT NULL,
            UNIQUE(UserId, ItemId)
            FOREIGN KEY (UserId)
               REFERENCES User (ID),
            FOREIGN KEY (ItemId)
               REFERENCES Shop (ID),
            CHECK (Amount >= 0)
        );

        CREATE TABLE IF NOT EXISTS Boost (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ChampId INT NOT NULL,
            ItemId INT NOT NULL,
            RoundId INT NOT NULL,
            Duration INT NOT NULL,
            UNIQUE(ChampId, ItemId, RoundId),
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID),
            FOREIGN KEY (ItemId)
               REFERENCES Shop (ID),
            FOREIGN KEY (RoundId)
               REFERENCES Round (ID)
        );

        CREATE TABLE IF NOT EXISTS Effect (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Item INT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Impact (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ChampId INT NOT NULL,
            ItemId INT NOT NULL,
            RoundId INT NOT NULL,
            Duration INT NOT NULL,
            Val INTEGER,
            IsActive BOOL NOT NULL,
            UNIQUE(ChampId, ItemId, RoundId),
            FOREIGN KEY (ChampId)
               REFERENCES Champ (ID),
            FOREIGN KEY (RoundId)
               REFERENCES Round (ID),
            FOREIGN KEY (ItemId)
               REFERENCES Effect (ID),
            CHECK (Duration > 0)
        );

        CREATE TABLE IF NOT EXISTS MonsterImpact (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            MonsterId INT NOT NULL,
            ItemId INT NOT NULL,
            RoundId INT NOT NULL,
            Duration INT NOT NULL,
            Val INTEGER,
            IsActive BOOL NOT NULL,
            UNIQUE(MonsterId, ItemId, RoundId),
            FOREIGN KEY (MonsterId)
               REFERENCES Monster (ID),
            FOREIGN KEY (RoundId)
               REFERENCES Round (ID),
            FOREIGN KEY (ItemId)
               REFERENCES Effect (ID),
            CHECK (Duration > 0)
        );

        CREATE TABLE IF NOT EXISTS KeyValueNum (
            Key TEXT NOT NULL PRIMARY KEY,
            Value NUMERIC NOT NULL
        );

        CREATE TABLE IF NOT EXISTS KeyValue (
            Key TEXT NOT NULL PRIMARY KEY,
            Value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS KeyValueBool (
            Key TEXT NOT NULL PRIMARY KEY,
            Value Bool NOT NULL
        );
    """

    let UserExistsByDiscordId = "SELECT EXISTS(SELECT 1 FROM User WHERE DiscordId = @discordId LIMIT 1);"
    let UserExistsByCustomId = "SELECT EXISTS(SELECT 1 FROM User WHERE CustomUserId = @customId LIMIT 1);"
    let UserExistsById = "SELECT EXISTS(SELECT 1 FROM User WHERE ID = @id LIMIT 1);"
        
    let WalletExists = "SELECT EXISTS(SELECT 1 FROM Wallet WHERE Address = @wallet LIMIT 1);"
    let ConfirmedWalletExists = "SELECT EXISTS(SELECT 1 FROM Wallet WHERE Address = @wallet AND IsConfirmed = 1 LIMIT 1);"
    let UnConfirmedWalletExists = "SELECT EXISTS(SELECT 1 FROM Wallet WHERE IsConfirmed = 0 LIMIT 1);"
    let ChampExists = "SELECT EXISTS(SELECT 1 FROM Champ WHERE AssetId = @assetId LIMIT 1);"
    let DepositExists = "SELECT EXISTS(SELECT 1 FROM Deposit WHERE Tx = @tx LIMIT 1);"
    let TxExists = "SELECT EXISTS(SELECT 1 FROM TxHistory WHERE Tx = @tx LIMIT 1);"

    let MonsterExists = "SELECT EXISTS(SELECT 1 FROM Monster WHERE ID = @monsterId LIMIT 1);"
    let MonsterExistsByName = "SELECT EXISTS(SELECT 1 FROM Monster WHERE Name = @name LIMIT 1);"
    let RoundExists = "SELECT EXISTS(SELECT 1 FROM Round LIMIT 1);"
    let MonsterBelongsToAUser = "SELECT EXISTS(SELECT 1 FROM UserMonster WHERE MonsterId = @monsterId AND UserId = @userId LIMIT 1);"
    let ChampBelongsToAUser = "SELECT EXISTS(SELECT 1 FROM UserChamp WHERE ChampId = @champId AND UserId = @userId LIMIT 1);"
    let WalletBelongsToAUser = "SELECT EXISTS(SELECT 1 FROM Wallet WHERE Address = @wallet AND UserId = @userId LIMIT 1);"

    let GetAliveMonsters = """
        SELECT m.ID, um.UserId FROM Monster m
        LEFT JOIN UserMonster um ON um.MonsterId = m.ID 
        WHERE m.ID NOT IN (
	        SELECT MonsterId FROM MonsterDefeats
	        WHERE RoundId <= @roundId AND RoundId + RevivalDuration >= @roundId)      
    """
    
    let GetMonsters = """
        SELECT m.ID, um.UserId FROM Monster m
        LEFT JOIN UserMonster um ON um.MonsterId = m.ID
    """
    let FilterMonsters = """
        SELECT ID, Name FROM Monster
        WHERE Type = @mtype AND SubType = @msubtype
    """


    let UnfinishedBattleExists = "SELECT EXISTS(SELECT 1 FROM Battle WHERE Status != 2);"
    let UnfinishedRoundExists = "SELECT EXISTS(SELECT 1 FROM Round WHERE Status != 2);"

    let GetUserIdByDiscordId = "SELECT ID FROM User WHERE DiscordId = @discordId LIMIT 1"
    let GetUserIdByCustomId = "SELECT ID FROM User WHERE CustomUserId = @customId LIMIT 1"
    let GetUserIdByWallet = "SELECT UserId FROM Wallet WHERE Address = @wallet LIMIT 1"
    let GetDiscordIdByWallet = """
        SELECT DiscordId FROM User
        JOIN Wallet w ON w.UserId = User.ID
        WHERE Address = @wallet
        LIMIT 1
    """

    let GetUserIdByChampId = "SELECT UserId FROM UserChamp WHERE ChampId = @champId LIMIT 1"
    let GetChampIdsForUser = """
        SELECT Champ.ID, AssetId FROM Champ
        JOIN UserChamp uc ON uc.ChampId = Champ.ID
        WHERE UserId = @userId
    """
    let GetChampIdByAssetId = "SELECT ID FROM Champ WHERE AssetId = @assetId LIMIT 1;"
    let GetChampIdByName = "SELECT ID FROM Champ WHERE Name = @name LIMIT 1;"
    let GetAssetIdByName = "SELECT AssetId FROM Champ WHERE Name = @name LIMIT 1;"
    let GetChampNameIPFSById = "SELECT Name, IPFS FROM Champ WHERE ID = @id LIMIT 1;"
    let GetShopItemIdByItem = "SELECT Id FROM Shop WHERE Item = @item LIMIT 1"
    let GetEffectItemIdByItem = "SELECT Id FROM Effect WHERE Item = @item LIMIT 1"
    
    let GetChampsCount = "SELECT Count(*) FROM Champ"
    let GetUserChampsCount = "SELECT Count(*) FROM UserChamp WHERE UserId = @userId"
    let GetUserMonstersCount = "SELECT Count(*) FROM UserMonster WHERE UserId = @userId"
    let GetUserRequestsCount = "SELECT Count(*) FROM UserGenMonsterRequest WHERE UserId = @userId AND IsFinished = 0"

    let GetLastActiveRound = "SELECT Max(ID) FROM Round WHERE Status = 0"
    let GetLastRound = "SELECT Max(ID) FROM Round";
    let GetRoundsCount = "SELECT Count(*) FROM Round"
    let GetRoundStatus = "SELECT Status FROM Round WHERE ID = @roundId"
    let GetRoundTimestamp = "SELECT Timestamp FROM Round WHERE ID = @roundId"
    let GetRoundInfo = "SELECT BattleId, Timestamp, Status FROM Round WHERE ID = @roundId"

    let GetLastActiveBattle = "SELECT Max(ID) FROM Battle WHERE Status = 0"
    let GetLastBattle = "SELECT Max(ID) FROM Battle";
    let GetBattleRewards = "SELECT Rewards FROM Battle Where ID = @battleId"
    let GetBattleStatus = "SELECT Status FROM Battle Where ID = @battleId"

    let GetActiveConfirmedUserWallets = """
        SELECT Address FROM Wallet
        WHERE UserId = @userId AND IsConfirmed = 1 AND IsActive = 1
    """

    let ConfirmedActiveWalletExistsByDiscordId = """
        SELECT EXISTS(
            SELECT 1 FROM Wallet
            WHERE
                IsConfirmed = 1 AND IsActive = 1 AND
                UserId = (SELECT ID FROM User WHERE DiscordId = @discordId)
            LIMIT 1);
    """

    let GetUserWallets = """
        SELECT Address, IsConfirmed, IsActive, ConfirmationCode FROM Wallet
        WHERE UserId = @userId
    """

    let GetConfirmedWallets = """
        SELECT UserId, Address FROM Wallet
        WHERE IsConfirmed = 1
    """

    let GetKey = "SELECT Value FROM KeyValue WHERE Key = @key"
    let SetKey = "
        INSERT INTO KeyValue(Key, Value) VALUES(@key, @value)
        ON CONFLICT(Key) DO UPDATE SET Value = @value;"
   
 
    let GetKeyNum = "SELECT Value FROM KeyValueNum WHERE Key = @key"
    let SetKeyNum = "
        INSERT INTO KeyValueNum(Key, Value) VALUES(@key, @value)
        ON CONFLICT(Key) DO UPDATE SET Value = @value;"
    let AddToKeyNum = "UPDATE KeyValueNum SET Value = Value + @amount WHERE Key = @key"

    let GetKeyBool = "SELECT Value FROM KeyValueBool WHERE Key = @key"
    let SetKeyBool = "
        INSERT INTO KeyValueBool(Key, Value) VALUES(@key, @value)
        ON CONFLICT(Key) DO UPDATE SET Value = @value;"
        
    let AddNewDiscordUser = "INSERT INTO User(DiscordId) VALUES(@discordId);"
    let UserNameAlreadyExists = "SELECT EXISTS(SELECT 1 FROM CustomUser WHERE Nickname = @name LIMIT 1);"
    let GetCustomUserInfoByNickname = "SELECT ID, Password FROM CustomUser WHERE Nickname = @name;"
    let AddNewCustomUser = """
        INSERT INTO CustomUser(Nickname, Password) VALUES(@nickname, @password);
        SELECT last_insert_rowid();
        """
    let UpdatePassword = "UPDATE CustomUser SET Password = @password WHERE ID = @cId;"
    let AddCustomUser = "INSERT INTO User(CustomUserId) VALUES(@customId, 0);"

    let PrimaryWalletAlreadyExists = "SELECT EXISTS(SELECT 1 FROM User WHERE PrimaryWallet = @wallet LIMIT 1);"
    let AddNewWeb3User = """
        INSERT INTO User(PrimaryWallet) VALUES(@wallet);
        SELECT last_insert_rowid();
        """
    let RegisterNewWallet = "INSERT INTO Wallet(UserId, Address, IsConfirmed, ConfirmationCode, IsActive) VALUES(@userId, @wallet, 0, @code, 1);"
    let RegisterNewWeb3Wallet = "INSERT INTO Wallet(UserId, Address, IsConfirmed, ConfirmationCode, IsActive) VALUES(@userId, @wallet, 1, '', 1);"
        
    let CodeIsMatchedForUnconfirmedWallet = "SELECT EXISTS(SELECT 1 FROM Wallet WHERE NOT IsConfirmed AND Address = @wallet AND ConfirmationCode = @code LIMIT 1);"
    let ConfirmWallet = "UPDATE Wallet SET IsConfirmed = 1 WHERE Address = @wallet;"
    let DeactivateWallet = "UPDATE Wallet SET IsActive = 0 WHERE Address = @wallet;"

    let CreateChamp = """
        INSERT INTO Champ(Name, AssetId, IPFS, Balance, TotalEarned, Withdrawn) VALUES(@name, @assetId, @ipfs, 0, 0, 0);
        SELECT last_insert_rowid();
    """

    let CreateChampStat = """
        INSERT INTO ChampStat(ChampId, Xp, Health, Magic, Accuracy, Luck, Attack, MagicAttack, Defense, MagicDefense)
        VALUES(@champId, 0, @health, @magic, @accuracy, @luck, @attack, @mattack, @defense, @mdefense);
    """

    let UpsertChampAndUser = "
        INSERT INTO UserChamp(ChampId, UserId) VALUES((SELECT ID FROM Champ WHERE AssetId = @assetId LIMIT 1), @userId)
        ON CONFLICT (ChampId) DO UPDATE SET UserId = @userId;"
    
    let ConnectChampToUser = "INSERT INTO UserChamp(ChampId, UserId) VALUES(@champId, @userId)"
    

    let GetUserChamps = """
        SELECT
            c.ID, Name, IPFS,
            Health, Magic, Accuracy, Luck, Attack, MagicAttack, Defense, MagicDefense,
            Xp
        FROM Champ c
        JOIN UserChamp uc ON uc.ChampId = c.ID
        JOIN ChampStat cs ON cs.ChampId = c.ID
        WHERE UserId = @userId
    """

    let GetUserChampsInfo = """
        SELECT uc.ChampId, Name, IPFS, Xp, Balance FROM Champ
        JOIN UserChamp uc ON uc.ChampId = Champ.ID
        JOIN ChampStat cs ON cs.ChampId = Champ.ID
        WHERE UserId = @userId
    """

    let GetPendingRewards = """
        SELECT Sum(Balance) FROM Champ
        JOIN UserChamp uc ON uc.ChampId = Champ.ID
        WHERE UserId = @userId
    """

    let GetActiveUserChamps = """
        SELECT
            c.ID, Name, IPFS,
            Health, Magic, Accuracy, Luck, Attack, MagicAttack, Defense, MagicDefense,
            Xp
        FROM Champ c
        JOIN UserChamp uc ON uc.ChampId = c.ID
        JOIN ChampStat cs ON cs.ChampId = c.ID
        WHERE UserId = @userId
            AND c.ID NOT IN (SELECT ChampId FROM Action WHERE RoundId = @roundId)
            AND c.ID NOT IN  (
	            SELECT ChampId FROM Impact
	            WHERE 
                    (RoundId <= @roundId AND 
                    RoundId + Duration >= @roundId) AND
                    ItemId = 1 AND
                    IsActive = 1
            )
    """

    let GetChampsFromRound = """
        SELECT ChampId, Name, IPFS FROM Champ
        JOIN Action a ON a.ChampId = Champ.ID
        WHERE RoundId = @roundId
    """

    let GetUserChampsUnderEffect = """
        SELECT Champ.ID, Name, RoundId, Duration, Item, IPFS FROM Champ
        JOIN UserChamp uc ON uc.ChampId = Champ.ID
		JOIN Impact i ON i.ChampId = Champ.ID
        JOIN Effect e ON e.ID = i.ItemId
        WHERE UserId = @userId AND
			(RoundId <= @roundId AND 
				RoundId + Duration >= @roundId) AND
			IsActive = 1
    """

    let GetDefeatedChamps = """
        SELECT Champ.ID, Name, RoundId, Duration, IPFS FROM Champ
		JOIN Impact i ON i.ChampId = Champ.ID
        WHERE (RoundId <= @roundId AND 
				RoundId + Duration >= @roundId) AND
			IsActive = 1 AND ItemId = 1
    """

    let GetMonstersUnderEffect = """
        SELECT Monster.ID, Name, Type, SubType, RoundId, Duration, Item, Picture FROM Monster
        JOIN MonsterImpact mi ON mi.MonsterId = Monster.ID
        JOIN Effect e ON e.ID = mi.ItemId
        WHERE (RoundId <= @roundId AND 
	        RoundId + Duration >= @roundId) AND
	        IsActive = 1
    """

    let GetDefeatedMonsters = """
        SELECT Monster.ID, Name, Type, SubType, RoundId, Duration, Picture FROM Monster
        JOIN MonsterImpact mi ON mi.MonsterId = Monster.ID
        WHERE (RoundId <= @roundId AND 
	        RoundId + Duration >= @roundId) AND
	        IsActive = 1
    """

    let UpdateIpfsByChampId = "UPDATE Champ SET IPFS = @ipfs WHERE ID = @champId;"
    let RenameChamp = "UPDATE Champ SET Name = @newName WHERE ID = @champId"
    
    let GetUserChampsWithStats = """
        SELECT
            Name, AssetId, IPFS, Balance,
            Xp, Health, Magic, Accuracy, Luck, Attack, MagicAttack, Defense, MagicDefense
        FROM Champ
        JOIN UserChamp uc ON uc.ChampId = Champ.ID
        JOIN ChampStat cs ON cs.ChampId = Champ.ID
        WHERE UserId = @userId
    """

    let GetChampInfoByID = """
        SELECT
            Champ.ID, Name, IPFS, Balance,
            Xp, Health, cs.Magic, Accuracy, Luck, Attack, MagicAttack, Defense, MagicDefense,
            Background, Skin, Weapon, ct.Magic, Head, Armour, Extra,
			UserId
        FROM Champ
        JOIN ChampStat cs ON cs.ChampId = Champ.ID
        JOIN ChampTrait ct ON ct.ChampId = Champ.ID
        JOIN UserChamp uc ON uc.ChampId = Champ.ID
        WHERE Champ.ID = @champId
        LIMIT 1
    """

    let UpsertChampTrait = """
        INSERT INTO ChampTrait(ChampId, Background, Skin, Weapon, Magic, Head, Armour, Extra)
        VALUES(@champId, @background, @skin, @weapon, @magic, @head, @armour, @extra)
        ON CONFLICT(ChampId)
        DO UPDATE SET
            Background = @background,
            Skin = @skin,
            Weapon = @weapon,
            Magic = @magic,
            Head = @head,
            Armour = @armour,
            Extra = @extra"""
    
    let GetChampTrait = """
        SELECT Background, Skin, Weapon, Magic, Head, Armour, Extra
        FROM ChampTrait
        WHERE ChampId = @champId
    """

    let GetChampWithBalances = """
        SELECT ID, AssetId, Balance, Name FROM Champ
        WHERE Balance > 0
    """

    let InsertDeposit = """
        INSERT INTO Deposit(WalletId, TX, Amount) VALUES((SELECT ID FROM Wallet WHERE Address = @wallet LIMIT 1), @tx, @amount)
    """
  
    let InsertTx = """
        INSERT INTO TxHistory(TX, Wallet, Note, Amount, Type, IsValid, IsFinished, Comment)
        VALUES(@tx, @wallet, @note, @amount, @type, @isValid, @isFinished, @comment);

        SELECT last_insert_rowid();
    """ 

    let GetShopItems = "SELECT Item FROM Shop"
    let GetEffects = "SELECT Item FROM Effect"

    /// Adds amount to current value if record already exists
    let AddToStorage = """
        INSERT INTO Storage(UserId, ItemId, Amount) VALUES(@userId, @itemId, @amount)
        ON CONFLICT(UserId, ItemId) DO UPDATE SET Amount = Amount + @amount      
    """

    let AddToExistedStorage = """
        UPDATE Storage SET Amount = Amount + @amount
        WHERE UserId = @userId AND ItemId = @itemId
    """
    
    let GetUserStorage = """
       SELECT Item, Amount FROM Storage
       JOIN Shop shop ON shop.ID = Storage.ItemId
       WHERE UserId = @userId AND Amount > 0
    """

    let UserHasItemInStorage = """
       SELECT Item, Amount FROM Storage
       JOIN Shop shop ON shop.ID = Storage.ItemId
       WHERE UserId = @userId AND Amount > 0
    """

    let GetAmountFromStorage = """
        SELECT Amount FROM Storage WHERE UserId = @userId AND ItemId = @itemId LIMIT 1;"        
    """

    let GetChampsBalance = """
        SELECT Sum(Balance) FROM Champ
    """

    let CreateMonster = """
        INSERT INTO Monster(Name, Description, Picture, Xp, Health, Magic, Accuracy, Luck, Attack, MagicAttack, Defense, MagicDefense, Type, SubType)
        VALUES(@name, @description, @img, 0, @health, @magic, @accuracy, @luck, @attack, @mattack, @defense, @mdefense, @type, @subtype);
        SELECT last_insert_rowid();
    """

    let MonsterDefeat = """
        INSERT INTO MonsterDefeats(MonsterId, RoundId, ChampId, RevivalDuration)
        VALUES(@monsterId, @roundId, @champId, @revivalDuration);
    """

    let MonsterVictory = """
        INSERT INTO MonsterVictories(MonsterId, RoundId, ChampId)
        VALUES(@monsterId, @roundId, @champId);
    """

    let StartBattle = """
        INSERT INTO Battle(MonsterId, Timestamp, Status, Rewards) VALUES(@monsterId, DATETIME('now'), 0, @rewards);
        SELECT last_insert_rowid();
    """
    
    let BattleIsActive = "SELECT EXISTS(SELECT 1 FROM Battle WHERE ID = @battleId AND Status = 0)"
    let RoundsInBattle = "SELECT Count(*) FROM Round Where BattleId = @battleId"
    let RoundInfoByBattleId = """SELECT Count(ID), Max(Timestamp), Rewards FROM Round WHERE BattleId = @battleId"""

    let MonsterIsDeadAtRound = """
        SELECT EXISTS (
            SELECT 1 FROM MonsterDefeats
            WHERE MonsterId = @monsterId AND (RoundId < @roundId AND RoundId + RevivalDuration >= @roundId)
        );
    """

    let StartRound = """
        INSERT INTO Round(BattleId, Timestamp, Rewards, Status) VALUES(@battleId, DATETIME('now'), @rewards, 0);
        SELECT last_insert_rowid();
    """

    let AddAction = """
        INSERT INTO Action(RoundId, ChampId, Timestamp, Move, RewardsStatus)
        VALUES(@roundId, @champId, DATETIME('now'), @move, 0)
    """

    let AddMonsterAction = """
       INSERT INTO MonsterAction(RoundId, ChampId, Move, MoveRes, XpEarned)
       VALUES(@roundId, @champId, @move, @moveRes, @xp)
    """

    let GetTimestampForRound = """
        SELECT Timestamp From Round WHERE ID = @roundId LIMIT 1;
    """

    let GetBattleIdForRound = """
        SELECT BattleId From Round WHERE ID = @roundId LIMIT 1;
    """
    
    let FinishRound = """
        UPDATE Round SET Status = 1 WHERE ID = @roundId AND Status = 0;
    """

    let FinalizeRound = """
        UPDATE Round SET Status = 2 WHERE ID = @roundId AND Status = 1;
    """

    let FinishBattle = """
        UPDATE Battle SET Status = 1 WHERE ID = @battleId AND Status = 0;
    """

    let FinalizeBattle = """
        UPDATE Battle SET Status = 2 WHERE ID = @battleId AND Status = 1;
    """

    let ChampsPlayedInRound = """
        SELECT EXISTS (
            SELECT 1 FROM Action
            WHERE RoundId = @roundId
            LIMIT 1
        )
    """

    let ChampsIsUnderEffectInRound = """
        SELECT EXISTS (
	        SELECT 1 FROM Impact
	        WHERE 
                ChampId = @champId AND
                (RoundId < @roundId AND 
                RoundId + Duration >= @roundId) AND
                ItemId = @itemId AND
                IsActive = 1
        )
    """

    let GetActionsForRound = """
        SELECT ChampId, Timestamp, Move FROM Action
        WHERE RoundId = @roundId
    """

    let GetEffectsForRound = """
        SELECT ChampId, RoundId, Item, Duration, Val FROM Impact
        JOIN Effect e ON e.ID = Impact.ItemId
        WHERE RoundId >= @roundId AND RoundId + Duration <= @roundId AND IsActive = 1
    """

    let InsertToBoosts = """
        INSERT INTO Boost(ChampId, ItemId, RoundId, Duration)
        VALUES(@champId, @itemId, @roundId, @duration);
    """

    let IsBoostAlreadyUsedAtRound = """
        SELECT EXISTS(
            SELECT 1 FROM Boost
            WHERE ChampId = @champId AND ItemId = @itemId AND RoundId = @roundId
            LIMIT 1
        )

    """

    let UpdateBoostDuration = """
        UPDATE Boost
            SET Duration = @duration 
        WHERE ChampId = @champId AND ItemId = @itemId AND RoundId = roundId 
    """

    let GetBoostsForRound = """
        SELECT ChampId, RoundId, Item, Duration FROM Boost
        JOIN Shop s ON s.ID = Boost.ItemId
        WHERE RoundId <= @roundId AND Duration != 0 AND RoundId + Duration >= @roundId
    """

    let GetBoostsForChampAtRound = """
        SELECT RoundId, Item, Duration FROM Boost
        JOIN Shop s ON s.ID = Boost.ItemId
        WHERE ChampId = @champId AND RoundId <= @roundId AND Duration != 0 AND RoundId + Duration >= @roundId
    """

    let InsertChampLvl = """
        INSERT INTO ChampLevel(ChampId, Characteristic, Timestamp)
        VALUES(@champId, @characteristic, DATETIME('now'));
    """

    // ToDo: use Timestamp
    let GetLvlsForRound = """
        SELECT ChampId, Characteristic FROM ChampLevel
        WHERE ChampId IN (
            SELECT ChampId FROM Action
            WHERE RoundId = @roundId
        )
    """

    let GetLvlsForUserChamps = """
        SELECT cl.ChampId, Characteristic FROM ChampLevel cl
        JOIN UserChamp uc ON uc.ChampId = cl.ChampId
        WHERE UserId = @userId
    """

    let GetBoostsForUserChampsAtRound = """
        SELECT b.ChampId, RoundId, Item, Duration FROM Boost b
        JOIN Shop s ON s.ID = b.ItemId
        JOIN UserChamp uc ON uc.ChampId = b.ChampId
        WHERE UserId = @userId AND RoundId <= @roundId AND Duration != 0 AND RoundId + Duration >= @roundId
    """
            
    let GetLvlsForChamp = """
        SELECT Characteristic FROM ChampLevel
        WHERE ChampId = @champId
    """

    let GetLvledCharsForChamp = """
        SELECT Count(*) FROM ChampLevel
        WHERE ChampId = @champId
    """

    let GetChampStats = """
        SELECT Health, Magic, Accuracy, Luck, Attack, MagicAttack, Defense, MagicDefense
        FROM ChampStat
        WHERE ChampId = @champId
    """

    let GetChampStatsForRound = """
        SELECT
            ChampId, Xp, Health, Magic, Accuracy, Luck, Attack, MagicAttack, Defense, MagicDefense, Name
        FROM ChampStat
        JOIN Champ c ON c.ID = ChampStat.ChampId
        WHERE ChampId IN (
            SELECT ChampId FROM Action
            WHERE RoundId = @roundId
        )
    """

    let GetMonsterIdForRound = """
        SELECT MonsterId FROM Battle
        WHERE ID = (
            SELECT BattleId FROM Round
            WHERE ID = @roundId
            LIMIT 1
        )
       LIMIT 1
    """

    let GetMonsterStats = """
        SELECT
	        Xp, Name, Description, Picture, Health, Magic, Accuracy, Luck, Attack, MagicAttack, Defense, MagicDefense, Type, SubType,
	        UserId
        FROM Monster
        LEFT JOIN UserMonster um ON um.MonsterId = Monster.ID
        WHERE Monster.ID = @monsterId
        LIMIT 1
    """

    let GetMonsterEffectsForRound = """
        SELECT RoundId, Item, Duration, Val FROM MonsterImpact
        JOIN Effect e ON e.ID = MonsterImpact.ItemId
        WHERE RoundId >= @roundId AND RoundId + Duration <= @roundId AND MonsterId = @monsterId AND IsActive = 1
    """

    let GetChampsLeaderBoard25 = """
        SELECT Name, AssetId, IPFS, Xp, cs.ChampId FROM Champ
        JOIN ChampStat cs ON cs.ChampId = Champ.ID
        ORDER BY
            Xp DESC
        LIMIT 25
    """

    let GetMonsterLeaderBoard25 = """
        SELECT ID, Name, Type, Subtype, Picture, Xp FROM Monster
        ORDER BY
            Xp DESC
        LIMIT 25
    """

    let GetTopDonaters = """
    SELECT c.DiscordId, c.CustomUserId, c.Nickname, c.PrimaryWallet, SUM(Amount) as Total
    FROM (
         SELECT u.ID, DiscordId, CustomUserId, PrimaryWallet, cu.Nickname, PrimaryWallet, Amount FROM 
			InGameDonation igd
         JOIN User u ON u.ID = igd.UserId
         LEFT JOIN CustomUser cu ON cu.ID = u.CustomUserId
        
         UNION ALL

         SELECT u.ID, u.DiscordId, u.CustomUserId, u.PrimaryWallet, cu.Nickname, th.Wallet, Amount FROM TxHistory th
         JOIN Wallet w ON w.Address = th.Wallet
         JOIN User u on u.ID = w.UserId
         LEFT JOIN CustomUser cu ON cu.ID = u.CustomUserId
         WHERE th.IsValid = 1 AND th.IsFinished = 1 AND th.Type = 1
    ) AS c
	
    GROUP BY c.ID
    ORDER BY Total DESC
    LIMIT 25
    """

    let Get5LatestDonations = """
         SELECT u.DiscordId, u.CustomUserId, cu.Nickname, u.PrimaryWallet, th.Amount, th.TX FROM TxHistory th
         JOIN Wallet w ON w.Address = th.Wallet
         JOIN User u on u.ID = w.UserId
         LEFT JOIN CustomUser cu ON cu.ID = u.CustomUserId
         WHERE th.IsValid = 1 AND th.IsFinished = 1 AND th.Type = 1
		 ORDER BY th.ID DESC
		 LIMIT 5
    """

    let InsertRoundRewards = """
        INSERT INTO RewardsHistory(RoundId, Unclaimed, Burn, DAO, Reserve, Devs, Champs, Staking)
        VALUES(@roundId, @unclaimed, @burn, @dao, @reserve, @devs, @champs, 0)
    """

    let InsertRewardsPayed = """
        INSERT INTO RewardsPayed(ChampId, BattleId, Tx, Rewards)
        VALUES(@champId, @battleId, @tx, @rewards)
    """

    let InsertSpecialWithdrawal = """
        INSERT INTO SpecialWithdrawal(WalletType, BattleId, Tx, Amount)
        VALUES(@walletType, @battleId, @tx, @amount)
    """

    let WinthdrawFromBalance = """
        UPDATE Champ SET
            Withdrawn = Withdrawn + Balance,
            Balance = 0
        WHERE ID = @champId
    """

    let UpdateChampAction = """
        UPDATE Action SET 
            MoveRes = @moveRes,
            XpEarned = @xpEarned,
            Rewards = @rewards,
            RewardsStatus = 1
        WHERE RoundId = @roundId AND ChampId = @champId
    """

    let SetChampActionRewardsStatusToSend = """
        UPDATE Action SET RewardsStatus = 2
        WHERE ChampId = @champId
    """

    let UpdateChampStat = """
        UPDATE ChampStat SET 
            Health = @health,
            Magic = @magic,
            Accuracy = @accuracy,
            Luck = @luck,
            Attack = @attack,
            MagicAttack = @mattack,
            Defense = @defense,
            MagicDefense = @mdefense
        WHERE ChampId = @champId;
    """

    let UpdateMonsterStat = """
        UPDATE Monster SET 
            Health = @health,
            Magic = @magic,
            Accuracy = @accuracy,
            Luck = @luck,
            Attack = @attack,
            MagicAttack = @mattack,
            Defense = @defense,
            MagicDefense = @mdefense
        WHERE ID = @monsterId;
    """

    let UpdateChampEarnedRewards = """
        UPDATE Champ SET
            Balance = Balance + @rewards,
            TotalEarned = TotalEarned + @rewards
        WHERE ID = @champId;
    """

    let AddChampXp = """
        UPDATE ChampStat SET 
            Xp = Xp + @xp
        WHERE ChampId = @champId;
    """

    let GetChampXp = "SELECT Xp FROM ChampStat WHERE ChampId = @champId"

    let AddMonsterXp = """
        UPDATE Monster SET 
            Xp = Xp + @xp
        WHERE ID = @monsterId;
    """

    let MarkEffectsAsPassiveBeforeRound = """
        UPDATE Impact SET 
            IsActive = 0
        WHERE ChampId = @champId AND RoundId < @roundId;
    """

    let ApplyEffect = """
        INSERT INTO Impact(ChampId, ItemId, RoundId, Duration, IsActive)
        VALUES(@champId, @itemId, @roundId, @duration, 1);
        SELECT last_insert_rowid();
    """

    let ApplyEffectWithIsActive = """
        INSERT INTO Impact(ChampId, ItemId, RoundId, Duration, IsActive)
        VALUES(@champId, @itemId, @roundId, @duration, @isActive);
    """

    let ApplyEffectToMonster = """
        INSERT INTO MonsterImpact(MonsterId, ItemId, RoundId, Duration, IsActive)
        VALUES(@monsterId, @itemId, @roundId, @duration, 1);
        SELECT last_insert_rowid();
    """

    let InitGenRequest = """
        INSERT INTO UserGenMonsterRequest(UserId, Timestamp, Status, Payload, Cost, IsFinished, Type, SubType)
        VALUES(@userId, DATETIME('now'), @status, @payload, @cost, 0, @type, @subtype);
        SELECT last_insert_rowid();
    """

    let AddUserGenRequestRefund = """
        INSERT INTO UserGenRequestRefunds(UserId, RequestId, IsFinished)
        VALUES(@userId, @requestId, 0);
    """

    let FinishUserGenRequestRefund = """
        UPDATE UserGenRequestRefunds SET 
            IsFinished = 1
        WHERE ID = @id
    """

    let ReOpenUserGenRequestRefund = """
        UPDATE UserGenRequestRefunds SET 
            IsFinished = 0
        WHERE ID = @id AND OutTx IS NULL
    """

    let SetOutputTxForUserGenRequestRefund = """
        UPDATE UserGenRequestRefunds SET 
            OutTx = @tx
        WHERE ID = @id
    """

    let GetPendingRefunds = """
        SELECT r.ID, t.Amount, t.Wallet FROM UserGenRequestRefunds r
        JOIN GenRequestTx g ON g.RequestId = r.RequestId
        JOIN TxHistory t ON t.ID = g.TxId
        WHERE r.IsFinished = 0 AND r.OutTx IS NULL
    """

    let AddGenRequestTx = """
        INSERT INTO GenRequestTx(RequestId, TxId) VALUES(@requestId, @txId);
    """

    let UpdateGenRequest = """
        UPDATE UserGenMonsterRequest SET 
            Status = @status,
            Payload = @payload,
            IsFinished = @isFinished
        WHERE ID = @id
    """

    let SelectUnfinishedRequests = """
        SELECT ID, UserId, Status, Payload, Cost, Type, SubType
        FROM UserGenMonsterRequest
        WHERE IsFinished = 0
        ORDER BY Timestamp ASC
    """

    let ConnectMonsterToUser = "INSERT INTO UserMonster(MonsterId, UserId, RequestId) VALUES(@monsterId, @userId, @requestId)"
    
    let IsMonsterNameExists = """
        SELECT EXISTS(SELECT 1 FROM Monster WHERE Name = @name);
    """
    
    let IsChampNameExists = """
        SELECT EXISTS(SELECT 1 FROM Champ WHERE Name = @name);
    """

    let IsMonsterDescriptionExists = """
        SELECT EXISTS(SELECT 1 FROM Monster WHERE Description = @description);
    """
    
    let CountUserMonsters = """
        SELECT Count(*) FROM UserMonster
        JOIN Monster m ON m.ID = UserMonster.MonsterId
        WHERE UserId = @userId AND Type = @type AND SubType = @subtype
    """

    let CountUserRequests = """
        SELECT Count(*) FROM UserGenMonsterRequest
        WHERE UserId = @userId AND IsFinished = 0 AND Type = @type AND SubType = @subtype
    """

    let CountUnfinishedUserRequests = """
        SELECT Count(*) FROM UserGenMonsterRequest
        WHERE UserId = @userId AND IsFinished = 0
    """

    let CountMonster = """SELECT Count(*) FROM Monster"""

    let GetUserMonsters = """
        SELECT m.ID, Name, Type, SubType, m.Picture, m.Xp FROM UserMonster
        JOIN Monster m ON m.ID = UserMonster.MonsterId
        WHERE UserId = @userId
    """

    let FilterUserMonsters = """
        SELECT m.ID, Name FROM UserMonster
        JOIN Monster m ON m.ID = UserMonster.MonsterId
        WHERE UserId = @userId AND Type = @type AND SubType = @subtype
    """

    let GetPendingUserRequests = """
        SELECT ID, Timestamp, Status  FROM UserGenMonsterRequest
        WHERE UserId = @userId AND IsFinished = 0
    """

    let UserEarnings = """
        SELECT Sum(Rewards) FROM Action
        WHERE
            ChampId IN (SELECT ChampId FROM UserChamp WHERE UserId = @userId)
            AND RoundId >= @startRound AND RoundId <= @endRound
    """

    let RenameMonster = """
        UPDATE Monster SET Name = @newName WHERE ID = @id
    """
    
    let GetLastBattleInfo = """
        SELECT b.ID as BattleId, Status, m.*, UserId FROM Battle b
        JOIN Monster m ON m.ID = b.MonsterId
        LEFT JOIN UserMonster um ON um.MonsterId = b.MonsterId
        WHERE b.ID = (SELECT Max(ID) FROM Battle)
    """

    let GetBattleChampActions = """
        SELECT a.RoundId, a.Timestamp, a.MoveRes, c.ID, c.Name, c.IPFS, a.XpEarned, a.Rewards FROM Action a
        JOIN Round r ON r.ID = a.RoundId
        JOIN Battle b ON b.ID = r.BattleId
        JOIN Champ c ON c.ID = a.ChampId
        WHERE r.BattleId = @battleId AND r.Status = 2
    """

    let GetBattleMonsterActions = """
        SELECT a.RoundId, a.MoveRes, c.ID, c.Name, c.IPFS, a.XpEarned FROM MonsterAction a
        JOIN Round r ON r.ID = a.RoundId
        JOIN Battle b ON b.ID = r.BattleId
        LEFT JOIN Champ c ON c.ID = a.ChampId
        WHERE r.BattleId = @battleId AND r.Status = 2
    """

    let GetRewardsForBattle = """
        SELECT h.RoundId, h.Burn, h.DAO, h.Reserve, h.Devs, h.Champs FROM RewardsHistory h
        JOIN Round r ON r.ID = h.RoundId
        JOIN Battle b ON b.ID = r.BattleId
        WHERE r.BattleId = @battleId
    """

    let GetListOfDefeatedChamps = """
        SELECT mv.RoundId, mv.ChampId FROM MonsterVictories mv
        JOIN Round r ON r.ID = mv.RoundId
        JOIN Battle b ON b.ID = r.BattleId
        WHERE r.BattleId = @battleId
    """

    let GetMonsterDefeater = """
        SELECT ChampId FROM MonsterDefeats md
        JOIN Round r ON r.ID = md.RoundId
        JOIN Battle b ON b.ID = r.BattleId
        WHERE r.BattleId = @battleId
    """

    let GetTotalUserCount = "SELECT Count(*) FROM User"
    let GetConfirmedPlayersCount = "SELECT Count(*) FROM (SELECT DISTINCT UserId FROM Wallet WHERE IsConfirmed = 1)"
    let GetCustomMonstersCount = "SELECT Count(*) FROM UserMonster"
    let GetBattlesCount = "SELECT Count(*) FROM Battle"
    
    let GetSpecialWithdrawalSum = """
        SELECT WalletType, Sum(Amount) FROM SpecialWithdrawal
        GROUP BY WalletType
    """
    
    let PlayersEarned = "SELECT Sum(Rewards) FROM RewardsPayed"

    let SaveNonce = "INSERT OR REPLACE INTO Web3Nonces (Wallet, Nonce, ExpiresAt) VALUES (@wallet, @nonce, @expiresAt)"
    let GetNonceByWallet = "SELECT Nonce, ExpiresAt FROM Web3Nonces WHERE Wallet = @wallet"
    let DeleteNonce = "DELETE FROM Web3Nonces WHERE Wallet = @wallet"

    let GetPendingTxRefunds = """
        SELECT ID, Wallet, Amount FROM TxHistory
        WHERE IsFinished = 0
    """

    let ClosePendingTxRefund = """
        UPDATE TxHistory SET IsFinished = 1
        WHERE ID = @id AND IsFinished = 0
    """

    let ReopenPendingTxRefund = """
        UPDATE TxHistory SET IsFinished = 0
        WHERE ID = @id
    """

    let AddTxRevertHistory = """
        INSERT INTO TxRevertHistory(TxId, OutTx)
        VALUES(@txId, @outTx)
    """

    let GetUserInfo = """
         SELECT u.DiscordId, cu.Nickname, u.PrimaryWallet FROM User u
         LEFT JOIN CustomUser cu ON cu.ID = u.CustomUserId
         WHERE u.ID = @userId
    """