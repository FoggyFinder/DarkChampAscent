namespace DarkChampAscent.Api

[<RequireQualifiedAccess>]
type Tx =
    | Deposit of amount:decimal
    | Confirm of wallet:string * code:string

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
   | BattleParticipants
   | BattleStatusInfo

   | Shop
   | ShopBuyItem
   | Storage
   | StorageUseItem

   | Champs
   | ChampsUnderEffects
   | ChampsRename
   | ChampsLevelUp
   | ChampsRescan
   | ChampsDetail of id:uint64 option

   | Monsters
   | MonstersUnderEffects
   | MonstersRename
   | MonstersCreate
   | MonstersDetail of id:uint64 option

   | Requests
   | Donate

   | LeaderboardChamps
   | LeaderboardMonsters
   | LeaderboardDonaters
   | LeaderboardUnknownDonaters
   
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
       | Pattern.BattleParticipants -> "/api/battle/participants"
       | Pattern.BattleStatusInfo -> "/api/battle/statusinfo"

       | Pattern.Shop -> "/api/shop"
       | Pattern.ShopBuyItem -> "/api/shop/buy"
       | Pattern.Storage -> "/api/storage"
       | Pattern.StorageUseItem -> "/api/storage/use"

       | Pattern.Champs -> "/api/champs/my"
       | Pattern.ChampsUnderEffects -> "/api/champs/effects"
       | Pattern.ChampsRename -> "/api/champs/rename"
       | Pattern.ChampsLevelUp -> "/api/champs/levelup"
       | Pattern.ChampsRescan -> "/api/champs/rescan"
       | Pattern.ChampsDetail ido ->
            match ido with
            | Some id -> $"/api/champs/{id}"
            | None -> "/api/champs/{id:long}"
       | Pattern.Monsters -> "/api/monsters/my"
       | Pattern.MonstersUnderEffects -> "/api/monsters/effects"
       | Pattern.MonstersRename -> "/api/monsters/rename"
       | Pattern.MonstersCreate -> "/api/monsters/create"
       | Pattern.MonstersDetail ido ->
            match ido with
            | Some id -> $"/api/monsters/{id}"
            | None -> "/api/monsters/{id:long}"

       | Pattern.Requests -> "/api/requests/my"
       | Pattern.Donate -> "/api/donate"

       | Pattern.LeaderboardChamps -> "/api/leaderboard/champs"
       | Pattern.LeaderboardMonsters -> "/api/leaderboard/monsters"
       | Pattern.LeaderboardDonaters -> "/api/leaderboard/donaters"
       | Pattern.LeaderboardUnknownDonaters -> "/api/leaderboard/unknown-donaters"

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
       | Pattern.Shop
       | Pattern.Storage
       | Pattern.Champs
       | Pattern.ChampsUnderEffects
       | Pattern.ChampsDetail _
       | Pattern.Monsters
       | Pattern.MonstersUnderEffects
       | Pattern.MonstersDetail _
       | Pattern.Requests

       | Pattern.LeaderboardChamps
       | Pattern.LeaderboardMonsters
       | Pattern.LeaderboardDonaters
       | Pattern.LeaderboardUnknownDonaters

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
       | Pattern.ShopBuyItem
       | Pattern.StorageUseItem
       | Pattern.ChampsRename
       | Pattern.ChampsLevelUp
       | Pattern.ChampsRescan
       | Pattern.MonstersRename
       | Pattern.MonstersCreate
       | Pattern.Donate
       | Pattern.CreateTx
       | Pattern.SubmitTx
       | Pattern.VerifyTx
           -> Method.Post