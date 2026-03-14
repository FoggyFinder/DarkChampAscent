namespace GameLogic.Effects
open System

// effect that may occur during a battle
type Effect =
    | Death = 0

    /// negative temp effects
    | Hit = 1 // suppress health
    | MagicHit = 2 // supress magic
    | Desolation = 3 // reduces luck
    | Scattermind = 4 // reduces accuracy
    | Despondency = 5 // reduces attack
    | Sorrow = 6 // reduces magic attack
    | Shatter = 7 // reduces defense
    | Disrupt = 8 // reduces magic defense

type EffectItem(effect:Effect, value:uint64 option, duration: int) =
    member _.Effect = effect
    member _.Value = value
    member _.Duration = duration

[<RequireQualifiedAccess>]
module Effects =
    let getRoundDuration(effect:Effect) =
        match effect with
        | Effect.Death -> 16
        | Effect.Hit
        | Effect.MagicHit
        | Effect.Desolation
        | Effect.Scattermind
        | Effect.Despondency
        | Effect.Sorrow
        | Effect.Shatter
        | Effect.Disrupt -> 4

    let getDescription(shopItem:Effect) = ""
