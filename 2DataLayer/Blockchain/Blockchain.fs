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
let [<Literal>] DarkChampAscent    = "SZYECJF52SCJYMDQG4M3RGGLDTQPEEPTD36SW4PABEGZ6MCA4KH67QKRAU"
let [<Literal>] DarkChampAscentNFD = "darkchampascent.algo"

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
        match getAccountBalanceBalance(wallet, DarkCoinAssetId) with
        | Some v -> Math.Round(decimal v / Algo6Decimals, 6) |> Ok
        | None -> Error("Unexpected error")
    with exp ->
        Error(exp.ToString())

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

[<RequireQualifiedAccess>]
type TxStatus =
    | Confirmed of tx:string * confirmed:Nullable<uint64>
    | Unconfirmed of tx:string
    | Error of exn

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
            // default timeout is 3 rounds and it's not enough
            // https://github.com/FoggyFinder/DarkChampAscent/issues/8
            try
                let! resp = Utils.WaitTransactionToComplete(algodApiInstance, id.Txid, 12UL)
                return TxStatus.Confirmed(id.Txid, resp.ConfirmedRound)
            with inner ->
                if inner.Message.StartsWith("Transaction not confirmed") then
                    return TxStatus.Unconfirmed id.Txid
                else
                    return TxStatus.Error <| Exception($"Unable to confirm {id.Txid}", inner)
            
        with exn ->
            return TxStatus.Error(exn)
    }

let getTxB64(wallet:string, message:string) =
    task {
        let! transParams = algodApiInstance.TransactionParamsAsync()
        let txn =
            PaymentTransaction.GetPaymentTransactionWithSuggestedFee(
                from              = Address(wallet),
                ``to``            = Address(wallet),
                amount            = 0UL,
                message           = message,
                suggestedFeePerByte = 0UL,
                lastRound         = transParams.LastRound + 1000UL,
                genesisId         = transParams.GenesisId,
                genesishashb64    = Convert.ToBase64String(transParams.GenesisHash)
            )
        
        return Convert.ToBase64String(Encoder.EncodeToMsgPackOrdered txn)
    }

let getAssetTransferTransactionTxB64(sender:string, assetId:uint64, receiver:string, amount:uint64, note:string) =
    task {
        let! transParams = algodApiInstance.TransactionParamsAsync()
        let txn =
            AssetTransferTransaction(
                XferAsset = assetId,
                AssetReceiver = Address(receiver),
                AssetAmount = amount,
                Fee = transParams.MinFee,
                FirstValid = transParams.LastRound,
                LastValid = transParams.LastRound + 1000UL,
                GenesisHash = Digest(transParams.GenesisHash),
                GenesisId = transParams.GenesisId,
                Sender = Address(sender),
                Note = System.Text.Encoding.UTF8.GetBytes(note))
        
        return Convert.ToBase64String(Encoder.EncodeToMsgPackOrdered txn)
    }

let isValidAddress = Address.IsValid

let sendTX64(signedTxnB64: string) =
    task {
        let signedTxnBytes = Convert.FromBase64String(signedTxnB64)
        let signedTx      = Encoder.DecodeFromMsgPack<SignedTransaction>(signedTxnBytes)

        let! id = Utils.SubmitTransaction(algodApiInstance, signedTx)
        try
            let! resp = Utils.WaitTransactionToComplete(algodApiInstance, id.Txid, 12UL)
            return TxStatus.Confirmed(id.Txid, resp.ConfirmedRound)
        with inner ->
            if inner.Message.StartsWith("Transaction not confirmed") then
                return TxStatus.Unconfirmed id.Txid
            else
                return TxStatus.Error <| Exception($"Unable to confirm {id.Txid}", inner)
    }

let decodeTX64(signedTxnB64: string) =
    task {
        let signedTxnBytes = Convert.FromBase64String(signedTxnB64)
        let signedTx      = Encoder.DecodeFromMsgPack<SignedTransaction>(signedTxnBytes)
        let note = System.Text.Encoding.UTF8.GetString signedTx.Tx.Note
        return signedTx.Tx.Sender.ToString(), note
    }

open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.Crypto.Signers

let verifyAlgorandTxnSignature (wallet: string) (signedTxnB64: string) (txnnote: string) : bool =
    try
        let signedTxnBytes = Convert.FromBase64String(signedTxnB64)
        let signedTxn      = Encoder.DecodeFromMsgPack<SignedTransaction>(signedTxnBytes)
        let txn            = signedTxn.Tx

        let note = System.Text.Encoding.UTF8.GetString(txn.Note)
        if note = txnnote then
            let pubKeyBytes =
                // re-keyed wallet case
                if signedTxn.AuthAddr <> null then
                    signedTxn.AuthAddr.Bytes
                else
                    Address(wallet).Bytes
            let sigBytes    = signedTxn.Sig.Bytes
            let prefix      = System.Text.Encoding.UTF8.GetBytes "TX"

            let verify (msgBytes: byte[]) =
                let pubKey = Ed25519PublicKeyParameters(pubKeyBytes, 0)
                let signer = Ed25519Signer()
                signer.Init(false, pubKey)
                signer.BlockUpdate(msgBytes, 0, msgBytes.Length)
                signer.VerifySignature(sigBytes)

            let msgOrdered   = Array.concat [ prefix; Encoder.EncodeToMsgPackOrdered txn ]
            let msgUnordered = Array.concat [ prefix; Encoder.EncodeToMsgPackNoOrder txn ]

            verify msgOrdered || verify msgUnordered
        else
            false
    with _ ->
        false

// TODO: pass 0, 6, 9 instead
let toLong (d:decimal, decimals:decimal) =
    uint64 (d * decimals)