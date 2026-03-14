module Helpers

open Microsoft.AspNetCore.Http

type IFormCollection with
    member x.tryGetFormValue (key: string) =
        match x.TryGetValue key with
        | true, s -> Some s
        | false, _ -> None

[<RequireQualifiedAccess>]
module Parser =
    open System
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
    let private monstrsDir = "monstrs"
    let private wwwroot = "wwwroot"
    
    // TODO: add cacher
    let mapToLocalImg (mpic:MonsterImg) =

        let imgDir = Path.Combine(wwwroot, monstrsDir)

        if Directory.Exists(imgDir) |> not then
            Directory.CreateDirectory(imgDir) |> ignore

        let fullpath = (match mpic with | MonsterImg.File fn -> fn)

        let filename =
            Path.Combine(monstrsDir, Path.GetFileName(fullpath))

        let destFileName = Path.Combine(wwwroot, filename)
        if File.Exists destFileName |> not then
            try
                File.Copy(fullpath, destFileName)
            with
            | _ -> () // Ignore if source file doesn't exist

        if File.Exists destFileName then filename else ""
        |> MonsterImg.File

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
    open System

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
    let getDepositNote amount = $"deposit:{amount}"
    let createDepositTx (sender:string) (amount:decimal) =
        Blockchain.getAssetTransferTransactionTxB64(
            sender, 
            Blockchain.DarkCoinAssetId,
            Blockchain.DarkChampAscent,
            Blockchain.toLong(amount, Blockchain.Algo6Decimals),
            getDepositNote amount
        )
