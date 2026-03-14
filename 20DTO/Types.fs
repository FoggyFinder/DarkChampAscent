namespace Types

open GameLogic.Champs
open GameLogic.Battle
open GameLogic.Monsters

open System.Globalization
open System
open DarkChampAscent.Account

type CurrentBattleInfo(battleNum:uint64, battleStatus: BattleStatus, monster:MonsterInfo, mId: uint64) =
    member _.BattleNum = battleNum
    member _.BattleStatus = battleStatus
    member _.Monster = monster
    member _.MonsterId = mId
    member x.WithMonsterImg(pic:MonsterImg) =
        CurrentBattleInfo(x.BattleNum, x.BattleStatus, { x.Monster with Picture = pic }, x.MonsterId)

[<RequireQualifiedAccess>]
type PM =
    | Monster of uint64 * PerformedMove * string option
    | Champ of uint64 * string * PerformedMove * DateTime
    member x.RoundId =
        match x with
        | Monster(id, _, _) -> id
        | Champ(id,_,_,_) -> id
    member x.PM =
        match x with
        | Monster(_, pm, _) -> pm
        | Champ(_,_,pm,_) -> pm

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