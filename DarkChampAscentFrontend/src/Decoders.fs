module Decoders

open Fable.SimpleJson
open Types
open DTO
open GameLogic.Champs
open GameLogic.Battle
open GameLogic.Monsters
open GameLogic.Shop
open GameLogic.Effects
open DarkChampAscent.Account
open GameLogic.Rewards

let private str (j: Json) =
    match j with JString s -> Some s | _ -> None

let private num (j: Json) =
    match j with JNumber n -> Some n | _ -> None

let private bl (j: Json) =
    match j with JBool b -> Some b | _ -> None

let private field (key: string) (fields: Map<string, Json>) =
    Map.tryFind key fields

let private reqStr key m =
    field key m |> Option.bind str

let private reqNum key m =
    field key m |> Option.bind num

let private reqBool key m =
    field key m |> Option.bind bl

let private optNum key m =
    field key m
    |> Option.bind (function JNull -> None | j -> num j)

let private optStr key m =
    field key m
    |> Option.bind (function JNull -> None | j -> str j)

let private reqArr key m =
    field key m
    |> Option.bind (function JArray a -> Some a | _ -> None)
    |> Option.defaultValue []

let private asObj (j: Json) =
    match j with JObject m -> Some m | _ -> None

let private asInt (n: float) = int n
let private asInt64 (n: float) = int64 n
let private asUInt64 (n: float) = uint64 n
let private asDecimal (n: float) = decimal n

let private decodeEffect (j: Json) : Effect option =
    match j with JNumber n -> Some (enum<Effect> (int n)) | _ -> None

let private decodeMonsterType (j: Json) : MonsterType option =
    match j with JNumber n -> Some (enum<MonsterType> (int n)) | _ -> None

let private decodeMonsterSubType (j: Json) : MonsterSubType option =
    match j with JNumber n -> Some (enum<MonsterSubType> (int n)) | _ -> None

let private decodeShopItem (j: Json) : ShopItem option =
    match j with JNumber n -> Some (enum<ShopItem> (int n)) | _ -> None

let private decodeBattleStatus (j: Json) : BattleStatus option =
    match j with JNumber n -> Some (enum<BattleStatus> (int n)) | _ -> None

let private decodeGenStatus (j: Json) : GenStatus option =
    match j with JNumber n -> Some (enum<GenStatus> (int n)) | _ -> None

let private decodeBackground (j: Json) : Background option =
    match j with JNumber n -> Some (enum<Background> (int n)) | _ -> None

let private decodeSkin (j: Json) : Skin option =
    match j with JNumber n -> Some (enum<Skin> (int n)) | _ -> None

let private decodeWeapon (j: Json) : Weapon option =
    match j with JNumber n -> Some (enum<Weapon> (int n)) | _ -> None

let private decodeMagic (j: Json) : Magic option =
    match j with JNumber n -> Some (enum<Magic> (int n)) | _ -> None

let private decodeHead (j: Json) : Head option =
    match j with JNumber n -> Some (enum<Head> (int n)) | _ -> None

let private decodeArmour (j: Json) : Armour option =
    match j with JNumber n -> Some (enum<Armour> (int n)) | _ -> None

let private decodeExtra (j: Json) : Extra option =
    match j with JNumber n -> Some (enum<Extra> (int n)) | _ -> None

let private decodeStat (m: Map<string, Json>) : Stat option =
    match reqNum "Health" m, reqNum "Magic" m,
          reqNum "Accuracy" m, reqNum "Luck" m,
          reqNum "Attack" m, reqNum "MagicAttack" m,
          reqNum "Defense" m, reqNum "MagicDefense" m with
    | Some h, Some mg, Some ac, Some lu, Some at, Some ma, Some de, Some md ->
        Some {
            Health       = asInt64 h
            Magic        = asInt64 mg
            Accuracy     = asInt64 ac
            Luck         = asInt64 lu
            Attack       = asInt64 at
            MagicAttack  = asInt64 ma
            Defense      = asInt64 de
            MagicDefense = asInt64 md
        }
    | _ -> None

