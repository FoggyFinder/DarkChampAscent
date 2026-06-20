module Utils

open System.Data
open GameLogic.Monsters
open System
open GameLogic.Monsters

let eps = 0.001M
let isCloseEnough (x:decimal) (y:decimal) = Math.Abs(x - y) < eps

let ofList (xs:Result<_,_> list) =
    if xs |> List.exists(fun r -> r.IsError) then
        None
    else
        xs |> List.map(function|Ok ok -> ok | _ -> failwith "invalid") |> Some

// https://stackoverflow.com/q/2983087
let getBytesData (reader : IDataReader) (i:int) = 
    let len = reader.GetBytes(i, int64 0, null, 0, 0);
    // Create a buffer to hold the bytes, and then
    // read the bytes from the DataTableReader.
    let buffer : byte array = Array.zeroCreate (int32 len)
    reader.GetBytes(i, int64 0, buffer, 0, int32 len) |> ignore
    buffer

[<RequireQualifiedAccess>]
module MonsterImg =
    let DefaultName(mtype:MonsterType, msubtype:MonsterSubType) =
        mtype.ToString().ToLower() + "_" + msubtype.ToString().ToLower() + ".png"
    let DefaultFile(monster:Monster) =
        let filename = DefaultName (monster.MType, monster.MSubType)
        System.IO.Path.Combine("Assets", filename) |> MonsterImg.File

[<RequireQualifiedAccess>]
module DefaultData =
    // dpmpp_2,; sgm_uniform
    let DefaultsMonsters =
        [
            Monster.TryCreate(MonsterType.Zombie, MonsterSubType.None),
                "Rotting Cadaver",
                "A reanimated corpse with decaying flesh, empty eyes, and tattered clothing. It moves slowly, driven solely by a primal urge to feed. Its body is covered in dirt, moss, and fungi, showing signs of prolonged decomposition. The stench of rot lingers around it, and its hollow moans echo as it shambles forward",
                MonsterImg.DefaultFile
            
            Monster.TryCreate(MonsterType.Zombie, MonsterSubType.Fire),
                "Inferno Corpse",
                "A zombie imbued with fiery magic, its body engulfed in small, flickering flames that seem to burn without consuming it. Its skin is charred and blackened, with glowing embers in its empty eye sockets. The flames on its body seem to grow stronger when it is agitated, and it leaves a trail of scorched ground in its wake",
                MonsterImg.DefaultFile
            
            Monster.TryCreate(MonsterType.Zombie, MonsterSubType.Frost),
                "Frozen Wretch",
                "A zombie encased in a thin layer of ice, its flesh pale and frostbitten. Its movements are stiff and slow, as if the cold has sapped what little life it had. Frost crystals cling to its tattered clothing, and its breath is visible as a faint mist of ice in the air. Its presence causes the air around it to grow colder, and its touch can freeze living tissue",
                MonsterImg.DefaultFile

            Monster.TryCreate(MonsterType.Demon, MonsterSubType.None),
                "Hellish Scourge",
                "A towering demon with crimson skin, sharp horns, and glowing red eyes that seem to pierce through darkness. Its massive wings are tattered and dark, casting ominous shadows as it moves. It carries a jagged whip that crackles with dark energy, and its presence is accompanied by an overwhelming sense of dread and malevolence",
                MonsterImg.DefaultFile
            
            Monster.TryCreate(MonsterType.Demon, MonsterSubType.Fire),
                "Infernal Pyreborn",
                "A hulking demon with molten, lava-like skin that flows like liquid fire. Its body is covered in glowing cracks that emit intense heat, and its eyes are like burning embers. It has large, bat-like wings that glow with a fiery aura, and its breath can ignite anything in its path. The air around it distorts from the heat, and its roar sounds like crackling flames.",
                MonsterImg.DefaultFile
            
            Monster.TryCreate(MonsterType.Demon, MonsterSubType.Frost),
                "Glacial Abomination",
                "A pale, towering demon with skin like polished ice, its body covered in jagged, frosty armor. Its eyes glow a chilling blue, and its breath forms frost in the air. Its massive wings are translucent and shimmer like ice, and its touch can freeze anything it grasps. The ground around it freezes over, and its presence is accompanied by an unnatural, biting cold.",
                MonsterImg.DefaultFile

            Monster.TryCreate(MonsterType.Necromancer, MonsterSubType.None),
                "Shadow Weaver",
                "A mysterious necromancer cloaked in dark, tattered robes, their face hidden beneath a hood. Their glowing, otherworldly eyes pierce through the shadows, and they wield a staff topped with a glowing orb of dark energy. The air around them is thick with the scent of decay, and the ground is littered with the bones of their fallen enemies. They move with an unnatural grace, as though death itself has given them power.",
                MonsterImg.DefaultFile
            
            Monster.TryCreate(MonsterType.Necromancer, MonsterSubType.Fire),
                "Pyromantic Sorcerer",
                "A necromancer who has mastered the forbidden art of fire and death. Their robes are a fiery red, and their eyes burn with an inner flame. They wield a staff that crackles with heat, its tip shaped like a flaming skull. The air around them seems to ripple with heat, and the ground beneath their feet is scorched and blackened. They are often surrounded by swirling flames and the faint whispers of restless spirits.",
                MonsterImg.DefaultFile
            
            Monster.TryCreate(MonsterType.Necromancer, MonsterSubType.Frost),
                "Cryomancer Lich",
                "A necromancer who has transcended mortality, becoming a lich imbued with the power of frost. Their body is encased in tattered, ice-encrusted robes, and their pale blue eyes glow with a cold, unnatural light. They wield a staff that freezes the ground beneath their feet, and the air around them is bitterly cold. The faint sound of whispers and the soft crunch of frost accompany their every move, and they are often surrounded by swirling clouds of icy mist.",
                MonsterImg.DefaultFile

            Monster.TryCreate(MonsterType.Universal, MonsterSubType.None),
                "MONSTR #1212",
                "MONSTRS. Only the Weird Survive. Mutate, collect, and embrace the chaos of Algorand’s strangest creatures.",
                fun _ -> MonsterImg.Ipfs "bafybeihouzpxueollwi7sqgl636zync7rcmkbjplbtgh2bz3y442flctca"
        ]
        |> List.filter(fun (v,_,_,_) -> Option.isSome v)
        |> List.map(fun (m, name, desc, getImg) ->
            MonsterRecord(name, desc, m.Value, Monster.getStats(m.Value), 0UL, getImg m.Value))

type IReadOnlySignal<'a> =
    abstract Value: 'a
    abstract Publish: IEvent<'a>

type Signal<'a when 'a: equality>(initial: 'a) =
    let mutable current = initial
    let locker = obj()
    let changed = Event<'a>()

    member _.Value = lock locker (fun _ -> current)
    member _.Publish = changed.Publish

    member _.Set(value: 'a) =
        lock locker (fun _ ->
            if current <> value then
                current <- value
                changed.Trigger value)

    interface IReadOnlySignal<'a> with
        member this.Value = this.Value
        member this.Publish = this.Publish
