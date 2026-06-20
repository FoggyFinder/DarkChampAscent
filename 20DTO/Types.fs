namespace Types

open GameLogic.Champs
open GameLogic.Battle
open GameLogic.Monsters

open System
open DarkChampAscent.Account
open GameLogic.Rewards

type UserLink(uId:uint64, nickname:string) =
    member _.UserRawId = uId
    member _.Nickname = nickname

type ChampInfo(id: uint64, name: string, ipfs: string, balance: decimal, xp: uint64, stat: Stat, traits: Traits, boostStat: Stat option, levelsStat: Stat option, leveledChars: uint64, ownerId: uint64) =
    member _.ID = id
    member _.Name = name
    member _.Ipfs = ipfs
    member _.Balance = balance
    member _.XP = xp
    member _.Stat = stat
    member _.Traits = traits
    member _.BoostStat = boostStat
    member _.LevelsStat = levelsStat
    member _.LeveledChars = leveledChars
    member _.OwnerId = ownerId
    member x.WithFullStats(levelsStat, boostedStat) =
        ChampInfo(x.ID, x.Name, x.Ipfs, x.Balance, x.XP, x.Stat, x.Traits, Some boostedStat, Some levelsStat, x.LeveledChars, x.OwnerId)

type ChampInfoWithUserLink(champInfo:ChampInfo, userLink: UserLink) =
    member _.ChampInfo = champInfo
    member _.UserLink = userLink

type MonsterGenType =
    | Generative
    | NFTBased of assetId: uint64 * website: string

type MonsterInfo(id: uint64, xp: uint64, name: string, description: string, picture: MonsterImg, stat: Stat, mType: MonsterType, mSubType: MonsterSubType, ownerId: uint64 option, genType: MonsterGenType) =
    member _.Id = id
    member _.XP = xp
    member _.Name = name
    member _.Description = description
    member _.Picture = picture
    member _.Stat = stat
    member _.MType = mType
    member _.MSubType = mSubType
    member _.OwnerId = ownerId
    member _.GenType = genType
    member x.WithPic(pic:MonsterImg) =
        MonsterInfo(x.Id, x.XP, x.Name, x.Description, pic, x.Stat, x.MType, x.MSubType, x.OwnerId, x.GenType)
    member x.WithStat(stat:Stat) =
        MonsterInfo(x.Id, x.XP, x.Name, x.Description, x.Picture, stat, x.MType, x.MSubType, x.OwnerId, x.GenType)

type MonsterInfoWithUserLink(monsterInfo:MonsterInfo, userLinkO:UserLink option) =
    member _.MonsterInfo = monsterInfo
    member _.UserLink = userLinkO

type CurrentBattleInfo(battleNum:uint64, battleStatus: BattleStatus, monster:MonsterInfo) =
    member _.BattleNum = battleNum
    member _.BattleStatus = battleStatus
    member _.Monster = monster
    member x.WithMonsterImg(pic:MonsterImg) =
        CurrentBattleInfo(x.BattleNum, x.BattleStatus, x.Monster.WithPic pic)
    member x.WithMonsterInfo(mi:MonsterInfo) =
        CurrentBattleInfo(x.BattleNum, x.BattleStatus, mi)

type CurrentRoundInfo(rounds:int, roundStarted:DateTime, rewards:decimal) =
    member _.Rounds = rounds
    member _.RoundStarted = roundStarted
    member _.Rewards = rewards

type CurrentFullBattleInfo(cbi:CurrentBattleInfo, cri:CurrentRoundInfo) =
    member _.CurrentBattleInfo = cbi
    member _.CurrentRoundInfo = cri
    member x.WithMonsterImg(pic:MonsterImg) =
        CurrentFullBattleInfo(x.CurrentBattleInfo.WithMonsterImg pic, x.CurrentRoundInfo)
    member x.WithMonsterInfo(mi:MonsterInfo) =
        CurrentFullBattleInfo(x.CurrentBattleInfo.WithMonsterInfo mi, x.CurrentRoundInfo)

