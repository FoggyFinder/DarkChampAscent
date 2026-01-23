module Display

open System      
open GameLogic.Shop
open GameLogic.Effects
open GameLogic.Champs

type ShopItemRow(item:ShopItem, dcprice: decimal) =
    member _.Item = item
    member _.Price = Math.Round(Shop.getPrice item / dcprice, 6)
    member _.Duration = Shop.getRoundDuration item
    member _.Description = Shop.getDescription item
    member _.Kind = Shop.getKind item
    member _.Target = Shop.getTarget item

type EffectItemRow(effect:Effect) =
    member _.Item = effect
    member _.Duration = Effects.getRoundDuration(effect)
    member _.Description = Effects.getDescription effect

module Emoj =
    let [<Literal>] Coin = "<:darkcoin:1403078833648832583>"
    let [<Literal>] Rocket = ":rocket:"
    let [<Literal>] TaDa = ":tada:"

    let [<Literal>] Gem = ":gem:"
    let [<Literal>] Critical = ":zap:"

    let [<Literal>] Rounds = ":hourglass:"

    let [<Literal>] Trophy = ":trophy:"
    let [<Literal>] Level = ":star:"

    let [<Literal>] Health = ":herb:"
    let [<Literal>] Magic = ":test_tube:"

    let [<Literal>] Luck = ":game_die:"
    let [<Literal>] Accuracy = ":dart:"

    let [<Literal>] Attack = ":dagger:"
    let [<Literal>] MagicAttack = ":magic_wand:"

    let [<Literal>] Shield = ":shield:"
    let [<Literal>] MagicShield = "<:magic_shield:1403111440302215281>"

    let [<Literal>] Background = ":white_medium_square:"
    let [<Literal>] Skin = ":coat:"
    let [<Literal>] Weapon = ":bow_and_arrow:"
    let [<Literal>] Head = ":military_helmet:"
    let [<Literal>] Armour = ":shield:"
    let [<Literal>] Extra = ":package:"

    let [<Literal>] Yes = ":white_check_mark:"
    let [<Literal>] No = ":x:"

    let [<Literal>] Fire = ":fire:"
    let [<Literal>] Frost = ":snowflake:"

    let [<Literal>] ElixirOfLife = "<:Elixir_of_life:1401550961138208931>"
    let [<Literal>] ElixirOfMagic = "<:Elixir_of_magic:1401560956945039421>"

    let [<Literal>] ElixirOfLuck = "<:Elixir_of_luck:1401560993590411344>"
    let [<Literal>] ElixirOfAccuracy = "<:Elixir_of_accuracy:1401561036816908360>"

    let [<Literal>] ElixirOfDamage = "<:Elixir_of_damage:1401561080718950521>"
    let [<Literal>] ElixirOfMagicalDamage = "<:Elixir_of_magical_damage:1401561104240611512>"

    let [<Literal>] ElixirOfDefense = "<:Elixir_of_defense:1401561142970679336>"
    let [<Literal>] ElixirOfMagicalDefense = "<:Elixir_of_magical_defense:1401561170460278815>"

module WebEmoji =
    let [<Literal>] Gem = "💎"
    let [<Literal>] Level = "⭐"
    let [<Literal>] Health = "🌿"
    let [<Literal>] Magic = "🧪"

    let [<Literal>] Luck = "🎲"
    let [<Literal>] Accuracy = "🎯"

    let [<Literal>] Attack = "🗡️"
    let [<Literal>] MagicAttack = "🌀"

    let [<Literal>] Shield = "🛡️"
    let [<Literal>] MagicShield = "💠"

    let [<Literal>] Background = "◻️"
    let [<Literal>] Skin = "🧥"
    let [<Literal>] Weapon = "🏹"
    let [<Literal>] Head = "🎩"
    let [<Literal>] Armour = "🛡️"
    let [<Literal>] Extra = "📦"
    let [<Literal>] Rounds = "⌛"

    let [<Literal>] Home = "🏠"
    let [<Literal>] Account = "👤"
    let [<Literal>] MyStorage = "📦"
    let [<Literal>] Champs = "🛡️"
    let [<Literal>] ChampsUnderEffects = "✨"
    let [<Literal>] Monsters = "👾"
    let [<Literal>] MyRequests = "✉️"

    let [<Literal>] LogIn = "🔐"
    let [<Literal>] Battle = "⚔️"
    let [<Literal>] Shop = "🛒"
    let [<Literal>] MonstersUnderEffects = "✨"

    let [<Literal>] TopDonaters = "💝"
    let [<Literal>] TopUnknownDonaters = "💳"

    let [<Literal>] FAQ = "❓"
    let [<Literal>] SourceCode = "🔗"
    // TODO: replace with discord icon: https://www.w3schools.com/icons/tryit.asp?icon=fab_fa-discord&unicon=f392
    let [<Literal>] Discord = "💡"
    let [<Literal>] USDC = "💲"
    let [<Literal>] Toggle = "☰"
    let [<Literal>] Leaderboard = "🏆"
    let [<Literal>] GameAccountOptions = "🎮"
    let [<Literal>] About = "ℹ️"
    let [<Literal>] Stats = "📊"
    let [<Literal>] CheckMark = "✔️"
    let [<Literal>] DarkCoin = """<img class="inline-coin" src="DC.png" alt="DarkCoin" />"""
    let [<Literal>] MoneyBag = "💰"
    let [<Literal>] Fire = "🔥"
    let [<Literal>] DAO = "🏛️"
    let [<Literal>] Dev = "💻"
    let [<Literal>] Staking = "♻️"
    let [<Literal>] Reserve = "🔑"
    let [<Literal>] History = "📜"
    let [<Literal>] Progress = "⟳"

