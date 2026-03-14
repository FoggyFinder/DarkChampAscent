module CommonHelpers

open Db
open GameLogic.Champs
open DarkChampAscent.Account
open System

let private scanChamps (db:SqliteStorage) (userId:uint64) (wallet:string) =
    Blockchain.getChampsForWallet wallet
    |> Seq.map(fun assetId ->
        let r = db.ChampExists assetId
        match r with
        | Ok b ->
            if b then db.UpdateUserForChamp(userId, assetId)
            else
                match Blockchain.tryGetChampInfo assetId with
                | Some (trait', ipfs) ->
                    let b =
                        db.AddOrInsertChamp {
                            Name = Blockchain.getAssetName assetId
                            AssetId = assetId
                            IPFS = ipfs
                            UserId = userId
                            Stats = Champ.generateStats trait'
                            Traits = trait'
                        }
                    if b then Ok ()
                    else Error $"Unable to upsert {assetId} champ"
                | None -> Error "Unable to fetch data from blockchain"
        | Error err -> Error err)

let updateChampsForAUser(db:SqliteStorage, userId:uint64, wallets:string list) =
    let results =
        wallets
        |> Seq.collect (scanChamps db userId)
        |> Seq.toList
    if results |> Seq.forall Result.isOk then
        Ok ()
    else
        results |> Seq.fold (fun acc r ->
            match r with
            | Ok () -> acc
            | Error err -> $"{acc} {Environment.NewLine} {err}") ""
        |> Error

let updateChamps(db:SqliteStorage, uId:UserId, wallets:string list) =
    match db.FindUserIdByUserId uId with
    | Some userId ->
        let userId' = uint64 userId
        updateChampsForAUser(db, userId', wallets)
    | None -> Error "Can't find user"