[<Struct>]
type RoundParticipantChamp(chId:uint64, name:string, ipfs:string) =
    member _.ID = chId
    member _.Name = name
    member _.IPFS = ipfs

[<Struct>]
type RoundParticipantMonster(mId:uint64, name:string, pic:MonsterImg) =
    member _.ID = mId
    member _.Name = name
    member _.Img = pic
    member x.WithMonsterImg(pic:MonsterImg) =
        RoundParticipantMonster(x.ID, x.Name, pic)

[<Struct>]
type PMMonster(pm:PerformedMove, target:RoundParticipantChamp option) =
    member _.PM = pm
    member _.Target = target

[<Struct>]
type PMChamp(pm:PerformedMove, champ:RoundParticipantChamp, timestamp:DateTime) =
    member _.PM = pm
    member _.Champ = champ
    member _.Timestamp = timestamp
    
[<RequireQualifiedAccess>]
type PMDetail =
    | Monster of pmm:PMMonster
    | Champ of pmc:PMChamp

[<Struct>]
type PMResult(detail:PMDetail, xp:uint64, rewards:decimal option) =
    member _.Detail = detail
    member _.XP = xp
    member _.Rewards = rewards

[<Struct>]
type RoundReward(sr:SpecialReward, champs:decimal) =
    member _.DAO = sr.DAO
    member _.Reserve = sr.Reserve
    member _.Burn = sr.Burn
    member _.Dev = sr.Dev
    member _.Champs = champs
    member _.Total = sr.Total + champs

[<Struct>]
type RoundInfo(roundId:uint64, details:PMResult list, rewards:RoundReward, 
    defeatedChamps:uint64 list, monsterKiller: uint64 option) =
    member _.RoundId = roundId
    member _.Details = details
    member _.Rewards = rewards
    member _.DefeatedChamps = defeatedChamps
    member _.MonsterKiller = monsterKiller

[<Struct>]
type FullRoundInfo(roundInfo:RoundInfo, monster:RoundParticipantMonster) =
    member _.RoundInfo = roundInfo
    member _.Monster = monster

[<Struct>]
type BattleInfo(battleId:uint64, rounds: RoundInfo list, monster:RoundParticipantMonster) =
    member _.BattleId = battleId
    member _.Rounds = rounds
    member _.Monster = monster

[<Struct>]
type BattleHistory(rounds: RoundInfo list, monster:RoundParticipantMonster) =
    member _.Rounds = rounds
    member _.Monster = monster
    member x.WithMonsterImg(pic:MonsterImg) =
        BattleHistory(x.Rounds, x.Monster.WithMonsterImg pic)

type Wallet(wallet:string, isConfirmed:bool, code: string, isActive:bool) =
    member _.Wallet = wallet
    member _.IsConfirmed = isConfirmed
    member _.Code = code
    member _.IsActive = isActive

type UserAccount(user:Account, wallets:Wallet list, champs:int, monsters:int, requests:int) =
    member _.User = user
    member _.Wallets = wallets
    member _.Champs = champs
    member _.Monsters = monsters
    member _.Requests = requests

type ChampShortInfo(cid:uint64, name:string, ipfs:string, xp:uint64) =
    member _.ID = cid
    member _.Name = name
    member _.IPFS = ipfs
    member _.XP = xp

type ChampInfoWithStat(cId:uint64, name:string, ipfs:string, xp:uint64, stat:Stat) =
    member _.ID = cId
    member _.Name = name
    member _.IPFS = ipfs
    member _.XP = xp
    member _.Stat = stat

type MonsterShortInfo(mid:uint64, name:string, mtype:MonsterType, msubtype:MonsterSubType, pic:MonsterImg, xp:uint64) =
    member _.ID = mid
    member _.Name = name
    member _.MType = mtype
    member _.MSubType = msubtype
    member _.Pic = pic
    member _.XP = xp
    member x.WithMonsterImg(img:MonsterImg) =
        MonsterShortInfo(x.ID, x.Name, x.MType, x.MSubType, img, x.XP)