let webEmojiFromChar (ch:Characteristic) =
    match ch with
    | Characteristic.Health -> WebEmoji.Health
    | Characteristic.Magic -> WebEmoji.Magic
    | Characteristic.Luck -> WebEmoji.Luck
    | Characteristic.Accuracy -> WebEmoji.Accuracy
    | Characteristic.Attack -> WebEmoji.Attack
    | Characteristic.MagicAttack -> WebEmoji.MagicAttack
    | Characteristic.Defense -> WebEmoji.Shield
    | Characteristic.MagicDefense -> WebEmoji.MagicShield

let fromStat (s:Stat) =
    $""" {Emoj.Health} {s.Health} | {Emoj.Magic} {s.Magic} | {Emoj.Attack} {s.Attack} | {Emoj.MagicAttack} {s.MagicAttack} | {Emoj.Luck} {s.Luck} | {Emoj.Accuracy} {s.Accuracy} | {Emoj.Shield} {s.Defense} | {Emoj.MagicShield} {s.MagicDefense}"""

let fromBool (b:bool) =
    if b then Emoj.Yes else Emoj.No

let fromShopItem (item:ShopItem) =
    match item with
    | ShopItem.ElixirOfLife -> $"{Emoj.ElixirOfLife} {item} {Emoj.ElixirOfLife}"
    | ShopItem.ElixirOfMagic -> $"{Emoj.ElixirOfMagic} {item} {Emoj.ElixirOfMagic}"
    | ShopItem.ElixirOfLuck -> $"{Emoj.ElixirOfLuck} {item} {Emoj.ElixirOfLuck}"
    | ShopItem.ElixirOfAccuracy -> $"{Emoj.ElixirOfAccuracy} {item} {Emoj.ElixirOfAccuracy}"
    | ShopItem.ElixirOfDamage -> $"{Emoj.ElixirOfDamage} {item} {Emoj.ElixirOfDamage}"
    | ShopItem.ElixirOfMagicalDamage -> $"{Emoj.ElixirOfMagicalDamage} {item} {Emoj.ElixirOfMagicalDamage}"
    | ShopItem.ElixirOfDefense -> $"{Emoj.ElixirOfDefense} {item} {Emoj.ElixirOfDefense}"
    | ShopItem.ElixirOfMagicalDefense -> $"{Emoj.ElixirOfMagicalDefense} {item} {Emoj.ElixirOfMagicalDefense}"
    | _ -> item.ToString()

let toRound2StrF(f:float) = $"{Math.Round(f, 2)}"
let toRound2StrD(d:decimal) = $"{Math.Round(d, 2)}"
let toRound6StrD(d:decimal) = $"{Math.Round(d, 6)}"

let toMDTable(t:Traits) = $"""
    |Trait|Value|
    |----|-----|
    |{nameof Background}|{t.Background}|
    |{nameof Skin}|{t.Skin}|
    |{nameof Weapon}|{t.Weapon}|
    |{nameof Magic}|{t.Magic}|
    |{nameof Head}|{t.Head}|
    |{nameof Armour}|{t.Armour}|
    |{nameof Extra}|{t.Extra}|
"""

