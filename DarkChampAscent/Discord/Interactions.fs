module DiscordBot.Interactions

open System
open Db
open NetCord.Services.ComponentInteractions
open NetCord.Rest

open Serilog
open GameLogic.Shop
open NetCord
open Display
open NetCord.Gateway
open Components
open GameLogic.Champs
open GameLogic.Battle
  
let donate (db:SqliteStorage) (context:ButtonInteractionContext) (str:string) = task {
    let! res =
        task {
            match Decimal.TryParse str with
            | true, d ->
                let r = db.Donate(context.User.Id, d)
                match r with
                | Ok () ->
                    let donationCard =
                        ComponentContainerProperties([
                            TextDisplayProperties($"{Emoj.Rocket} **New Donation!** {Emoj.Rocket}")
                            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            TextDisplayProperties($" {d} {Emoj.Coin} added to reward pool ")
                            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            TextDisplayProperties($"Thank you, {context.User}")
                        ])
                            
                    let newInGameDonationMessage =
                        MessageProperties()
                            .WithComponents([ donationCard ])
                            .WithFlags(MessageFlags.IsComponentsV2)
                            .WithAllowedMentions(AllowedMentionsProperties.None)

                    do! Utils.sendMsgToLogChannel context.Client newInGameDonationMessage
                    return Ok(())
                | Error err -> return Error(err)
            | false, _ ->
                Log.Error($"Can't parse {str} to decimal")
                return Error("Something went wrong")
        }

    let str = 
        match res with
        | Ok(()) -> "Thanks!"
        | Error err -> err
                
    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Components <- [ TextDisplayProperties(str) ]
    )

    return callback
}


let buyItem (db:SqliteStorage) (context:ButtonInteractionContext) (str:string) = task {
    let res =
        try
            let i = Int32.Parse str
            let item = enum<ShopItem> i 
            let r = db.BuyItem(context.User.Id, item, 1)
            r
        with exn ->
            Log.Error(exn, "buyItemCommand")
            Error("Something went wrong")

    let str = 
        match res with
        | Ok(()) -> "Done!"
        | Error err -> err
                
    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Components <- [ TextDisplayProperties(str) ]
    )

    return callback
}

let useItem (db:SqliteStorage) (context:ButtonInteractionContext) (str:string) = task {
    let r = db.GetUserChamps(context.User.Id)
                
    let callback = InteractionCallback.ModifyMessage(fun options ->
        match r with
        | Some champs ->
            if champs.IsEmpty then
                options.Components <- [ TextDisplayProperties("You do not have any champs") ]
            else
                let selectMenu = 
                    StringMenuProperties($"useselect:{str}",
                        champs |> List.map(fun c -> StringMenuSelectOptionProperties(c.Name, c.ID.ToString())),
                        Placeholder = "Choose an option")
                options.Components <- [ 
                    ComponentContainerProperties([
                        TextDisplayProperties("Champs")
                        selectMenu
                    ])
                ]
        | None -> options.Components <- [ TextDisplayProperties("Something went wrong") ]
    )

    return callback
}

let useSelectItem (db:SqliteStorage) (context:StringMenuInteractionContext) (str:string) = task {
    let res =
        try
            let i = Int32.Parse str
            let item = enum<ShopItem> i 
            let id = UInt64.Parse <| (context.SelectedValues |> Seq.tryHead |> Option.defaultValue "").Trim()
            let r = db.UseItemFromStorage(context.User.Id, item, id)
            r
        with exn ->
            Log.Error(exn, "useSelectItem")
            Error("Something went wrong")

    let str = 
        match res with
        | Ok(()) -> "Done!"
        | Error err -> err
                
    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Components <- [ TextDisplayProperties(str) ]
    )

    return callback
}

let lvlup (db:SqliteStorage) (context:StringMenuInteractionContext) (str:string) = task {
    let res =
        try
            let champId = UInt64.Parse str
            let characteristic = Enum.Parse<Characteristic>((context.SelectedValues |> Seq.tryHead |> Option.defaultValue "").Trim())
            if db.LevelUp(champId, characteristic) then Ok()
            else Error("Unexpected error")
        with exn ->
            Log.Error(exn, "lvlup")
            Error("Something went wrong")

    let str = 
        match res with
        | Ok(()) -> "Done!"
        | Error err -> err
                
    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Components <- [ TextDisplayProperties(str) ]
    )

    return callback
}

