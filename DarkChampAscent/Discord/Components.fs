module DiscordBot.Components

open NetCord.Rest
open NetCord
open Display
open GameLogic.Champs

let champComponent (champ:ChampInfo) =
    ComponentContainerProperties([
        TextDisplayProperties($"**{champ.Name} Info **")
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            
        ComponentSectionProperties
            (ComponentSectionThumbnailProperties(
                ComponentMediaProperties($"https://ipfs.dark-coin.io/ipfs/{champ.Ipfs}")),
            [
                TextDisplayProperties(xp champ.XP)
                TextDisplayProperties(health champ.Stat.Health)
                TextDisplayProperties(magic champ.Stat.Magic)
            ])
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
        TextDisplayProperties(balance champ.Balance)
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
        yield! toTable2 champ.Stat |> List.map TextDisplayProperties |> Seq.cast
    ])

let champDetailedComponent (champ:ChampInfo) =
    let traits =
        [
            Trait.Background
            Trait.Skin
            Trait.Weapon
            Trait.Magic
            Trait.Head
            Trait.Armour
            Trait.Extra
        ]

    let lvl = Levels.getLvlByXp champ.XP
    let freePoints = lvl - champ.LeveledChars
    let hasFreePoints = freePoints > 0UL
    
    let bs = champ.BoostStat |> Option.defaultValue Stat.Zero
    let ls = champ.LevelsStat |> Option.defaultValue Stat.Zero
    let fs = FullStat(champ.Stat, bs, ls)
    ComponentContainerProperties([
        TextDisplayProperties($"**{champ.Name} Info **")
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            
        ComponentSectionProperties
            (ComponentSectionThumbnailProperties(
                ComponentMediaProperties($"https://ipfs.dark-coin.io/ipfs/{champ.Ipfs}")),
            [
                TextDisplayProperties($"{xp champ.XP} ({lvl} lvl)")
                TextDisplayProperties(fs.Health)
                TextDisplayProperties(fs.Magic)
            ])
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
        TextDisplayProperties(balance champ.Balance)
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
        yield! fs.WithoutHM |> List.map TextDisplayProperties |> Seq.cast
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
        yield! 
            [
                if bs <> Stat.Zero || ls <> Stat.Zero then
                    yield ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small) :> IComponentProperties
                    if bs <> Stat.Zero && ls <> Stat.Zero then
                        yield TextDisplayProperties($"(*) - values gained from items bought in the shop")
                        yield TextDisplayProperties($"(**) - values gained from levels up")
                    elif bs <> Stat.Zero then
                        yield TextDisplayProperties($"(*) - boosted gained from items bought in the shop")
                    elif ls <> Stat.Zero then
                        yield TextDisplayProperties($"(*) - values gained from levels up")
                    else ()
                    yield ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            ] |> Seq.cast
        if hasFreePoints then
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            TextDisplayProperties($"{champ.Name} has {freePoints} free points. You can level up any characteristic")
            ActionRowProperties([ ButtonProperties($"lvlupbtn:{champ.ID}", "Level up", ButtonStyle.Primary) ])
        yield! traits |> List.map(fun t -> Display.getTraitInfo(t, champ.Traits) |> TextDisplayProperties) |> Seq.cast
    ])

open GameLogic.Monsters
open GameLogic.Battle

let monsterComponent (monster:MonsterInfo) =
    let imgUrl = $"https://raw.githubusercontent.com/FoggyFinder/DarkChampAscent/refs/heads/main/DarkChampAscent/Assets/{MonsterImg.DefaultName(monster.MType, monster.MSubType)}"
    ComponentContainerProperties([
        TextDisplayProperties($"**{monster.Name} Info **")
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            
        ComponentSectionProperties
            (ComponentSectionThumbnailProperties(
                ComponentMediaProperties(imgUrl)),
            [
                TextDisplayProperties(monster.Description)
            ])
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
        TextDisplayProperties(xp monster.XP)
        TextDisplayProperties(health monster.Stat.Health)
        TextDisplayProperties(magic monster.Stat.Magic)
        ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
        yield! toTable2 monster.Stat |> List.map TextDisplayProperties |> Seq.cast
        MediaGalleryProperties([
            MediaGalleryItemProperties(
                ComponentMediaProperties(imgUrl)    
            )
        ])
    ])

let monsterAttachnment (name:string) (monster:MonsterInfo) =
    let filename =
        match monster.Picture with
        | MonsterImg.File fn -> fn
    let bytes = System.IO.File.ReadAllBytes(filename)
    let imageStream = new System.IO.MemoryStream(bytes)
    AttachmentProperties(name, imageStream)

let monsterCreatedComponent (monster:MonsterInfo) (url:string)=
    ComponentContainerProperties([
        TextDisplayProperties($"** {monster.Name} is {Format.createMsg monster.MType}! **")
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
    ])

let customMonsterComponent (monster:MonsterInfo) (mid:int64) (url:string) =
    ComponentContainerProperties([
        TextDisplayProperties($"** {monster.Name} **")
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
        MediaGalleryProperties([
            MediaGalleryItemProperties(
                ComponentMediaProperties(url)    
            )
        ])
        ActionRowProperties([ ButtonProperties($"cmrename:{mid}:{monster.Name}", "Rename", ButtonStyle.Success) ])
    ])

