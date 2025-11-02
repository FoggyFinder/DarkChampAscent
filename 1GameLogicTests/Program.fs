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
let total = 900000M
//[1..90] |> List.fold (fun state i ->
//    let battleRewards = Math.Round(BattleReward state, 6)
//    let roundRewards = Math.Round(RoundReward battleRewards, 6)
//    printfn $"{i} | {state} | {battleRewards} | {roundRewards}"
//    state - battleRewards
//) total |> ignore
let getRandomStat() =
    {
        Health = Random.Shared.NextInt64(50L, 100L)
        Magic = Random.Shared.NextInt64(50L, 100L)
        Accuracy = Random.Shared.NextInt64(3L, 10L)
        Luck = Random.Shared.NextInt64(3L, 10L)
        Attack = Random.Shared.NextInt64(5L, 10L)
        MagicAttack = Random.Shared.NextInt64(5L, 10L)
        Defense = Random.Shared.NextInt64(4L, 8L)
        MagicDefense = Random.Shared.NextInt64(4L, 8L)
    }

let createAction (id:uint64) (move:Move) : RoundAction =
    {
        Move = move
        Timestamp = DateTime.Now.Subtract(TimeSpan.FromDays(1)).AddMinutes(float <| Random.Shared.NextInt64(1L, 10000L))
        ChampId = id
        ChampName = ""
        Stat = getRandomStat()
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
    $"[{stat.Health}H|{stat.Magic}M|{stat.Attack}A|{stat.Defense}D|{stat.MagicDefense}MD|]"
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
                    let nextMove = Move.MagicAttack
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

//monsters
//|> List.truncate 1
//|> List.iter(fun m ->
//    printfn $"{m.MType}, {m.MSubType}"
//    battle m Constants.RoundsInBattle
//    |> Seq.iter(printf "%A")
//)
monsters
|> List.iter(fun m ->
    let lvl = Random.Shared.NextInt64(0, 10) |> uint64
    let stat = Monster.getMonsterStatsByLvl(m.MType, m.MSubType, lvl)
    printfn $"{m.MType}, {m.MSubType}: {lvl}"
    printfn $"{statShort stat}"
    printfn ""
)

type TestChamp(id:uint64, name:string, stat:Stat) =
    member _.CID = id
    member _.Name = name
    member _.Stat = stat

type TestRound(rid:uint64, mchar:MonsterChar, ractions:RoundAction list, champs:TestChamp list) =
    member _.RID = rid
    member _.MChar = mchar
    member _.RActions = ractions
    member _.Champs = champs

let emulateBattle(chCount:int) =
    let champs = List.init chCount (fun i ->
        TestChamp(uint64 i, $"Champ #{i}", getRandomStat())
    )
    let monster = Monster.TryCreate(MonsterType.Demon, MonsterSubType.Fire).Value
    let mstat = Monster.getStats monster
    
    let monsterChar = MonsterChar(0UL, monster, mstat, 1000UL, "Monster")
    let randActions (champs:TestChamp list) =
        champs
        |> List.map(fun ch ->
            {
                Move = Move.Attack
                Timestamp = DateTime.Now
                ChampId = ch.CID
                ChampName = ch.Name
                ChampLvl = 0UL
                Stat = ch.Stat
            }
        )
    let startRound = TestRound(0UL, monsterChar, randActions champs, champs)

    Seq.unfold(fun (tr:TestRound) ->
        match Battle.fight(tr.RID, 0UL, tr.RActions, Map.empty, Map.empty, tr.MChar, 1000.0M) with
        | Ok res ->
            let champs' = tr.Champs |> List.filter(fun ch -> res.DeadChamps |> List.contains ch.CID |> not)
            let tr' = TestRound(res.RoundId + 1UL, res.MonsterChar, randActions champs', champs')
            System.Diagnostics.Debug.Assert(tr.MChar.Stat.Defense = res.MonsterChar.Stat.Defense)
            if tr'.Champs.IsEmpty || (not res.MonsterChar.Stat.IsAlive) then None
            else (res, tr') |> Some
        | Error err ->
            printfn "%A" err
            None

    ) startRound

emulateBattle 1 |> Seq.iter(fun br -> printfn "%A" br)