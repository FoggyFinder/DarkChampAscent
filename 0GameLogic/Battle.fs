namespace GameLogic.Battle

open System
open GameLogic.Shop
open GameLogic.Effects
open GameLogic.Champs
open GameLogic.Monsters
open GameLogic.Rewards

[<RequireQualifiedAccess>]
type BattleStatus =
    | Started = 0
    | Processing = 1
    | Finished = 2

[<RequireQualifiedAccess>]
type RoundStatus =
    | Started = 0
    | Processing = 1
    | Finished = 2

type RoundActionRecord = {
    Move: Move
    ChampId: uint64
}

type RoundAction = {
    Move: Move
    Timestamp: DateTime
    ChampId: uint64
    ChampName: string
    Stat: Stat
    ChampLvl: uint64
}

// effect is something that already took into account boosts and other effects
type RoundEffect = {
    StartRoundId: int64
    Item : Effect
    Duration : int
    Val : uint64 option
}

type BattleResult = {
    RoundId: uint64
    BattleId: uint64
    MonsterChar: MonsterChar

    Rewards: RoundRewardSplit
            
    ChampsMoveAndXp: Map<uint64, PerformedMove * uint64>
    ChampsFinalStat: Map<uint64, Stat>
    DeadChamps: uint64 list

    MonsterDefeater: uint64 option
    MonsterPM: PerformedMove option
    MonsterActions: Map<uint64, PerformedMove * uint64>
}

