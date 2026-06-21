module GameLogic.Rewards

open System
open GameLogic.Champs

type Balances = {
    Rewards: decimal
    Dev: decimal
    Reserve: decimal
    DAO: decimal
    Champs: decimal
    Monstrs: decimal
} with
    member t.Total =
        t.Rewards + t.Dev + t.Reserve + t.DAO + t.Champs + t.Monstrs

let [<Literal>] Window = 90

let getBattleReward v = Math.Round(v / decimal Window, 6)
let getRoundReward v = Math.Round(v / decimal Constants.RoundsInBattle, 6)

type ChampEarnedReward = {
    ChampId: uint64
    Reward: decimal
}

[<Struct>]
type SpecialReward(dao: decimal, reserve: decimal, dev: decimal) =
    member _.DAO = dao
    member _.Reserve = reserve
    member _.Dev = dev
    member _.Total = dao + reserve + dev

type RoundRewardSplit private (
        champs: ChampEarnedReward list,
        srewards:SpecialReward,
        monster:decimal,
        unclaimed:decimal) =

    let champsTotal = champs |> List.sumBy(fun cer -> cer.Reward)
    let claimed = champsTotal + srewards.Total + monster
    
    member _.Champs = champs
    member _.Monster = monster
    member _.SRewards = srewards
    member _.ChampsTotal = champsTotal
    member _.Claimed = claimed
    member _.Unclaimed = unclaimed

    static member CalculateRewards (roundRewards:decimal) (monsterDefeater:uint64 option) (actions:(Move * uint64 * Dmg option) list) =

        // 10% - Move.Shield
        // 10% - Move.MagicShield
        // 10% - Move.Heal
        // 10% - Move.Meditate
        // 5%  - Move.Attack
        // 5%  - Move.MagicAttack
        // 20% - is splitted proportionally as (Damage / TotalDamage)

        let x = roundRewards / 100M
    
        let fixedMove = 10M * x
        let fixedAttack = 5M * x

        let groupsByMoves = actions |> List.countBy(fun (move, _, _) -> move) |> dict
                        
        // 10% - goes to those who used "shield"
        let shieldFixed =
            if groupsByMoves.ContainsKey Move.Shield then fixedMove / (decimal groupsByMoves.[Move.Shield])
            else 0M

        // 10% - goes to those who used "magic shield"            
        let mshieldFixed =
            if groupsByMoves.ContainsKey Move.MagicShield then fixedMove / (decimal groupsByMoves.[Move.MagicShield])
            else 0M

        // 10% - goes to those who used "heal"
        let healFixed =
            if groupsByMoves.ContainsKey Move.Heal then fixedMove / (decimal groupsByMoves.[Move.Heal])
            else 0M

        // 10% - goes to those who used "meditate"            
        let meditateFixed =
            if groupsByMoves.ContainsKey Move.Meditate then fixedMove / (decimal groupsByMoves.[Move.Meditate])
            else 0M

        // 5% - goes to those who used "attack"     
        let attackFixed =
            if groupsByMoves.ContainsKey Move.Attack then fixedAttack / (decimal groupsByMoves.[Move.Attack])
            else 0M

        // 5% - goes to those who used "magic attack"            
        let mAttackFixed =
            if groupsByMoves.ContainsKey Move.MagicAttack then fixedAttack / (decimal groupsByMoves.[Move.MagicAttack])
            else 0M

        let totalDmg = actions |> List.sumBy(fun (_, _, d) -> d |> Option.map(fun dmg -> dmg.Value) |> Option.defaultValue 0UL)


        // 20% is splitted as (Damage / TotalDamage)
        let damageR =
            if totalDmg = 0UL then
                0M
            else
                20M * x / (decimal totalDmg)

        let champs =
            actions
            |> List.map(fun (move, champId, d) ->
                let dmgReward =
                    match move with
                    | Move.Attack
                    | Move.MagicAttack ->
                        // special case - monster is killed, all dmg pot goes to defeater
                        match monsterDefeater with
                        | Some mdefeaterId -> if champId = mdefeaterId then damageR else 0M
                        | None ->
                            match d.Value with
                            | damage when damage.Value > 0UL -> (decimal damage.Value) * damageR
                            | _ -> 0M
                    | _ -> 0M
        
                let fixedReward =
                    match move with
                    | Move.Attack -> attackFixed
                    | Move.MagicAttack -> mAttackFixed
                    | Move.Shield -> shieldFixed
                    | Move.MagicShield -> mshieldFixed
                    | Move.Meditate -> meditateFixed
                    | Move.Heal -> healFixed

                {
                    ChampId = champId
                    Reward = Math.Round(dmgReward + fixedReward, 6)
                }
            )

        // 70% - champ's rewards
        // 5% - monster's owner
        // 10% - dao
        // 9% - to devs
        // 6% - reserve
        
        let dao = Math.Round(10M * x, 6)
        let devs = Math.Round(9M * x, 6)
        let reserve = Math.Round(6M * x, 6)
        let sRewards = SpecialReward(dao, reserve, devs)
        let champsTotal = champs |> List.sumBy(fun cer -> cer.Reward)
        let monstr = Math.Round(5M * x, 6)
        
        let unclaimed = roundRewards - (champs |> List.sumBy(fun cer -> cer.Reward)) - dao - devs - reserve - monstr
        // rounding error?
        //let reserve' =
        //    if unclaimed < 0M && unclaimed >= -0.0001M then
        //        reserve + unclaimed
        //    else reserve
        if unclaimed < -0.0001M then
            Error($"Unclaimed is less than prec: {roundRewards};ChampsTotal = {champsTotal};DAO = {dao}; Devs = {devs}; Reserve = {reserve}; Monstr = {monstr}")
        else RoundRewardSplit(champs, sRewards, monstr, unclaimed) |> Ok
        