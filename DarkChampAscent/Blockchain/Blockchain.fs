module Blockchain

open Algorand
open System
open Algorand.Indexer
open Newtonsoft.Json.Linq
open GameLogic.Champs
open Algorand.Algod

// https://algonode.io/api/#free-as-in--algorand-api-access
let ALGOD_API_ADDR = "https://mainnet-idx.algonode.cloud"
let ALGOD_FULL_API_ADDR = "https://mainnet-api.4160.nodely.dev"
let ALGOD_API_TOKEN = ""
let httpClient = HttpClientConfigurator.ConfigureHttpClient(ALGOD_API_ADDR, ALGOD_API_TOKEN)
let fullHttpClient = HttpClientConfigurator.ConfigureHttpClient(ALGOD_FULL_API_ADDR, ALGOD_API_TOKEN)

let lookUpApi = LookupApi(httpClient)
let searchApi = SearchApi(httpClient)
let algodApiInstance = DefaultApi(fullHttpClient)

let [<Literal>] DarkCoinChampsCreator = "L6VIKAHGH4D7XNH3CYCWKWWOHYPS3WYQM6HMIPNBVSYZWPNQ6OTS5VERQY"
let [<Literal>] DarkCoinAssetId = 1088771340UL
let [<Literal>] Algo6Decimals = 1000000M

let getApplAccountTransactions(applId:uint64, afterTimeOpt:DateTime option) =
    let afterTimeStr = afterTimeOpt |> Option.map(fun dt -> dt.ToString("yyyy-MM-dd")) |> Option.defaultValue ""
    let rec getTransactions (next:string) acc = 
        async {
            let! r = searchApi.searchForTransactionsAsync(applicationId=Nullable(applId),next=next,txType="appl", afterTime=afterTimeStr) |> Async.AwaitTask
            let acc' = acc |> Seq.append r.Transactions
            if System.String.IsNullOrWhiteSpace(r.NextToken) then
                return acc'
            else
                return! getTransactions r.NextToken acc'
        }
    getTransactions null Seq.empty |> Async.RunSynchronously

let getAssetMetadata(assetId:uint64) =
    async { 
        return! lookUpApi.lookupAssetByIDAsync(assetId) |> Async.AwaitTask
    } |> Async.RunSynchronously

let getAssetName(assetId:uint64) =
    getAssetMetadata(assetId).Asset.Params.Name

let getAssetHolder(assetId:uint64) =
    async {
        do! Async.Sleep(TimeSpan.FromSeconds(1L))
        try
            let! v = lookUpApi.lookupAssetBalancesAsync(assetId, currencyGreaterThan = Nullable(0UL)) |> Async.AwaitTask
            match v.Balances |> Seq.tryPick(fun b -> if b.Amount > 0UL then Some(b.Address) else None) with
            | Some wallet -> return Ok(wallet)
            | None -> return Error("Can't find wallet")
        with exn ->
            return Error(exn.ToString())
    } |> Async.RunSynchronously

let getAssets(wallet:string) =
    let rec getAssets (next:string) acc = 
        async { 
            let! r = lookUpApi.lookupAccountAssetsAsync(wallet, next=next) |> Async.AwaitTask
            let acc' = acc |> Seq.append r.Assets
            if System.String.IsNullOrWhiteSpace(r.NextToken) then
                return acc'
            else
                return! getAssets r.NextToken acc'
        }
    getAssets null Seq.empty |> Async.RunSynchronously

let getAccountCreatedAssets(wallet:string) =
    let rec getAssets (next:string) acc = 
        async { 
            let! r = lookUpApi.lookupAccountCreatedAssetsAsync(wallet, next=next) |> Async.AwaitTask
            let acc' = acc |> Seq.append r.Assets
            if System.String.IsNullOrWhiteSpace(r.NextToken) then
                return acc'
            else
                return! getAssets r.NextToken acc'
        }
    getAssets null Seq.empty |> Async.RunSynchronously

