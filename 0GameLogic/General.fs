namespace GameLogic.Champs

type Background =
    | Blood = 0
    | Aqua = 1
    | Noir = 2
    | Toxic = 3
    | Midnight = 4
    | Golden = 5
    | Forest = 6
    | Sunset = 7
    | Cosmos = 8
    | Dungeon = 9
    | Valley = 10
    | RedMoon = 11
    | Waves = 12
    | Unknown = 1000

type Skin =
    | Light = 0
    | Dark = 1
    | TribalLight = 2
    | TribalDark = 3
    | Undead = 4
    | Snake = 5
    | ElderDragon = 6
    | FireDragon = 7
    | Chameleon = 8
    | Unknown = 1000

type Weapon =
    | None = 0
    | Trident = 1
    | ExecutionerAxe = 2
    | Spear = 3
    | Shield = 4
    | DualKatana = 5
    | Sickle = 6
    | Scythe = 7
    | DragonLongSword = 8
    | DragonStaff = 9
    | DarkSword = 10
    | ElfBow = 11
    | Unknown = 1000

type Magic =
    | None = 0
    | Water = 1
    | Fire = 2
    | IceDaggers = 3
    | Lightning = 4
    | Dark = 5
    | Unknown = 1000

type Head =
    | Dragon = 0
    | Purity = 1
    | Snake = 2
    | UniHorn = 3
    | Elder = 4
    | Scarred = 5
    | DarkKnightHelm = 6
    | Undead = 7
    | Bone = 8
    | AllKnowing = 9
    | GladiatorHelm = 10
    | DragonKnightHelm = 11
    | CrownOfHorns = 12
    | Samurai = 13
    | Farmer = 14
    | SilverHermesHelm = 15
    | PirateBandana = 16
    | GoldHermesHelm = 17
    | Barbarian = 18
    | Unknown = 1000

type Armour =
    | None = 0
    | Rags = 1
    | HiddenOne = 2
    | Unchained = 3
    | MagiciansRobe = 4
    | Shinobi = 5
    | Pharaoh = 6
    | DragonHunter = 7
    | Gladiator = 8
    | DarkKnight = 9
    | DragonKnight = 10
    | ElfRobe = 11
    | ExecutionerRobe = 12
    | LeatherGarb = 13
    | PirateCoat = 14
    | Emperor = 15
    | Unknown = 1000

type Extra =
    | None = 0
    | CrescentMoonEarring = 1
    | FusionPearlEarring = 2
    | DragonFangsEarring = 3
    | TentacleEarring = 4
    | HoopEarring = 5
    | Unknown = 1000

[<RequireQualifiedAccess>]
type Trait =
    | Background
    | Skin
    | Weapon
    | Magic
    | Head
    | Armour
    | Extra

type Traits = {
    Background: Background
    Skin: Skin
    Weapon: Weapon
    Magic: Magic
    Head: Head
    Armour: Armour
    Extra: Extra
}

