namespace DarkChampAscent.Api

open GameLogic.Shop
open GameLogic.Monsters
open System

[<RequireQualifiedAccess>]
type Tx =
    | Donate of wallet:string * amount:decimal
    | Confirm of wallet:string * code:string

    | BuyItem of wallet:string * item:ShopItem * amount:uint
    | RenameChamp of wallet:string * champid:uint64 * newName:string
    | CreateCustomMonster of wallet:string * mtype:MonsterType * msubtype:MonsterSubType
    member x.Wallet =
        match x with
        | Donate (wallet, _)
        | Confirm (wallet, _)
        | BuyItem (wallet, _, _)
        | RenameChamp (wallet, _, _)
        | CreateCustomMonster (wallet, _, _) -> wallet
    member x.Note =
        match x with
        | Donate (_, amount) -> $"donate:{amount}"
        | Confirm (_, code) -> $"confirm:{code}"
        | BuyItem (_, item, amount) -> $"buyItem:{int item}:{amount}"
        | RenameChamp (_, champid, newName) -> $"rename:{champid}:{newName}"
        | CreateCustomMonster (_, mtype, msubtype) -> $"create:{int mtype}:{int msubtype}"
    static member TryParse (wallet:string) (str:string) =
        try
            if(String.IsNullOrWhiteSpace(str)) then None
            else
                let fields = str.Split(":") |> Array.map(fun s -> s.Trim())
                match fields with
                | [| "donate"; amountStr |] ->
                    match Decimal.TryParse amountStr with
                    | true, amount -> Tx.Donate(wallet, amount) |> Some
                    | _ -> None
                | [| "confirm"; code |] ->
                    Tx.Confirm(wallet, code) |> Some
                | [| "buyItem"; itemS; amountStr |] ->
                    match Int32.TryParse itemS, UInt32.TryParse amountStr with
                    | (true, item), (true, amount) -> 
                        Tx.BuyItem(wallet, enum<ShopItem> item, amount) |> Some
                    | _ -> None
                | [| "rename"; cIdS; name |] ->
                    match UInt64.TryParse cIdS with
                    | true, cId -> Tx.RenameChamp(wallet, cId, name) |> Some
                    | _ -> None
                | [| "create"; mtypeS; msubtypeS |] ->
                    match Int32.TryParse mtypeS, Int32.TryParse msubtypeS with
                    | (true, mtype), (true, msubtype) -> 
                        Tx.CreateCustomMonster(wallet, enum<MonsterType> mtype, enum<MonsterSubType> msubtype) |> Some
                    | _ -> None
                | _ -> None
        with _ -> None


[<RequireQualifiedAccess>]
type Method =
    | Get
    | Post