let balance(balance:decimal) =
    $"{Emoj.Coin} {toRound2StrD balance} DarkCoins"

let xp(xp:uint64) =
    $"{Emoj.Gem} {xp} xp"

let format (emoj:string) (name:string) (v:int64) = $"{emoj} {name}: {v}"

let health = format Emoj.Health (nameof(Emoj.Health))
let magic = nameof Emoj.Magic |> format Emoj.Magic

let toTable2(s:Stat) =
    [
        format Emoj.Luck (nameof s.Luck) s.Luck
        format Emoj.Accuracy (nameof s.Accuracy) s.Accuracy
        
        format Emoj.Attack (nameof s.Attack) s.Attack
        format Emoj.MagicAttack (nameof s.MagicAttack) s.MagicAttack
        format Emoj.Shield (nameof s.Defense) s.Defense
        format Emoj.MagicShield (nameof s.MagicDefense) s.MagicDefense
    ]

type FullStat(s:Stat, bs:Stat, ls:Stat) =
    let a1, a2 = 
        if bs <> Stat.Zero && ls <> Stat.Zero then
            "(*)", "(**)"
        elif bs <> Stat.Zero then
            "(*)", ""
        elif ls <> Stat.Zero then
            "", "(*)"
        else
            "", ""
    let format2 (emoj:string) (name:string) (v:string) = $"{emoj} {name}: {v}"

    let property getValue =
        let c, b, l = getValue s, getValue bs, getValue ls 
        let boosted = if b > 0L then $" +`{b}` {a1} " else ""
        let lvled = if l > 0L then $" +`{l}` {a2}" else ""
        $"{c}{boosted}{lvled}"   
    
    member _.GetValue (ch:Characteristic) =
        match ch with
        | Characteristic.Health -> (fun s -> s.Health)
        | Characteristic.Magic -> (fun s -> s.Magic)
        | Characteristic.Luck -> (fun s -> s.Luck)
        | Characteristic.Accuracy -> (fun s -> s.Accuracy)
        | Characteristic.Attack -> (fun s -> s.Attack)
        | Characteristic.MagicAttack -> (fun s -> s.MagicAttack)
        | Characteristic.Defense -> (fun s -> s.Defense)
        | Characteristic.MagicDefense -> (fun s -> s.MagicDefense)
        |> property

    member x.Health = x.GetValue Characteristic.Health |> format2 Emoj.Health (nameof s.Health)
    member x.Magic = x.GetValue Characteristic.Magic |> format2 Emoj.Magic (nameof s.Magic)

    member _.WithoutHM =
        [
            (fun s -> s.Luck) |> property |> format2 Emoj.Luck (nameof s.Luck)
            (fun s -> s.Accuracy) |> property |> format2 Emoj.Accuracy (nameof s.Accuracy)
        
            (fun s -> s.Attack) |> property |> format2 Emoj.Attack (nameof s.Attack)
            (fun s -> s.MagicAttack) |> property |> format2 Emoj.MagicAttack (nameof s.MagicAttack)
            (fun s -> s.Defense) |> property |> format2 Emoj.Shield (nameof s.Defense)
            (fun s -> s.MagicDefense) |> property |> format2 Emoj.MagicShield (nameof s.MagicDefense)
        ]

open GameLogic.Monsters
let fromSubType(st:MonsterSubType) =
    match st with
    | MonsterSubType.None -> ""
    | MonsterSubType.Fire -> Emoj.Fire
    | MonsterSubType.Frost -> Emoj.Frost

let fromMonster(mt:MonsterType, mst:MonsterSubType) =
    let st = fromSubType mst
    $"{st} {mt} {st}"

let monsterClass(mt:MonsterType, mst:MonsterSubType) =
    let subType = match mst with | MonsterSubType.None -> "" | _ -> $"({mst})"
    $"{mt} {subType}"

let fullMonsterName(name:string, mt:MonsterType, mst:MonsterSubType) =
    $"**{name}** - {monsterClass (mt, mst)}"

open System.Text.RegularExpressions

let splitCamel (s:string) =
    let pattern = @"(?<!^)([A-Z])"
    Regex.Replace(s, pattern, " $1")

let withPlusOrEmpty (v:int64) =
    if v = 0L then ""
    else $"+ {v}"

