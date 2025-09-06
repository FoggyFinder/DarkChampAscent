namespace DiscordBot.Commands

open System
open GameLogic.Champs
open GameLogic.Monsters
open GameLogic.Battle
open Db
open NetCord.Services.ApplicationCommands
open NetCord.Rest

open NetCord
open GameLogic.Shop
open Display
open System.Threading.Tasks
open Microsoft.Extensions.Options
open DiscordBot.Components
open DiscordBot

[<SlashCommand("wallet", "Wallet command")>]
type WalletModule(db:SqliteStorage, options: IOptions<Conf.Configuration>) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()

    [<SubSlashCommand("register", "Register Algorand wallet, nfd not supported yet")>]
    member x.Register([<SlashCommandParameter(Name = "wallet", Description = "your wallets")>] wallet:string): Task =
        let res = db.RegisterNewWallet(x.Context.User.Id, wallet)
        let str =
            match res with
            | Ok code -> $"Good, to confirm your wallet ({wallet}) send 0-cost Algo tx to {options.Value.Wallet.GameWallet} with following note: {code}"
            | Error err -> $"Oh, no...there was error: {err}"
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! icr = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options -> options.Content <- str)
            ()
        } :> Task

    [<SubSlashCommand("wallets", "Get user wallets")>]
    member x.GetWallets() =
        let res = db.GetUserWallets(x.Context.User.Id)
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res with
                | Ok xs ->
                    if xs.IsEmpty then
                        options.Content <- "No registered wallets found"
                    else
                        let ep = EmbedProperties()
                        ep.Title <- "Wallets:"
                        ep.Fields <-
                            xs |> List.map(fun ar ->
                                let isConfirmed =
                                    let isConfStr = Display.fromBool ar.IsConfirmed
                                    if ar.IsConfirmed then isConfStr
                                    else $"{isConfStr} ({ar.Code})"
                                let isActive = Display.fromBool ar.IsActive
                                EmbedFieldProperties(Name = ar.Walllet, Value = $"Confirmed: {isConfirmed} | Active: {isActive}")
                            )
                        options.Embeds <- [ ep ]
                | Error err -> 
                    options.Content <- $"Oh, no...there was error: {err}")
            ()
        } :> Task

    [<SubSlashCommand("gamewallets", "Get wallets used in game")>]
    member x.GetGameWallets() =
        let w = options.Value.Wallet
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                let toComponent (name:string) (wallet:string) =
                    let uri = $"https://allo.info/account/{wallet}"
                    ComponentContainerProperties([
                        TextDisplayProperties(name)
                        TextDisplayProperties(wallet)
                        ActionRowProperties(
                            [
                                LinkButtonProperties(uri, "Explorer")
                            ]
                        )
                    ])        
                options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                
                options.Components <- [
                    toComponent "Game Wallet" w.GameWallet
                    toComponent "Burn Wallet" w.BurnWallet
                    toComponent "DAO Wallet" w.DAOWallet
                    toComponent "Devs Wallet" w.DevsWallet
                    toComponent "Reserve Wallet" w.ReserveWallet
                ]
            )
            ()
        } :> Task

type UserModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()

    [<SlashCommand("balance", "shows in-game balance")>]
    member x.GetBalance(): Task =
        let res = db.GetUserBalance(x.Context.User.Id)
        let str =
            match res with
            | Some d -> $"Your balance: {Emoj.Coin} {d} DarkCoins"
            | None -> $"Can't get balance."
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options -> options.Content <- str)
            ()
        } :> Task

    [<SlashCommand("shop", "Shows shop")>]
    member x.ShowShop() =
        let res = db.GetShopItems()
        let priceO = db.GetNumKey(Db.DbKeysNum.DarkCoinPrice)
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res, priceO with
                | Some xs, Some price ->
                    if xs.IsEmpty then
                        options.Content <- "No items found"
                    else
                        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                        let items =
                            xs |> List.collect(fun item ->
                                let sir = ShopItemRow(item, price)
                                let name = Display.fromShopItem item
                                let stablePrice = toRound2StrD <| Shop.getPrice item
                                let vStr = 
                                    let v = Shop.getValue item
                                    if v <> 0L then v.ToString() else ""

                                [
                                    TextDisplayProperties($"{name} {stablePrice}({toRound2StrD sir.Price} {Emoj.Coin}) {sir.Duration} {Emoj.Rounds} {vStr}") :> IComponentProperties
                                    ActionRowProperties([ ButtonProperties($"buy:{int item}", "Buy!", ButtonStyle.Primary) ])
                                ]
                            )
                            
                        options.Components <- [
                            TextDisplayProperties("**Storage**")
                            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            TextDisplayProperties($"Name Price (DarkCoins) Duration (rounds)") :> IComponentProperties
                            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            ComponentContainerProperties(items)
                        ]
                | _ -> 
                    options.Content <- $"Oh, no...there was error")
            ()
        } :> Task

    [<SlashCommand("buy", "buy item from shop")>]
    member x.BuyItem(
        [<SlashCommandParameter(Name = "item", Description = "shop item")>] shopItem:ShopItem,
        [<SlashCommandParameter(Name = "amount", Description = "amount to use")>] amount:int
    ): Task =
        let res = db.BuyItem(x.Context.User.Id, shopItem, amount)
        let str =
            match res with
            | Ok () -> $"Well done! Check your storage"
            | Error err -> $"Oh, no...there was error: {err}"
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options -> options.Content <- str)
            ()
        } :> Task

    [<SlashCommand("storage", "Shows user storage")>]
    member x.ShowStorage() =
        let res = db.GetUserStorage(x.Context.User.Id)
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res with
                | Some xs ->
                    if xs.IsEmpty then
                        options.Content <- "No items found"
                    else
                        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                        let items =
                            xs |> List.collect(fun (item, amount) ->
                                [
                                    TextDisplayProperties($"{Display.fromShopItem item} {amount}") :> IComponentProperties
                                    ActionRowProperties([ ButtonProperties($"use:{int item}", "Use!", ButtonStyle.Primary) ])
                                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                                ]
                            )
                            
                        options.Components <- [
                            TextDisplayProperties("**Storage**")
                            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            ComponentContainerProperties(items)
                        ]
                        
                | None -> 
                    options.Content <- $"Oh, no...there was error")
            ()
        } :> Task

    [<SlashCommand("champs", "Shows user champs")>]
    member x.ShowChamps() =
        let res = db.GetUserChampsWithStats(x.Context.User.Id)
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res with
                | Some xs ->
                    if xs.IsEmpty then
                        options.Content <- "No champs found"
                    else
                        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                        options.Components <-
                            xs
                            |> List.sortByDescending(fun c -> c.XP)
                            |> List.map(fun champ ->
                                ComponentContainerProperties([
                                    ComponentSectionProperties
                                        (ComponentSectionThumbnailProperties(
                                            ComponentMediaProperties($"https://ipfs.dark-coin.io/ipfs/{champ.Ipfs}")),
                                        [
                                            TextDisplayProperties($"**{champ.Name}** | {xp champ.XP} | {balance champ.Balance}")
                                        ])
                                ]))
                | None -> 
                    options.Content <- $"Oh, no...there was error")
            ()
        } :> Task

    [<SlashCommand("donate", "donate to in-game rewards")>]
    member x.Donate([<SlashCommandParameter(Name = "amount", Description = "amount to donate")>] amount:decimal
    ): Task =
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                options.Components <- [
                    ComponentContainerProperties([
                        TextDisplayProperties($"Are you sure you want to donate {amount} {Emoj.Coin}")
                        ActionRowProperties(
                            [
                                ButtonProperties($"donate:{amount}", "Confirm", ButtonStyle.Success)
                            ]
                        )
                     ]
                    )
                ]
                )
            ()
        } :> Task