[<RequireQualifiedAccess>]
type Pattern =
   | Auth
   | AuthLoginDiscord
   | AuthDiscordCallback
   | AuthLogin
   | AuthRegister
   | AuthLogout
   | AuthWeb3Challenge
   | AuthWeb3Login

   | Account
   | AccountNewWallet

   | Battle
   | BattleJoin
   | BattleJoinGroup
   | BattleJoinAll
   | BattleParticipants
   | BattleRoundStatusInfo
   | BattleStatusInfo

   | Shop
   | Storage
   | StorageUseItem

   | Champs
   | ChampsUnderEffects
   | ChampsDefeated
   | ChampsLevelUp
   | ChampsDetail of id:uint64 option

   | Monsters
   | MonstersDefeated
   | MonstersRename
   | MonstersDetail of id:uint64 option

   | UsersDetail of id:uint64 option

   | Requests

   | LeaderboardChamps
   | LeaderboardMonsters
   | LeaderboardDonaters
   
   | Home
   | Stats

   | CreateTx
   | SubmitTx
   | VerifyTx

   member t.Str =
       match t with
       | Pattern.Auth -> "/auth/me"
       | Pattern.AuthLoginDiscord -> "/auth/login/discord"
       | Pattern.AuthDiscordCallback -> "/auth/discord/callback"
       | Pattern.AuthLogin -> "/auth/login"
       | Pattern.AuthRegister -> "/auth/register"
       | Pattern.AuthLogout -> "/auth/logout"
       | Pattern.AuthWeb3Challenge -> "/auth/web3/challenge"
       | Pattern.AuthWeb3Login -> "/auth/web3/login"

       | Pattern.Account -> "/api/account"
       | Pattern.AccountNewWallet -> "/api/account/wallet"

       | Pattern.Battle -> "/api/battle"
       | Pattern.BattleJoin -> "/api/battle/join"
       | Pattern.BattleJoinGroup -> "/api/battle/joingroup"
       | Pattern.BattleJoinAll -> "/api/battle/joinall"
       | Pattern.BattleParticipants -> "/api/battle/participants"
       | Pattern.BattleRoundStatusInfo -> "/api/battle/roundstatusinfo"
       | Pattern.BattleStatusInfo -> "/api/battle/statusinfo"

       | Pattern.Shop -> "/api/shop"
       | Pattern.Storage -> "/api/storage"
       | Pattern.StorageUseItem -> "/api/storage/use"

       | Pattern.Champs -> "/api/champs/my"
       | Pattern.ChampsDefeated -> "/api/champs/defeated"
       | Pattern.ChampsUnderEffects -> "/api/champs/effects"
       | Pattern.ChampsLevelUp -> "/api/champs/levelup"
       | Pattern.ChampsDetail ido ->
            match ido with
            | Some id -> $"/api/champs/{id}"
            | None -> "/api/champs/{id:long}"

       | Pattern.Monsters -> "/api/monsters/my"
       | Pattern.MonstersDefeated -> "/api/monsters/defeated"
       | Pattern.MonstersRename -> "/api/monsters/rename"
       | Pattern.MonstersDetail ido ->
            match ido with
            | Some id -> $"/api/monsters/{id}"
            | None -> "/api/monsters/{id:long}"

       | Pattern.UsersDetail ido ->
            match ido with
            | Some id -> $"/api/users/{id}"
            | None -> "/api/users/{id:long}"

       | Pattern.Requests -> "/api/requests/my"

       | Pattern.LeaderboardChamps -> "/api/leaderboard/champs"
       | Pattern.LeaderboardMonsters -> "/api/leaderboard/monsters"
       | Pattern.LeaderboardDonaters -> "/api/leaderboard/donaters"

       | Pattern.Home -> "/api/home"
       | Pattern.Stats -> "/api/stats"
       | Pattern.CreateTx -> "/api/tx/create"
       | Pattern.SubmitTx -> "/api/tx/submit"
       | Pattern.VerifyTx -> "/api/tx/verify"

   member t.Method =
       match t with
       | Pattern.Auth
       | Pattern.AuthLoginDiscord
       | Pattern.AuthDiscordCallback
       | Pattern.Account
       | Pattern.Battle
       | Pattern.BattleParticipants
       | Pattern.BattleStatusInfo
       | Pattern.BattleRoundStatusInfo
       | Pattern.Shop
       | Pattern.Storage
       | Pattern.Champs
       | Pattern.ChampsUnderEffects
       | Pattern.ChampsDefeated
       | Pattern.ChampsDetail _ 
       | Pattern.Monsters
       | Pattern.MonstersDefeated
       | Pattern.MonstersDetail _
       | Pattern.Requests

       | Pattern.UsersDetail _

       | Pattern.LeaderboardChamps
       | Pattern.LeaderboardMonsters
       | Pattern.LeaderboardDonaters

       | Pattern.Home
       | Pattern.Stats
           -> Method.Get

       | Pattern.AuthLogin
       | Pattern.AuthWeb3Login
       | Pattern.AuthWeb3Challenge
       | Pattern.AuthRegister
       | Pattern.AuthLogout
       | Pattern.AccountNewWallet
       | Pattern.BattleJoin
       | Pattern.BattleJoinGroup
       | Pattern.BattleJoinAll
       | Pattern.StorageUseItem
       | Pattern.ChampsLevelUp
       | Pattern.MonstersRename
       | Pattern.CreateTx
       | Pattern.SubmitTx
       | Pattern.VerifyTx
           -> Method.Post