module Trait =
    let parseArmour str =
        match str with
        | "None" -> Armour.None
        | "Rags" -> Armour.Rags
        | "Hidden One" -> Armour.HiddenOne
        | "Unchained" -> Armour.Unchained
        | "Magicians Robe" -> Armour.MagiciansRobe
        | "Shinobi" -> Armour.Shinobi
        | "Pharaoh" -> Armour.Pharaoh
        | "Dragon Hunter Armour" -> Armour.DragonHunter
        | "Gladiator Armour" -> Armour.Gladiator
        | "Dark Knight Armour" -> Armour.DarkKnight
        | "Dragon Knight Armour" -> Armour.DragonKnight
        | "Elf Robe" -> Armour.ElfRobe
        | "Executioner Robe" -> Armour.ExecutionerRobe
        | "Leather Garb" -> Armour.LeatherGarb
        | "Pirate Coat" -> Armour.PirateCoat
        | "Emperor Armour" -> Armour.Emperor
        | _ -> Armour.Unknown

    let parseBackground str =
        match str with
        | "Blood Background" -> Background.Blood
        | "Aqua Background" -> Background.Aqua
        | "Noir Background" -> Background.Noir
        | "Toxic Background" -> Background.Toxic
        | "Midnight Background" -> Background.Midnight
        | "Golden Background" -> Background.Golden
        | "Forest Background" -> Background.Forest
        | "Sunset Background" -> Background.Sunset
        | "Cosmos Background" -> Background.Cosmos
        | "Dungeon Background" -> Background.Dungeon
        | "Valley Background" -> Background.Valley
        | "Red Moon Background" -> Background.RedMoon
        | "Waves Background" -> Background.Waves
        | _ -> Background.Unknown

    let parseExtra str =
        match str with
        | "None" -> Extra.None
        | "Crescent Moon Earring" -> Extra.CrescentMoonEarring 
        | "Fusion Pearl Earring" -> Extra.FusionPearlEarring
        | "Dragon Fangs Earring" -> Extra.DragonFangsEarring
        | "Tentacle Earring" -> Extra.TentacleEarring
        | "Hoop Earring" -> Extra.HoopEarring
        | _ -> Extra.Unknown

    let parseHead str =
        match str with
        | "Dragon" -> Head.Dragon
        | "Purity" -> Head.Purity
        | "Snake" -> Head.Snake
        | "Uni Horn" -> Head.UniHorn
        | "Elder" -> Head.Elder
        | "Scarred" -> Head.Scarred
        | "Dark Knight Helm" -> Head.DarkKnightHelm
        | "Undead" -> Head.Undead
        | "Bone" -> Head.Bone
        | "All Knowing" -> Head.AllKnowing
        | "Gladiator Helm" -> Head.GladiatorHelm
        | "Dragon Knight Helm" -> Head.DragonKnightHelm
        | "Crown of Horns" -> Head.CrownOfHorns
        | "Samurai" -> Head.Samurai
        | "Farmer" -> Head.Farmer
        | "Silver Hermes Helm" -> Head.SilverHermesHelm 
        | "Pirate Bandana" -> Head.PirateBandana
        | "Gold Hermes Helm" -> Head.GoldHermesHelm
        | "Barbarian" -> Head.Barbarian
        | _ -> Head.Unknown
        
    let parseMagic str =
        match str with
        | "None" -> Magic.None
        | "Water Magic" -> Magic.Water
        | "Fire Magic" -> Magic.Fire
        | "Ice Daggers" -> Magic.IceDaggers
        | "Lightning Magic" -> Magic.Lightning
        | "Dark Magic" -> Magic.Dark
        | _ -> Magic.Unknown

    let parseSkin str =
        match str with
        | "Light Skin" -> Skin.Light
        | "Dark Skin" -> Skin.Dark
        | "Tribal Light Skin" -> Skin.TribalLight
        | "Tribal Dark Skin" -> Skin.TribalDark
        | "Undead" -> Skin.Undead
        | "Snake" -> Skin.Snake
        | "Elder Dragon" -> Skin.ElderDragon
        | "Fire Dragon" -> Skin.FireDragon
        | "Chameleon" -> Skin.Chameleon
        | _ -> Skin.Unknown

    let parseWeapon str =
        match str with
        | "None" -> Weapon.None
        | "Trident" -> Weapon.Trident
        | "Executioner Axe" -> Weapon.ExecutionerAxe
        | "Spear" -> Weapon.Spear
        | "Shield" -> Weapon.Shield
        | "Dual Katana" -> Weapon.DualKatana
        | "Sickle" -> Weapon.Sickle
        | "Scythe" -> Weapon.Scythe
        | "Dragon Long Sword" -> Weapon.DragonLongSword
        | "Dragon Staff" -> Weapon.DragonStaff
        | "Dark Sword" -> Weapon.DarkSword
        | "Elf Bow" -> Weapon.ElfBow
        | _ -> Weapon.Unknown

(*              Health Magic Accuracy Luck Attack MAttack Defense MDefense
Background ->     -     +       -      +      -     -        +        -
      Skin ->     +     -       -      -      -     -        +        +
    Weapon ->     -     +       +      -      +     +        -        -
     Magic ->     -     +       -      -      -     +        -        +
      Head ->     +     -       +      -      +     +        -        -
    Armour ->     +     -       -      +      -     -        +        +
     Extra ->     -     -       +      +      +     -        -        -
*)

