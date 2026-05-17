module DiscordBot.Interactions

open System
open NetCord.Services.ComponentInteractions
open NetCord.Rest

open Serilog
open NetCord
open GameLogic.Champs
open GameLogic.Battle

open Services
open DarkChampAscent.Account
open Db
open Helpers

let actionselect (bs:BattleService) (context:StringMenuInteractionContext) (move:string) = task {
    let res =
        let str = (context.SelectedValues |> Seq.tryHead |> Option.defaultValue "").Trim()
        match UInt64.TryParse(str) with
        | true, id -> Some id
        | false, _ ->
            Log.Error($"Unable to parse {str}")
            None
        |> Option.bind(fun id ->
            match Enum.TryParse<Move>(move) with
            | true, m ->
                let rar = { ChampId = id; Move = m }
                Some(rar)
            | false, _ ->
                Log.Error($"Unable to parse {move}")
                None)
        
    let str =
        match res with
        | Some rar ->
            let r = bs.JoinRound(UserId.Discord context.User.Id, rar)
            match r with
            | Ok() -> "Action is recorded"
            | Error str ->  str
        | None -> "Something went wrong"

    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Components <- [ TextDisplayProperties(str) ]
    )

    return callback
}

let sendGroup (bs:BattleService) (context:ButtonInteractionContext) =
    task {
        let r = bs.SendGroup(UserId.Discord context.User.Id)
        let str = match r with | Ok count -> $"Done! You send {count} champs" | Error err -> err
        let callback =
            [ TextDisplayProperties(str) :> IMessageComponentProperties ]
            |> DUtils.interactionMessageFromComponents
            |> InteractionCallback.Message
        let! _ = context.Interaction.SendResponseAsync(callback)
    
        ()
    } :> System.Threading.Tasks.Task

let sendAll (bs:BattleService) (context:ButtonInteractionContext) =
    task {
        let r = bs.SendAll(UserId.Discord context.User.Id)
        let str = match r with | Ok count -> $"Done! You send {count} champs" | Error err -> err
        let callback =
            [ TextDisplayProperties(str) :> IMessageComponentProperties ]
            |> DUtils.interactionMessageFromComponents
            |> InteractionCallback.Message
        let! _ = context.Interaction.SendResponseAsync(callback)
    
        ()
    } :> System.Threading.Tasks.Task

let getPendingRewards (db:SqliteStorage) (context:ButtonInteractionContext) =
    task {
        let r = db.GetPendingRewards(UserId.Discord context.User.Id)
        let str = match r with | Some d -> $"Your Champs earned you {d} {Display.Emoj.Coin} DarkCoins so far! They will be distributed at the end of the battle" | None -> "Unexpected error. Make sure you have registered your Algorand wallet first."
        let callback =
            [ TextDisplayProperties(str) :> IMessageComponentProperties ]
            |> DUtils.interactionMessageFromComponents
            |> InteractionCallback.Message
        let! _ = context.Interaction.SendResponseAsync(callback)
    
        ()
    } :> System.Threading.Tasks.Task

open System.Linq
open Microsoft.Extensions.Options

let registerWalletModal (db:SqliteStorage) (options: IOptions<Conf.WalletConfiguration>) (context:ModalInteractionContext) =
    task {
        let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
        let! _ = context.Interaction.SendResponseAsync(callback)
        let! _ =
            context.Interaction.ModifyResponseAsync(fun options ->
                options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                options.Components <- [ TextDisplayProperties($"Registering...") ])

        let nWalletO = 
            context.Components
                .OfType<Label>().Select(fun l -> l.Component)
                .OfType<TextInput>().Select(fun ti ->
                    if ti.CustomId = "newWallet" then Some(ti.Value)
                    else None)
            |> Seq.tryPick id

        let str = 
            match nWalletO with
            | Some newWallet ->
                let wallet' = newWallet.Trim()
                let cInstr code = 
                    let frontendOrigin =
                        match Environment.GetEnvironmentVariable "frontendorigin" with
                        | null -> "http://localhost:5173"
                        | str -> str
                    $"Good, this wallet ({wallet'}) is registered. There 2 different ways to confirm it:
1. Send 0-cost Algo tx to {options.Value.GameWallet} with following note: {code}.
2. Auth with discord on [web-app]({frontendOrigin}), navigate to Account page and follow instructions there"
                if Blockchain.isValidAddress wallet' then
                    match db.RegisterNewWallet(UserId.Discord context.User.Id, wallet') with
                    | Ok code ->
                        cInstr code
                    | Error err ->
                        let uId = UserId.Discord context.User.Id
                        match db.GetUserWallets uId with
                        | Ok wallets when wallets.Length > 0 ->
                            match wallets |> List.tryFind(fun w -> w.Wallet = wallet') with
                            | Some w ->
                                if w.IsConfirmed then "You already registered this wallet"
                                else cInstr w.Code
                            | None -> err
                        | _ -> err
                else
                    $"Invalid Algorand address. Please, check correctness: {wallet'}"
            | _ -> "Error: Incorrect input"

        let! _ = context.Interaction.ModifyResponseAsync(fun options ->
            options.Components <- [ TextDisplayProperties(str) ])

        ()
    } :> System.Threading.Tasks.Task

let register (_:ButtonInteractionContext) =
    task {
        let callback = InteractionCallback.Modal(
            ModalProperties($"registerwalletmodal", "Register wallet", [
                LabelProperties("New wallet", TextInputProperties("newWallet", TextInputStyle.Short))
                    .WithDescription($"No nfd support")
            ]))
        return callback
    }

let info (context:ButtonInteractionContext) =
    task {
        let frontendOrigin =
            match Environment.GetEnvironmentVariable "frontendorigin" with
            | null -> "http://localhost:5173"
            | str -> str

        let about = $"""
        **DarkChampAscent** is a discord bot and [web app]({frontendOrigin}) where players collect DarkCoins ({Display.Emoj.Coin}) by performing actions each round.
The main goal of this project is to create an easy & fun way to slightly improve [DarkCoin](https://dark-coin.io/) distribution among active community members.
You can find details on tokenomics [here]({frontendOrigin}//#/tokenomics)"""

        let commands = """
Not all features are available in the Discord bot, but the main ones are:

* `Send group` – since rewards are split among all moves, sometimes the best strategy is to send only a few Champs while keeping the rest idle. Send group tries to use at most 6 Champs.
* `Send all` – all Champs join battle. First it tries to apply different moves and the rest use attack.
* `Pending rewards` – displays earned rewards for the current battle.
* `Info` – displays short info about the project and available commands.
* `Register` – allows adding a new wallet, or displays current wallets in case the user already has one registered.

More commands are available as application commands, e.g. `/rewards`, ...
The project is in an early stage of development; everything is subject to change without prior notice. If you have any ideas or suggestions, feel free to share them
            """
        let callback =
            [
                TextDisplayProperties(about) :> IMessageComponentProperties
                TextDisplayProperties(commands) :> IMessageComponentProperties
            ]
            |> DUtils.interactionMessageFromComponents
            |> InteractionCallback.Message

        let! _ = context.Interaction.SendResponseAsync(callback)
    
        ()
    } :> System.Threading.Tasks.Task