let private decodeTraits (m: Map<string, Json>) : Traits option =
    let get key decoder = field key m |> Option.bind decoder
    match get "Background" decodeBackground,
          get "Skin" decodeSkin,
          get "Weapon" decodeWeapon,
          get "Magic" decodeMagic,
          get "Head" decodeHead,
          get "Armour" decodeArmour,
          get "Extra" decodeExtra with
    | Some bg, Some sk, Some wp, Some mg, Some hd, Some ar, Some ex ->
        Some { Background = bg; Skin = sk; Weapon = wp; Magic = mg; Head = hd; Armour = ar; Extra = ex }
    | _ -> None

let private decodeMonsterImg (j: Json) : MonsterImg option =
    match j with
    | JString path -> Some (MonsterImg.File path)
    | _ -> None

let private decodeDiscordUser (m: Map<string, Json>) : DiscordUser option =
    match reqStr "Nickname" m, reqNum "DiscordId" m with
    | Some nick, Some did ->
        Some (DiscordUser(nick, asUInt64 did, optStr "PicRaw" m))
    | _ -> None

let private decodeCustomUser (m: Map<string, Json>) : CustomUser option =
    match reqStr "Nickname" m, reqNum "CustomId" m with
    | Some nick, Some cid ->
        Some (CustomUser(nick, asUInt64 cid))
    | _ -> None

let private decodeWeb3User (m: Map<string, Json>) : Web3User option =
    match reqStr "Wallet" m, reqNum "UserId" m with
    | Some wallet, Some userId -> Some (Web3User(wallet, asUInt64 userId))
    | _ -> None

let private decodeAccount (j: Json) : Account option =
    match j with
    | JObject fields ->
        match Map.tryFind "Discord" fields |> Option.bind asObj |> Option.bind decodeDiscordUser with
        | Some d -> Some (Account.Discord d)
        | None ->
            match Map.tryFind "Custom" fields |> Option.bind asObj |> Option.bind decodeCustomUser with
            | Some c -> Some (Account.Custom c)
            | None ->
                match Map.tryFind "Web3" fields |> Option.bind asObj |> Option.bind decodeWeb3User with
                | Some w -> Some (Account.Web3 w)
                | None -> None
    | _ -> None

let decodeAccountVal (j: Json) : Account option =
    decodeAccount j

let decodeWallet (m: Map<string, Json>) : Wallet option =
    match reqStr "Wallet" m, reqBool "IsConfirmed" m,
          reqStr "Code" m, reqBool "IsActive" m with
    | Some w, Some ic, Some c, Some ia ->
        Some (Wallet(w, ic, c, ia))
    | _ -> None

let decodeChampShortInfo (m: Map<string, Json>) : ChampShortInfo option =
    match reqNum "ID" m, reqStr "Name" m,
          reqStr "IPFS" m, reqNum "XP" m with
    | Some id, Some name, Some ipfs, Some xp ->
        Some (ChampShortInfo(asUInt64 id, name, ipfs, asUInt64 xp))
    | _ -> None

let decodeMonsterShortInfo (m: Map<string, Json>) : MonsterShortInfo option =
    match reqNum "ID" m, reqStr "Name" m,
          field "MType" m |> Option.bind decodeMonsterType,
          field "MSubType" m |> Option.bind decodeMonsterSubType,
          field "Pic" m |> Option.bind decodeMonsterImg,
          reqNum "XP" m with
    | Some id, Some name, Some mt, Some ms, Some pic, Some xp ->
        Some (MonsterShortInfo(asUInt64 id, name, mt, ms, pic, asUInt64 xp))
    | _ ->
        None

let decodeUserAccount (m: Map<string, Json>) : UserAccount option =
    match field "User" m |> Option.bind decodeAccount,
          reqNum "Balance" m,
          reqNum "Champs" m,
          reqNum "Monsters" m,
          reqNum "Requests" m with
    | Some user, Some bal, Some champs, Some monsters, Some requests ->
        let wallets =
            reqArr "Wallets" m
            |> List.choose (fun j -> j |> asObj |> Option.bind decodeWallet)
        Some (UserAccount(user, wallets, asDecimal bal, asInt champs, asInt monsters, asInt requests))
    | _ -> None