[<RequireQualifiedAccess>]
type Characteristic =
    | Health = 0
    | Magic = 1

    | Accuracy = 2 // increase chance to take damage
    | Luck = 3 // increase chance to hit critical
    
    | Attack = 4 // increase attack
    | MagicAttack = 5 // increase magic attack

    | Defense = 6 // increases defense
    | MagicDefense = 7 // increases defense from magic attack 

module TraitCharacteristic =
    let impact =
        [
            Trait.Background, [ Characteristic.Magic; Characteristic.Luck; Characteristic.Defense ]
            Trait.Skin, [ Characteristic.Health; Characteristic.Defense; Characteristic.MagicDefense ]
            Trait.Weapon, [ Characteristic.Magic; Characteristic.Accuracy; Characteristic.Attack; Characteristic.MagicAttack ]
            Trait.Magic, [ Characteristic.Magic; Characteristic.MagicAttack; Characteristic.MagicDefense ]
            Trait.Head, [ Characteristic.Health; Characteristic.Accuracy; Characteristic.Attack; Characteristic.MagicAttack ]
            Trait.Armour, [ Characteristic.Health; Characteristic.Luck; Characteristic.Defense; Characteristic.MagicDefense ]
            Trait.Extra, [ Characteristic.Accuracy; Characteristic.Luck; Characteristic.Attack ]
        ]
        |> Map.ofList

// Add validation
type Stat = {
    Health: int64
    Magic: int64

    Accuracy: int64
    Luck: int64

    Attack: int64
    MagicAttack: int64

    Defense: int64
    MagicDefense: int64
} with
    member s.IsAlive = s.Health > 0L
    static member Zero =
        {
            Health = 0L
            Magic = 0L
            Accuracy = 0L
            Luck = 0L
            Attack = 0L
            MagicAttack = 0L
            Defense = 0L
            MagicDefense = 0L           
        }
    static member One =
        {
            Health = 1L
            Magic = 1L
            Accuracy = 1L
            Luck = 1L
            Attack = 1L
            MagicAttack = 1L
            Defense = 1L
            MagicDefense = 1L           
        }
    static member (+) (x:Stat, y:Stat) =
        { x with
            Health = x.Health + y.Health
            Magic = x.Magic + y.Magic
            Accuracy = x.Accuracy + y.Accuracy
            Luck = x.Luck + y.Luck
            Attack = x.Attack + y.Attack
            MagicAttack = x.MagicAttack + y.MagicAttack
            Defense = x.Defense + y.Defense
            MagicDefense = x.MagicDefense + y.MagicDefense            
        }

    static member (-) (x:Stat, y:Stat) =
        { x with
            Health = x.Health - y.Health
            Magic = x.Magic - y.Magic
            Accuracy = x.Accuracy - y.Accuracy
            Luck = x.Luck - y.Luck
            Attack = x.Attack - y.Attack
            MagicAttack = x.MagicAttack - y.MagicAttack
            Defense = x.Defense - y.Defense
            MagicDefense = x.MagicDefense - y.MagicDefense            
        }

[<RequireQualifiedAccess>]
module Constants = 
    let [<Literal>] RoundsInBattle = 48

[<RequireQualifiedAccess>]
module Levels =
    open System.Collections.Generic

    let [<Literal>] XPPerLvl = 100UL
    
    let statFromCharacteristics(lvls:IDictionary<Characteristic, int>) =
        {
            Health = 10L * (Utils.getValueOrDefault lvls 0 Characteristic.Health |> int64)
            Magic = 5L * (Utils.getValueOrDefault lvls 0 Characteristic.Magic |> int64)
            Accuracy = Utils.getValueOrDefault lvls 0 Characteristic.Accuracy |> int64
            Luck = Utils.getValueOrDefault lvls 0 Characteristic.Luck |> int64
            Attack = Utils.getValueOrDefault lvls 0 Characteristic.Attack |> int64
            MagicAttack = Utils.getValueOrDefault lvls 0 Characteristic.MagicAttack |> int64
            Defense = Utils.getValueOrDefault lvls 0 Characteristic.Defense |> int64
            MagicDefense = Utils.getValueOrDefault lvls 0 Characteristic.MagicDefense |> int64
        }

    let statFromCharacteristicSeq(lvls:Characteristic seq) =
        lvls |> Seq.countBy id |> dict |> statFromCharacteristics

    let getLvlByXp (xp:uint64) =
        xp / XPPerLvl

