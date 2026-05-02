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

[<RequireQualifiedAccess>]
module MonsterImg =
    let DefaultName(mtype:MonsterType, msubtype:MonsterSubType) =
        mtype.ToString().ToLower() + "_" + msubtype.ToString().ToLower() + ".png"
    let DefaultFile(monster:Monster) =
        let filename = DefaultName (monster.MType, monster.MSubType)
        System.IO.Path.Combine("Assets", filename) |> MonsterImg.File

[<RequireQualifiedAccess>]
module MonstersComponent =
    let monsterComponents (monster:MonsterInfo) (title:string) (url:string) : IComponentContainerComponentProperties list =
        [
            TextDisplayProperties($"**{monster.Name} {title} **")
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            
            ComponentSectionProperties
                (ComponentSectionThumbnailProperties(
                    ComponentMediaProperties(url)),
                [
                    TextDisplayProperties(monster.Description)
                ])
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            TextDisplayProperties(xp monster.XP)
            TextDisplayProperties(health monster.Stat.Health)
            TextDisplayProperties(magic monster.Stat.Magic)
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            yield! toTable2 monster.Stat |> List.map TextDisplayProperties |> Seq.cast
        ]

    let monsterComponent (monster:MonsterInfo) (title:string) (url:string) =
        ComponentContainerProperties(monsterComponents monster title url)

    let monsterAttachnment (name:string) (mimg:MonsterImg) =
        let filename = match mimg with | MonsterImg.File fn -> fn
        let bytes = System.IO.File.ReadAllBytes(filename)
        let imageStream = new System.IO.MemoryStream(bytes)
        AttachmentProperties(name, imageStream)

[<RequireQualifiedAccess>]
module BattleComponent =
    let champJoinRoundComponent (name:string) (ipfs:string) =
        ComponentContainerProperties(
            [ ComponentSectionProperties(
                    ComponentSectionThumbnailProperties(
                        ComponentMediaProperties($"https://ipfs.dark-coin.io/ipfs/{ipfs}")),
                    [ TextDisplayProperties($"**{name}**")
                      TextDisplayProperties("joined round!") ]) ]) :> IMessageComponentProperties

    let roundCard (roundId:int64) (rewards:decimal) =
        ComponentContainerProperties([
            TextDisplayProperties($"Round {roundId} has started!")
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            TextDisplayProperties($" {Emoj.Coin} Rewards: {rewards} {Emoj.Coin}")
        ])

    let battleResults(br:BattleResult) (names:Map<uint64, string>) (lvlsStat: Map<uint64, Stat>) (boosts:Map<uint64, Stat>)=
        let movesComponent =
            ComponentContainerProperties([
                TextDisplayProperties($"** Actions **")
                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            
                yield! br.ChampsMoveAndXp |> Seq.map(fun kv ->
                    let name = names.[kv.Key]
                    let move, xp = kv.Value
                    TextDisplayProperties($"{Display.performedMoveDiscord move name br.MonsterChar.Name} (+{xp} {Emoj.Gem})") :> IComponentContainerComponentProperties
                )
        ])
    
        let monsterActionsComponent =
            match br.MonsterPM with
            | Some pm ->
                ComponentContainerProperties([
                    TextDisplayProperties($"""** Monster Action: ** {Display.performedMoveDiscord pm br.MonsterChar.Name ""}""")
                ])
                |> Some
            | None ->
                if br.MonsterActions.IsEmpty then None
                else
                    ComponentContainerProperties([
                        TextDisplayProperties("** Monster Actions **")
                        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            
                        yield! br.MonsterActions |> Seq.map(fun kv ->
                            let name = names.[kv.Key]
                            let move, xp = kv.Value
                            TextDisplayProperties($"{Display.performedMoveDiscord move br.MonsterChar.Name name} (+{xp} {Emoj.Gem})") :> IComponentContainerComponentProperties
                        )
                    ])
                    |> Some

        let totalRewardsComponent =
            let rewards = br.Rewards.SRewards
            ComponentContainerProperties([
                TextDisplayProperties($"** Rewards **")
                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                TextDisplayProperties($"** DAO **: {Display.toRound6StrD rewards.DAO} {Emoj.Coin}")                 
                TextDisplayProperties($"** Dev **: {Display.toRound6StrD rewards.Dev} {Emoj.Coin}")
                TextDisplayProperties($"** Reserve **: {Display.toRound6StrD rewards.Reserve} {Emoj.Coin}")
                TextDisplayProperties($"** Burn **: {Display.toRound6StrD rewards.Burn} {Emoj.Coin}")
                TextDisplayProperties($"** Champs **: {Display.toRound6StrD br.Rewards.ChampsTotal} {Emoj.Coin}")
            ])

        // ToDo: split by group with 30 champs
        let champRewardsComponent =
            let rewards = br.Rewards
            ComponentContainerProperties([
                TextDisplayProperties($"** Champ Rewards **")
                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                yield! rewards.Champs |> List.map(fun r ->
                    let name = names.[r.ChampId]
                    TextDisplayProperties($"{name}: {Display.toRound6StrD r.Reward} {Emoj.Coin}") :> IComponentContainerComponentProperties
                )
            ])

        let defeatedChampsComponent =
            if br.DeadChamps.IsEmpty then None
            else
                ComponentContainerProperties([
                    TextDisplayProperties($"** Defeated Champs **")
                    ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                    yield! br.DeadChamps |> List.map(fun r ->
                        TextDisplayProperties($"{names.[r]}") :> IComponentContainerComponentProperties
                    )
                ]) |> Some
        let monster = br.MonsterChar.Monster
        let monsterLvl = Levels.getLvlByXp br.MonsterChar.XP
        let monsterLvlStats = Monster.getMonsterStatsByLvl(monster.MType, monster.MSubType, monsterLvl)
        let mstat' = monsterLvlStats + br.MonsterChar.Stat
        let stats =
            ComponentContainerProperties([
                TextDisplayProperties($"** Stats **")
                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                TextDisplayProperties($"{br.MonsterChar.Name}: {mstat'.Health} {Emoj.Health} {mstat'.Magic} {Emoj.Magic}") :> IComponentContainerComponentProperties
                ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                yield! br.ChampsFinalStat |> Seq.map(fun kv ->
                    let name = names.[kv.Key]
                    let lStat = Map.tryFind kv.Key lvlsStat |> Option.defaultValue (Stat.Zero)
                    let bStat = Map.tryFind kv.Key boosts |> Option.defaultValue (Stat.Zero)
                    let stat = kv.Value + lStat + bStat
                    TextDisplayProperties($"{name}: {stat.Health} {Emoj.Health} {stat.Magic} {Emoj.Magic}") :> IComponentContainerComponentProperties
                )
            ])
    
        let defeatedMonsterComponent =
            if br.MonsterDefeater.IsNone then None
            else
                ComponentContainerProperties([
                    TextDisplayProperties($"{Emoj.TaDa} {Emoj.TaDa} {names.[br.MonsterDefeater.Value]} finished {br.MonsterChar.Name} off!")
                ]) |> Some

        [
            yield movesComponent
            if monsterActionsComponent.IsSome then
                yield monsterActionsComponent.Value
            yield totalRewardsComponent
            yield champRewardsComponent
            if defeatedChampsComponent.IsSome then
                yield defeatedChampsComponent.Value
            if defeatedMonsterComponent.IsSome then
                yield defeatedMonsterComponent.Value
            yield stats
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