type GameStats(players:uint64 option, confirmedPlayers:uint64 option,
    champs:uint64 option, customMonsters:uint64 option,
    battles: uint64 option, rounds: uint64 option) =
    member _.Players = players
    member _.ConfirmedPlayers = confirmedPlayers
    member _.Champs = champs
    member _.CustomMonsters = customMonsters
    member _.Battles = battles
    member _.Rounds = rounds

type WalletValue(wallet:string, value:decimal) =
    member _.Wallet = wallet
    member _.Value = value

type TStats(burnt: WalletValue option, dao: WalletValue option, reserve: WalletValue option, devs: WalletValue option) =
    member _.Burnt = burnt
    member _.Dao = dao
    member _.Reserve = reserve
    member _.Devs = devs

type Stats(gameStats:GameStats, tStats:TStats, rewards: decimal option) =
    member _.GameStats = gameStats
    member _.Rewards = rewards
    member _.TStats = tStats

[<RequireQualifiedAccess>]
type Donater =
    | Discord of uint64
    | Custom of uint64 * string
    | Unknown of string
    
type Donation(donater:Donater, amount:decimal) =
    member _.Donater = donater
    member _.Amount = amount

type LatestDonation(donater:Donater, amount:decimal, tx:string) =
    member _.Donater = donater
    member _.Amount = amount
    member _.Tx = tx

type DonationDTO(donater:string, amount:decimal) =
    member _.Donater = donater
    member _.Amount = amount

type LatestDonationDTO(donater:string, amount:decimal, tx:string) =
    member _.Donater = donater
    member _.Amount = amount
    member _.Tx = tx

type TopDonatersDTO(topDonaters:DonationDTO list, latestDonaters:LatestDonationDTO list) =
    member _.Top = topDonaters
    member _.Latest = latestDonaters

type GenStatus =
    | RequstCreated = 0 // coins charged from user's balance and added to locked amount
    | TextRequestReceived = 1 // text request send to API and its id is returned
    | TextPayloadReceived = 2 // name and description was generated
    | ImgRequestReceived = 3 // img request sent to API and its id is returned
    | Failure = 4 // failure
    | Success = 5

type GenRequest(id:int64, timestamp:DateTime, status:GenStatus) =
    member _.ID = id
    member _.Timestamp = timestamp
    member _.Status = status

open GameLogic.Effects

type ChampUnderEffect(id:int64, name:string, endsAt: int64, effect:Effect, roundsLeft:int64, ipfs:string) =
    member _.ID = id
    member _.Name = name
    member _.EndsAt = endsAt
    member _.Effect = effect
    member _.RoundsLeft = roundsLeft
    member _.IPFS = ipfs

type MonsterUnderEffect(id:int64, name:string, mtype:MonsterType, msubtype:MonsterSubType, endsAt: int64, effect:Effect, roundsLeft:int64, mpic:MonsterImg) =
    member _.ID = id
    member _.Name = name
    member _.MType = mtype
    member _.MSubType = msubtype
    member _.EndsAt = endsAt
    member _.Effect = effect
    member _.RoundsLeft = roundsLeft
    member _.Pic = mpic
    member x.WithMonsterImg(img:MonsterImg) =
        MonsterUnderEffect(x.ID, x.Name, x.MType, x.MSubType, x.EndsAt, x.Effect, x.RoundsLeft, img)

type UserInfo(nickname:string, champs:ChampInfoWithStat list, monsters: MonsterInfo list) =
    member _.Nickname = nickname
    member _.Champs = champs
    member _.Monsters = monsters

type AssetInfo(assetId:uint64, name:string, ipfs:string, externalUrl:string) =
    member _.AssetId = assetId
    member _.Name = name
    member _.IPFS = ipfs
    member _.ExternalUrl = externalUrl