module Champ =
    let fromBackground(background:Background) : Stat =
        let magic =
            match background with
            | Background.Blood -> 3L
            | Background.Aqua -> 3L
            | Background.Noir -> 2L
            | Background.Toxic -> 2L
            | Background.Midnight -> 4L
            | Background.Golden -> 3L
            | Background.Forest -> 4L
            | Background.Sunset -> 4L
            | Background.Cosmos -> 3L
            | Background.Dungeon -> 3L
            | Background.Valley -> 3L
            | Background.RedMoon -> 5L
            | Background.Waves -> 4L
            | _ -> 1L
        
        let luck =
            match background with
            | Background.Blood
            | Background.Aqua
            | Background.Noir
            | Background.Toxic
            | Background.Midnight -> 1L
            | Background.Golden
            | Background.Forest -> 2L
            | Background.Sunset -> 1L
            | Background.Cosmos -> 3L
            | Background.Dungeon -> 2L
            | Background.Valley -> 3L
            | Background.RedMoon -> 2L
            | Background.Waves -> 2L
            | _ -> 1L

        let defense =
            match background with
            | Background.Blood -> 2L
            | Background.Aqua -> 2L
            | Background.Noir -> 3L
            | Background.Toxic -> 3L
            | Background.Midnight -> 1L
            | Background.Golden -> 2L
            | Background.Forest -> 1L
            | Background.Sunset -> 4L
            | Background.Cosmos -> 1L
            | Background.Dungeon -> 3L
            | Background.Valley -> 2L
            | Background.RedMoon -> 1L
            | Background.Waves -> 3L
            | _ -> 1L

        {
            Stat.Zero with
                Magic = magic + 10L
                Luck = luck + 1L
                Defense = defense + 1L
        }
    
    let fromSkin(skin:Skin) : Stat  =
        let health =
            match skin with
            | Skin.Light
            | Skin.Dark -> 20L
            | Skin.TribalLight
            | Skin.TribalDark -> 25L
            | Skin.Undead
            | Skin.Snake
            | Skin.ElderDragon
            | Skin.FireDragon
            | Skin.Chameleon -> 30L
            | _ -> 1L

        let defense =
            match skin with
            | Skin.Light -> 1L
            | Skin.Dark -> 2L
            | Skin.TribalLight -> 1L
            | Skin.TribalDark -> 2L
            | Skin.Undead
            | Skin.Snake -> 2L
            | Skin.ElderDragon -> 3L
            | Skin.FireDragon -> 2L
            | Skin.Chameleon -> 3L
            | _ -> 1L

        let mdefense =
            match skin with
            | Skin.Light -> 2L
            | Skin.Dark -> 1L
            | Skin.TribalLight -> 2L
            | Skin.TribalDark -> 1L
            | Skin.Undead
            | Skin.Snake
            | Skin.ElderDragon -> 2L
            | Skin.FireDragon
            | Skin.Chameleon -> 3L
            | _ -> 1L

        {
            Stat.Zero with
                Health = health + 10L
                Defense = defense + 1L
                MagicDefense = mdefense + 1L
        }
    
    let fromWeapon(weapon:Weapon) : Stat =
        let magic =
            match weapon with
            | Weapon.None -> 0L
            | Weapon.Trident -> 1L
            | Weapon.ExecutionerAxe -> 1L
            | Weapon.Spear -> 1L
            | Weapon.Shield -> 2L
            | Weapon.DualKatana -> 1L
            | Weapon.Sickle -> 2L
            | Weapon.Scythe -> 2L
            | Weapon.DragonLongSword -> 2L
            | Weapon.DragonStaff -> 3L
            | Weapon.DarkSword -> 3L
            | Weapon.ElfBow -> 3L
            | _ -> 1L

        let accuracy =
            match weapon with
            | Weapon.None -> 0L
            | Weapon.Trident -> 2L
            | Weapon.ExecutionerAxe -> 2L
            | Weapon.Spear -> 2L
            | Weapon.Shield -> 3L
            | Weapon.DualKatana -> 2L
            | Weapon.Sickle -> 1L
            | Weapon.Scythe -> 2L
            | Weapon.DragonLongSword -> 3L
            | Weapon.DragonStaff -> 3L
            | Weapon.DarkSword -> 2L
            | Weapon.ElfBow -> 5L
            | _ -> 1L

        let attack =
            match weapon with
            | Weapon.None -> 0L
            | Weapon.Trident -> 2L
            | Weapon.ExecutionerAxe -> 2L
            | Weapon.Spear -> 2L
            | Weapon.Shield -> 1L
            | Weapon.DualKatana -> 3L
            | Weapon.Sickle -> 2L
            | Weapon.Scythe -> 2L
            | Weapon.DragonLongSword -> 2L
            | Weapon.DragonStaff -> 2L
            | Weapon.DarkSword -> 5L
            | Weapon.ElfBow -> 3L
            | _ -> 1L

        let mattack =
            match weapon with
            | Weapon.None -> 0L
            | Weapon.Trident -> 2L
            | Weapon.ExecutionerAxe -> 2L
            | Weapon.Spear -> 2L
            | Weapon.Shield -> 1L
            | Weapon.DualKatana -> 2L
            | Weapon.Sickle -> 3L
            | Weapon.Scythe -> 2L
            | Weapon.DragonLongSword -> 2L
            | Weapon.DragonStaff -> 3L
            | Weapon.DarkSword -> 4L
            | Weapon.ElfBow -> 0L
            | _ -> 1L

        {
            Stat.Zero with
                Magic = magic + 10L
                Accuracy = accuracy + 1L
                Attack = attack + 1L
                MagicAttack = mattack + 1L
        }
    
    let fromHead(head:Head) : Stat =
        let health =
            match head with
            | Head.Dragon -> 15L
            | Head.Purity
            | Head.Snake
            | Head.UniHorn
            | Head.Elder -> 20L
            | Head.Scarred
            | Head.DarkKnightHelm
            | Head.Undead
            | Head.Bone -> 22L
            | Head.AllKnowing
            | Head.GladiatorHelm
            | Head.DragonKnightHelm -> 25L
            | Head.CrownOfHorns
            | Head.Samurai
            | Head.Farmer
            | Head.SilverHermesHelm
            | Head.PirateBandana
            | Head.GoldHermesHelm -> 30L
            | Head.Barbarian -> 35L
            | _ -> 1L

        let accuracy =
            match head with
            | Head.Dragon
            | Head.Purity
            | Head.Snake -> 1L
            | Head.UniHorn -> 2L
            | Head.Elder
            | Head.Scarred -> 1L
            | Head.DarkKnightHelm
            | Head.Undead -> 2L
            | Head.Bone
            | Head.AllKnowing -> 3L
            | Head.GladiatorHelm -> 2L
            | Head.DragonKnightHelm
            | Head.CrownOfHorns -> 3L
            | Head.Samurai -> 4L
            | Head.Farmer -> 2L
            | Head.SilverHermesHelm -> 3L
            | Head.PirateBandana -> 2L
            | Head.GoldHermesHelm -> 4L
            | Head.Barbarian -> 3L
            | _ -> 1L

        let attack =
            match head with
            | Head.Dragon -> 2L
            | Head.Purity -> 1L
            | Head.Snake -> 2L
            | Head.UniHorn -> 1L
            | Head.Elder -> 2L
            | Head.Scarred -> 1L
            | Head.DarkKnightHelm -> 2L
            | Head.Undead -> 1L
            | Head.Bone -> 1L
            | Head.AllKnowing -> 1L
            | Head.GladiatorHelm -> 3L
            | Head.DragonKnightHelm -> 3L
            | Head.CrownOfHorns -> 2L
            | Head.Samurai -> 3L
            | Head.Farmer -> 3L
            | Head.SilverHermesHelm -> 3L
            | Head.PirateBandana -> 3L
            | Head.GoldHermesHelm -> 4L
            | Head.Barbarian -> 5L
            | _ -> 1L

        let mattack =
            match head with
            | Head.Dragon -> 2L
            | Head.Purity -> 2L
            | Head.Snake -> 1L
            | Head.UniHorn -> 1L
            | Head.Elder -> 2L
            | Head.Scarred -> 1L
            | Head.DarkKnightHelm -> 1L
            | Head.Undead -> 2L
            | Head.Bone -> 1L
            | Head.AllKnowing -> 2L
            | Head.GladiatorHelm -> 1L
            | Head.DragonKnightHelm -> 2L
            | Head.CrownOfHorns -> 2L
            | Head.Samurai -> 1L
            | Head.Farmer -> 3L
            | Head.SilverHermesHelm -> 4L
            | Head.PirateBandana -> 3L
            | Head.GoldHermesHelm -> 4L
            | Head.Barbarian -> 4L
            | _ -> 1L

        {
            Stat.Zero with
                Health = health + 10L
                Accuracy = accuracy + 1L
                Attack = attack + 1L
                MagicAttack = mattack + 1L
        }
    
    let fromArmour(armour:Armour) : Stat =
        let health =
            match armour with
            | Armour.None -> 15L
            | Armour.Rags
            | Armour.HiddenOne
            | Armour.Unchained -> 20L
            | Armour.MagiciansRobe
            | Armour.Shinobi
            | Armour.Pharaoh
            | Armour.DragonHunter -> 22L
            | Armour.Gladiator
            | Armour.DarkKnight
            | Armour.DragonKnight
            | Armour.ElfRobe -> 25L
            | Armour.ExecutionerRobe -> 27L
            | Armour.LeatherGarb -> 25L
            | Armour.PirateCoat -> 25L
            | Armour.Emperor -> 30L
            | _ -> 1L

        let luck =
            match armour with
            | Armour.None -> 0L
            | Armour.Rags
            | Armour.HiddenOne
            | Armour.Unchained
            | Armour.MagiciansRobe
            | Armour.Shinobi
            | Armour.Pharaoh
            | Armour.DragonHunter -> 1L
            | Armour.Gladiator
            | Armour.DarkKnight
            | Armour.DragonKnight -> 2L
            | Armour.ElfRobe -> 3L
            | Armour.ExecutionerRobe -> 2L
            | Armour.LeatherGarb
            | Armour.PirateCoat
            | Armour.Emperor -> 3L
            | _ -> 1L

        let defense =
            match armour with
            | Armour.None -> 0L
            | Armour.Rags -> 1L
            | Armour.HiddenOne -> 2L
            | Armour.Unchained -> 1L
            | Armour.MagiciansRobe -> 2L
            | Armour.Shinobi -> 1L
            | Armour.Pharaoh -> 2L
            | Armour.DragonHunter -> 1L
            | Armour.Gladiator -> 1L
            | Armour.DarkKnight -> 2L
            | Armour.DragonKnight -> 1L
            | Armour.ElfRobe -> 1L
            | Armour.ExecutionerRobe -> 2L
            | Armour.LeatherGarb -> 2L
            | Armour.PirateCoat -> 2L
            | Armour.Emperor -> 2L
            | _ -> 1L

        let magicDefense =
            match armour with
            | Armour.None -> 0L
            | Armour.Rags -> 1L
            | Armour.HiddenOne -> 1L
            | Armour.Unchained -> 2L
            | Armour.MagiciansRobe -> 2L
            | Armour.Shinobi -> 1L
            | Armour.Pharaoh -> 1L
            | Armour.DragonHunter -> 2L
            | Armour.Gladiator -> 1L
            | Armour.DarkKnight -> 1L
            | Armour.DragonKnight -> 2L
            | Armour.ElfRobe -> 2L
            | Armour.ExecutionerRobe -> 2L
            | Armour.LeatherGarb -> 1L
            | Armour.PirateCoat -> 2L
            | Armour.Emperor -> 2L    
            | _ -> 1L

        {
           Stat.Zero with
             Health = health + 10L
             Luck = luck + 1L
             Defense = defense + 1L
             MagicDefense = magicDefense + 1L
        }
    
    let fromExtra(extra:Extra) : Stat =
        let accuracy =
            match extra with
            | Extra.None -> 0L
            | Extra.CrescentMoonEarring -> 2L
            | Extra.FusionPearlEarring -> 1L
            | Extra.DragonFangsEarring -> 1L
            | Extra.TentacleEarring -> 2L
            | Extra.HoopEarring -> 2L
            | _ -> 1L

        let luck =
            match extra with
            | Extra.None -> 0L
            | Extra.CrescentMoonEarring -> 1L
            | Extra.FusionPearlEarring -> 2L 
            | Extra.DragonFangsEarring -> 1L
            | Extra.TentacleEarring -> 2L
            | Extra.HoopEarring -> 2L
            | _ -> 1L  
            
        let attack =
            match extra with
            | Extra.None -> 0L
            | Extra.CrescentMoonEarring -> 1L
            | Extra.FusionPearlEarring -> 1L 
            | Extra.DragonFangsEarring -> 2L
            | Extra.TentacleEarring -> 2L
            | Extra.HoopEarring -> 3L
            | _ -> 1L 

        { Stat.Zero with
            Accuracy = accuracy + 1L
            Luck = luck + 1L
            Attack = attack + 1L
        }

    let fromMagic(magic:Magic) : Stat =
        let m' =
            match magic with
            | Magic.None -> 0L
            | Magic.Water -> 20L
            | Magic.Fire -> 20L
            | Magic.IceDaggers -> 20L
            | Magic.Lightning -> 20L
            | Magic.Dark -> 20L
            | _ -> 1L

        let mattack =
            match magic with
            | Magic.None -> 0L
            | Magic.Water -> 5L
            | Magic.Fire -> 5L
            | Magic.IceDaggers -> 5L
            | Magic.Lightning -> 7L
            | Magic.Dark -> 10L
            | _ -> 1L

        let mdefense =
            match magic with
            | Magic.None -> 0L
            | Magic.Water -> 10L
            | Magic.Fire -> 10L
            | Magic.IceDaggers -> 10L
            | Magic.Lightning -> 7L
            | Magic.Dark -> 5L
            | _ -> 1L

        { Stat.Zero with
           Magic = m' + 10L
           MagicAttack = mattack + 1L
           MagicDefense = mdefense + 1L
           }

    let generateStats (trts:Traits) =
        [
            fromBackground trts.Background
            fromSkin trts.Skin
            fromWeapon trts.Weapon
            fromHead trts.Head
            fromArmour trts.Armour
            fromExtra trts.Extra
            fromMagic trts.Magic
        ] |> List.sum

