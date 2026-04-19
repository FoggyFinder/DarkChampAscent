namespace DiscordBot.Commands

open System
open GameLogic.Champs
open GameLogic.Battle
open Db
open NetCord.Services.ApplicationCommands
open NetCord.Rest

open NetCord
open GameLogic.Limits
open Display
open System.Threading.Tasks
open Serilog
open DarkChampAscent.Account

[<RequireQualifiedAccess>]
module ApplicationCommand =
    let deferredMessage (context:ApplicationCommandContext) (action:MessageOptions -> unit) =
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = context.Interaction.SendResponseAsync(callback)
            let! _ = context.Interaction.ModifyResponseAsync(action)
            ()
        } :> Task

[<SlashCommand("wallet", "Wallet command")>]
type WalletModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()
    let frontendOrigin =
        match Environment.GetEnvironmentVariable("frontendorigin") with
        | null -> "http://localhost:5173"
        | str -> str
    
    [<SubSlashCommand("register", "Register Algorand wallet, nfd not supported yet")>]
    member x.Register([<SlashCommandParameter(Name = "wallet", Description = "your wallets")>] wallet:string): Task =
        let wallet' = wallet.Trim()
        let res =
            if Blockchain.isValidAddress wallet' then
                db.RegisterNewWallet(UserId.Discord x.Context.User.Id, wallet')
            else
                try
                    Log.Error($"{x.Context.User} attempts to register invalid {wallet'} address")
                with exn ->
                    Log.Error(exn, $"attempt to register {wallet'} address")
                Error($"Invalid Algorand address. Please, check correctness: {wallet'}")
        let str =
            match res with
            | Ok () -> $"Good, auth with discord on [web-app]({frontendOrigin}) and follow instruction to confirm your wallet ({wallet'})"
            | Error err -> $"Oh, no...there was error: {err}"
        (fun (moptions:MessageOptions) -> moptions.Content <- str)
        |> ApplicationCommand.deferredMessage x.Context

type UserModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()

    [<SlashCommand("earnings", "returns amount of coins earned for specific range of rounds")>]
    member x.Earnings(
        [<SlashCommandParameter(Name = "start", Description = "from round (included)")>] startRound:uint64,
        [<SlashCommandParameter(Name = "end", Description = "to round (included)")>] endRound:uint64): Task =
        let res = db.GetUserEarnings(UserId.Discord x.Context.User.Id, startRound, endRound)
        let str =
            match res with
            | Some d -> $"Your earnings for [{startRound}..{endRound}] rounds: {Emoj.Coin} {d} DarkCoins"
            | None -> $"Can't get earnings."
        ApplicationCommand.deferredMessage x.Context (fun options -> options.Content <- str)
    
type GeneralModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()

    [<SlashCommand("rewards", "shows in-game rewards balance")>]
    member x.GetBalance(): Task =
        let res = db.GetNumKey DbKeysNum.Rewards
        let darkCoinPrice = db.GetNumKey DbKeysNum.DarkCoinPrice
        let str =
            match res with
            | Some d ->
                let usdcs =
                    match darkCoinPrice with
                    | Some dcPrice ->
                        let usdc = dcPrice * d
                        $"({Display.toRound2StrD usdc} USDC)"
                    | None -> ""
                $"In-game rewards balance: {Emoj.Coin} {Display.toRound6StrD d} DarkCoins {usdcs}"
            | None -> $"Can't get balance."
        
        ApplicationCommand.deferredMessage x.Context (fun options -> options.Content <- str)

    [<SlashCommand("limits", "shows limits")>]
    member x.Limits() =
        ApplicationCommand.deferredMessage x.Context (fun options -> 
            let ep =
                EmbedProperties(Title = "Limits:")
                    .WithFields([
                        EmbedFieldProperties(Name = "Max amount of custom monsters with the same combination of type and subtype", Value = $"{Limits.CustomMonstersPerTypeSubtype}", Inline = true)
                        EmbedFieldProperties(Name = "Max amount of generation requests", Value = $"{Limits.UnfinishedRequests}", Inline = true)
                    ])
            options.Embeds <- [ ep ])

[<SlashCommand("battle", "Battle command")>]
type BattleModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()
  
    [<SubSlashCommand("action", "records action against monster in active round")>]
    member x.PerformAction([<SlashCommandParameter(Name = "action", Description = "action")>] move:Move) =
        let champs =
            match db.GetLastRoundId() with
            | Some roundId ->
                match db.GetRoundStatus roundId with
                | Some status ->
                    if status = RoundStatus.Started then
                        db.GetActiveUserChamps(UserId.Discord x.Context.User.Id, roundId)
                    else
                        Error("Please wait until new round is started")
                | None -> Error("Something went wrong - unable to get round status")
            | None -> Error("Something went wrong - unable to get round. Maybe there is no any?")
        
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match champs with
            | Ok xs ->
                if xs.IsEmpty then
                    options.Content <- $"You do not have any champs left"
                else
                    options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    let selectMenu = 
                        StringMenuProperties($"actionselect:{move}", xs |> List.map(fun rpc -> StringMenuSelectOptionProperties(rpc.Name, rpc.ID.ToString())),
                            Placeholder = "Choose an option")
                        
                    options.Components <- [
                        ComponentContainerProperties([
                            TextDisplayProperties("Content")
                            selectMenu
                        ])
                    ]
            | Error err ->
                options.Content <- err)

    [<SubSlashCommand("params", "shows battle parameters")>]
    member x.BattleParams() =
        ApplicationCommand.deferredMessage x.Context (fun options -> 
            let ep =
                EmbedProperties(Title = "Params:")
                    .WithFields([
                        EmbedFieldProperties(Name = "Rounds in battle", Value = $"{Constants.RoundsInBattle}", Inline = true)
                        EmbedFieldProperties(Name = "Round duration", Value = $"{BattleParams.RoundDuration()}", Inline = true)
                        EmbedFieldProperties(Name = "XP per level", Value = $"{Levels.XPPerLvl}", Inline = true)
                    ])
            options.Embeds <- [ ep ])