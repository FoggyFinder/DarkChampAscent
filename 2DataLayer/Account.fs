// CREATE TABLE IF NOT EXISTS InGameUser (
// ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
// Nickname TEXT NOT NULL UNIQUE,
// Password TEXT NOT NULL,
//);

//CREATE TABLE IF NOT EXISTS User (
// ID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
// DiscordId INTEGER UNIQUE,
// InGameUserId INTEGER UNIQUE,
// Balance NUMERIC NOT NULL,
// FOREIGN KEY (InGameUserId)
// REFERENCES InGameUser (ID),
//    CHECK (Balance >= 0)
//);

module DarkChampAscent.Account

open System
open System.Globalization

[<RequireQualifiedAccess>]
type UserId =
    | Discord of uint64
    | InGame of uint64

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

type InGameUser(nickname:string, inGameId: uint64) =
    member _.Nickname = nickname
    member _.InGameId = inGameId

[<RequireQualifiedAccess>]
type Account =
    | Discord of DiscordUser
    | InGame of InGameUser
    member x.Nickname =
        match x with
        | Discord d -> d.Nickname
        | InGame u -> u.Nickname
    member x.ID =
        match x with
        | Discord d -> UserId.Discord d.DiscordId
        | InGame u -> UserId.InGame u.InGameId
    member x.Picture =
        match x with
        | Discord d -> d.Pic
        | InGame u -> None