[<SlashCommand("champ", "Champ command")>]
type ChampsModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()
    
    [<SubSlashCommand("show", "shows detailed info")>]
    member x.Show(
         [<SlashCommandParameter(Name = "assetid", Description = "ASA Id AssetId on Algorand")>] assetId:uint64
    ): Task =
        let res = db.GetChampInfo(assetId)
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res with
                | Some champ ->
                    options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    options.Components <- [ champComponent champ ]
                | None ->
                    options.Content <- $"Oh, no...something went wrong"
                )
            ()
        } :> Task

    [<SubSlashCommand("card", "shows detailed info")>]
    member x.ShowCard(): Task =
        let champNames = db.GetUserChamps(x.Context.User.Id)
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match champNames with
                | Some champs ->
                    if champs.IsEmpty then
                        options.Content <- $"You do not have any champs"
                    else
                        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                        let selectMenu = 
                            StringMenuProperties($"select", champs |> List.map(fun c -> StringMenuSelectOptionProperties(c.Name, c.Name)),
                                Placeholder = "Choose an option")
                        
                        options.Components <- [
                            ComponentContainerProperties([
                               TextDisplayProperties("Select a champ")
                               selectMenu
                            ])
                        ]
                | None ->
                    options.Content <- $"Oh, no...something went wrong"
                )
            ()
        } :> Task

    [<SubSlashCommand("rename", "rename champ")>]
    member x.Rename([<SlashCommandParameter(Name = "name", Description = "New unique name", MinLength = 1)>] name:string): Task =
        let champNames = db.GetUserChamps(x.Context.User.Id)
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match champNames with
                | Some champs ->
                    if champs.IsEmpty then
                        options.Content <- $"You do not have any champs"
                    else
                        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                        let selectMenu = 
                            StringMenuProperties($"rename:{name}", champs |> List.map(fun c -> StringMenuSelectOptionProperties(c.Name, c.Name)),
                                Placeholder = "Choose an option")
                        
                        options.Components <- [
                            ComponentContainerProperties([
                               TextDisplayProperties("Content")
                               selectMenu
                            ])
                        ]
                | None ->
                    options.Content <- $"Oh, no...something went wrong"
                )
            ()
        } :> Task

    [<SubSlashCommand("top10", "shows top-10 champs by xp")>]
    member x.GetLeaderboard(): Task =
        let res = db.GetChampLeaderboard()
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res with
                | Ok xs ->
                    if xs.IsEmpty then
                        options.Content <- "No champs found"
                    else
                        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                        options.Components <-
                            xs
                            |> List.sortByDescending(fun c -> c.Xp)
                            |> List.map(fun champ ->
                                ComponentContainerProperties([
                                    ComponentSectionProperties
                                        (ComponentSectionThumbnailProperties(
                                            ComponentMediaProperties($"https://ipfs.dark-coin.io/ipfs/{champ.IPFS}")),
                                        [
                                            TextDisplayProperties($"**{champ.Name}** | {xp <| uint64 champ.Xp}")
                                        ])
                                ]))
                | Error err -> 
                    options.Content <- $"Oh, no...there was error: {err}")
            ()
        } :> Task