let getTraitInfo(t:Trait, traits:Traits) =
    let chs = TraitCharacteristic.impact.[t]
    let name, icon, stat, v =
        match t with
        | Trait.Background ->
            nameof traits.Background, Emoj.Background, Champ.fromBackground traits.Background, string traits.Background |> splitCamel
        | Trait.Skin ->
            nameof traits.Skin, Emoj.Skin, Champ.fromSkin traits.Skin, string traits.Skin |> splitCamel
        | Trait.Weapon ->
            nameof traits.Weapon, Emoj.Weapon, Champ.fromWeapon traits.Weapon, string traits.Weapon |> splitCamel
        | Trait.Magic ->
            nameof traits.Magic, Emoj.Magic, Champ.fromMagic traits.Magic, string traits.Magic |> splitCamel
        | Trait.Head ->
            nameof traits.Head, Emoj.Head, Champ.fromHead traits.Head, string traits.Head |> splitCamel
        | Trait.Armour ->
            nameof traits.Armour, Emoj.Armour, Champ.fromArmour traits.Armour, string traits.Armour |> splitCamel
        | Trait.Extra ->
            nameof traits.Extra, Emoj.Extra, Champ.fromExtra traits.Extra, string traits.Extra |> splitCamel

    let str =
        chs |> List.map(fun ch ->
            match ch with
            | Characteristic.Health -> $" +{stat.Health} {Emoj.Health}"
            | Characteristic.Magic -> $" +{stat.Magic} {Emoj.Magic}"
            | Characteristic.Accuracy -> $" +{stat.Accuracy} {Emoj.Accuracy}"
            | Characteristic.Luck -> $" +{stat.Luck} {Emoj.Luck}"
            | Characteristic.Attack -> $" +{stat.Attack} {Emoj.Attack}"
            | Characteristic.MagicAttack -> $" +{stat.MagicAttack} {Emoj.MagicAttack}"
            | Characteristic.Defense -> $" +{stat.Defense} {Emoj.Shield}"
            | Characteristic.MagicDefense  -> $" +{stat.MagicDefense} {Emoj.MagicShield}"
        ) |> String.concat ";"

    $"{icon} {name} : {v} [{str}]"

let private performedMove (isDiscord:bool) (pm:PerformedMove) (sname:string) (tname:string)  =
    let attack, mattack, shield, mshield, health, magic =
        if isDiscord then
            Emoj.Attack, Emoj.MagicAttack, Emoj.Shield,
            Emoj.MagicShield, Emoj.Health, Emoj.Magic
        else
            WebEmoji.Attack, WebEmoji.MagicAttack, WebEmoji.Shield,
            WebEmoji.MagicShield, WebEmoji.Health, WebEmoji.Magic
    match pm with
    | PerformedMove.Attack dmg ->
        match dmg with
        | Dmg.Critical v
        | Dmg.Default v ->
            if v > 0UL then $"{attack} {sname} overpowered {tname} protection and took {v} {health}"
            else $"{tname} blocked {sname} attack"
        | Dmg.Missed -> $"{sname} missed {tname}. Maybe next time?"
    | PerformedMove.MagicAttack(dmg, m) ->
        match dmg with
        | Dmg.Critical v
        | Dmg.Default v ->
            if v > 0UL then $"{mattack} {sname} overpowered {tname} protection and took {v} {health}"
            else $"{tname} blocked {sname} attack, {m} {magic} was taken nevertheless"
        | Dmg.Missed -> $"{sname} missed {tname}. Maybe next time?"
    | PerformedMove.Shield v -> $"{sname} increased their defense: + {v} to {shield}"
    | PerformedMove.MagicShield(v1, v2) ->
        if v1 > 0UL && v2 > 0UL then 
            $"{sname} casted magical protection with {v1} {mshield} and spend {v2} {magic}"
        elif v1 = 0UL && v2 > 0UL then
            $"{sname} casted magical protection, used {v2} {magic} but failed to produce anything sustainable"
        else
            $"{sname} don't have enough magic power to cast magic shield"
    | PerformedMove.Heal(v1, v2) ->
        if v1 > 0UL && v2 > 0UL then 
            $"{sname} healed {v1} {health} life with {v2} {magic} magic"
        elif v1 = 0UL && v2 > 0UL then
            $"{sname} used {v2} {magic} but failed to heal themself"
        else
            $"{sname} don't have enough magic power to heal"
    | PerformedMove.Meditate v -> $"{sname} gained {v} {magic}"

let performedMoveDiscord = performedMove true
let performedMoveWebUi = performedMove false