namespace DiscordBot

open NetCord.Rest
open NetCord.Gateway
open Serilog

[<RequireQualifiedAccess>]
module Channels =
    let [<Literal>] LogChannel = "dark-champs-ascent-logs"
    let [<Literal>] BattleChannel = "dark-champs-ascent-battle"
    let [<Literal>] ChatChannel = "dark-champs-ascent-chat"
    let [<Literal>] Category = "Dark Champs Ascent"

    let [<Literal>] DarkAscentPlayerRole = "DarkChampAscent Player"
    let [<Literal>] DarkAscentBotRole = "DarkChampAscent"

[<RequireQualifiedAccess>]
module Utils =
    open System
    open NetCord
    let sendMsgToChannel(channel:string) (client:GatewayClient)(mp:MessageProperties)(pin:bool) = task {
        try
            for guild in client.Cache.Guilds do
                match guild.Value.Channels |> Seq.tryFind(fun c -> c.Value.Name = channel) with
                | Some channel ->
                    let! rm = client.Rest.SendMessageAsync(channel.Key, mp)
                    if pin then
                        try
                            do! rm.PinAsync()
                        with exn ->
                            Log.Error(exn, "Unable to pin message")
                | None ->
                    Log.Error($"Can't find channel in guild {guild}")
        with exn ->
            Log.Error(exn, $"Unable to send a msg")
    }

    let createAndSendMsgToChannel(channel:string) (client:GatewayClient)(getMP:unit -> MessageProperties)(pin:bool) = task {
        try
            for guild in client.Cache.Guilds do
                match guild.Value.Channels |> Seq.tryFind(fun c -> c.Value.Name = channel) with
                | Some channel ->
                    let! rm = client.Rest.SendMessageAsync(channel.Key, getMP())
                    if pin then
                        try
                            do! rm.PinAsync()
                        with exn ->
                            Log.Error(exn, "Unable to pin message")
                | None ->
                    Log.Error($"Can't find channel in guild {guild}")
        with exn ->
            Log.Error(exn, $"Unable to send a msg")
    }

    let sendMsgToBattleChannel (client:GatewayClient) (mp:MessageProperties) =
        sendMsgToChannel Channels.BattleChannel client mp false
    
    let sendMsgToBattleChannelSilently (client:GatewayClient) (mp:MessageProperties) = task {
        let flags = mp.Flags
        mp.Flags <-
            if flags.HasValue then Nullable(flags.Value ||| MessageFlags.SuppressNotifications)
            else Nullable(MessageFlags.SuppressNotifications)
        do! sendMsgToChannel Channels.BattleChannel client mp false
        mp.Flags <- flags
    }

    let sendMsgToLogChannel (client:GatewayClient) (mp:MessageProperties) = task {
        let flags = mp.Flags
        mp.Flags <-
            if flags.HasValue then Nullable(flags.Value ||| MessageFlags.SuppressNotifications)
            else Nullable(MessageFlags.SuppressNotifications)
            
        do! sendMsgToChannel Channels.LogChannel client mp false
        mp.Flags <- flags
    }

    let sendMsgToLogChannelWithNotifications (client:GatewayClient) (mp:MessageProperties) =
        sendMsgToChannel Channels.LogChannel client mp false
