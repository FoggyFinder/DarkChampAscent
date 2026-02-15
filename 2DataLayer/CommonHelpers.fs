module CommonHelpers

open Db
open GameLogic.Champs
open DarkChampAscent.Account

let updateChamps(db:SqliteStorage, uId:UserId, wallets:string list) =
    match db.FindUserIdByUserId uId with
    | Some userId ->
        wallets
        |> Seq.collect Blockchain.getChampsForWallet
        |> Seq.iter(fun assetId ->
            let r = db.ChampExists assetId
            match r with
            | Ok b ->
                if b |> not then
                    Blockchain.tryGetChampInfo assetId
                    |> Option.iter(fun (trait', ipfs) ->
                        db.AddOrInsertChamp ({
                            Name = Blockchain.getAssetName assetId
                            AssetId = assetId
                            IPFS = ipfs
                            UserId = uint64 userId
                            Stats = Champ.generateStats trait'
                            Traits = trait'
                        }) |> ignore)
            | Error _ -> ())
    | None -> ()
