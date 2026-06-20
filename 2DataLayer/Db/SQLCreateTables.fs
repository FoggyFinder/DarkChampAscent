namespace Db.Sqlite

[<RequireQualifiedAccess>]
module internal SQLTables =

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

        CREATE TABLE IF NOT EXISTS NFTMonster (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            AssetId INTEGER NOT NULL UNIQUE,
            ExternalLink TEXT
        );

        CREATE TABLE IF NOT EXISTS NFTMonsterCreationRequest (
            ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            AssetId INTEGER NOT NULL UNIQUE,
            UserId INTEGER NOT NULL,
            Name TEXT NOT NULL UNIQUE,
            Description TEXT NOT NULL UNIQUE,
            Picture BLOB NOT NULL,
            ExternalLink TEXT,
            Timestamp DATETIME NOT NULL,
            IsFinished BOOL NOT NULL,
            FOREIGN KEY (UserId)
               REFERENCES User (ID)
        );

        CREATE TABLE IF NOT EXISTS UserMonster (
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