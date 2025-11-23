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
open GameLogic.Limits
open Display
open System.Threading.Tasks
open Microsoft.Extensions.Options
open DiscordBot.Components
open Serilog
open System.Text
open DiscordBot

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
type WalletModule(db:SqliteStorage, options: IOptions<Conf.WalletConfiguration>) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()

    [<SubSlashCommand("register", "Register Algorand wallet, nfd not supported yet")>]
    member x.Register([<SlashCommandParameter(Name = "wallet", Description = "your wallets")>] wallet:string): Task =
        let wallet' = wallet.Trim()
        let res =
            if Blockchain.isValidAddress wallet' then
                db.RegisterNewWallet(x.Context.User.Id, wallet')
            else
                try
                    Log.Error($"{x.Context.User} attempts to register invalid {wallet'} address")
                with exn ->
                    Log.Error(exn, $"attempt to register {wallet'} address")
                Error($"Invalid Algorand address. Please, check correctness: {wallet'}")
        let str =
            match res with
            | Ok code -> $"Good, to confirm your wallet ({wallet'}) send 0-cost Algo tx to {options.Value.GameWallet} with following note: {code}"
            | Error err -> $"Oh, no...there was error: {err}"
        (fun (moptions:MessageOptions) -> moptions.Content <- str)
        |> ApplicationCommand.deferredMessage x.Context

    [<SubSlashCommand("wallets", "Get user wallets")>]
    member x.GetWallets() =
        let res = db.GetUserWallets(x.Context.User.Id)
        (fun (moptions:MessageOptions) ->
            match res with
            | Ok xs ->
                if xs.IsEmpty then
                    moptions.Content <- "No registered wallets found"
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
                            EmbedFieldProperties(Name = ar.Wallet, Value = $"Confirmed: {isConfirmed} | Active: {isActive}")
                        )
                    moptions.Embeds <- [ ep ]
            | Error err -> 
                moptions.Content <- $"Oh, no...there was error: {err}")
        |> ApplicationCommand.deferredMessage x.Context

    [<SubSlashCommand("gamewallets", "Get wallets used in game")>]
    member x.GetGameWallets() =
        let w = options.Value
        (fun (moptions:MessageOptions) ->
            moptions.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
            moptions.Components <- [
                    Components.walletComponent "Game Wallet" w.GameWallet
                    Components.walletComponent "Burn Wallet" w.BurnWallet
                    Components.walletComponent "DAO Wallet" w.DAOWallet
                    Components.walletComponent "Devs Wallet" w.DevsWallet
                    Components.walletComponent "Reserve Wallet" w.ReserveWallet
                    Components.walletComponent "Staking Wallet" w.StakingWallet
                ])
        |> ApplicationCommand.deferredMessage x.Context

type UserModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()

    [<SlashCommand("balance", "shows in-game balance")>]
    member x.GetBalance(): Task =
        let res = db.GetUserBalance(x.Context.User.Id)
        let str =
            match res with
            | Some d -> $"Your balance: {Emoj.Coin} {d} DarkCoins"
            | None -> $"Can't get balance."
        ApplicationCommand.deferredMessage x.Context (fun options -> options.Content <- str)

    [<SlashCommand("shop", "Shows shop")>]
    member x.ShowShop() =
        let res = db.GetShopItems()
        let priceO = db.GetNumKey(Db.DbKeysNum.DarkCoinPrice)
        (fun (moptions:MessageOptions) ->
            match res, priceO with
            | Some xs, Some price ->
                if xs.IsEmpty then
                    moptions.Content <- "No items found"
                else
                    moptions.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    let items =
                        xs |> List.collect(fun item ->
                            let sir = ShopItemRow(item, price)
                            let name = Display.fromShopItem item
                            let stablePrice = toRound2StrD <| Shop.getPrice item
                            let vStr = 
                                let v = Shop.getValue item
                                if v <> 0L then v.ToString() else ""

                            [
                                TextDisplayProperties($"{name} {stablePrice}({toRound2StrD sir.Price} {Emoj.Coin}) {sir.Duration} {Emoj.Rounds} {vStr}") :> IComponentContainerComponentProperties
                                ActionRowProperties([ ButtonProperties($"buy:{int item}", "Buy!", ButtonStyle.Primary) ])
                            ]
                        )
                            
                    moptions.Components <- [
                        TextDisplayProperties("**Storage**")
                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                        TextDisplayProperties($"Name Price (DarkCoins) Duration (rounds)") :> IMessageComponentProperties
                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                        ComponentContainerProperties(items)
                    ]
            | _ -> 
                moptions.Content <- $"Oh, no...there was error")
        |> ApplicationCommand.deferredMessage x.Context

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
        ApplicationCommand.deferredMessage x.Context (fun options -> options.Content <- str)

    [<SlashCommand("storage", "Shows user storage")>]
    member x.ShowStorage() =
        let res = db.GetUserStorage(x.Context.User.Id)
        (fun (moptions:MessageOptions) ->
            match res with
            | Some xs ->
                if xs.IsEmpty then
                    moptions.Content <- "No items found"
                else
                    moptions.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    let items =
                        xs |> List.collect(fun (item, amount) ->
                            [
                                TextDisplayProperties($"{Display.fromShopItem item} {amount}") :> IComponentContainerComponentProperties
                                ActionRowProperties([ ButtonProperties($"use:{int item}", "Use!", ButtonStyle.Primary) ])
                                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            ]
                        )
                            
                    moptions.Components <- [
                        TextDisplayProperties("**Storage**")
                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                        ComponentContainerProperties(items)
                    ]
                        
            | None -> 
                moptions.Content <- $"Oh, no...there was error")
        |> ApplicationCommand.deferredMessage x.Context

    [<SlashCommand("champs", "Shows user champs")>]
    member x.ShowChamps() =
        let res = db.GetUserChampsWithStats(x.Context.User.Id)
        (fun (moptions:MessageOptions) ->
            match res with
            | Some xs ->
                if xs.IsEmpty then
                    moptions.Content <- "No champs found"
                else
                    moptions.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    moptions.Components <-
                        xs
                        |> List.sortByDescending(fun c -> c.XP)
                        |> List.map(fun champ ->
                            ComponentContainerProperties([
                                ComponentSectionProperties
                                    (ComponentSectionThumbnailProperties(
                                        ComponentMediaProperties($"https://ipfs.dark-coin.io/ipfs/{champ.Ipfs}")),
                                    [
                                        TextDisplayProperties($"**{champ.Name}** ({xp champ.XP}) | {balance champ.Balance}")
                                    ])
                            ]))
            | None -> 
                moptions.Content <- $"Oh, no...there was error")
        |> ApplicationCommand.deferredMessage x.Context

    [<SlashCommand("champsundereffects", "Shows user's champs under effects")>]
    member x.ShowChampsUnderEffects() =
        let res =
            match db.GetLastRoundId() with
            | Some roundId -> db.GetUserChampsUnderEffect(x.Context.User.Id, roundId)
            | None -> Error("Unable to find round")
        (fun (moptions:MessageOptions) ->
            match res with
            | Ok xs ->
                if xs.IsEmpty then
                    moptions.Content <- "No champs currently are under effects"
                else
                    moptions.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    moptions.Components <-
                        xs
                        |> List.sortBy(fun ar -> ar.EndsAt)
                        |> List.map(fun ar ->
                            ComponentContainerProperties([
                                TextDisplayProperties($"**{ar.Name}** : {ar.Item} ({Emoj.Rounds} {ar.RoundsLeft} rounds)")
                            ]))
            | Error err ->
                moptions.Content <- $"Oh, no...there was error: {err}")
        |> ApplicationCommand.deferredMessage x.Context

    [<SlashCommand("donate", "donate to in-game rewards")>]
    member x.Donate([<SlashCommandParameter(Name = "amount", Description = "amount to donate")>] amount:decimal
    ): Task =
        ApplicationCommand.deferredMessage x.Context (fun options ->
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

    [<SlashCommand("rescan", "Fetches info on champs from user's registered wallets")>]
    member x.Rescan() =
        try
            Log.Information($"{x.Context.User} uses rescan command")
        with exn ->
            Log.Error(exn, $"rescan command")
        // ToDo: improve, return results to a user
        let updateChamps(wallets:string list) =
            match db.FindUserIdByDiscordId x.Context.User.Id with
            | Some userId ->
                wallets
                |> Seq.collect Blockchain.getChampsForWallet
                |> Seq.iter(fun assetId ->
                    let r = db.ChampExists assetId
                    match r with
                    | Ok b ->
                        if b |> not then
                            Blockchain.tryGetChampInfo assetId
                            |> Option.iter(fun (trait', ipfs) ->
                                db.AddOrInsertChamp ({
                                    Name = Blockchain.getAssetName assetId
                                    AssetId = assetId
                                    IPFS = ipfs
                                    UserId = uint64 userId
                                    Stats = Champ.generateStats trait'
                                    Traits = trait'
                                }) |> ignore)
                    | Error _ -> ())
            | None -> ()
        task {
            let callback = InteractionCallback.DeferredMessage(MessageFlags.Ephemeral);
            let! _ = x.Context.Interaction.SendResponseAsync(callback)
            match db.GetUserWallets(x.Context.User.Id) with
            | Ok xs ->
                let xs' = xs |> List.choose(fun ar -> if ar.IsConfirmed then Some ar.Wallet else None)
                let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                    if xs'.IsEmpty then
                        options.Content <- "No confirmed wallets found"
                    else
                        options.Content <- "Updating...")
                if xs'.IsEmpty |> not then
                    updateChamps xs'
                    let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                        options.Content <- "Done")
                    ()
            | Error err ->
                let! _ = x.Context.Interaction.ModifyResponseAsync(fun options ->
                        options.Content <- $"Oh, no...there was error: {err}")
                ()
            ()
        } :> Task

    [<SlashCommand("earnings", "returns amount of coins earned for specific range of rounds")>]
    member x.Earnings(
        [<SlashCommandParameter(Name = "start", Description = "from round (included)")>] startRound:uint64,
        [<SlashCommandParameter(Name = "end", Description = "to round (included)")>] endRound:uint64): Task =
        let res = db.GetUserEarnings(x.Context.User.Id, startRound, endRound)
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