let decodeChampFullInfo (m: Map<string, Json>) : ChampFullInfo option =
    match reqNum "ID" m, reqNum "Assetid" m, reqStr "Name" m,
          reqStr "IPFS" m, reqNum "Balance" m with
    | Some id, Some aid, Some name, Some ipfs, Some bal ->
        Some (ChampFullInfo(asUInt64 id, asUInt64 aid, name, ipfs, asDecimal bal))
    | _ -> None

let decodeMonsterInfo (m: Map<string, Json>) : MonsterInfo option =
    match reqNum "XP" m, reqStr "Name" m, reqStr "Description" m,
          field "Picture" m |> Option.bind decodeMonsterImg,
          field "Stat" m |> Option.bind asObj |> Option.bind decodeStat,
          field "MType" m |> Option.bind decodeMonsterType,
          field "MSubType" m |> Option.bind decodeMonsterSubType with
    | Some xp, Some name, Some desc, Some pic, Some stat, Some mt, Some ms ->
        Some { XP = asUInt64 xp; Name = name; Description = desc
               Picture = pic; Stat = stat; MType = mt; MSubType = ms }
    | _ -> None

let decodeChampInfo (m: Map<string, Json>) : ChampInfo option =
    match reqNum "ID" m, reqStr "Name" m, reqStr "Ipfs" m,
          reqNum "Balance" m, reqNum "XP" m,
          field "Stat" m |> Option.bind asObj |> Option.bind decodeStat,
          field "Traits" m |> Option.bind asObj |> Option.bind decodeTraits with
    | Some id, Some name, Some ipfs, Some bal, Some xp, Some stat, Some traits ->
        let boostStat = field "BoostStat" m |> Option.bind (function JNull -> None | j -> asObj j |> Option.bind decodeStat)
        let levelsStat = field "LevelsStat" m |> Option.bind (function JNull -> None | j -> asObj j |> Option.bind decodeStat)
        let leveledChars = reqNum "LeveledChars" m |> Option.map asUInt64 |> Option.defaultValue 0UL
        Some {
            ID = asUInt64 id; Name = name; Ipfs = ipfs
            Balance = asDecimal bal; XP = asUInt64 xp
            Stat = stat; Traits = traits
            BoostStat = boostStat; LevelsStat = levelsStat
            LeveledChars = leveledChars
        }
    | _ -> None

let decodeGenRequest (m: Map<string, Json>) : GenRequest option =
    match reqNum "ID" m, reqStr "Timestamp" m,
          field "Status" m |> Option.bind decodeGenStatus with
    | Some id, Some ts, Some status ->
        match System.DateTime.TryParse(ts) with
        | true, dt -> Some (GenRequest(asInt64 id, dt, status))
        | _ -> None
    | _ -> None

let decodeDonater (j: Json) : Donater option =
    match j with
    | JObject m ->
        match field "Discord" m |> Option.bind num with
        | Some id -> Some (Donater.Discord (asUInt64 id))
        | None ->
            match field "Custom" m |> Option.bind (function JArray [JNumber id; JString name] -> Some (id, name) | _ -> None) with
            | Some (id, name) -> Some (Donater.Custom(asUInt64 id, name))
            | None ->
                match field "Unknown" m |> Option.bind str with
                | Some s -> Some (Donater.Unknown s)
                | None -> None
    | _ -> None

let decodeDonation (m: Map<string, Json>) : Donation option =
    match field "Donater" m |> Option.bind decodeDonater,
          reqNum "Amount" m with
    | Some donater, Some amount -> Some (Donation(donater, asDecimal amount))
    | _ -> None

