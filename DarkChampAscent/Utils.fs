module Helpers

open Microsoft.AspNetCore.Http

type IFormCollection with
    member x.tryGetFormValue (key: string) =
        match x.TryGetValue key with
        | true, s -> Some s
        | false, _ -> None
    member x.tryGetUInt64 (key: string) =
        x.tryGetFormValue key
        |> Option.bind (fun s ->
            match System.UInt64.TryParse s with
            | true, v -> Some v
            | false, _ -> None)

type IQueryCollection with
    member x.tryGetQueryValue (key: string) =
        match x.TryGetValue key with
        | true, s -> Some s
        | false, _ -> None

open System
open System.Threading.Tasks
open Types
open DarkChampAscent.Account
open Db

let retry (f: unit -> bool) (retries: int) (delay: TimeSpan) : Task<bool> =
    task {
        let mutable attempts = retries
        let mutable success = false
        while attempts > 0 && not success do
            success <- f()
            if not success then
                attempts <- attempts - 1
                do! Task.Delay(delay)
        return success
    }

[<RequireQualifiedAccess>]
module Parser =
    let enumFromIntAsStr<'a when 'a: (new: unit -> 'a) and 'a:struct and 'a:> Enum and 'a:enum<int32>>(s:string) =
        match Int32.TryParse s with
        | true, i ->
            let v = enum<'a> i
            if Enum.IsDefined<'a> v then
                v |> Ok
            else Error("Value is out of range")
        | false, _ -> Error("Not integer value")

[<RequireQualifiedAccess>]
module FileUtils =
    open GameLogic.Monsters
    open System.IO
    open System.Collections.Concurrent
    let private monstrsDir = "monstrs"
    let private wwwroot = "wwwroot"

    let filepathes = ConcurrentDictionary<string, string>()
    let mapToLocalImg (mpic:MonsterImg) =
        match mpic with
        | MonsterImg.File fullpath ->
            match filepathes.TryGetValue fullpath with
            | true, filename -> filename |> MonsterImg.File
            | false, _ ->
                let imgDir = Path.Combine(wwwroot, monstrsDir)

                if Directory.Exists(imgDir) |> not then
                    Directory.CreateDirectory(imgDir) |> ignore

                let filename =
                    Path.Combine(monstrsDir, Path.GetFileName(fullpath))

                let destFileName = Path.Combine(wwwroot, filename)
                if File.Exists destFileName |> not then
                    try
                        File.Copy(fullpath, destFileName)
                    with
                    | _ -> () // Ignore if source file doesn't exist
                
                let v = if File.Exists destFileName then filename else ""
                filepathes.TryAdd(fullpath, v) |> ignore
                v |> MonsterImg.File
        | MonsterImg.Ipfs _ -> mpic

[<RequireQualifiedAccess>]
module RateLimiting =
    open System
    open System.Collections.Concurrent

    let private attempts = ConcurrentDictionary<string, DateTime list>()

    let checkRateLimit (key: string) (maxAttempts: int) (window: TimeSpan) : bool =
        let now    = DateTime.UtcNow
        let cutoff = now - window
        let newAttempts =
            attempts.AddOrUpdate(
                key,
                (fun _ -> [now]),
                (fun _ old ->
                    let recent = old |> List.filter (fun t -> t > cutoff)
                    if recent.Length >= maxAttempts then recent
                    else now :: recent))
        newAttempts.Length < maxAttempts


[<RequireQualifiedAccess>]
module Cookie =
    [<Literal>]
    let Name = "DiscordAuth"

[<RequireQualifiedAccess>]
module AuthenticationHandler =
    open DarkChampAscent.Account
    open System.Security.Claims
    open Microsoft.AspNetCore.Authentication

    let [<Literal>] Custom = "Custom"
    let [<Literal>] Web3 = "Web3"

    let private createCustomClaims (user: CustomUser) : Claim list =
        [ Claim(ClaimTypes.NameIdentifier, user.CustomId.ToString())
          Claim(ClaimTypes.Name, user.Nickname)
          Claim(ClaimTypes.AuthenticationMethod, Custom) ]

    let private createCustomPrincipal (user: CustomUser) (authScheme: string) : ClaimsPrincipal =
        let identity = ClaimsIdentity(createCustomClaims user, authScheme)
        ClaimsPrincipal(identity)

    let createCustomTicket (user: CustomUser) (authScheme: string) : AuthenticationTicket =
        let principal  = createCustomPrincipal user authScheme
        let properties = AuthenticationProperties(IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7.0))
        AuthenticationTicket(principal, properties, authScheme)

    let private createWeb3Claims (user: Web3User) : Claim list =
        [ Claim(ClaimTypes.NameIdentifier, user.UserId.ToString())
          Claim(ClaimTypes.Name, user.Wallet)
          Claim(ClaimTypes.AuthenticationMethod, Web3) ]

    let private createWeb3Principal (user: Web3User) (authScheme: string) : ClaimsPrincipal =
        let identity = ClaimsIdentity(createWeb3Claims user, authScheme)
        ClaimsPrincipal(identity)

    let createWeb3Ticket (user: Web3User) (authScheme: string) : AuthenticationTicket =
        let principal  = createWeb3Principal user authScheme
        let properties = AuthenticationProperties(IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7.0))
        AuthenticationTicket(principal, properties, authScheme)