[<SlashCommand("champ", "Champ command")>]
type ChampsModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()
    
    [<SubSlashCommand("show", "shows detailed info")>]
    member x.Show(
         [<SlashCommandParameter(Name = "assetid", Description = "ASA Id AssetId on Algorand")>] assetId:uint64
    ): Task =
        let res = db.GetChampInfo(assetId)
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match res with
            | Some champ ->
                options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                options.Components <- [ champComponent champ ]
            | None ->
                options.Content <- $"Oh, no...something went wrong")

    [<SubSlashCommand("card", "shows detailed info")>]
    member x.ShowCard(): Task =
        let champNames = db.GetUserChamps(x.Context.User.Id)
        ApplicationCommand.deferredMessage x.Context (fun options ->
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
                options.Content <- $"Oh, no...something went wrong")

    [<SubSlashCommand("rename", "rename champ")>]
    member x.Rename([<SlashCommandParameter(Name = "name", Description = "New unique name", MinLength = 1)>] name:string): Task =
        let champNames = db.GetUserChamps(x.Context.User.Id)
        ApplicationCommand.deferredMessage x.Context (fun options ->
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
                options.Content <- $"Oh, no...something went wrong")

    [<SubSlashCommand("top10", "shows top-10 champs by xp")>]
    member x.GetLeaderboard(): Task =
        let res = db.GetChampLeaderboard()
        ApplicationCommand.deferredMessage x.Context (fun options ->
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
                                        TextDisplayProperties($"**{champ.Name} ({xp <| uint64 champ.Xp})**")
                                    ])
                            ]))
            | Error err -> 
                options.Content <- $"Oh, no...there was error: {err}")

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
                | None -> Error "Unable to find monster")
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match res with
            | Ok monster ->
                options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                options.Components <- [ DiscordBot.Components.monsterComponent monster ]
            | Error err ->
                options.Content <- $"Oh, no...something went wrong: {err}"
            )

    [<SubSlashCommand("show", "shows monster info")>]
    member x.Show(
        [<SlashCommandParameter(Name = "mtype", Description = "action")>] mtype:MonsterType,
        [<SlashCommandParameter(Name = "msubtype", Description = "action")>] msubtype:MonsterSubType
    ): Task =
        let monstersO = db.GetMonsters(mtype, msubtype)
        ApplicationCommand.deferredMessage x.Context (fun options ->
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
                options.Content <- $"Oh, no...something went wrong")

    [<SubSlashCommand("top10", "shows top-10 monsters by xp")>]
    member x.GetLeaderboard(): Task =
        let res = db.GetMonsterLeaderboard()
        ApplicationCommand.deferredMessage x.Context (fun options ->
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
                                        TextDisplayProperties($"{Display.fullMonsterName(ar.Name, ar.MType, ar.MSubType)} | {xp <| uint64 ar.Xp}")
                                    ])
                            ]))
            | Error err -> 
                options.Content <- $"Oh, no...{err}")

    [<SubSlashCommand("undereffects", "Shows monsters under effects")>]
    member x.UnderEffects() =
        let res =
            match db.GetLastRoundId() with
            | Some roundId -> db.GetMonstersUnderEffect(roundId)
            | None -> Error("Unable to find round")
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match res with
            | Ok xs ->
                if xs.IsEmpty then
                    options.Content <- "No monsters are under effects currently"
                else
                    options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    options.Components <-
                        xs
                        |> List.sortBy(fun ar -> ar.EndsAt)
                        |> List.map(fun ar ->
                            ComponentContainerProperties([
                                TextDisplayProperties($"{Display.fullMonsterName(ar.Name, ar.MType, ar.MSubType)} : {ar.Item} ({Emoj.Rounds} {ar.RoundsLeft} rounds)")
                            ]))
            | Error err ->
                options.Content <- $"Oh, no...there was error: {err}")

    [<SubSlashCommand("create", "create custom monster")>]
    member x.Create(
        [<SlashCommandParameter(Name = "mtype", Description = "action")>] mtype:MonsterType,
        [<SlashCommandParameter(Name = "msubtype", Description = "action")>] msubtype:MonsterSubType
    ): Task =
        let priceO = db.GetNumKey(Db.DbKeysNum.DarkCoinPrice)
        let monstersCreatedR = db.MonstersByTypeSubtype(x.Context.User.Id, mtype, msubtype)
        let pendingRequestsR = db.UnfinishedRequestsByUser(x.Context.User.Id)
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match monstersCreatedR, pendingRequestsR, priceO with
            | Ok m, Ok r, Some dcPrice when m < Limits.CustomMonstersPerTypeSubtype && r < Limits.UnfinishedRequests ->
                let amount = Math.Round(Shop.GenMonsterPrice / dcPrice, 6)
                options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                options.Components <- [
                    ComponentContainerProperties([
                        TextDisplayProperties($"Are you sure you want to create custom monster for {amount} {Emoj.Coin}")
                        ActionRowProperties(
                            [
                                ButtonProperties($"mcreate:{mtype}:{msubtype}", "Confirm", ButtonStyle.Success)
                            ]
                        )
                        ]
                    )
                ]
            | Ok m, Ok r, Some _ ->
                let sb = StringBuilder()
                    
                if m >= Limits.CustomMonstersPerTypeSubtype then
                    sb.AppendLine $"Max amount of custom monsters for this type and subtype reached: {m} >= {Limits.CustomMonstersPerTypeSubtype}"
                    |> ignore
                    
                if r >= Limits.UnfinishedRequests then
                    sb.AppendLine $"Max amount of pending requests reached: {r} >= {Limits.UnfinishedRequests}"
                    |> ignore

                options.Content <- sb.ToString()
            | Error err1, _, _ -> 
                options.Content <- $"Oh, no...there was error: {err1}"
            | _, Error err2, _ -> 
                options.Content <- $"Oh, no...there was error: {err2}"
            | Ok _, Ok _, None ->
                options.Content <- $"Oh, no...can't get price, try again later")


