module GameLogic.Rewards

open System
open GameLogic.Champs

type Balances = {
    Rewards: decimal
    Burn: decimal
    Dev: decimal
    Reserve: decimal
    DAO: decimal
    Staking: decimal
    Users: decimal
    Champs: decimal
    Locked: decimal
} with
    member t.Total =
        t.Rewards + t.Burn + t.Dev + t.Reserve + t.DAO + t.Staking + t.Users + t.Champs + t.Locked

let [<Literal>] Window = 90

let getBattleReward v = Math.Round(v / decimal Window, 6)
let getRoundReward v = Math.Round(v / decimal Constants.RoundsInBattle, 6)

type ChampEarnedReward = {
    ChampId: uint64
    Reward: decimal
}

type RoundRewardSplit private (
        champs: ChampEarnedReward list,
        dao: decimal,
        reserve: decimal,
        burn: decimal,
        dev: decimal,
        staking: decimal,
        unclaimed: decimal) =

    let champsTotal = champs |> List.sumBy(fun cer -> cer.Reward)
    let claimed = champsTotal + dao + reserve + burn + dev + staking
    
    member _.Champs = champs
    member _.DAO = dao
    member _.Reserve = reserve
    member _.Burn = burn
    member _.Dev = dev
    member _.ChampsTotal = champsTotal
    member _.Claimed = claimed
    member _.Unclaimed = unclaimed
    member _.Staking = staking

    static member CalculateRewards (roundRewards:decimal) (monsterDefeater:uint64 option) (actions:(Move * uint64 * Dmg option) list) =

        // 11% - Move.Shield
        // 11% - Move.MagicShield
        // 11% - Move.Heal
        // 11% - Move.Meditate
        // 5.5%  - Move.Attack
        // 5.5%  - Move.MagicAttack
        // 20% - is splitted proportionally as (Damage / TotalDamage)

        let x = roundRewards / 100M
    
        let fixedMove = 11M * x
        let fixedAttack = 5.5M * x

        let groupsByMoves = actions |> List.countBy(fun (move, _, _) -> move) |> dict
                        
        // 11% - goes to those who used "shield"
        let shieldFixed =
            if groupsByMoves.ContainsKey Move.Shield then fixedMove / (decimal groupsByMoves.[Move.Shield])
            else 0M

        // 11% - goes to those who used "magic shield"            
        let mshieldFixed =
            if groupsByMoves.ContainsKey Move.MagicShield then fixedMove / (decimal groupsByMoves.[Move.MagicShield])
            else 0M

        // 11% - goes to those who used "heal"
        let healFixed =
            if groupsByMoves.ContainsKey Move.Heal then fixedMove / (decimal groupsByMoves.[Move.Heal])
            else 0M

        // 11% - goes to those who used "meditate"            
        let meditateFixed =
            if groupsByMoves.ContainsKey Move.Meditate then fixedMove / (decimal groupsByMoves.[Move.Meditate])
            else 0M

        // 5.5% - goes to those who used "attack"     
        let attackFixed =
            if groupsByMoves.ContainsKey Move.Attack then fixedAttack / (decimal groupsByMoves.[Move.Attack])
            else 0M

        // 5.5% - goes to those who used "magic attack"            
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

        // 75% - champ's rewards
        // 10% - dao
        // 8% - to devs
        // 5% - reserve
        // 1% - staking
        // 1% - burn
        let dao = Math.Round(10M * x, 6)
        let devs = Math.Round(8M * x, 6)
        let reserve = Math.Round(5M * x, 6)
        let staking = Math.Round(x, 6)
        let burn = Math.Round(x, 6)
        let champsTotal = champs |> List.sumBy(fun cer -> cer.Reward)
        
        let unclaimed = roundRewards - (champs |> List.sumBy(fun cer -> cer.Reward)) - dao - devs - reserve - burn - staking
        // rounding error?
        //let reserve' =
        //    if unclaimed < 0M && unclaimed >= -0.0001M then
        //        reserve + unclaimed
        //    else reserve
        if unclaimed < -0.0001M then
            Error($"Unclaimed is less than prec: {roundRewards};ChampsTotal = {champsTotal};DAO = {dao}; Devs = {devs}; Reserve = {reserve}; Burn = {burn}; Staking = {staking}")
        else RoundRewardSplit(champs, dao, reserve, burn, devs, staking, unclaimed) |> Ok
        