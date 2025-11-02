namespace GameLogic.Monsters

open GameLogic.Champs

type MonsterType =
    /// no magic
    | Zombie = 0
    /// moderate magic
    | Demon = 1
    /// strong magic
    | Necromancer = 2
    
type MonsterSubType =
    | None = 0
    | Fire = 1
    | Frost = 2
    
type Monster private (mtype:MonsterType, msubtype:MonsterSubType) =
    member _.MType = mtype
    member _.MSubType = msubtype

    static member TryCreate (mtype:MonsterType, msubtype:MonsterSubType) =
        Monster(mtype, msubtype) |> Some

type MonsterChar(id: uint64, monster:Monster, stat:Stat, xp: uint64, name:string) =
    member _.Id = id
    member _.Monster = monster
    member _.Stat = stat
    member _.XP = xp
    member _.Name = name
    member t.UpdateStat(nStat:Stat) = MonsterChar(id, monster, nStat, xp, name)
    override x.ToString (): string = 
        $"{id}. {monster}: {stat} ({xp})"

type MonsterRecord(name:string, description:string, monster:Monster, stats:Stat, xp: uint64) =
    member _.Name = name
    member _.Description = description
    member _.Monster = monster
    member _.Stats = stats
    member _.Xp = xp

[<RequireQualifiedAccess>]
type MonsterImg =
    | File of filepath:string
    static member DefaultName(mtype:MonsterType, msubtype:MonsterSubType) =
        mtype.ToString().ToLower() + "_" + msubtype.ToString().ToLower() + ".png"
    static member DefaultFile(monster:Monster) =
        let filename = MonsterImg.DefaultName (monster.MType, monster.MSubType)
        System.IO.Path.Combine("Assets", filename) |> MonsterImg.File

type MonsterInfo = {
    XP : uint64
    Name : string
    Description : string
    Picture : MonsterImg
    Stat: Stat
    MType : MonsterType
    MSubType : MonsterSubType
}

