namespace DiscordBot

open NetCord.Rest
open NetCord.Gateway
open NetCord.Hosting.Gateway
open System.Threading.Tasks
open NetCord
open System
open Serilog
open FSharp.Control
open Db

type GuildCreateHandler(db:SqliteStorage) =
    interface IGuildCreateGatewayHandler with
        member t.HandleAsync(arg:GuildCreateEventArgs) =
            task {
                Log.Information("GuildCreateHandler")
                let botRole = arg.Guild.Roles |> Seq.tryFind(fun r -> r.Value.Name = Channels.DarkAscentBotRole)
                Log.Information($"Bot Role found?...{botRole.IsSome}")
                let categoryChannel = arg.Guild.Channels |> Seq.tryFind(fun ch -> ch.Value.Name = Channels.Category)
                let! categoryId =
                    if categoryChannel.IsNone then
                        task {
                            let categoryChannel = GuildChannelProperties(Channels.Category, ChannelType.CategoryChannel)
                            Log.Information($"Creating...{Channels.Category}")
                            let! ch = arg.Guild.CreateChannelAsync(categoryChannel)
                            Log.Information($"Done! {ch.Id}")
                            return ch.Id
                        }
                    else task { return categoryChannel.Value.Key }
                let logChannelIsCreated = arg.Guild.Channels |> Seq.exists(fun ch -> ch.Value.Name = Channels.LogChannel)
                if logChannelIsCreated |> not then
                    let logChannel = GuildChannelProperties(Channels.LogChannel, ChannelType.TextGuildChannel)
                    logChannel.ParentId <- Nullable(categoryId)
                    logChannel.PermissionOverwrites <- [
                        PermissionOverwriteProperties(arg.Guild.EveryoneRole.Id, PermissionOverwriteType.Role).WithDenied(
                            Nullable(Permissions.SendMessages)
                        )
                        match botRole with
                        | Some brole ->
                            PermissionOverwriteProperties(brole.Key, PermissionOverwriteType.Role).WithAllowed(
                                Nullable(Permissions.SendMessages)
                            )                            
                        | None -> ()
                    ]
                    Log.Information($"Creating...{Channels.LogChannel}")
                    let! ch = arg.Guild.CreateChannelAsync(logChannel)
                    Log.Information($"Done! {ch.Id}")
                
                let battleChannelIsCreated = arg.Guild.Channels |> Seq.exists(fun ch -> ch.Value.Name = Channels.BattleChannel)
                if battleChannelIsCreated |> not then
                    let battleChannel = GuildChannelProperties(Channels.BattleChannel, ChannelType.TextGuildChannel)
                    battleChannel.ParentId <- Nullable(categoryId)
                    battleChannel.PermissionOverwrites <- [
                        PermissionOverwriteProperties(arg.Guild.EveryoneRole.Id, PermissionOverwriteType.Role).WithDenied(
                            Nullable(Permissions.SendMessages)
                        )
                        match botRole with
                        | Some brole ->
                            PermissionOverwriteProperties(brole.Key, PermissionOverwriteType.Role).WithAllowed(
                                Nullable(Permissions.SendMessages 
                                    // ||| Permissions.ManageMessages
                                )
                            )                            
                        | None -> ()
                    ]
                    Log.Information($"Creating...{Channels.BattleChannel}")
                    let! ch = arg.Guild.CreateChannelAsync(battleChannel)
                    Log.Information($"Done! {ch.Id}")
                
                let chatChannelIsCreated = arg.Guild.Channels |> Seq.exists(fun ch -> ch.Value.Name = Channels.ChatChannel)
                if chatChannelIsCreated |> not then
                    let logChannel = GuildChannelProperties(Channels.ChatChannel, ChannelType.TextGuildChannel)
                    logChannel.ParentId <- Nullable(categoryId)
                    Log.Information($"Creating...{Channels.ChatChannel}")
                    let! ch = arg.Guild.CreateChannelAsync(logChannel)
                    Log.Information($"Done! {ch.Id}")

                let pRoleO = arg.Guild.Roles |> Seq.tryFind(fun r -> r.Value.Name = Channels.DarkAscentPlayerRole)
                let! playerRole =
                    task {
                        if pRoleO.IsNone then
                            let rp = RoleProperties(Name = Channels.DarkAscentPlayerRole, Mentionable = Nullable(true), Hoist = Nullable(true))
                            rp.Color <- Color(170uy, 170uy, 150uy)
                            Log.Information($"Adding...{rp.Name}")
                            let! r = arg.Guild.CreateRoleAsync(rp)
                            return r
                        else
                            return pRoleO.Value.Value
                    }
                do! arg.Guild.GetUsersAsync()
                    |> TaskSeq.iterAsync(fun u ->
                        match db.ConfirmedUserByDiscordId u.Id with
                        | Ok b ->
                            if b then
                                task {
                                    if u.RoleIds |> Seq.exists(fun r -> r = playerRole.Id) |> not then
                                        try
                                            let! v = arg.Guild.AddUserRoleAsync(u.Id, playerRole.Id)
                                            ()
                                        with exn ->
                                            Log.Error(exn, $"Unable to add role to user inside {arg.Guild.Name} guild")
                                }
                            else task { () }
                        | Error err ->
                            task { Log.Error(err) }
                    )

                return null
            } |> ValueTask