open Ipfs

let private ipfsFromACFG(acfg:Model.TransactionAssetConfig) =
    let addr = Algorand.Address(acfg.Params.Reserve)
    let cid = Cid(ContentType = "dag-pb", Version = 0, Hash = MultiHash("sha2-256", addr.Bytes))
    cid.ToString()

let tryGetIpfs (assetId: uint64) =
    async {
        let! d = lookUpApi.lookupAssetByIDAsync(assetId) |> Async.AwaitTask
        let! tr = lookUpApi.lookupAssetTransactionsAsync(assetId, txType = "acfg") |> Async.AwaitTask
        return
            tr.Transactions
            |> Seq.tryLast
            |> Option.map(fun tx -> ipfsFromACFG tx.AssetConfigTransaction)
    } |> Async.RunSynchronously

open GameLogic
let tryGetChampInfo(assetId:uint64) =
    async {
        let! d = lookUpApi.lookupAssetByIDAsync(assetId) |> Async.AwaitTask
        let! tr = lookUpApi.lookupAssetTransactionsAsync(assetId, txType = "acfg") |> Async.AwaitTask
        return
            tr.Transactions
            |> Seq.tryLast
            |> Option.map(fun tx ->
                let json = tx.Note |> System.Text.ASCIIEncoding.ASCII.GetString |> JObject.Parse
                let properties = json.["properties"]
                {
                    Armour = Trait.parseArmour(properties.Value<string>("Armour"))
                    Background = Trait.parseBackground(properties.Value<string>("Background"))
                    Extra = Trait.parseExtra(properties.Value<string>("Extra"))
                    Head = Trait.parseHead(properties.Value<string>("Head"))
                    Magic = Trait.parseMagic(properties.Value<string>("Magic"))
                    Skin = Trait.parseSkin(properties.Value<string>("Skin"))
                    Weapon = Trait.parseWeapon(properties.Value<string>("Weapon"))
                }, ipfsFromACFG tx.AssetConfigTransaction
            )
    } |> Async.RunSynchronously

let getDCChampAcfgTransactions(afterTimeOpt:DateTime option) = 
    let afterTimeStr = afterTimeOpt |> Option.map(fun dt -> dt.ToString("yyyy-MM-dd")) |> Option.defaultValue ""
    let rec getTransactions (next:string) acc = 
        async {
            let! r = lookUpApi.lookupAccountTransactionsAsync(DarkCoinChampsCreator, next = next, txType = "acfg", afterTime=afterTimeStr) |> Async.AwaitTask
            let acc' = acc |> Seq.append r.Transactions
            if System.String.IsNullOrWhiteSpace(r.NextToken) then
                return acc'
            else
                return! getTransactions r.NextToken acc'
        }
    getTransactions null Seq.empty |> Async.RunSynchronously
    |> Seq.choose(fun tx ->
        tx.AssetConfigTransaction.AssetId |> Option.ofNullable |> Option.bind(fun assetId ->
            try
                let str = tx.Note |> System.Text.ASCIIEncoding.ASCII.GetString
                let json = str |> JObject.Parse
                let properties = json.["properties"]
                let t = {
                        Armour = Trait.parseArmour(properties.Value<string>("Armour"))
                        Background = Trait.parseBackground(properties.Value<string>("Background"))
                        Extra = Trait.parseExtra(properties.Value<string>("Extra"))
                        Head = Trait.parseHead(properties.Value<string>("Head"))
                        Magic = Trait.parseMagic(properties.Value<string>("Magic"))
                        Skin = Trait.parseSkin(properties.Value<string>("Skin"))
                        Weapon = Trait.parseWeapon(properties.Value<string>("Weapon"))
                    }
                Some(assetId, ipfsFromACFG tx.AssetConfigTransaction, t)
            with _ -> None))

let allChamps = 
    lazy(getAccountCreatedAssets DarkCoinChampsCreator |> Seq.map(fun asset -> asset.Index) |> Set.ofSeq)

