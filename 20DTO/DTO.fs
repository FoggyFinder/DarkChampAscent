module DTO

open Types
open GameLogic.Shop
open GameLogic.Champs
open GameLogic.Monsters
open GameLogic.Battle
open System

type AccountDTO(account:UserAccount, price:decimal option) =
    member _.Account = account
    member _.Price = price
     
type ShopDTO(items:ShopItem list, price:decimal, userBalance: decimal option) =
    member _.Items = items
    member _.Price = price
    member _.Balance = userBalance

type UserStorageDTO(storage:(ShopItem * int) list, champs:ChampFullInfo list) =
    member _.Storage = storage
    member _.Champs = champs

type ChampDTO(champ:ChampInfo, balance: decimal option, price: decimal) =
    member _.ChampInfo = champ
    member _.Balance = balance
    member _.Price = price

type MonsterDTO(monster:MonsterInfo, id:uint64, isOwned:bool) =
    member _.Monster = monster
    member _.ID = id
    member _.IsOwned = isOwned
    member x.WithMonsterImg(img:MonsterImg) =
        MonsterDTO({ x.Monster with Picture = img }, x.ID, x.IsOwned)

type UserMonstersDTO(monsters:MonsterShortInfo list, price: decimal, userBalance: decimal option) =
    member _.Monsters = monsters
    member _.Price = price
    member _.UserBalance = userBalance

type RewardsPriceDTO(rewards:decimal, price:decimal option) =
    member _.Rewards = rewards
    member _.Price = price

[<Struct>]
type RoundInfoDTO(status:RoundStatus, roundStarted:DateTime option) =
    member _.Status = status
    member _.RoundStarted = roundStarted

[<Struct>]
type RoundParticipantDTO(chId:uint64, name:string, ipfs:string) =
    member _.ID = chId
    member _.Name = name
    member _.IPFS = ipfs

type BattleDTO(cbr:Result<CurrentBattleInfo, string>,
    history: Result<(uint64 * (string * PerformedMove * string) list) list, string>,
    champsRes: Result<(uint64 * string * string) list, string> option) =
    member _.CurrentBattleInfoR = cbr
    member _.History = history
    member _.ChampsRes = champsRes

type NonceDTO(txnB64: string, nonce: string) =
    member _.TxnB64 = txnB64
    member _.Nonce  = nonce