[<SlashCommand("my", "Custom commands")>]
type CustomModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()

    [<SubSlashCommand("requests", "shows list of requests to create monsters")>]
    member x.Requests(): Task =
        let res = db.GetPendingUserRequests(x.Context.User.Id)
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match res with
            | Ok requests ->
                if requests.IsEmpty then
                    options.Content <- $"No pending requests found"
                else
                    options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    options.Components <- [ 
                        ComponentContainerProperties(
                        requests
                        |> List.sortByDescending(fun (_, dt, _) -> dt)
                        |> List.map(fun (id, dt, status) ->
                            TextDisplayProperties($"{id} : [{dt}] : {status}")
                        )
                        )
                    ]
            | Error err ->
                options.Content <- $"Oh, no...something went wrong: {err}")
    
    [<SubSlashCommand("monsters", "shows list of created monsters and allow to select one")>]
    member x.Monsters(): Task =
        let monstersR = db.GetUserMonsters(x.Context.User.Id)
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match monstersR with
            | Ok monsters ->
                if monsters.IsEmpty then
                    options.Content <- $"No monsters found"
                else
                    options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    let selectMenu = 
                        StringMenuProperties($"cmselect", monsters |> List.map(fun (id, name, mt, mst) ->
                            let label = $"{name} | {mt} | {mst}"
                            StringMenuSelectOptionProperties(label, id.ToString())),
                            Placeholder = "Choose an option")
                        
                    options.Components <- [
                        ComponentContainerProperties([
                            TextDisplayProperties("Select a monster")
                            selectMenu
                        ])
                    ]
            | Error err ->
                options.Content <- $"Oh, no...something went wrong: {err}")

    [<SubSlashCommand("monster", "shows list of created monsters and allow to select one")>]
    member x.Monster(
        [<SlashCommandParameter(Name = "mtype", Description = "action")>] mtype:MonsterType,
        [<SlashCommandParameter(Name = "msubtype", Description = "action")>] msubtype:MonsterSubType
    ): Task =
        let monstersR = db.FilterUserMonsters(x.Context.User.Id, mtype, msubtype)
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match monstersR with
            | Ok monsters ->
                if monsters.IsEmpty then
                    options.Content <- $"No monsters found with these filters"
                else
                    options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
                    let selectMenu = 
                        StringMenuProperties($"cmselect", monsters |> List.map(fun (id, name) ->
                            StringMenuSelectOptionProperties(name, id.ToString())),
                            Placeholder = "Choose an option")
                        
                    options.Components <- [
                        ComponentContainerProperties([
                            TextDisplayProperties("Select a monster")
                            selectMenu
                        ])
                    ]
            | Error err ->
                options.Content <- $"Oh, no...something went wrong: {err}")

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
        
        ApplicationCommand.deferredMessage x.Context (fun options ->
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
                options.Content <- err)

    [<SubSlashCommand("timetonextround", "returns approx time to next round")>]
    member x.TimeToNextRound() =
        let str =
            match db.GetLastRoundId() with
            | Some roundId ->
                match db.GetRoundStatus roundId with
                | Some status ->
                    if status = RoundStatus.Started then
                        match db.GetRoundTimestamp(roundId) with
                        | Some roundStared ->
                            let dt = DateTime.UtcNow
                            let diff = ((roundStared + Battle.RoundDuration) - dt)
                            if(diff.TotalMinutes > 0.0) then $"approx ~{diff} to the end of the round +2-3 min to process"
                            else "round is likely processing now"
                        | None -> "Something went wrong - unable to get round timestamp"
                    else
                        "New round should start in no time. Few minutes if no errors max"
                | None -> "Something went wrong - unable to get round status"
            | None -> "Something went wrong - unable to get round. Maybe there is no any?"
        
        ApplicationCommand.deferredMessage x.Context (fun options -> options.Content <- str)

    [<SubSlashCommand("params", "shows battle parameters")>]
    member x.BattleParams() =
        ApplicationCommand.deferredMessage x.Context (fun options -> 
            let ep =
                EmbedProperties(Title = "Params:")
                    .WithFields([
                        EmbedFieldProperties(Name = "Rounds in battle", Value = $"{Constants.RoundsInBattle}", Inline = true)
                        EmbedFieldProperties(Name = "Round duration", Value = $"{Battle.RoundDuration}", Inline = true)
                        EmbedFieldProperties(Name = "XP per level", Value = $"{Levels.XPPerLvl}", Inline = true)
                    ])
            options.Embeds <- [ ep ])

