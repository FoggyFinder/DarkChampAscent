module Api

open Fable.SimpleHttp
open GameLogic.Shop
open GameLogic.Monsters
open GameLogic.Champs
open DarkChampAscent.Api
open DarkChampAscent.Account
open Decoders

let baseUrl = 
    #if DEBUG
        "http://localhost:53656"
    #else
        ""
    #endif

let private fetchJson (url: string) (method: Method) (body: string option) =
    async {
        let! response =
            Http.request (baseUrl + url)
            |> Http.method (match method with | Method.Get -> HttpMethod.GET | Method.Post -> HttpMethod.POST)
            |> Http.header (Headers.contentType "application/x-www-form-urlencoded")
            |> Http.withCredentials true
            |> (match body with Some b -> Http.content (BodyContent.Text b) | None -> id)
            |> Http.send
        return response.responseText
    }

let private formBody (pairs: (string * string) list) =
    pairs
    |> List.map (fun (k, v) ->
        System.Uri.EscapeDataString(k) + "=" + System.Uri.EscapeDataString(v))
    |> String.concat "&"

let getMe () =
    async {
        let p = Pattern.Auth
        let! json = fetchJson p.Str p.Method None
        return parseResultRaw decodeAccountVal json
    }

let loginCustom (nickname: string) (password: string) =
    async {
        let p = Pattern.AuthLogin
        let! json =
            formBody [ "nickname", nickname; "password", password ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseResultRaw decodeAccountVal json
    }

let getWeb3Challenge (wallet: string) =
    async {
        let p = Pattern.AuthWeb3Challenge
        let! json =
            formBody [ 
                "wallet", wallet
            ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseResult decodeNonceDTO json
    }

let loginWeb3 (wallet: string) (signedTxnB64: string) (nonce: string) : Async<Result<Account, string>> =
    async {
        let p = Pattern.AuthWeb3Login
        let! json =
            formBody [
                "wallet",       wallet
                "signedTxnB64", signedTxnB64
                "nonce",        nonce
            ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseResultRaw decodeAccountVal json
    }

let register (nickname: string) (password: string) =
    async {
        let p = Pattern.AuthRegister
        let! json =
            formBody [ "nickname", nickname; "password", password ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseResultRaw decodeAccountVal json
    }

let logout () =
    async {
        let p = Pattern.AuthLogout
        let! json = fetchJson p.Str p.Method None
        return parseBool json
    }

let getAccount () =
    async {
        let p = Pattern.Account
        let! json = fetchJson p.Str p.Method None
        return parseResult decodeAccountDTO json
    }

let registerWallet (wallet: string) =
    async { 
        let p = Pattern.AccountNewWallet
        let! json =
            formBody [ "wallet", wallet ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseUnit json
    }

let getBattle () =
    async { 
        let p = Pattern.Battle
        let! json = fetchJson p.Str p.Method None
        return parseResult decodeBattleDTO json
    }

let joinBattle (champId: uint64) (move: Move) =
    async { 
        let p = Pattern.BattleJoin
        let! json =
            formBody [ "champ", string champId; "move", (int move).ToString()]
            |> Some
            |> fetchJson p.Str p.Method
        return parseUnit json
    }

let getShop () =
    async {
        let p = Pattern.Shop
        let! json = fetchJson p.Str p.Method None
        return parseResult decodeShopDTO json
    }

let buyItem (item: ShopItem) =
    async {
        let p = Pattern.ShopBuyItem
        let! json =
            formBody [ "shopitem", string item ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseUnit json
    }

let getStorage () =
    async {
        let p = Pattern.Storage
        let! json = fetchJson p.Str p.Method None
        return parseResult decodeUserStorageDTO json
    }

let useItem (item: ShopItem) (champId: uint64) =
    async {
        let p = Pattern.StorageUseItem
        let! json =
            formBody [ "useitem", string item; "champ", string champId ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseUnit json
    }

let getMyChamps () =
    async {
        let p = Pattern.Champs
        let! json = fetchJson p.Str p.Method None
        return parseChampList json
    }

let getChamp (id: uint64) =
    async {
        let p = Pattern.ChampsDetail (Some id)
        let! json = fetchJson p.Str p.Method None
        return parseResult decodeChampDTO json
    }

let getChampsEffects () =
    async {
        let p = Pattern.ChampsUnderEffects
        let! json = fetchJson p.Str p.Method None
        return parseList decodeChampUnderEffect json
    }

let renameChamp (oldName: string) (newName: string) (champId: uint64) =
    async {
        let p = Pattern.ChampsRename
        let! json =
            formBody [ "oldname", oldName; "newname", newName; "chmpId", string champId ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseUnit json
    }

let levelUp (champId: uint64) (characteristic: Characteristic) =
    async {
        let p = Pattern.ChampsLevelUp
        let! json =
            formBody [ "champ", string champId; "char", (int characteristic).ToString() ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseUnit json
    }

let rescan () =
    async {
        let p = Pattern.ChampsRescan
        let! json = fetchJson p.Str p.Method None
        return parseUnit json
    }

let getMyMonsters () =
    async {
        let p = Pattern.Monsters
        let! json = fetchJson p.Str p.Method None
        return parseResult decodeUserMonstersDTO json
    }

let getMonster (id: uint64) =
    async {
        let p = Pattern.MonstersDetail (Some id)
        let! json = fetchJson p.Str p.Method None
        return parseResult decodeMonsterDTO json
    }

let getMonstersEffects () =
    async {
        let p = Pattern.MonstersUnderEffects
        let! json = fetchJson p.Str p.Method None
        return parseList decodeMonsterUnderEffect json
    }

let renameMonster (monsterId: uint64) (newName: string) =
    async {
        let p = Pattern.MonstersRename
        let! json =
            formBody [ "mnstrid", string monsterId; "mnstrname", newName ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseUnit json
    }

let createMonster (mtype: MonsterType) (msubtype: MonsterSubType) =
    async {
        let p = Pattern.MonstersCreate
        let! json =
            formBody [ "mtype", (int mtype).ToString(); "msubtype", (int msubtype).ToString() ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseString json
    }

let getMyRequests () =
    async {
        let p = Pattern.Requests
        let! json = fetchJson p.Str p.Method None
        return parseList decodeGenRequest json
    }

let donate (amount: decimal) =
    async {
        let p = Pattern.Donate
        let! json =
            formBody [ "amount", string amount ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseUnit json
    }

let getTopChamps () =
    async {
        let p = Pattern.LeaderboardChamps
        let! json = fetchJson p.Str p.Method None
        return parseList decodeChampShortInfo json
    }

let getTopMonsters () =
    async {
        let p = Pattern.LeaderboardMonsters
        let! json = fetchJson p.Str p.Method None
        
        return parseList decodeMonsterShortInfo json
    }

let getTopDonaters () =
    async {
        let p = Pattern.LeaderboardDonaters
        let! json = fetchJson p.Str p.Method None
        return parseDonaterList json
    }

let getTopUnknownDonaters () =
    async {
        let p = Pattern.LeaderboardUnknownDonaters
        let! json = fetchJson p.Str p.Method None
        return parseList decodeDonation json
    }

let getHome () =
    async {
        let p = Pattern.Home
        let! json = fetchJson p.Str p.Method None
        return parseResult decodeRewardsPriceDTO json
    }

let getStats () =
    async {
        let p = Pattern.Stats
        let! json = fetchJson p.Str p.Method None
        return parseResult decodeStats json
    }

open Fable.SimpleJson
let serializeTx (tx: Tx) =
    match tx with
    | Tx.Deposit amount ->
        JObject (Map.ofList [
            "Case", JString "Deposit"
            "Fields", JArray [ JNumber (float amount) ]
        ])
    | Tx.Confirm (wallet, code) ->
        JObject (Map.ofList [
            "Case", JString "Confirm"
            "Fields", JArray [ JString wallet; JString code ]
        ])
    |> SimpleJson.toString

let createTx (tx: Tx) =
    async {
        let p = Pattern.CreateTx
        let! json =
            formBody [ 
                "tx", serializeTx tx
            ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseString json
    }

let submitTx (signedTxnB64: string) =
    async {
        let p = Pattern.SubmitTx
        let! json =
            formBody [
                "signedTxnB64", signedTxnB64
            ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseString json
    }

let verifyTx (signedTxnB64: string) =
    async {
        let p = Pattern.VerifyTx
        let! json =
            formBody [
                "signedTxnB64", signedTxnB64
            ]
            |> Some
            |> fetchJson p.Str p.Method
        return parseUnit json
    }