let getChampsForWallet wallet =
    printfn "get champs"
    let acmps' = allChamps.Value
    printfn "champs collected %A" acmps'.Count
    getAssets wallet
    |> Seq.choose(fun m ->
        if m.Amount > 0UL && acmps'.Contains m.AssetId 
        then Some m.AssetId else None)

let getAccountBalanceBalance(wallet:string, assetId: uint64) =
    async {
        let! v = lookUpApi.lookupAccountAssetsAsync(wallet, Nullable(assetId)) |> Async.AwaitTask
        return v.Assets |> Seq.tryPick(fun asset -> if asset.AssetId = assetId then Some(asset.Amount) else None)
    } |> Async.RunSynchronously

let getDarkCoinBalance(wallet:string) =
    try
        getAccountBalanceBalance(wallet, DarkCoinAssetId)
        |> Option.map(fun v -> decimal v / Algo6Decimals)
    with exp ->
        None

let getAssetTxsForAddress(wallet:string, assetId: Nullable<uint64>, afterTimeOpt:DateTime option) =
    let afterTimeStr = afterTimeOpt |> Option.map(fun dt -> dt.ToString("yyyy-MM-dd")) |> Option.defaultValue ""
    let rec getTransactions (next:string) acc = 
        async {
            let! r = lookUpApi.lookupAccountTransactionsAsync(wallet, assetId = assetId, next=next, afterTime=afterTimeStr) |> Async.AwaitTask
            let acc' = acc |> Seq.append r.Transactions
            if System.String.IsNullOrWhiteSpace(r.NextToken) then
                return acc'
            else
                return! getTransactions r.NextToken acc'
        }
    getTransactions null Seq.empty |> Async.RunSynchronously

/// returns (id, sender, amount)
let getDarkCoinDepositForWallet(wallet:string, afterTimeOpt:DateTime option) =
    getAssetTxsForAddress(wallet, Nullable(DarkCoinAssetId), afterTimeOpt)
    |> Seq.choose(fun tx ->
        if tx.AssetTransferTransaction <> null && tx.AssetTransferTransaction.Receiver = wallet && tx.AssetTransferTransaction.Amount > 0UL then
            Some(tx.Id, tx.Sender, decimal tx.AssetTransferTransaction.Amount / Algo6Decimals)
        else None)

/// returns (wallet, note)
let getNotesForWallet(wallet:string, afterTimeOpt:DateTime option) =
    getAssetTxsForAddress(wallet, Nullable(), afterTimeOpt)
    |> Seq.choose(fun tx ->
        if tx.PaymentTransaction <> null && tx.PaymentTransaction.Amount = 0UL then
            Some(tx.Sender, tx.Note)
        else None)

open Algorand.Algod.Model
open Algorand.Utils
open Algorand.Algod.Model.Transactions

let sendTx(keys:string, receiver:string, amount:uint64, assetId:uint64, note:string) =
    task {
        try
            let src = Account(keys)
            let! transParams = algodApiInstance.TransactionParamsAsync()
            let assetTransferTx =
                AssetTransferTransaction(
                    XferAsset = assetId,
                    AssetReceiver = Address(receiver),
                    AssetAmount = amount,
                    Fee = transParams.MinFee,
                    FirstValid = transParams.LastRound,
                    LastValid = transParams.LastRound + 1000UL,
                    GenesisHash = Digest(transParams.GenesisHash),
                    GenesisId = transParams.GenesisId,
                    Sender = src.Address,
                    Note = System.Text.Encoding.UTF8.GetBytes(note))
            let signedTx = assetTransferTx.Sign(src)
            let! id = Utils.SubmitTransaction(algodApiInstance, signedTx)
            let! resp = Utils.WaitTransactionToComplete(algodApiInstance, id.Txid)
                
            return Ok(id.Txid, resp.ConfirmedRound)
        with exn ->
            return Error(exn)
    }