[<SlashCommand("top", "Leaderboard command")>]
type TopModule(db:SqliteStorage) =
    inherit ApplicationCommandModule<ApplicationCommandContext>()
  
    [<SubSlashCommand("ingamedonaters", "shows top-10 in-game donaters")>]
    member x.GetTopInGameDonaters(): Task =
        let res = db.GetTopInGameDonaters()
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match res with
            | Ok xs ->
                let topDonationsCard =
                    ComponentContainerProperties([
                        TextDisplayProperties($"{Emoj.Rocket} **Top-10 Donaters!** {Emoj.Rocket}")
                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                        yield!
                            xs |> List.mapi(fun i ar ->
                                TextDisplayProperties($"{i+1,-3}. <@{ar.DiscordId}> : {ar.Amount}") :> IComponentContainerComponentProperties
                            )
                    ])
                            
                options.Flags <- MessageFlags.IsComponentsV2 ||| MessageFlags.Ephemeral
                options.Components <- [ topDonationsCard ]
                options.AllowedMentions <- AllowedMentionsProperties.None
            | Error err -> 
                options.Content <- $"Oh, no...there was error: {err}")

  
    [<SubSlashCommand("donaters", "shows top-10 donaters")>]
    member x.GetTopDonaters(): Task =
        let res = db.GetTopDonaters()
        ApplicationCommand.deferredMessage x.Context (fun options ->
            match res with
            | Ok xs ->
                let topDonationsCard =
                    ComponentContainerProperties([
                        TextDisplayProperties($"{Emoj.Rocket} **Top-10 Donaters!** {Emoj.Rocket}")
                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                        yield!
                            xs |> List.mapi(fun i ar ->
                                TextDisplayProperties($"{i+1,-3}. {ar.Wallet} : {ar.Amount}") :> IComponentContainerComponentProperties
                            )
                    ])
                            
                options.Flags <- MessageFlags.IsComponentsV2 ||| MessageFlags.Ephemeral
                options.Components <- [ topDonationsCard ]
            | Error err -> 
                options.Content <- $"Oh, no...there was error: {err}")