[<RequireQualifiedAccess>]
module BlockchainUtils =
    let createTx (sender:string) (receiver:string) (amount:decimal) (note:string) =
        Blockchain.getAssetTransferTransactionTxB64(
            sender, 
            Blockchain.DarkCoinAssetId,
            receiver,
            Blockchain.toLong(amount, Blockchain.Algo6Decimals),
            note
        )

[<RequireQualifiedAccess>]
module Cache =
    open System.Collections.Concurrent
    open Serilog
    open Falco
    open NetCord.Rest

    let names = ConcurrentDictionary<uint64, string>()
    let userLinks = ConcurrentDictionary<int64, UserLink>()

    let tryGetNameByDiscordId (ctx:HttpContext) (dId:uint64) =
        task {
            match names.TryGetValue dId with
            | true, name -> return name
            | false, _ ->
                try
                    let client = ctx.Plug<RestClient>()
                    let! u = client.GetUserAsync dId
                    names.TryAdd(dId, u.GlobalName) |> ignore
                    return u.GlobalName
                with exn ->
                    Log.Error("Attempt to get user info", exn)
                    return $"Discord:{dId}"
        }

    let tryGetUserLinkByRawUserId (db:SqliteStorage) (restClient:RestClient) (dbId:int64) =
        task {
            match userLinks.TryGetValue dbId with
            | true, uLink -> return Some uLink
            | false, _ ->
                try
                    match db.GetUserInfoRaw dbId with
                    | Some ut ->
                        match ut with
                        | UserType.Discord dId ->
                            match names.TryGetValue dId with
                            | true, name -> return UserLink(uint64 dbId, name) |> Some
                            | false, _ ->
                                let! u = restClient.GetUserAsync dId
                                names.TryAdd(dId, u.GlobalName) |> ignore
                                return UserLink(uint64 dbId, u.GlobalName) |> Some
                        | UserType.Custom nickname ->
                            return UserLink(uint64 dbId, nickname) |> Some
                        | UserType.Web3 wallet ->
                            return UserLink(uint64 dbId, wallet.Substring(0, 5) + "...") |> Some
                    | None -> return None
                with exn ->
                    Log.Error("Attempt to get user info", exn)
                    return None
        }

    let tryGetUserLinkByUserId (db:SqliteStorage) (restClient:RestClient) (userId:UserId) =
        match db.FindUserIdByUserId userId with
        | Some dbId -> tryGetUserLinkByRawUserId db restClient dbId
        | None -> task { return None }

open NetCord.Rest
open NetCord.Gateway
open Serilog

[<RequireQualifiedAccess>]
module Channels =
    let [<Literal>] LogChannel = "dark-champs-ascent-logs"
    let [<Literal>] ChatChannel = "dark-champs-ascent-chat"
    let [<Literal>] EntryChannel = "dark-champs-ascent-entry"
    let [<Literal>] Category = "DARK CHAMPS ASCENT"

    let [<Literal>] DarkAscentPlayerRole = "DarkChampAscent Player"
    let [<Literal>] DarkAscentBotRole = "DarkChampAscent"

