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
        let str = match r with | Ok _ -> "Done!" | Error err -> err
        let m = 
            InteractionMessageProperties()
                .WithFlags(Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2))
                .WithComponents([ TextDisplayProperties(str) ])
        let callback = InteractionCallback.Message m
        let! _ = context.Interaction.SendResponseAsync(callback)
    
        ()
    } :> System.Threading.Tasks.Task

let sendAll (bs:BattleService) (context:ButtonInteractionContext) =
    task {
        let r = bs.SendAll(UserId.Discord context.User.Id)
        let str = match r with | Ok _ -> "Done!" | Error err -> err
        let m = 
            InteractionMessageProperties()
                .WithFlags(Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2))
                .WithComponents([ TextDisplayProperties(str) ])
        let callback = InteractionCallback.Message m
        let! _ = context.Interaction.SendResponseAsync(callback)
    
        ()
    } :> System.Threading.Tasks.Task

let register (context:ButtonInteractionContext) =
    task {
        let m = 
            InteractionMessageProperties()
                .WithFlags(Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2))
                .WithComponents([ TextDisplayProperties("Not implemented!") ])
        let callback = InteractionCallback.Message m

        let! _ = context.Interaction.SendResponseAsync(callback)
    
        ()
    } :> System.Threading.Tasks.Task