namespace GameLogic.Monsters

open GameLogic.Champs

type MonsterType =
    /// no magic
    | Zombie = 0
    /// moderate magic
    | Demon = 1
    /// strong magic
    | Necromancer = 2
    /// universal, created for an nft
    | Universal = 3
    
type MonsterSubType =
    | None = 0
    | Fire = 1
    | Frost = 2
    
type Monster private (mtype:MonsterType, msubtype:MonsterSubType) =
    member _.MType = mtype
    member _.MSubType = msubtype
    static member Universal() = Monster(MonsterType.Universal, MonsterSubType.None)
    static member TryCreate (mtype:MonsterType, msubtype:MonsterSubType) =
        Monster(mtype, msubtype) |> Some

type MonsterChar(id: uint64, monster:Monster, stat:Stat, xp: uint64, name:string) =
    member _.Id = id
    member _.Monster = monster
    member _.Stat = stat
    member _.XP = xp
    member _.Name = name
    member _.UpdateStat(nStat:Stat) = MonsterChar(id, monster, nStat, xp, name)
    override x.ToString (): string = 
        $"{id}. {monster}: {stat} ({xp})"

[<RequireQualifiedAccess>]
type MonsterImg =
    | File of filepath:string
    | Ipfs of ipfs:string

type MonsterRecord(name:string, description:string, monster:Monster, stats:Stat, xp: uint64, img:MonsterImg) =
    member _.Name = name
    member _.Description = description
    member _.Monster = monster
    member _.Stats = stats
    member _.Xp = xp
    member _.Img = img

