namespace GameLogic.Shop

open GameLogic.Champs

[<RequireQualifiedAccess>]
type ShopItemKind =
    | Elixir = 0
    | Spell = 1

[<RequireQualifiedAccess>]
type ShopItemTarget =
    | Life = 0
    | Magic = 1
    | Luck = 2
    | Accuracy = 3
    | Damage = 4
    | MagicalDamage = 5
    | Defense = 6
    | MagicalDefense = 7
    | Revival = 8

// something that one can buy from the shop
[<RequireQualifiedAccess>]
type ShopItem =
    | ElixirOfLife = 0 // heal
    | ElixirOfMagic = 1 // increase magic

    | ElixirOfLuck = 2
    | ElixirOfAccuracy = 3

    | ElixirOfDamage = 4
    | ElixirOfMagicalDamage = 5

    | ElixirOfDefense = 6
    | ElixirOfMagicalDefense = 7

    | RevivalSpell = 8

type RoundBoost = {
    StartRoundId: int64
    Boost: ShopItem
    Duration: int
}

[<RequireQualifiedAccess>]
module Shop =
    // ToDo: investigate why adding [<Literal>] breaks program
    let RenamePrice = 5M
    let GenMonsterPrice = 0.5M

    let getPrice =
        function
        | ShopItem.ElixirOfLife -> 0.02M
        | ShopItem.ElixirOfMagic -> 0.1M
        | ShopItem.ElixirOfLuck -> 0.05M
        | ShopItem.ElixirOfAccuracy -> 0.05M
        | ShopItem.ElixirOfDamage -> 0.05M
        | ShopItem.ElixirOfMagicalDamage -> 0.05M
        | ShopItem.ElixirOfDefense -> 0.05M
        | ShopItem.ElixirOfMagicalDefense -> 0.05M
        | ShopItem.RevivalSpell -> 0.25M
        | _ -> 0M

    let getRoundDuration =
        function
        | ShopItem.ElixirOfLife
        | ShopItem.ElixirOfMagic -> 1
        | ShopItem.ElixirOfLuck
        | ShopItem.ElixirOfAccuracy
        | ShopItem.ElixirOfDamage
        | ShopItem.ElixirOfMagicalDamage
        | ShopItem.ElixirOfDefense
        | ShopItem.ElixirOfMagicalDefense -> 8
        | ShopItem.RevivalSpell -> System.Int32.MaxValue

    let getDescription(shopItem:ShopItem) = ""

    let getKind =
        function
        | ShopItem.ElixirOfLife
        | ShopItem.ElixirOfMagic
        | ShopItem.ElixirOfLuck
        | ShopItem.ElixirOfAccuracy
        | ShopItem.ElixirOfDamage
        | ShopItem.ElixirOfMagicalDamage
        | ShopItem.ElixirOfDefense
        | ShopItem.ElixirOfMagicalDefense -> ShopItemKind.Elixir
        | ShopItem.RevivalSpell -> ShopItemKind.Spell     

    let getTarget =
        function
        | ShopItem.ElixirOfLife
            -> ShopItemTarget.Life
        | ShopItem.ElixirOfMagic
            -> ShopItemTarget.Magic
        | ShopItem.ElixirOfLuck
            -> ShopItemTarget.Luck
        | ShopItem.ElixirOfAccuracy
            -> ShopItemTarget.Accuracy
        | ShopItem.ElixirOfDamage
            -> ShopItemTarget.Damage
        | ShopItem.ElixirOfMagicalDamage
            -> ShopItemTarget.MagicalDamage
        | ShopItem.ElixirOfDefense
            -> ShopItemTarget.Defense
        | ShopItem.ElixirOfMagicalDefense
            -> ShopItemTarget.MagicalDefense
        | ShopItem.RevivalSpell -> ShopItemTarget.Revival

    let getValue =
        function
        | ShopItem.ElixirOfLife -> 50L
        | ShopItem.ElixirOfMagic -> 100L
        | ShopItem.ElixirOfLuck -> 3L
        | ShopItem.ElixirOfAccuracy -> 3L
        | ShopItem.ElixirOfDamage -> 2L
        | ShopItem.ElixirOfMagicalDamage -> 4L
        | ShopItem.ElixirOfDefense -> 3L
        | ShopItem.ElixirOfMagicalDefense -> 3L
        | ShopItem.RevivalSpell -> 0L

    let applyShopItem (item:ShopItem) (stat:Stat) =
        let value = getValue item
        match item with
        | ShopItem.ElixirOfLife -> { stat with Health = stat.Health + value }
        | ShopItem.ElixirOfMagic -> { stat with Magic = stat.Magic + value }
        | ShopItem.ElixirOfLuck -> { stat with Luck = stat.Luck + value }
        | ShopItem.ElixirOfAccuracy -> { stat with Accuracy = stat.Accuracy + value }
        | ShopItem.ElixirOfDamage -> { stat with Attack = stat.Attack + value }
        | ShopItem.ElixirOfMagicalDamage -> { stat with MagicAttack = stat.MagicAttack + value }
        | ShopItem.ElixirOfDefense -> { stat with Defense = stat.Defense + value }
        | ShopItem.ElixirOfMagicalDefense -> { stat with MagicDefense = stat.MagicDefense + value }
        | ShopItem.RevivalSpell -> stat