let decodeChampUnderEffect (m: Map<string, Json>) : ChampUnderEffect option =
    match reqNum "ID" m, reqStr "Name" m, reqNum "EndsAt" m,
          field "Effect" m |> Option.bind decodeEffect,
          reqNum "RoundsLeft" m, reqStr "IPFS" m with
    | Some id, Some name, Some endsAt, Some effect, Some roundsLeft, Some ipfs ->
        Some (ChampUnderEffect(asInt64 id, name, asInt64 endsAt, effect, asInt64 roundsLeft, ipfs))
    | _ -> None

let decodeMonsterUnderEffect (m: Map<string, Json>) : MonsterUnderEffect option =
    match reqNum "ID" m, reqStr "Name" m,
          field "MType" m |> Option.bind decodeMonsterType,
          field "MSubType" m |> Option.bind decodeMonsterSubType,
          reqNum "EndsAt" m,
          field "Effect" m |> Option.bind decodeEffect,
          reqNum "RoundsLeft" m,
          field "Pic" m |> Option.bind decodeMonsterImg with
    | Some id, Some name, Some mt, Some ms, Some endsAt, Some effect, Some roundsLeft, Some pic ->
        Some (MonsterUnderEffect(asInt64 id, name, mt, ms, asInt64 endsAt, effect, asInt64 roundsLeft, pic))
    | _ -> None

let decodeAccountDTO (m: Map<string, Json>) : AccountDTO option =
    match field "Account" m |> Option.bind asObj |> Option.bind decodeUserAccount with
    | Some account ->
        let price = optNum "Price" m |> Option.map asDecimal
        Some (AccountDTO(account, price))
    | _ -> None

let decodeNonceDTO (m: Map<string, Json>) : NonceDTO option =
    match reqStr "TxnB64" m, reqStr "Nonce" m with
    | Some msg, Some nonce -> Some (NonceDTO(msg, nonce))
    | _ -> None 

let decodeShopDTO (m: Map<string, Json>) : ShopDTO option =
    match reqNum "Price" m with
    | Some price ->
        let items =
            reqArr "Items" m
            |> List.choose decodeShopItem
        let balance = optNum "Balance" m |> Option.map asDecimal
        Some (ShopDTO(items, asDecimal price, balance))
    | _ -> None

let decodeUserStorageDTO (m: Map<string, Json>) : UserStorageDTO option =
    let storage =
        reqArr "Storage" m
        |> List.choose (function
            | JArray [item; JNumber count] ->
                item |> decodeShopItem |> Option.map (fun si -> si, int count)
            | _ -> None)
    let champs =
        reqArr "Champs" m
        |> List.choose (fun j -> j |> asObj |> Option.bind decodeChampFullInfo)
    Some (UserStorageDTO(storage, champs))

let decodeChampDTO (m: Map<string, Json>) : ChampDTO option =
    match field "ChampInfo" m |> Option.bind asObj |> Option.bind decodeChampInfo,
          reqNum "Price" m with
    | Some champ, Some price ->
        let balance = optNum "Balance" m |> Option.map asDecimal
        Some (ChampDTO(champ, balance, asDecimal price))
    | _ -> None

let decodeMonsterDTO (m: Map<string, Json>) : MonsterDTO option =
    match field "Monster" m |> Option.bind asObj |> Option.bind decodeMonsterInfo,
          reqNum "ID" m, reqBool "IsOwned" m with
    | Some monster, Some id, Some owned ->
        Some (MonsterDTO(monster, asUInt64 id, owned))
    | _ -> None

let decodeUserMonstersDTO (m: Map<string, Json>) : UserMonstersDTO option =
    match reqNum "Price" m with
    | Some price ->
        let monsters =
            reqArr "Monsters" m
            |> List.choose (fun j -> j |> asObj |> Option.bind decodeMonsterShortInfo)
        let balance = optNum "UserBalance" m |> Option.map asDecimal
        Some (UserMonstersDTO(monsters, asDecimal price, balance))
    | _ -> None

let decodeRewardsPriceDTO (m: Map<string, Json>) : RewardsPriceDTO option =
    match reqNum "Rewards" m with
    | Some rewards ->
        let price = optNum "Price" m |> Option.map asDecimal
        Some (RewardsPriceDTO(asDecimal rewards, price))
    | _ -> None

