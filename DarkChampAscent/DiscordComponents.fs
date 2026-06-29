module DiscordBot.Components

open NetCord.Rest
open NetCord
open Display
open GameLogic.Champs

let donationCard (d:decimal) (donater:string) (uriO:string option) =
    let title =
        match uriO with
        | Some uri -> $"[New Donation!]({uri})"
        | None -> "New Donation!"
    ComponentContainerProperties([
        TextDisplayProperties($"{Emoj.Rocket} **{title}** {Emoj.Rocket}")
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
        TextDisplayProperties($" {d} {Emoj.Coin} added to reward pool ")
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
        TextDisplayProperties($"Thank you, {donater}")
    ])

open GameLogic.Monsters
open GameLogic.Battle
open Types

[<RequireQualifiedAccess>]
module MonsterImg =
    let DefaultName(mtype:MonsterType, msubtype:MonsterSubType) =
        mtype.ToString().ToLower() + "_" + msubtype.ToString().ToLower() + ".png"
    let DefaultFile(monster:Monster) =
        let filename = DefaultName (monster.MType, monster.MSubType)
        System.IO.Path.Combine("Assets", filename) |> MonsterImg.File

[<RequireQualifiedAccess>]
module MonstersComponent =
    open Helpers
    let monsterComponents (monster:MonsterInfo) (title:string) (url:string) (createdBy: string option) : IComponentContainerComponentProperties list =
        let genTypeProp =
            match monster.GenType with
            | MonsterGenType.Generative -> TextDisplayProperties("Type: Generative")
            | MonsterGenType.NFTBased(assetId, website) ->
                let text =
                    $"Type: NFT based\n" +
                    $"ASA: [{assetId}](https://explorer.perawallet.app/asset/{assetId})\n"
                
                let text' =
                    if System.String.IsNullOrWhiteSpace(website) then text
                    else text + $"Project website: {website}"

                TextDisplayProperties(text')
        let isDefDesc = monster.Description.TrimStart().StartsWith("Not provided")
        [
            TextDisplayProperties($"**[{monster.Name}]({Links.monsterProfile monster.Id}) {title} **")
            match createdBy with
            | Some s -> TextDisplayProperties(s)
            | None -> ()
            if isDefDesc |> not then
                genTypeProp
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            let mediaComponent =
                if isDefDesc then genTypeProp
                else TextDisplayProperties(monster.Description)
            ComponentSectionProperties(ComponentSectionThumbnailProperties(ComponentMediaProperties(url)), [ mediaComponent ])

            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            TextDisplayProperties(xp monster.XP)
            TextDisplayProperties(health monster.Stat.Health)
            TextDisplayProperties(magic monster.Stat.Magic)
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            yield! toTable2 monster.Stat |> List.map TextDisplayProperties |> Seq.cast
        ]

    let monsterComponent (monster:MonsterInfo) (title:string) (url:string) (createdBy: string option) =
        ComponentContainerProperties(monsterComponents monster title url createdBy)

    let monsterAttachnment (name:string) (filename:string) =
        let bytes = System.IO.File.ReadAllBytes(filename)
        let imageStream = new System.IO.MemoryStream(bytes)
        AttachmentProperties(name, imageStream)

[<RequireQualifiedAccess>]
module BattleComponent =
    open Helpers
    let champJoinRoundComponent (name:string) (ipfs:string) (champId:uint64) =
        let components =
            [ TextDisplayProperties($"** [{name}]({Links.champProfile champId}) **")
              TextDisplayProperties("joined round!") ]
        ComponentContainerProperties(
            [ ComponentSectionProperties(
                ComponentSectionThumbnailProperties(
                    ComponentMediaProperties($"https://ipfs.dark-coin.io/ipfs/{ipfs}")),
                    components)]) :> IMessageComponentProperties

    let roundCard (roundId:int64) (rewards:decimal) =
        ComponentContainerProperties([
            TextDisplayProperties($"Round {roundId} has started!")
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            TextDisplayProperties($" {Emoj.Coin} Rewards: {rewards} {Emoj.Coin}")
        ])

    let battleResults(br:BattleResult) (names:Map<uint64, string>) (lvlsStat: Map<uint64, Stat>) (boosts:Map<uint64, Stat>)=
        let chunkSize = 25
        
        let movesComponents =
            br.ChampsMoveAndXp
            |> Seq.map(fun kv ->
                let name = names.[kv.Key]
                let move, xp = kv.Value
                TextDisplayProperties($"{Display.performedMoveDiscord move name br.MonsterChar.Name} (+{xp} {Emoj.Gem})") :> IComponentContainerComponentProperties)
            |> Seq.toList
            |> List.chunkBySize chunkSize
            |> List.map(fun chunk ->
                ComponentContainerProperties([
                    TextDisplayProperties($"** Actions **")
                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            
                    yield! chunk
                ]))
            
        
        let monsterActionsComponents =
            match br.MonsterPM with
            | Some pm ->
                [
                    ComponentContainerProperties([
                        TextDisplayProperties($"""** Monster Action: ** {Display.performedMoveDiscord pm br.MonsterChar.Name ""}""")
                    ])
                ]
            | None ->
                if br.MonsterActions.IsEmpty then [ ]
                else
                    br.MonsterActions
                    |> Seq.map(fun kv ->
                        let name = names.[kv.Key]
                        let move, xp = kv.Value
                        TextDisplayProperties($"{Display.performedMoveDiscord move br.MonsterChar.Name name} (+{xp} {Emoj.Gem})") :> IComponentContainerComponentProperties)
                    |> Seq.toList
                    |> List.chunkBySize chunkSize
                    |> List.map(fun chunk ->
                        ComponentContainerProperties([
                            TextDisplayProperties("** Monster Actions **")
                            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            
                            yield! chunk 
                        ]))

        let totalRewardsComponent =
            let rewards = br.Rewards.SRewards
            ComponentContainerProperties([
                TextDisplayProperties($"** Rewards **")
                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                TextDisplayProperties($"** DAO **: {Display.toRound6StrD rewards.DAO} {Emoj.Coin}")                 
                TextDisplayProperties($"** Dev **: {Display.toRound6StrD rewards.Dev} {Emoj.Coin}")
                TextDisplayProperties($"** Reserve **: {Display.toRound6StrD rewards.Reserve} {Emoj.Coin}")
                TextDisplayProperties($"** Champs **: {Display.toRound6StrD br.Rewards.ChampsTotal} {Emoj.Coin}")
                TextDisplayProperties($"** Monster **: {Display.toRound6StrD br.Rewards.Monster} {Emoj.Coin}")
            ])
        
        let champRewardsComponents =
            let rewards = br.Rewards
            rewards.Champs |> List.map(fun r ->
                let name = names.[r.ChampId]
                TextDisplayProperties($"{name}: {Display.toRound6StrD r.Reward} {Emoj.Coin}") :> IComponentContainerComponentProperties)
            |> List.chunkBySize chunkSize
            |> List.map(fun chunk ->
                ComponentContainerProperties([
                    TextDisplayProperties($"** Champ Rewards **")
                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                    yield! chunk
                ]))

        let defeatedChampsComponents =
            if br.DeadChamps.IsEmpty then [ ]
            else
                br.DeadChamps
                |> List.map(fun r -> TextDisplayProperties($"{names.[r]}") :> IComponentContainerComponentProperties)
                |> List.chunkBySize chunkSize
                |> List.map(fun chunk ->
                    ComponentContainerProperties([
                        TextDisplayProperties($"** Defeated Champs **")
                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                        yield! chunk
                    ]))

        let monster = br.MonsterChar.Monster
        let monsterLvl = Levels.getLvlByXp br.MonsterChar.XP
        let monsterLvlStats = Monster.getMonsterStatsByLvl(monster.MType, monster.MSubType, monsterLvl)
        let mstat' = monsterLvlStats + br.MonsterChar.Stat
        let stats =
            br.ChampsFinalStat |> Seq.map(fun kv ->
                let name = names.[kv.Key]
                let lStat = Map.tryFind kv.Key lvlsStat |> Option.defaultValue (Stat.Zero)
                let bStat = Map.tryFind kv.Key boosts |> Option.defaultValue (Stat.Zero)
                let stat = kv.Value + lStat + bStat
                TextDisplayProperties($"{name}: {stat.Health} {Emoj.Health} {stat.Magic} {Emoj.Magic}") :> IComponentContainerComponentProperties)
            |> Seq.toList
            |> List.chunkBySize chunkSize
            |> List.map(fun chunk ->
                ComponentContainerProperties([
                    TextDisplayProperties($"** Stats **")
                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                    TextDisplayProperties($"{br.MonsterChar.Name}: {mstat'.Health} {Emoj.Health} {mstat'.Magic} {Emoj.Magic}") :> IComponentContainerComponentProperties
                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                    yield! chunk
                ]))
    
        let defeatedMonsterComponent =
            if br.MonsterDefeater.IsNone then None
            else
                ComponentContainerProperties([
                    TextDisplayProperties($"{Emoj.TaDa} {Emoj.TaDa} {names.[br.MonsterDefeater.Value]} finished {br.MonsterChar.Name} off!")
                ]) |> Some

        [
            yield! movesComponents
            yield! monsterActionsComponents
            yield totalRewardsComponent
            yield! champRewardsComponents
            yield! defeatedChampsComponents
            if defeatedMonsterComponent.IsSome then
                yield defeatedMonsterComponent.Value
            yield! stats
        ]

[<RequireQualifiedAccess>]
module ChainComponent =
    let explorerComponent (uri:string) (title:string) (tn:string) =
        ComponentContainerProperties([
            TextDisplayProperties(title)
            TextDisplayProperties(tn)
            ActionRowProperties(
                [
                    LinkButtonProperties(uri, "Explorer")
                ]
            )
        ])

    let tnSend (title:string) (tn:string) =
        explorerComponent $"https://allo.info/tx/{tn}" title tn

    let walletComponent (name:string) (wallet:string) =
        explorerComponent $"https://allo.info/account/{wallet}" name wallet