[<RequireQualifiedAccess>]
module DUtils =
    open NetCord

    let sendMsgToChannel(channel:string)(client:GatewayClient)(mp:MessageProperties)(pin:bool) = task {
        for guild in client.Cache.Guilds do
            match guild.Value.Channels |> Seq.tryFind(fun c -> c.Value.Name = channel) with
            | Some channel ->
                try
                    let! rm = client.Rest.SendMessageAsync(channel.Key, mp)
                    if pin then
                        try
                            do! rm.PinAsync()
                        with exn ->
                            Log.Error(exn, "Unable to pin message")
                    do! Task.Delay(TimeSpan.FromSeconds(1.0))
                with exn ->
                    Log.Error(exn, $"Unable to send a msg to {channel.Value.Name} at {guild.Value.Name}")
            | None ->
                Log.Error($"Can't find channel in guild {guild}")

    }

    let createAndSendMsgToChannel(channel:string) (client:GatewayClient)(getMP:unit -> MessageProperties)(pin:bool) = task {
        for guild in client.Cache.Guilds do
            match guild.Value.Channels |> Seq.tryFind(fun c -> c.Value.Name = channel) with
            | Some channel ->
                try
                    let! rm = client.Rest.SendMessageAsync(channel.Key, getMP())
                    if pin then
                        try
                            do! rm.PinAsync()
                        with exn ->
                            Log.Error(exn, "Unable to pin message")
                    // do! Task.Delay(TimeSpan.FromSeconds(1.0))
                with exn ->
                    Log.Error(exn, $"Unable to send a msg to {channel.Value.Name} at {guild.Value.Name}")
            | None ->
                Log.Error($"Can't find channel in guild {guild}")
    }

    let sendMsgToLogChannel (client:GatewayClient) (mp:MessageProperties) = task {
        let flags = mp.Flags
        mp.Flags <-
            if flags.HasValue then Nullable(flags.Value ||| MessageFlags.SuppressNotifications)
            else Nullable(MessageFlags.SuppressNotifications)
            
        do! sendMsgToChannel Channels.LogChannel client mp false
        mp.Flags <- flags
    }

    let sendMsgToLogChannelWithNotifications (client:GatewayClient) (mp:MessageProperties) =
        sendMsgToChannel Channels.LogChannel client mp false

    let mention (uId:uint64) = $"<@{uId}>"

    let addDiscordRole (client:GatewayClient) (discordId:uint64) =
        task {
            for guild in client.Cache.Guilds do
                match guild.Value.Users.TryGetValue discordId with
                | true, duser ->
                    let rO =
                        guild.Value.Roles
                        |> Seq.tryFind(fun r -> r.Value.Name = Channels.DarkAscentPlayerRole)
                    match rO with
                    | Some role ->
                        try
                            if duser.RoleIds |> Seq.contains role.Key |> not then
                                do! guild.Value.AddUserRoleAsync(discordId, role.Key)
                                Log.Information("Role added to a user")
                        with exn ->
                            Log.Error(exn, $"Unable to add role to user inside {guild.Value.Name} guild")
                    | None ->
                        Log.Error($"Unable to find a role to user inside {guild.Value.Name} guild")
                | false, _ -> ()
        }
    let v2ComponentMessage(components:IMessageComponentProperties list) =
        MessageProperties().WithComponents(components).WithFlags(MessageFlags.IsComponentsV2)

    let interactionMessageFromComponents(components:IMessageComponentProperties list) =
        InteractionMessageProperties()
            .WithFlags(Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2))
            .WithComponents(components)

[<RequireQualifiedAccess>]
module Links =
    let frontendOrigin =
        match Environment.GetEnvironmentVariable "frontendorigin" with
        | null -> "http://localhost:5173"
        | str -> str

    let userProfile (userId:uint64) =
        $"{frontendOrigin}//#/user/{userId}"

    let monsterProfile (monsterId:uint64) =
        $"{frontendOrigin}//#/monster/{monsterId}"

    let champProfile (champId:uint64) =
        $"{frontendOrigin}//#/champ/{champId}"