let lvlupbtn (context:ButtonInteractionContext) (champId:string) = task {
    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Flags <- Nullable(MessageFlags.Ephemeral ||| MessageFlags.IsComponentsV2)
        let selectMenu = 
            StringMenuProperties($"lvlup:{champId}", Enum.GetValues<Characteristic>() |> Array.map(fun c -> StringMenuSelectOptionProperties(c.ToString(), c.ToString())),
                Placeholder = "Choose an option")
                        
        options.Components <- [
            ComponentContainerProperties([
                TextDisplayProperties("Choose Characteristic to level up")
                selectMenu
            ])
        ]
    )

    return callback
}

let confirmRename (db:SqliteStorage) (context:ButtonInteractionContext) (oldName:string) (newName:string)= task {
    let! str = 
        task {
            match db.RenameChamp(context.User.Id, oldName, newName) with
            | Ok(()) ->
                try      
                    let logMessage = MessageProperties(Content = $"{oldName} changed name to {newName}")

                    do! Utils.sendMsgToLogChannel context.Client logMessage
                with exn ->
                    Log.Error(exn, "Send to discord")
                return "Done!"
            | Error err -> return err
        }
                
    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Components <- [ TextDisplayProperties(str) ]
    )

    return callback
}

let renameItem (db:SqliteStorage) (context:StringMenuInteractionContext) (str:string) = task {
    let darkCoinPrice = db.GetNumKey DbKeysNum.DarkCoinPrice
    let priceStr =
        match darkCoinPrice with
        | Some dcPrice -> $"{Math.Round(Shop.RenamePrice / dcPrice, 6)} {Emoj.Coin}"
        | None -> $"{Shop.RenamePrice} USDC"
    let oldName = context.SelectedValues |> Seq.tryHead |> Option.defaultValue ""
    let newName = str.Trim()
                
    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Components <- [
            ComponentContainerProperties([
                TextDisplayProperties($"You're about to rename {oldName} to {newName}. This operation costs {priceStr}")
                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                TextDisplayProperties($"This is irreversible action. Are you sure you want to perform this operation?")
                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                ActionRowProperties([ ButtonProperties($"confrename:{oldName}:{newName}", "Confirm", ButtonStyle.Success) ]
            )])
        ]
    )

    return callback
}

let select (db:SqliteStorage) (context:StringMenuInteractionContext) = task {
    let name = (context.SelectedValues |> Seq.tryHead |> Option.defaultValue "").Trim()
    let res =
        db.GetAssetIdByName(name)
        |> Result.bind(fun o ->
            match o with
            | Some aid ->
                match db.GetChampInfo (uint64 aid) with
                | Some info -> Ok info
                | None -> Error("Unexpected error")
            | None -> Error("Unexpected error"))
        
    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Components <- 
            match res with
            | Ok champ -> [ champDetailedComponent champ ]
            | Error str -> [ TextDisplayProperties(str) ]
    )

    return callback
}


let mselect (db:SqliteStorage) (context:StringMenuInteractionContext) = task {
    let res =
        let str = (context.SelectedValues |> Seq.tryHead |> Option.defaultValue "").Trim()
        match Int64.TryParse(str) with
        | true, id ->
            db.GetMonsterById id
        | false, _ ->
            Log.Error($"Unable to parse {str}")
            None
    
    let callback = InteractionCallback.ModifyMessage(fun options ->
        match res with
        | Some monster ->
            let cs = DiscordBot.Components.monsterComponent monster
            options.Components <- [ cs ]
            options.Attachments <- [ Components.monsterAttachnment monster ]
        | None ->
            options.Content <- $"Oh, no...something went wrong"
    )

    return callback
}

let actionselect (db:SqliteStorage) (context:StringMenuInteractionContext) (move:string) = task {
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
                Some(rar, rar |> db.PerformAction)
            | false, _ ->
                Log.Error($"Unable to parse {move}")
                None)
        
    let! str =
        match res with
        | Some (rar, r) ->
            match r with
            | Ok() ->
                let name = db.GetChampNameById rar.ChampId |> Option.defaultValue("")
                let mp = MessageProperties(Content = $"{name} joined round!")
                task { 
                    do! Utils.sendMsgToLogChannel context.Client mp
                    return "Action is recorded"
                }
            | Error str ->  task { return str }
        | None -> task { return "Something went wrong" }

    let callback = InteractionCallback.ModifyMessage(fun options ->
        options.Components <- [ TextDisplayProperties(str) ]
    )

    return callback
}