let decodeStats (m: Map<string, Json>) : Stats option =
    let optU key = optNum key m |> Option.map asUInt64
    let optD key = optNum key m |> Option.map asDecimal
    Some (Stats(
        optU "Players", optU "ConfirmedPlayers",
        optU "Champs", optU "CustomMonsters",
        optU "Battles", optU "Rounds",
        optD "Rewards", optD "Burnt",
        optD "Dao", optD "Reserve",
        optD "Devs", optD "Staking"
    ))

let parseResult<'T> (decoder: Map<string, Json> -> 'T option) (json: string) : Result<'T, string> =
    let json =
        match SimpleJson.parseNative json with
        | JString inner -> inner
        | _ -> json
    match SimpleJson.parseNative json with
    | JObject fields ->
        match Map.tryFind "Ok" fields with
        | Some inner ->
            match inner |> asObj |> Option.bind decoder with
            | Some v -> Ok v
            | None -> Error $"Failed to decode Ok payload: {SimpleJson.toString inner}"
        | None ->
            match Map.tryFind "Error" fields with
            | Some (JString msg) -> Error msg
            | _ -> Error $"Unexpected response shape: {json}"
    | _ -> Error $"Failed to parse JSON: {json}"

let parseResultRaw<'T> (decoder: Json -> 'T option) (json: string) : Result<'T, string> =
    let json =
        match SimpleJson.parseNative json with
        | JString inner -> inner
        | _ -> json
    match SimpleJson.parseNative json with
    | JObject fields ->
        match Map.tryFind "Ok" fields with
        | Some inner ->
            match decoder inner with
            | Some v -> Ok v
            | None -> Error $"Failed to decode Ok payload: {SimpleJson.toString inner}"
        | None ->
            match Map.tryFind "Error" fields with
            | Some (JString msg) -> Error msg
            | _ -> Error $"Unexpected response: {json}"
    | _ -> Error $"Failed to parse JSON: {json}"

let parseUnit (json: string) : Result<unit, string> =
    parseResultRaw (function JNull -> Some () | JObject _ -> Some () | _ -> None) json

let parseBool (json: string) : Result<bool, string> =
    parseResultRaw (function JBool b -> Some b | _ -> None) json

let parseString (json: string) : Result<string, string> =
    parseResultRaw (function JString s -> Some s | _ -> None) json

let parseList<'T> (decoder: Map<string, Json> -> 'T option) (json: string) : Result<'T list, string> =
    parseResultRaw (function
        | JArray items -> items |> List.choose (fun j -> j |> asObj |> Option.bind decoder) |> Some
        | _ -> None) json

let parseChampList (json: string) : Result<(ChampShortInfo * decimal) list, string> =
    parseResultRaw (function
        | JArray items ->
            items
            |> List.choose (function
                | JArray [champJ; JNumber price] ->
                    champJ |> asObj |> Option.bind decodeChampShortInfo
                    |> Option.map (fun c -> c, decimal price)
                | _ -> None)
            |> Some
        | _ -> None) json

let parseDonaterList (json: string) : Result<{| name: string; amount: decimal |} list, string> =
    parseResultRaw (function
        | JArray items ->
            items
            |> List.choose (fun j ->
                j |> asObj |> Option.bind (fun m ->
                    match reqStr "name" m, reqNum "amount" m with
                    | Some n, Some a -> Some {| name = n; amount = decimal a |}
                    | _ -> None))
            |> Some
        | _ -> None) json

let private decodeDmg (j: Json) : Dmg option =
    match j with
    | JObject m ->
        match Map.tryFind "Default" m |> Option.bind num with
        | Some n -> Some (Dmg.Default (asUInt64 n))
        | None ->
            match Map.tryFind "Critical" m |> Option.bind num with
            | Some n -> Some (Dmg.Critical (asUInt64 n))
            | None ->
                match Map.tryFind "Missed" m with
                | Some _ -> Some (Dmg.Missed )
                | None -> None
    | _ -> None