[<RequireQualifiedAccess>]
module Monster =
    let private getTypeStats(mtype:MonsterType) =
        match mtype with
        | MonsterType.Zombie ->
            {
                Health = 800L
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
                Health = 1500L
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
                Health = 600L
                Magic = 2500L
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

    let selectMonsterAction (mc:MonsterChar) =
        let monster = mc.Monster
        let stat = mc.Stat
        let v = System.Random.Shared.NextInt64(101L)
        match monster.MType with
        | MonsterType.Zombie ->
            match monster.MSubType, stat.Magic > 0L with
            | MonsterSubType.Fire, true ->
                if v <= 80L then Move.Attack
                elif v <= 87L then Move.Shield
                elif v <= 93L then Move.MagicAttack
                elif v <= 96L then Move.MagicShield
                elif v <= 98L then Move.Meditate
                else Move.Heal                 
            | MonsterSubType.Frost, true ->
                if v <= 70L then Move.Attack
                elif v <= 85L then Move.Shield
                elif v <= 95L then Move.MagicAttack
                elif v <= 97L then Move.MagicShield
                elif v <= 98L then Move.Meditate
                else Move.Heal
            | MonsterSubType.None, _
            | MonsterSubType.Fire, false
            | MonsterSubType.Frost, false ->
                if v <= 90L then Move.Attack
                else Move.Shield
        | MonsterType.Demon ->
            if stat.Magic <= 0L then
                if v <= 60L then Move.Attack
                elif v <= 85L then Move.Meditate
                else Move.Shield
            else
                match monster.MSubType with
                | MonsterSubType.None ->
                    if v <= 65L then Move.Attack
                    elif v <= 80L then Move.Shield
                    elif v <= 85L then Move.MagicAttack
                    elif v <= 90L then Move.MagicShield
                    elif v <= 95L then Move.Meditate
                    else Move.Heal                       
                | MonsterSubType.Fire ->
                    if v <= 50L then Move.Attack
                    elif v <= 55L then Move.Shield
                    elif v <= 75L then Move.MagicAttack
                    elif v <= 85L then Move.MagicShield
                    elif v <= 97L then Move.Meditate
                    else Move.Heal
                | MonsterSubType.Frost ->
                    if v <= 60L then Move.Attack
                    elif v <= 65L then Move.Shield
                    elif v <= 80L then Move.MagicAttack
                    elif v <= 90L then Move.MagicShield
                    elif v <= 95L then Move.Meditate
                    else Move.Heal
        | MonsterType.Necromancer ->
            if stat.Magic = 0L then
                if v <= 30L then Move.Attack
                elif v < 90L then Move.Heal
                else Move.Shield
            else
                match monster.MSubType with
                | MonsterSubType.None ->
                    if v <= 25L then Move.Attack
                    elif v <= 26L then Move.Shield
                    elif v <= 76L then Move.MagicAttack
                    elif v <= 80L then Move.MagicShield
                    elif v <= 95L then Move.Meditate
                    else Move.Heal                     
                | MonsterSubType.Fire ->
                    if v <= 20L then Move.Attack
                    elif v <= 22L then Move.Shield
                    elif v <= 62L then Move.MagicAttack
                    elif v <= 67L then Move.MagicShield
                    elif v <= 87L then Move.Meditate
                    else Move.Heal
                | MonsterSubType.Frost ->
                    if v <= 22L then Move.Attack
                    elif v <= 23L then Move.Shield
                    elif v <= 66L then Move.MagicAttack
                    elif v <= 75L then Move.MagicShield
                    elif v <= 88L then Move.Meditate
                    else Move.Heal

    let getStats(monster:Monster) =
        getTypeStats monster.MType + getSubTypeStats monster.MSubType

    let getRevivalDuration(monster:Monster) =
        match monster.MType with
        | MonsterType.Zombie -> Constants.RoundsInBattle * 3
        | MonsterType.Demon -> Constants.RoundsInBattle * 7
        | MonsterType.Necromancer ->  Constants.RoundsInBattle * 5
        |> uint

    // dpmpp_2,; sgm_uniform
    let DefaultsMonsters =
        [
            Monster.TryCreate(MonsterType.Zombie, MonsterSubType.None),
                "Rotting Cadaver",
                "A reanimated corpse with decaying flesh, empty eyes, and tattered clothing. It moves slowly, driven solely by a primal urge to feed. Its body is covered in dirt, moss, and fungi, showing signs of prolonged decomposition. The stench of rot lingers around it, and its hollow moans echo as it shambles forward"
            
            Monster.TryCreate(MonsterType.Zombie, MonsterSubType.Fire),
                "Inferno Corpse",
                "A zombie imbued with fiery magic, its body engulfed in small, flickering flames that seem to burn without consuming it. Its skin is charred and blackened, with glowing embers in its empty eye sockets. The flames on its body seem to grow stronger when it is agitated, and it leaves a trail of scorched ground in its wake"
            
            Monster.TryCreate(MonsterType.Zombie, MonsterSubType.Frost),
                "Frozen Wretch",
                "A zombie encased in a thin layer of ice, its flesh pale and frostbitten. Its movements are stiff and slow, as if the cold has sapped what little life it had. Frost crystals cling to its tattered clothing, and its breath is visible as a faint mist of ice in the air. Its presence causes the air around it to grow colder, and its touch can freeze living tissue"

            Monster.TryCreate(MonsterType.Demon, MonsterSubType.None),
                "Hellish Scourge",
                "A towering demon with crimson skin, sharp horns, and glowing red eyes that seem to pierce through darkness. Its massive wings are tattered and dark, casting ominous shadows as it moves. It carries a jagged whip that crackles with dark energy, and its presence is accompanied by an overwhelming sense of dread and malevolence"
            
            Monster.TryCreate(MonsterType.Demon, MonsterSubType.Fire),
                "Infernal Pyreborn",
                "A hulking demon with molten, lava-like skin that flows like liquid fire. Its body is covered in glowing cracks that emit intense heat, and its eyes are like burning embers. It has large, bat-like wings that glow with a fiery aura, and its breath can ignite anything in its path. The air around it distorts from the heat, and its roar sounds like crackling flames."
            
            Monster.TryCreate(MonsterType.Demon, MonsterSubType.Frost),
                "Glacial Abomination",
                "A pale, towering demon with skin like polished ice, its body covered in jagged, frosty armor. Its eyes glow a chilling blue, and its breath forms frost in the air. Its massive wings are translucent and shimmer like ice, and its touch can freeze anything it grasps. The ground around it freezes over, and its presence is accompanied by an unnatural, biting cold."

            Monster.TryCreate(MonsterType.Necromancer, MonsterSubType.None),
                "Shadow Weaver",
                "A mysterious necromancer cloaked in dark, tattered robes, their face hidden beneath a hood. Their glowing, otherworldly eyes pierce through the shadows, and they wield a staff topped with a glowing orb of dark energy. The air around them is thick with the scent of decay, and the ground is littered with the bones of their fallen enemies. They move with an unnatural grace, as though death itself has given them power."
            
            Monster.TryCreate(MonsterType.Necromancer, MonsterSubType.Fire),
                "Pyromantic Sorcerer",
                "A necromancer who has mastered the forbidden art of fire and death. Their robes are a fiery red, and their eyes burn with an inner flame. They wield a staff that crackles with heat, its tip shaped like a flaming skull. The air around them seems to ripple with heat, and the ground beneath their feet is scorched and blackened. They are often surrounded by swirling flames and the faint whispers of restless spirits."
            
            Monster.TryCreate(MonsterType.Necromancer, MonsterSubType.Frost),
                "Cryomancer Lich",
                "A necromancer who has transcended mortality, becoming a lich imbued with the power of frost. Their body is encased in tattered, ice-encrusted robes, and their pale blue eyes glow with a cold, unnatural light. They wield a staff that freezes the ground beneath their feet, and the air around them is bitterly cold. The faint sound of whispers and the soft crunch of frost accompany their every move, and they are often surrounded by swirling clouds of icy mist."
        ]
        |> List.filter(fun (v,_,_) -> Option.isSome v)
        |> List.map(fun (m, name, desc) ->
            MonsterRecord(name, desc, m.Value, getStats(m.Value), 0UL))
    