[<SlashCommand("monster", "Monster command")>]
type MonsterModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()
  
    [<SubSlashCommand("showrandom", "shows random monster info")>]
    member x.Show(): Task =
        let res =
            db.GetRandomMonsterId()
            |> Result.bind(fun monsterId ->
                match db.GetMonsterById monsterId with
                | Some mi -> Ok mi
                | None -> Error "Unable to find monster"
                )
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res with
                | Ok monster ->
                    options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    options.Components <- [ DiscordBot.Components.monsterComponent monster ]
                | Error err ->
                    options.Content <- $"Oh, no...something went wrong: {err}"
                )
            ()
        } :> Task

    [<SubSlashCommand("show", "shows monster info")>]
    member x.Show(
        [<SlashCommandParameter(Name = "mtype", Description = "action")>] mtype:MonsterType,
        [<SlashCommandParameter(Name = "msubtype", Description = "action")>] msubtype:MonsterSubType
    ): Task =
        let monstersO = db.GetMonsters(mtype, msubtype)
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match monstersO with
                | Some monsters ->
                    if monsters.IsEmpty then
                        options.Content <- $"No monsters found with these filters"
                    else
                        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                        let selectMenu = 
                            StringMenuProperties($"mselect", monsters |> List.map(fun (id, name) -> StringMenuSelectOptionProperties(name, id.ToString())),
                                Placeholder = "Choose an option")
                        
                        options.Components <- [
                            ComponentContainerProperties([
                               TextDisplayProperties("Select a monster")
                               selectMenu
                            ])
                        ]
                | None ->
                    options.Content <- $"Oh, no...something went wrong"
                )
            ()
        } :> Task

    [<SubSlashCommand("top10", "shows top-10 monsters by xp")>]
    member x.GetLeaderboard(): Task =
        let res = db.GetMonsterLeaderboard()
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res with
                | Ok xs ->
                    if xs.IsEmpty then
                        options.Content <- "No monsters found"
                    else
                        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                        options.Components <-
                            xs
                            |> List.map(fun ar ->
                                let imgUrl = $"https://raw.githubusercontent.com/FoggyFinder/DarkChampAscent/refs/heads/main/DarkChampAscent/Assets/{MonsterImg.DefaultName(ar.MType, ar.MSubType)}"
                                ComponentContainerProperties([
                                    ComponentSectionProperties
                                        (ComponentSectionThumbnailProperties(
                                            ComponentMediaProperties(imgUrl)),
                                        [
                                            TextDisplayProperties($"**{ar.Name}** | {xp <| uint64 ar.Xp} |")
                                        ])
                                ]))
                | Error err -> 
                    options.Content <- $"Oh, no...{err}")
            ()
        } :> Task

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
                        db.GetActiveUserChamps(x.Context.User.Id, roundId)
                    else
                        Error("Please wait until new round is started")
                | None -> Error("Something went wrong - unable to get round status")
            | None -> Error("Something went wrong - unable to get round. Maybe there is no any?")
        
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral)
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match champs with
                | Ok xs ->
                    if xs.IsEmpty then
                        options.Content <- $"You do not have any champs left"
                    else
                        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                        let selectMenu = 
                            StringMenuProperties($"actionselect:{move}", xs |> List.map(fun c -> StringMenuSelectOptionProperties(c.Name, c.Id.ToString())),
                                Placeholder = "Choose an option")
                        
                        options.Components <- [
                            ComponentContainerProperties([
                               TextDisplayProperties("Content")
                               selectMenu
                            ])
                        ]
                | Error err ->
                    options.Content <- err
                )
            ()
        } :> Task

    [<SubSlashCommand("timetonextround", "returns approx time to next round")>]
    member x.TimeToNextRound() =
        let res =
            match db.GetLastRoundId() with
            | Some roundId ->
                match db.GetRoundStatus roundId with
                | Some status ->
                    if status = RoundStatus.Started then
                        match db.GetRoundTimestamp(roundId) with
                        | Some roundStared ->
                            let dt = DateTime.UtcNow
                            let diff = ((roundStared + Battle.RoundDuration) - dt)
                            if(diff.TotalMinutes > 0.0) then Ok($"approx ~{diff} to the end of the round +2-3 min to process")
                            else Ok("round is likely processing now")
                        | None -> Error("Something went wrong - unable to get round timestamp")
                    else
                        Error("New round should start in no time. Few minutes if no errors max")
                | None -> Error("Something went wrong - unable to get round status")
            | None -> Error("Something went wrong - unable to get round. Maybe there is no any?")
        
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral)
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                options.Content <- 
                    match res with
                    | Ok str -> str
                    | Error err -> err
                )
            ()
        } :> Task

[<SlashCommand("top", "Leaderboard command")>]
type TopModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()
  
    [<SubSlashCommand("ingamedonaters", "shows top-10 in-game donaters")>]
    member x.GetTopInGameDonaters(): Task =
        let res = db.GetTopInGameDonaters()
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res with
                | Ok xs ->
                    let topDonationsCard =
                        ComponentContainerProperties([
                            TextDisplayProperties($"{Emoj.Rocket} **Top-10 Donaters!** {Emoj.Rocket}")
                            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            yield!
                                xs |> List.mapi(fun i ar ->
                                    TextDisplayProperties($"{i+1,-3}. <@{ar.DiscordId}> : {ar.Amount}") :> IComponentProperties
                                )
                        ])
                            
                    options.Flags <- MessageFlags.IsComponentsV2 ||| MessageFlags.Ephemeral
                    options.Components <- [ topDonationsCard ]
                    options.AllowedMentions <- AllowedMentionsProperties.None
                | Error err -> 
                    options.Content <- $"Oh, no...there was error: {err}")
            ()
        } :> Task

  
    [<SubSlashCommand("donaters", "shows top-10 donaters")>]
    member x.GetTopDonaters(): Task =
        let res = db.GetTopDonaters()
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                match res with
                | Ok xs ->
                    let topDonationsCard =
                        ComponentContainerProperties([
                            TextDisplayProperties($"{Emoj.Rocket} **Top-10 Donaters!** {Emoj.Rocket}")
                            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            yield!
                                xs |> List.mapi(fun i ar ->
                                    TextDisplayProperties($"{i+1,-3}. {ar.Wallet} : {ar.Amount}") :> IComponentProperties
                                )
                        ])
                            
                    options.Flags <- MessageFlags.IsComponentsV2 ||| MessageFlags.Ephemeral
                    options.Components <- [ topDonationsCard ]
                | Error err -> 
                    options.Content <- $"Oh, no...there was error: {err}")
            ()
        } :> Task