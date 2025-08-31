open GameLogic.Champs
open GameLogic.Shop
open GameLogic.Battle
open GameLogic.Monsters

Shop.RenamePrice |> printfn "renamePrice = %A"
Shop.getPrice ShopItem.ElixirOfAccuracy |> printfn "getPrice = %A"


let Window = 90M
let RoundsInBattle = 48M

let BattleReward v = v / Window
let RoundReward v = v / RoundsInBattle
open System
let total = 100000M
//[1..360] |> List.fold (fun state i ->
//    let battleRewards = Math.Round(BattleReward state, 6)
//    let roundRewards = Math.Round(RoundReward battleRewards, 6)
//    printfn $"{i} | {state} | {battleRewards} | {roundRewards}"
//    state - battleRewards
//) total |> ignore

let createAction (id:uint64) (move:Move) : RoundAction =
    {
        Move = move
        Timestamp = DateTime.Now.Subtract(TimeSpan.FromDays(1)).AddMinutes(float <| Random.Shared.NextInt64(1L, 10000L))
        ChampId = id
        ChampName = ""
        Stat = {
            Health = Random.Shared.NextInt64(50L, 100L)
            Magic = Random.Shared.NextInt64(50L, 100L)
            Accuracy = Random.Shared.NextInt64(3L, 10L)
            Luck = Random.Shared.NextInt64(3L, 10L)
            Attack = Random.Shared.NextInt64(5L, 10L)
            MagicAttack = Random.Shared.NextInt64(5L, 10L)
            Defense = Random.Shared.NextInt64(4L, 8L)
            MagicDefense = Random.Shared.NextInt64(4L, 8L)
        }
        ChampLvl = 0UL
    }

let actions = [
    createAction 1UL Move.Attack
    createAction 2UL Move.Attack
    createAction 3UL Move.Attack
    createAction 4UL Move.Attack
    createAction 5UL Move.Attack
    createAction 6UL Move.Attack
]

open System.Text
let statShort (stat:Stat) =
    $"[{stat.Health}H|{stat.Magic}M|{stat.Attack}A|{stat.Defense}D|{stat.MagicAttack}MA|{stat.MagicDefense}MD|{stat.Luck}L|{stat.Accuracy}Acc]"
let battle(monster:Monster) (rounds:int) =
    let mStat = Monster.getStats monster
    printfn "Rounds %A" rounds
    Seq.unfold(fun (r, actions:RoundAction list, mstat:Stat) ->
        let hasPlayers = actions |> List.exists(fun a -> a.Stat.IsAlive)
        if hasPlayers && mstat.IsAlive && r < rounds then
            let log = StringBuilder()
 
            let actions', mstat' = Battle.processActions mstat actions
            let actions'', mstat'' =
                actions' |> List.choose id |> List.sortBy(fun (ra,_) -> ra.ChampId) |> List.mapFold(fun (ms:Stat) (ra, pm) ->
                    let nextMove = Move.Attack
                    let source, target, pm' =
                        Battle.attack ms 0UL
                        |> PerformedMove.Attack
                        |> Battle.updateStats ms ra.Stat
                    log.AppendLine($"{ra.ChampId} -> monster = {pm}; monster -> {ra.ChampId} = {pm'}") |> ignore
                    let ra' = {
                        ra with
                            Move = nextMove
                            Stat = target
                    }
                    ra', source
                ) mstat'
            log.AppendLine($"{r} Players left: {actions'.Length}") |> ignore
            Some(log, (r + 1, actions'', mstat''))
        else
            printfn "HasPlayers: %A MonsterAlive? %A %A" hasPlayers mstat.IsAlive (r < rounds)
            None
    ) (0, actions, mStat)
    
let monsters = Monster.DefaultsMonsters |> List.map(fun mr -> mr.Monster)

monsters |> List.iter(fun m ->
    printfn $"{m.MType}, {m.MSubType}"
    battle m (4 * Constants.RoundsInBattle)
    |> Seq.iter(printf "%A")
)