let private decodePerformedMove (j: Json) : PerformedMove option =
    match j with
    | JObject m ->
        match Map.tryFind "Attack" m with
        | Some dmgJ -> decodeDmg dmgJ |> Option.map PerformedMove.Attack
        | None ->
        match Map.tryFind "MagicAttack" m with
        | Some (JArray [dmgJ; JNumber mp]) ->
            decodeDmg dmgJ |> Option.map (fun d -> PerformedMove.MagicAttack(d, asUInt64 mp))
        | _ ->
        match Map.tryFind "Shield" m |> Option.bind num with
        | Some n -> Some (PerformedMove.Shield (asUInt64 n))
        | None ->
        match Map.tryFind "MagicShield" m with
        | Some (JArray [JNumber a; JNumber b]) -> Some (PerformedMove.MagicShield(asUInt64 a, asUInt64 b))
        | _ ->
        match Map.tryFind "Heal" m with
        | Some (JArray [JNumber a; JNumber b]) -> Some (PerformedMove.Heal(asUInt64 a, asUInt64 b))
        | _ ->
        match Map.tryFind "Meditate" m |> Option.bind num with
        | Some n -> Some (PerformedMove.Meditate (asUInt64 n))
        | None -> None
    | _ -> None

let private decodeRoundParticipantChamp (j: Json) =
    match j with
    | JObject m ->
        match reqNum "ID" m, reqStr "Name" m, reqStr "IPFS" m with
        | Some id, Some name, Some ipfs ->
            Some (RoundParticipantChamp(uint64 id, name, ipfs))
        | _ -> None
    | _ -> None

let decodeChampInfoWithStat (m: Map<string, Json>) =
    let stat = (field "Stat" m) |> Option.bind asObj |> Option.bind decodeStat
    match reqNum "ID" m, reqStr "Name" m, reqStr "IPFS" m, stat with
    | Some id, Some name, Some ipfs, Some stat ->
        Some (ChampInfoWithStat(uint64 id, name, ipfs, stat))
    | _ -> None

let decodeActiveChamps (raw: string) =
    parseResultRaw (function
        | JNull -> Some None
        | JArray items ->
            items
            |> List.choose (fun j -> j |> asObj |> Option.bind decodeChampInfoWithStat)
            |> Some
            |> Some
        | _ -> None) raw

let private decodeRoundParticipantMonster (j: Json) =
    match j with
    | JObject m ->
        match reqNum "ID" m, reqStr "Name" m, (field "Img" m) |> Option.bind decodeMonsterImg with
        | Some id, Some name, Some ipfs ->
            Some (RoundParticipantMonster(uint64 id, name, ipfs))
        | _ -> None
    | _ -> None

let private decodePMMonster (j: Json) : PMMonster option =
    match j with
    | JObject m ->
        match field "PM" m |> Option.bind decodePerformedMove with
        | Some pm ->
            let target = field "Target" m |> Option.bind (function JNull -> None | j -> decodeRoundParticipantChamp j)
            Some (PMMonster(pm, target))
        | _ -> None
    | _ -> None

let private decodePMChamp (j: Json) : PMChamp option =
    match j with
    | JObject m ->
        match field "PM" m |> Option.bind decodePerformedMove,
              field "Champ" m |> Option.bind decodeRoundParticipantChamp,
              reqStr "Timestamp" m with
        | Some pm, Some champ, Some ts ->
            match System.DateTime.TryParse(ts) with
            | true, dt -> Some (PMChamp(pm, champ, dt))
            | _ -> None
        | _ -> None
    | _ -> None

let private decodePMDetail (j: Json) : PMDetail option =
    match j with
    | JObject m ->
        match field "Monster" m |> Option.bind decodePMMonster with
        | Some pmm -> Some (PMDetail.Monster pmm)
        | None ->
            match field "Champ" m |> Option.bind decodePMChamp with
            | Some pmc -> Some (PMDetail.Champ pmc)
            | None -> None
    | _ -> None