[<RequireQualifiedAccess>]
module Battle =
    open MathNet.Numerics.Distributions
    let RoundDuration = TimeSpan.FromMinutes(30.0)
    
    let processEffect (stat:Stat) (re:RoundEffect) (currenrRound:int64) =
        match re.Item with
        | Effect.Death -> stat
        | Effect.Hit ->
            match re.Val with
            | Some v when re.StartRoundId + int64 re.Duration <= currenrRound ->
                { stat with Health = stat.Health - int64 v }
            | _ -> stat
        | Effect.MagicHit ->
            match re.Val with
            | Some v when re.StartRoundId + int64 re.Duration <= currenrRound ->
                { stat with Magic = stat.Magic - int64 v }
            | _ -> stat
        | Effect.Desolation ->
            match re.Val with
            | Some v when re.StartRoundId + int64 re.Duration <= currenrRound ->
                { stat with Luck = stat.Luck - int64 v }
            | _ -> stat
        | Effect.Scattermind ->
            match re.Val with
            | Some v when re.StartRoundId + int64 re.Duration <= currenrRound ->
                { stat with Accuracy = stat.Accuracy - int64 v }
            | _ -> stat
        | Effect.Despondency ->
            match re.Val with
            | Some v when re.StartRoundId + int64 re.Duration <= currenrRound ->
                { stat with Attack = stat.Attack - int64 v }
            | _ -> stat
        | Effect.Sorrow ->
            match re.Val with
            | Some v when re.StartRoundId + int64 re.Duration <= currenrRound ->
                { stat with MagicAttack = stat.MagicAttack - int64 v }
            | _ -> stat
        | Effect.Shatter ->
            match re.Val with
            | Some v when re.StartRoundId + int64 re.Duration <= currenrRound ->
                { stat with Defense = stat.Defense - int64 v }
            | _ -> stat
        | Effect.Disrupt ->
            match re.Val with
            | Some v when re.StartRoundId + int64 re.Duration <= currenrRound ->
                { stat with MagicDefense = stat.MagicDefense - int64 v }
            | _ -> stat

    let getEffects() = failwith "..."
    
    let processShopItem (stat:Stat) (rb:RoundBoost) (currenrRound:int64) =
        if rb.Duration > 0 && rb.StartRoundId + int64 rb.Duration >= currenrRound then
            Shop.applyShopItem rb.Boost stat
        else
            stat

    let isHit (accuracy:int64) (lvl:uint64) =
        let n = 6UL + lvl / 12UL
        let v = uint64 (max 0L accuracy)
        let p =
            if v > n then
                0.97 + 0.03 * float v / (10.0 + float lvl)
            else
                0.5 + 0.25 * float v / float n
        if p > 1.0 then printfn "Bernoulli: %A %A %A %A" n p v lvl
        let bernoulliDist = Bernoulli(min 1.0 p)
        bernoulliDist.Sample() = 1

    let isLucky (stat:Stat) =
        let p = 3.0 * Math.Log2(float stat.Luck) / 100.0
        let bernoulliDist = Bernoulli(p)
        bernoulliDist.Sample() = 1

    // - attack -> takes damage -> gives amount of coins proportional to damage + fixed min amount
    let attack (stat:Stat) (lvl:uint64) : Dmg =
        if isHit stat.Accuracy lvl then
            let attack = uint64 (max 0L stat.Attack)
            if isLucky stat then Dmg.Critical(attack * 3UL) else Dmg.Default attack
        else Dmg.Missed
    
    //- magic attack -> takes damage but takes magic health as well -> gives amount of coins proportional to damage + fixed min amount
    let private magicAttack (stat:Stat) (lvl:uint64) : Dmg * uint64 =
        if stat.Magic >= 1L then
            if isHit stat.Accuracy lvl then
                let magicCost = 5UL
                let mattack = uint64 (max 0L stat.MagicAttack)
                let dmg' =
                    if stat.Magic >= int64 magicCost then mattack
                    else 1UL
                let dmg' =
                    if isLucky stat
                    then Dmg.Critical(dmg' * 3UL)
                    else Dmg.Default dmg'
                dmg', magicCost
            else Dmg.Missed, 1UL
        else Dmg.Missed, 0UL
    
    //- shield -> increase defense for 1 round -> gives fixed min amount of coins 
    let private shield (stat:Stat) (lvl:uint64) : uint64 =
        3UL + lvl / 8UL

    //- magic shield -> increase magic defense for 1 round but takes magic health -> gives fixed min amount of coins 
    let private magicShield (stat:Stat) (lvl:uint64) : uint64 * uint64 =
        let magicCost = 5UL
        if stat.Magic >= int64 magicCost then
            4UL + lvl / 8UL, magicCost
        elif stat.Magic >= int64 1UL then
            1UL + lvl / 8UL, uint64 stat.Magic
        else
            0UL, 0UL
    
    //- heal -> increase health but takes magic health -> gives fixed min amount of coins
    let private heal (stat:Stat) (lvl:uint64) : uint64 * uint64  =
        let magicCost = 5UL + lvl
        if stat.Magic >= int64 magicCost then
            3UL + lvl / 8UL, magicCost
        elif stat.Magic >= int64 1UL then
            1UL + lvl / 8UL, uint64 stat.Magic
        else
            0UL, 0UL

    //- meditate -> increase magic health -> gives fixed min amount of coins
    let private meditate (stat:Stat) (lvl:uint64) : uint64 =
        2UL + lvl

    let performMove (move:Move) (stat:Stat) (lvl:uint64) =
        match move with
        | Move.Attack ->
            attack stat lvl |> PerformedMove.Attack
        | Move.MagicAttack ->
            magicAttack stat lvl |> PerformedMove.MagicAttack
        | Move.Shield ->
            shield stat lvl |> PerformedMove.Shield
        | Move.MagicShield ->
            magicShield stat lvl |> PerformedMove.MagicShield
        | Move.Heal ->
            heal stat lvl |> PerformedMove.Heal
        | Move.Meditate ->
            meditate stat lvl |> PerformedMove.Meditate

    let updateStats (stat:Stat) (targetStat:Stat) (pm:PerformedMove) =
        match pm with
        | PerformedMove.Attack d ->
            match d with
            | Dmg.Missed -> stat, targetStat, pm
            | Dmg.Default v | Dmg.Critical v ->
                let isL = isLucky targetStat
                let dmg = int64 v - targetStat.Defense * (if isL then 3L else 1L)
                let dmg' = max dmg 0L
                stat, { targetStat with Health = targetStat.Health - dmg' }, PerformedMove.Attack(d.WithValue(uint64 dmg'))
        | PerformedMove.MagicAttack (d, m) ->
            let dmg = int64 d.Value - targetStat.MagicDefense * (if isLucky targetStat then 3L else 1L)
            let dmg' = max dmg 0L
            { stat with Magic = stat.Magic - int64 m },
            { targetStat with Health = targetStat.Health - dmg' },
            PerformedMove.MagicAttack(d.WithValue(uint64 dmg'), m)
        | PerformedMove.Shield f ->
            { stat with Defense = stat.Defense + int64 f }, targetStat, pm
        | PerformedMove.MagicShield(defenseIncrease, magicTaken) ->
            { stat with
                Defense = stat.Defense + int64 defenseIncrease
                Magic = stat.Magic - int64 magicTaken
            }, targetStat, pm
        | PerformedMove.Heal(healthIncrease, magicTaken) ->
            { stat with
                Health = stat.Health + int64 healthIncrease
                Magic = stat.Magic - int64 magicTaken
            }, targetStat, pm
        |  PerformedMove.Meditate f ->
            { stat with Magic = stat.Magic + int64 f }, targetStat, pm    

    let processActions (ms:Stat) (ractions:RoundAction list) =
        ractions
        |> List.sortBy(fun ra -> ra.Timestamp)
        |> List.mapFold(fun (monsterStat:Stat) champAction ->
            if monsterStat.IsAlive then
                if champAction.Stat.IsAlive then
                    let source, target, pm =
                        performMove champAction.Move champAction.Stat champAction.ChampLvl
                        |> updateStats champAction.Stat monsterStat

                    Some({ champAction with Stat = source }, pm), target
                else
                    None, monsterStat
            else
                None, monsterStat) ms  

    let fight(roundId: uint64, battleId: uint64, roundMoves: RoundAction list,
            boosts:Map<uint64, RoundBoost list>, lvlsStat: Map<uint64, Stat>,
            monster:MonsterChar, rewards:decimal) =
        let monsterAction = Monster.selectMonsterAction monster
        let monsterLvl = Levels.getLvlByXp monster.XP
        let monsterLvlStats = Monster.getMonsterStatsByLvl(monster.Monster.MType, monster.Monster.MSubType, monsterLvl)
            
        let monsterStat = monster.Stat + monsterLvlStats
        let monsterPM =
            match monsterAction with
            // if monster attacks champs then it happens after champs move
            // otherwise it happens before
            | Move.Attack | Move.MagicAttack -> None
            | Move.Shield ->
                shield monsterStat monsterLvl
                |> PerformedMove.Shield |> Some
            | Move.MagicShield ->
                magicShield monsterStat monsterLvl
                |> PerformedMove.MagicShield |> Some
            | Move.Heal ->
                heal monsterStat monsterLvl
                |> PerformedMove.Heal |> Some
            | Move.Meditate ->
                meditate monsterStat monsterLvl
                |> PerformedMove.Meditate |> Some
        
        let monsterStat' =
            match monsterPM with
            | Some pm -> updateStats monsterStat Stat.Zero pm |> Utils.fstOf3
            | None -> monsterStat

        let boostsStat =
            boosts
            |> Map.map(fun k v -> 
                v |> List.fold(fun stat boost ->
                    processShopItem stat boost (int64 roundId)) Stat.Zero)
            
        let actions, mstat =
            roundMoves
            |> List.map(fun ra ->
                let champStatFull =
                    let lvlStat = Map.tryFind ra.ChampId lvlsStat |> Option.defaultValue Stat.Zero
                    let boostStat = Map.tryFind ra.ChampId boostsStat |> Option.defaultValue Stat.Zero
                    ra.Stat + lvlStat + boostStat
                { ra with Stat = champStatFull })
            |> processActions monsterStat'
        
        let monster' = monster.UpdateStat mstat
        let actions' = actions |> List.choose id
        
        let monsterDefeater = if mstat.IsAlive then None else actions' |> List.tryLast |> Option.map(fun (ra,_) -> ra.ChampId)
        
        let monsterActions, monsterState =
            match monsterAction with
            | Move.Attack | Move.MagicAttack ->
                match monsterDefeater with
                | Some _ -> [], monster'
                | None ->
                    let isMagicAttack = monsterAction = Move.MagicAttack
                    let isMassAttack = int64(Monster.massAttackChance(monster.Monster, isMagicAttack)) > Random.Shared.NextInt64(101L)
                    let target =
                        if isMassAttack then None
                        else actions' |> List.randomChoice |> fun (ra, _) -> ra.ChampId |> Some
                
                    actions' |> List.mapFold(fun (mc:MonsterChar) (ra:RoundAction, _) ->
                        let b =
                            match target with
                            | Some champId -> champId = ra.ChampId
                            | None -> true
                        if b then
                            let source, target, pm =
                                if isMagicAttack then
                                    let stat' =
                                        if isMassAttack then mc.Stat
                                        else { mc.Stat with MagicAttack = 4L * mc.Stat.MagicAttack }
                                    magicAttack stat' monsterLvl
                                    |> PerformedMove.MagicAttack
                                else
                                    let stat' =
                                        if isMassAttack then mc.Stat
                                        else { mc.Stat with Attack = 3L * mc.Stat.Attack }
                                    attack stat' monsterLvl
                                    |> PerformedMove.Attack
                                |> updateStats mc.Stat ra.Stat
                            let mc' = mc.UpdateStat source
                            
                            let champStat = {| 
                                ChampId = ra.ChampId
                                ChampStat = target
                            |}
                            
                            let xpEarned = if isMassAttack then 2UL else 10UL

                            let monsterMove = {|
                                PerformedMove = pm
                                XpEarned = xpEarned 
                            |}

                            Some(champStat, monsterMove), mc'
                        else
                            None, mc               
                    ) monster'
            | Move.Shield | Move.MagicShield
            | Move.Heal | Move.Meditate -> [], monster'
            
        let rewardsRes =
            actions'
            |> List.map(fun (ra, pa) -> ra.Move, ra.ChampId, pa.Dmg)
            |> GameLogic.Rewards.RoundRewardSplit.CalculateRewards rewards monsterDefeater
        
        let champsMoveAndXp =
            actions'
            |> List.map(fun (ra, pa) -> ra.ChampId, (pa, 1UL))
            |> Map.ofList
        
        let monsterActions' =
            monsterActions
            |> List.choose(Option.map(fun (car, mar) ->
                car.ChampId, (car.ChampStat, mar.PerformedMove, mar.XpEarned)))
            |> Map.ofList

        let monsterFinalStat =
            match monsterAction with
            // in case of shield reduce to basic afterwards
            | Move.Shield | Move.MagicShield ->
                { monsterState.Stat with
                    Defense = min monsterState.Stat.Defense monster.Stat.Defense
                    MagicDefense = min monsterState.Stat.MagicDefense monster.Stat.MagicDefense
                }
            | Move.Attack | Move.MagicAttack
            | Move.Heal | Move.Meditate ->
                monsterState.Stat

        let champsFinalStat =
            actions'
            |> List.map(fun (ra, pa) ->
                let stat =
                    match monsterActions'.TryFind ra.ChampId with
                    | Some (cs, _, _) -> cs
                    | None -> ra.Stat
                let stat' =
                    match ra.Move with
                    | Move.Shield | Move.MagicShield ->
                        let champOriginalStat =
                            roundMoves
                            |> List.pick(fun rm ->
                                if rm.ChampId = ra.ChampId then rm.Stat |> Some
                                else None)
                        { stat with
                            Defense = min stat.Defense champOriginalStat.Defense
                            MagicDefense = min stat.MagicDefense champOriginalStat.MagicDefense
                        }
                    | Move.Attack | Move.MagicAttack
                    | Move.Heal | Move.Meditate ->
                        stat
                let lvlStat = Map.tryFind ra.ChampId lvlsStat |> Option.defaultValue Stat.Zero
                let boostStat = Map.tryFind ra.ChampId boostsStat |> Option.defaultValue Stat.Zero
                let baseStat = stat' - lvlStat - boostStat
                ra.ChampId, baseStat, stat.IsAlive)
            

        rewardsRes |> Result.map(fun rewards ->
            {
                RoundId = roundId
                BattleId = battleId
                MonsterChar = monster.UpdateStat (monsterFinalStat - monsterLvlStats)

                Rewards = rewards
            
                ChampsMoveAndXp = champsMoveAndXp
                ChampsFinalStat = champsFinalStat |> List.map(fun (x,y,z) -> x, y) |> Map.ofList
                DeadChamps = champsFinalStat |> List.choose(fun (x, y, z) -> if z then None else Some x)
                MonsterDefeater = monsterDefeater

                MonsterPM = monsterPM
                MonsterActions = monsterActions' |> Map.map(fun k (_, pm, xp) -> pm, xp)
            })