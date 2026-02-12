module DarkChampAscent.Account

open System
open System.Globalization

[<RequireQualifiedAccess>]
type UserId =
    | Discord of uint64
    | Custom of uint64

type DiscordUser(nickname:string, discordId: uint64, pic:string option) =
    let picture =
        pic |> Option.map(fun p ->
            let f = if p.StartsWith "a_" then "gif" else "png"
            String.Format(CultureInfo.InvariantCulture,
                "https://cdn.discordapp.com/avatars/{0}/{1}.{2}",
                discordId, p, f))

    member _.Nickname = nickname
    member _.DiscordId = discordId
    member _.Pic = picture

type CustomUser(nickname:string, customId: uint64) =
    member _.Nickname = nickname
    member _.CustomId = customId

[<RequireQualifiedAccess>]
type Account =
    | Discord of DiscordUser
    | Custom of CustomUser
    member x.Nickname =
        match x with
        | Discord d -> d.Nickname
        | Custom u -> u.Nickname
    member x.ID =
        match x with
        | Discord d -> UserId.Discord d.DiscordId
        | Custom u -> UserId.Custom u.CustomId
    member x.Picture =
        match x with
        | Discord d -> d.Pic
        | Custom u -> None