let private decodePMResult (j: Json) : PMResult option =
    match j with
    | JObject m ->
        match field "Detail" m |> Option.bind decodePMDetail with
        | Some detail ->
            let xp = reqNum "XP" m |> Option.map asUInt64 |> Option.defaultValue 0UL
            let rewards = optNum "Rewards" m |> Option.map asDecimal
            Some (PMResult(detail, xp, rewards))
        | _ -> None
    | _ -> None

let private decodeRoundReward (m: Map<string, Json>) : RoundReward option =
    match reqNum "DAO" m, reqNum "Reserve" m, reqNum "Burn" m,
          reqNum "Dev" m, reqNum "Staking" m, reqNum "Champs" m with
    | Some dao, Some reserve, Some burn, Some dev, Some staking, Some champs ->
        let sr = SpecialReward(asDecimal dao, asDecimal reserve, asDecimal burn, asDecimal dev, asDecimal staking)
        RoundReward(sr, asDecimal champs) |> Some
    | _ -> None

let private decodeRoundInfo (j: Json) : RoundInfo option =
    match j with
    | JObject m ->
        match reqNum "RoundId" m,
              field "Rewards" m |> Option.bind asObj |> Option.bind decodeRoundReward with
        | Some roundId, Some rewards ->
            let details =
                reqArr "Details" m
                |> List.choose decodePMResult
            let defeatedChamps =
                reqArr "DefeatedChamps" m
                |> List.choose (function JNumber n -> Some (asUInt64 n) | _ -> None)
            let monsterKiller =
                field "MonsterKiller" m
                |> Option.bind (function JNull -> None | JNumber n -> Some (asUInt64 n) | _ -> None)
            Some (RoundInfo(asUInt64 roundId, details, rewards, defeatedChamps, monsterKiller))
        | _ -> None
    | _ -> None

let private decodeBattleHistory (j: Json) : BattleHistory option =
    match j with
    | JObject m ->
        match field "Monster" m |> Option.bind decodeRoundParticipantMonster with
        | Some monster ->
            let rounds =
                reqArr "Rounds" m
                |> List.choose decodeRoundInfo
            Some (BattleHistory(rounds, monster))
        | _ -> None
    | _ -> None

let parseParticipants (json: string) : RoundParticipantChamp list option =
    match SimpleJson.parseNative json with
    | JArray items ->
        items
        |> List.choose decodeRoundParticipantChamp
        |> Some
    | _ -> None

let private decodeCurrentBattleInfo (m: Map<string, Json>) : CurrentBattleInfo option =
    match reqNum "BattleNum" m,
          field "BattleStatus" m |> Option.bind decodeBattleStatus,
          field "Monster" m |> Option.bind asObj |> Option.bind decodeMonsterInfo,
          reqNum "MonsterId" m with
    | Some bn, Some bs, Some monster, Some mid ->
        Some (CurrentBattleInfo(asUInt64 bn, bs, monster, asUInt64 mid))
    | _ -> None

let decodeBattleInfoDTO json : BattleInfoDTO option =
    match SimpleJson.tryParseNative json with
    | Some o ->
        match o with
        | JObject m ->
            let cbrO =
                field "CurrentBattleInfo" m
                |> Option.bind asObj
                |> Option.bind decodeCurrentBattleInfo

            let historyO =
                field "History" m
                |> Option.bind decodeBattleHistory

            match cbrO, historyO with
            | Some cbr, Some battleHistory ->
                BattleInfoDTO(cbr, battleHistory) |> Some
            | _ -> None
        | _ ->
            None
    | None ->
        None

let decodeRoundInfoDTO (json:string) =
    match SimpleJson.tryParseNative json with
    | Some o ->
        match o with
        | JObject m ->
            match reqNum "Status" m, reqNum "Round" m with
            | Some n, Some r ->
                let status = enum<RoundStatus> (int n)
                let started = optStr "RoundStarted" m |> Option.map (fun s -> System.DateTimeOffset.Parse(s + "Z").UtcDateTime)
                let round =  r |> asUInt64
                Some (RoundInfoDTO(status, started, round))
            | _ ->
                None
        | _ -> None
    | _ -> None