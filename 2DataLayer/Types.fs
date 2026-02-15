module Types

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

type Wallet(wallet:string, isConfirmed:bool, code: string) =
    member _.Wallet = wallet
    member _.IsConfirmed = isConfirmed
    member _.Code = code

type UserAccount(user:Account, wallets:Wallet list, balance: decimal, champs:int, monsters:int) =
    member _.User = user
    member _.Wallets = wallets
    member _.Champs = champs
    member _.Monsters = monsters
    member _.Balance = balance

type ChampShortInfo(cid:uint64, name:string, ipfs:string, xp:uint64) =
    member _.ID = cid
    member _.Name = name
    member _.IPFS = ipfs
    member _.XP = xp

type MonsterShortInfo(mid:uint64, name:string, mtype:MonsterType, msubtype:MonsterSubType, pic:MonsterImg, xp:uint64) =
    member _.ID = mid
    member _.Name = name
    member _.MType = mtype
    member _.MSubType = msubtype
    member _.Pic = pic
    member _.XP = xp

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