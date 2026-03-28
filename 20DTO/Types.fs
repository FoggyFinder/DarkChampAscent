namespace Types

open GameLogic.Champs
open GameLogic.Battle
open GameLogic.Monsters

open System
open DarkChampAscent.Account
open GameLogic.Rewards

type CurrentBattleInfo(battleNum:uint64, battleStatus: BattleStatus, monster:MonsterInfo, mId: uint64) =
    member _.BattleNum = battleNum
    member _.BattleStatus = battleStatus
    member _.Monster = monster
    member _.MonsterId = mId
    member x.WithMonsterImg(pic:MonsterImg) =
        CurrentBattleInfo(x.BattleNum, x.BattleStatus, { x.Monster with Picture = pic }, x.MonsterId)
    member x.WithMonsterInfo(mi:MonsterInfo) =
        CurrentBattleInfo(x.BattleNum, x.BattleStatus, mi, x.MonsterId)

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
    member _.Staking = sr.Staking
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

type UserAccount(user:Account, wallets:Wallet list, balance: decimal, champs:int, monsters:int, requests:int) =
    member _.User = user
    member _.Wallets = wallets
    member _.Champs = champs
    member _.Monsters = monsters
    member _.Balance = balance
    member _.Requests = requests

type ChampShortInfo(cid:uint64, name:string, ipfs:string, xp:uint64) =
    member _.ID = cid
    member _.Name = name
    member _.IPFS = ipfs
    member _.XP = xp

type ChampFullInfo(cid:uint64, assetId:uint64, name:string, ipfs:string, balance:decimal) =
    member _.ID = cid
    member _.Assetid = assetId
    member _.Name = name
    member _.IPFS = ipfs
    member _.Balance = balance

type ChampInfoWithStat(cid:uint64, name:string, ipfs:string, stat:Stat) =
    member _.ID = cid
    member _.Name = name
    member _.IPFS = ipfs
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

type Stats(players:uint64 option, confirmedPlayers:uint64 option,
    champs:uint64 option, customMonsters:uint64 option,
    battles: uint64 option, rounds: uint64 option,
    rewards: decimal option, burnt: decimal option,
    dao: decimal option, reserve: decimal option,
    devs: decimal option, staking: decimal option) =
    member _.Players = players
    member _.ConfirmedPlayers = confirmedPlayers
    member _.Champs = champs
    member _.CustomMonsters = customMonsters
    member _.Battles = battles
    member _.Rounds = rounds
    member _.Rewards = rewards
    member _.Burnt = burnt
    member _.Dao = dao
    member _.Reserve = reserve
    member _.Devs = devs
    member _.Staking = staking

[<RequireQualifiedAccess>]
type Donater =
    | Discord of uint64
    | Custom of uint64 * string
    | Unknown of string
    
type Donation(donater:Donater, amount:decimal) =
    member _.Donater = donater
    member _.Amount = amount

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