type Champ = {
    Id: int64
    Stats: Stat
}

type ChampInfo = {
    ID: uint64
    Name : string
    Ipfs : string
    Balance : decimal
    XP : uint64

    Stat: Stat
    Traits: Traits
    BoostStat: Stat option
    LevelsStat: Stat option
    LeveledChars: uint64
}

[<RequireQualifiedAccess>]
type Move =
    /// attack -> takes damage
    | Attack = 0
    /// magic attack -> takes damage but takes magic health as well
    | MagicAttack = 1
    /// shield -> increase defense for 1 round
    | Shield = 2
    /// shield -> increase magic defense for 1 round but takes magic health as well
    | MagicShield = 3
    /// heal -> increase health but takes magic health 
    | Heal = 4
    /// meditate -> increase magic health
    | Meditate = 5

[<RequireQualifiedAccess>]
type Dmg =
    | Missed
    | Default of uint64
    | Critical of uint64
    member d.Value =
        match d with
        | Missed -> 0UL
        | Default v
        | Critical v -> v
    member d.WithValue (v:uint64) =
        match d with
        | Missed -> Dmg.Missed
        | Default _ -> Dmg.Default v
        | Critical _ -> Dmg.Critical v

[<RequireQualifiedAccess>]
type PerformedMove =
    | Attack of Dmg
    | MagicAttack of Dmg * uint64
    | Shield of uint64
    | MagicShield of uint64 * uint64
    | Heal of uint64 * uint64
    | Meditate of uint64
    member t.Dmg =
        match t with
        | Attack d
        | MagicAttack (d, _) -> Some d
        | Shield _
        | MagicShield _
        | Heal _
        | Meditate _ -> None
    member t.Move =
        match t with
        | Attack _ -> Move.Attack
        | MagicAttack _ -> Move.MagicAttack
        | Shield _ -> Move.Shield
        | MagicShield _ -> Move.MagicShield
        | Heal _ -> Move.Heal
        | Meditate _ -> Move.Meditate