[<RequireQualifiedAccess>]
module Monster =
    let private getTypeStats(mtype:MonsterType) =
        match mtype with
        | MonsterType.Zombie ->
            {
                Health = 2000L
                Magic = 0L
                Accuracy = 1L
                Luck = 1L
                Attack = 7L
                MagicAttack = 0L
                Defense = 2L
                MagicDefense = 0L
            }            
        | MonsterType.Demon ->
            {
                Health = 3500L
                Magic = 550L
                Accuracy = 2L
                Luck = 4L
                Attack = 12L
                MagicAttack = 8L
                Defense = 5L
                MagicDefense = 4L
            }
        | MonsterType.Necromancer ->
            {
                Health = 1500L
                Magic = 2500L
                Accuracy = 3L
                Luck = 2L
                Attack = 6L
                MagicAttack = 15L
                Defense = 1L
                MagicDefense = 3L
            }
        | MonsterType.Universal ->
            {
                Health = 1000L
                Magic = 1000L
                Accuracy = 3L
                Luck = 2L
                Attack = 6L
                MagicAttack = 15L
                Defense = 1L
                MagicDefense = 3L
            }
    
    let private getSubTypeStats(msubtype:MonsterSubType) =
        match msubtype with
        | MonsterSubType.None -> Stat.Zero            
        | MonsterSubType.Fire ->
            {
                Health = 500L
                Magic = 100L
                Accuracy = 1L
                Luck = 1L
                Attack = 2L
                MagicAttack = 2L
                Defense = 1L
                MagicDefense = 1L
            }
        | MonsterSubType.Frost ->
            {
                Health = 500L
                Magic = 100L
                Accuracy = 1L
                Luck = 1L
                Attack = 1L
                MagicAttack = 1L
                Defense = 3L
                MagicDefense = 3L
            }

    let getMonsterStatsByLvl(mtype:MonsterType, msubtype:MonsterSubType, lvl:uint64) =
        let arr =
            match mtype, msubtype with
            | MonsterType.Zombie, MonsterSubType.None ->
                [|
                    Characteristic.Attack; Characteristic.Health
                    Characteristic.Defense; Characteristic.Health

                    // no magic so health + attack + defense instead of magic + mattack + mdefense
                    Characteristic.Luck; Characteristic.Accuracy
                    Characteristic.Health; Characteristic.Health
                    Characteristic.Attack; Characteristic.Attack
                    Characteristic.Defense; Characteristic.Defense

                    Characteristic.Attack; Characteristic.Health
                    Characteristic.Defense; Characteristic.Health
                |]
            | MonsterType.Zombie, MonsterSubType.Frost ->
                [|
                    Characteristic.Attack; Characteristic.Health
                    Characteristic.Defense; Characteristic.Defense

                    Characteristic.Luck; Characteristic.Accuracy
                    Characteristic.Health; Characteristic.Magic
                    Characteristic.Attack; Characteristic.MagicAttack
                    Characteristic.Defense; Characteristic.MagicDefense

                    Characteristic.Attack; Characteristic.Health
                    Characteristic.Defense; Characteristic.Defense
                |]
            | MonsterType.Zombie, MonsterSubType.Fire ->
                [|
                    Characteristic.Attack; Characteristic.Health
                    Characteristic.Defense; Characteristic.Attack

                    Characteristic.Luck; Characteristic.Accuracy
                    Characteristic.Health; Characteristic.Magic
                    Characteristic.Attack; Characteristic.MagicAttack
                    Characteristic.Defense; Characteristic.MagicDefense

                    Characteristic.Attack; Characteristic.Health
                    Characteristic.Defense; Characteristic.Attack
                |]
            | MonsterType.Demon, MonsterSubType.None ->
                [|
                    Characteristic.Health; Characteristic.Defense
                    Characteristic.Luck; Characteristic.Magic

                    Characteristic.Luck; Characteristic.Accuracy
                    Characteristic.Health; Characteristic.Magic
                    Characteristic.Attack; Characteristic.MagicAttack
                    Characteristic.Defense; Characteristic.MagicDefense

                    Characteristic.Health; Characteristic.Defense
                    Characteristic.Luck; Characteristic.Magic
                |]
            | MonsterType.Demon, MonsterSubType.Frost ->
                [|
                    Characteristic.Health; Characteristic.Defense
                    Characteristic.Luck; Characteristic.MagicDefense

                    Characteristic.Luck; Characteristic.Accuracy
                    Characteristic.Health; Characteristic.Magic
                    Characteristic.Attack; Characteristic.MagicAttack
                    Characteristic.Defense; Characteristic.MagicDefense

                    Characteristic.Health; Characteristic.Defense
                    Characteristic.Luck; Characteristic.MagicDefense
                |]
            | MonsterType.Demon, MonsterSubType.Fire ->
                [|
                    Characteristic.Health; Characteristic.Defense
                    Characteristic.Luck; Characteristic.MagicAttack

                    Characteristic.Luck; Characteristic.Accuracy
                    Characteristic.Health; Characteristic.Magic
                    Characteristic.Attack; Characteristic.MagicAttack
                    Characteristic.Defense; Characteristic.MagicDefense

                    Characteristic.Health; Characteristic.Defense
                    Characteristic.Luck; Characteristic.MagicAttack
                |]
            | MonsterType.Necromancer, MonsterSubType.None ->
                [|
                    Characteristic.Magic; Characteristic.MagicAttack
                    Characteristic.Accuracy; Characteristic.Health

                    Characteristic.Luck; Characteristic.Accuracy
                    Characteristic.Health; Characteristic.Magic
                    Characteristic.Attack; Characteristic.MagicAttack
                    Characteristic.Defense; Characteristic.MagicDefense

                    Characteristic.Magic; Characteristic.MagicAttack
                    Characteristic.Accuracy; Characteristic.Health
                |]
            | MonsterType.Necromancer, MonsterSubType.Frost ->
                [|
                    Characteristic.Magic; Characteristic.MagicAttack
                    Characteristic.Accuracy; Characteristic.MagicDefense

                    Characteristic.Luck; Characteristic.Accuracy
                    Characteristic.Health; Characteristic.Magic
                    Characteristic.Attack; Characteristic.MagicAttack
                    Characteristic.Defense; Characteristic.MagicDefense

                    Characteristic.Magic; Characteristic.MagicAttack
                    Characteristic.Accuracy; Characteristic.MagicDefense
                |]
            | MonsterType.Necromancer, MonsterSubType.Fire ->
                [|
                    Characteristic.Magic; Characteristic.MagicAttack
                    Characteristic.Accuracy; Characteristic.MagicAttack

                    Characteristic.Luck; Characteristic.Accuracy
                    Characteristic.Health; Characteristic.Magic
                    Characteristic.Attack; Characteristic.MagicAttack
                    Characteristic.Defense; Characteristic.MagicDefense

                    Characteristic.Magic; Characteristic.MagicAttack
                    Characteristic.Accuracy; Characteristic.MagicAttack
                |]
            | MonsterType.Universal, _ ->
                [|
                    Characteristic.Health; Characteristic.Magic
                    Characteristic.Accuracy; Characteristic.Attack

                    Characteristic.Defense; Characteristic.MagicDefense
                    Characteristic.Luck; Characteristic.MagicAttack
                |]

        Seq.init (int lvl) (fun i -> arr.[i % arr.Length]) |> Levels.statFromCharacteristicSeq
    
    /// from 0 to 100
    let massAttackChance(monster:Monster, isMagicAttack:bool) =
        match monster.MType with
        | MonsterType.Zombie ->
            match monster.MSubType with
            | MonsterSubType.None -> 75
            | MonsterSubType.Fire -> if isMagicAttack then 90 else 75
            | MonsterSubType.Frost -> if isMagicAttack then 80 else 75
        | MonsterType.Demon ->
            match monster.MSubType with
            | MonsterSubType.None -> if isMagicAttack then 75 else 60
            | MonsterSubType.Fire -> if isMagicAttack then 60 else 55
            | MonsterSubType.Frost -> if isMagicAttack then 55 else 60
        | MonsterType.Necromancer ->
            match monster.MSubType with
            | MonsterSubType.None -> if isMagicAttack then 95 else 80
            | MonsterSubType.Fire -> if isMagicAttack then 90 else 75
            | MonsterSubType.Frost -> if isMagicAttack then 85 else 90
        | MonsterType.Universal ->
            match monster.MSubType with
            | MonsterSubType.None -> 80
            | MonsterSubType.Fire -> 75
            | MonsterSubType.Frost -> 70            

    let getStats(monster:Monster) =
        getTypeStats monster.MType + getSubTypeStats monster.MSubType

    let getRevivalDuration(monster:Monster) =
        match monster.MType with
        | MonsterType.Zombie -> Constants.RoundsInBattle * 2
        | MonsterType.Demon -> Constants.RoundsInBattle * 7
        | MonsterType.Necromancer -> Constants.RoundsInBattle * 6
        | MonsterType.Universal -> Constants.RoundsInBattle * 4
        |> uint

[<RequireQualifiedAccess>]
module Format =
    let createMsg (mtype:MonsterType) =
        match mtype with
        | MonsterType.Zombie -> "resurrected"
        | MonsterType.Demon -> "summoned"
        | MonsterType.Necromancer -> "invited"
        | MonsterType.Universal -> "revealed"

