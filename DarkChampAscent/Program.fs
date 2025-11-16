module Test
open Serilog

Log.Logger <-
    (new LoggerConfiguration())
          .Enrich.FromLogContext()
          .WriteTo.Console()
          .WriteTo.File("log.txt", rollingInterval=RollingInterval.Day)
          .CreateLogger()

open GameLogic.Monsters
open Db
open Microsoft.Extensions.Hosting
open NetCord.Gateway
open NetCord.Hosting.Gateway
open NetCord.Hosting.Services.ApplicationCommands
open NetCord.Hosting.Services.ComponentInteractions
open NetCord.Services.ComponentInteractions
open Microsoft.Extensions.DependencyInjection
open DiscordBot.Services
open DiscordBot.Commands
open Conf
open NetCord

let cs = "Data Source=darkchampascentdb.sqlite; Cache=Shared;Foreign Keys = True"
let db = new Db.SqliteStorage(cs)

let builder = Host.CreateApplicationBuilder()

builder.Services
    .AddSingleton<SqliteStorage>(fun _ -> db)
    .AddHostedService<ConfirmationService>()
    .AddHostedService<UpdatePriceService>()
    .AddHostedService<DepositService>()
    .AddHostedService<TrackChampCfgService>()
    .AddHostedService<BattleService>()
    .AddHostedService<BackupService>()
    .AddHostedService<GenService>()
    .AddDiscordGateway(fun options ->
        options.Intents <- GatewayIntents.GuildMessages
                          ||| GatewayIntents.GuildMessageReactions
                          ||| GatewayIntents.Guilds
                          ||| GatewayIntents.GuildUsers
                          ||| GatewayIntents.GuildPresences
                          )
    .AddApplicationCommands()
    .AddGatewayHandlers(typeof<DiscordBot.GuildCreateHandler>.Assembly)
    .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>()
    .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
    |> ignore

builder.Logging.AddSerilog(dispose=true) |> ignore
builder.Services.AddOptions<Configuration>().BindConfiguration(nameof Configuration) |> ignore
builder.Services.AddOptions<WalletConfiguration>().BindConfiguration("Configuration:Wallet") |> ignore
builder.Services.AddOptions<ChainConfiguration>().BindConfiguration("Configuration:Chain") |> ignore
builder.Services.AddOptions<DbConfiguration>().BindConfiguration("Configuration:Db") |> ignore
builder.Services.AddOptions<GenConfiguration>().BindConfiguration("Configuration:Gen") |> ignore

let host = builder.Build()

open System
open DiscordBot

host
    .AddApplicationCommandModule(typeof<WalletModule>)
    .AddApplicationCommandModule(typeof<UserModule>)
    .AddApplicationCommandModule(typeof<ChampsModule>)
    .AddApplicationCommandModule(typeof<MonsterModule>)
    .AddApplicationCommandModule(typeof<BattleModule>)
    .AddApplicationCommandModule(typeof<TopModule>)
    .AddApplicationCommandModule(typeof<GeneralModule>)
    .AddApplicationCommandModule(typeof<CustomModule>)

    .AddComponentInteraction<StringMenuInteractionContext>("rename", Func<_,_,_,_>(Interactions.renameItem))
    .AddComponentInteraction<ButtonInteractionContext>("donate", Func<_,_,_,_>(Interactions.donate))
    .AddComponentInteraction<ButtonInteractionContext>("use", Func<_,_,_,_>(Interactions.useItem))
    .AddComponentInteraction<StringMenuInteractionContext>("useselect", Func<_,_,_,_>(Interactions.useSelectItem))
    .AddComponentInteraction<ButtonInteractionContext>("buy", Func<_,_,_,_>(Interactions.buyItem))
    .AddComponentInteraction<ButtonInteractionContext>("confrename", Func<_,_,_,_,_>(Interactions.confirmRename))
    .AddComponentInteraction<StringMenuInteractionContext>("select", Func<_,_,_>(Interactions.select))
    .AddComponentInteraction<StringMenuInteractionContext>("mselect", Func<_,_,_>(Interactions.mselect))
    .AddComponentInteraction<StringMenuInteractionContext>("cmselect", Func<_,_,_>(Interactions.cmselect))
    .AddComponentInteraction<StringMenuInteractionContext>("actionselect", Func<_,_,_,_>(Interactions.actionselect))
    .AddComponentInteraction<StringMenuInteractionContext>("lvlup", Func<_,_,_,_>(Interactions.lvlup))
    .AddComponentInteraction<ButtonInteractionContext>("lvlupbtn", Func<_,_,_>(Interactions.lvlupbtn))
    .AddComponentInteraction<ButtonInteractionContext>("mcreate", Func<_,_,_,_,_>(Interactions.mcreate))
    |> ignore

Monster.DefaultsMonsters
|> List.iter(db.CreateNewMonster >> ignore)

host.Run()

System.Console.ReadKey(true) |> ignore