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
        | Custom _ -> None

[<RequireQualifiedAccess>]
module Validation =
    let validateNickname (nickname: string) : Result<string, string> =
        let trimmed = nickname.Trim()
        if String.IsNullOrWhiteSpace(trimmed) then
            Error "Nickname is required"
        elif trimmed.Length < 3 then
            Error "Nickname must be at least 3 characters"
        elif trimmed.Length > 16 then
            Error "Nickname must be 16 characters or less"
        elif not (System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^[a-zA-Z0-9_-]+$")) then
            Error "Nickname can only contain letters, numbers, underscores and hyphens"
        else
            Ok trimmed

    let validatePassword (password: string) : Result<string, string> =
        if String.IsNullOrWhiteSpace(password) then
            Error "Password is required"
        elif password.Length < 8 then
            Error "Password must be at least 8 characters"
        elif password.Length > 24 then
            Error "Password must be 24 characters or less"
        else
            Ok password