let battleResults(br:BattleResult) (names:Map<uint64, string>) =
    let movesComponent =
        ComponentContainerProperties([
            TextDisplayProperties($"** Actions **")
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
                            
            yield! br.ChampsMoveAndXp |> Seq.map(fun kv ->
                let name = names.[kv.Key]
                let move, xp = kv.Value
                TextDisplayProperties($"{Display.performedMove move name br.MonsterChar.Name} (+{xp} {Emoj.Gem})") :> IComponentContainerComponentProperties
            )
    ])
    
    let monsterActionsComponent =
        match br.MonsterPM with
        | Some pm ->
            ComponentContainerProperties([
                TextDisplayProperties($"""** Monster Action: ** {Display.performedMove pm br.MonsterChar.Name ""}""")
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
                        TextDisplayProperties($"{Display.performedMove move br.MonsterChar.Name name} (+{xp} {Emoj.Gem})") :> IComponentContainerComponentProperties
                    )
                ])
                |> Some

    let totalRewardsComponent =
        let rewards = br.Rewards
        ComponentContainerProperties([
            TextDisplayProperties($"** Rewards **")
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            TextDisplayProperties($"** DAO **: {Display.toRound6StrD rewards.DAO} {Emoj.Coin}")                 
            TextDisplayProperties($"** Dev **: {Display.toRound6StrD rewards.Dev} {Emoj.Coin}")
            TextDisplayProperties($"** Reserve **: {Display.toRound6StrD rewards.Reserve} {Emoj.Coin}")
            TextDisplayProperties($"** Burn **: {Display.toRound6StrD rewards.Burn} {Emoj.Coin}")
            TextDisplayProperties($"** Champs **: {Display.toRound6StrD rewards.ChampsTotal} {Emoj.Coin}")
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

    let stats =
        ComponentContainerProperties([
            TextDisplayProperties($"** Basic stats (without boosts and levels) **")
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            TextDisplayProperties($"{br.MonsterChar.Name}: {br.MonsterChar.Stat.Health} {Emoj.Health} {br.MonsterChar.Stat.Magic} {Emoj.Magic}") :> IComponentContainerComponentProperties
            ComponentSeparatorProperties(Divider = true, Spacing = ComponentSeparatorSpacingSize.Small)
            yield! br.ChampsFinalStat |> Seq.map(fun kv ->
                let name = names.[kv.Key]
                let stat = kv.Value
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

let tnSend (title:string) (tn:string) =
    let uri = $"https://allo.info/tx/{tn}"
    ComponentContainerProperties([
        TextDisplayProperties(title)
        TextDisplayProperties(tn)
        ActionRowProperties(
            [
                LinkButtonProperties(uri, "Explorer")
            ]
        )
    ]) 

[<RequireQualifiedAccess>]
module Embeds =
    let monsterInfoEmbeds(monster:MonsterInfo) =
        // https://discordjs.guide/popular-topics/embeds.html#using-the-embed-constructor
        let ep =
            EmbedProperties()
                .WithTitle($"**{monster.Name}**")
                .WithImage(EmbedImageProperties($"attachment://image.png"))
                .WithDescription(monster.Description)
                .WithFields([
                    EmbedFieldProperties(Name = "Kind",
                        Value = Display.fromMonster(monster.MType, monster.MSubType))

                    EmbedFieldProperties(Name = $"{Emoj.Gem} XP",
                        Value = monster.XP.ToString(),
                        Inline = true)

                    EmbedFieldProperties(Name = $"{Emoj.Health} Health",
                        Value = string monster.Stat.Health,
                        Inline = true)

                    EmbedFieldProperties(Name = $"{Emoj.Magic} Magic",
                        Value = string monster.Stat.Magic,
                        Inline = true)


                    EmbedFieldProperties(Name = $"{Emoj.Accuracy} Accuracy",
                        Value = string monster.Stat.Accuracy,
                        Inline = true)

                    EmbedFieldProperties(Name = $"{Emoj.Luck} Luck",
                        Value = string monster.Stat.Luck,
                        Inline = true)


                    EmbedFieldProperties(Name = $"{Emoj.Attack} Attack",
                        Value = string monster.Stat.Attack,
                        Inline = true)

                    EmbedFieldProperties(Name = $"{Emoj.MagicAttack} MagicAttack",
                        Value = string monster.Stat.MagicAttack,
                        Inline = true)


                    EmbedFieldProperties(Name = $"{Emoj.Shield} Defense",
                        Value = string monster.Stat.Defense,
                        Inline = true)

                    EmbedFieldProperties(Name = $"{Emoj.MagicShield} MagicDefense",
                        Value = string monster.Stat.MagicDefense,
                        Inline = true)
                ])
                        
        let filename =
            match monster.Picture with
            | MonsterImg.File fn -> fn
        let bytes = System.IO.File.ReadAllBytes(filename)
        
        let imageStream = new System.IO.MemoryStream(bytes)
        [ ep ], [ AttachmentProperties("image.png", imageStream) ]

let walletComponent (name:string) (wallet:string) =
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