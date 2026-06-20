module Pages.User

open Feliz
open Types
open Display
open Components
open GameLogic.Shop
open GameLogic.Monsters
open DTO
open System
open UseWallet
open Fable.Core
open DarkChampAscent.Api
open UI

let shortAddr (addr: string) =
    if addr.Length > 12 then $"{addr.[..5]}...{addr.[addr.Length-5..]}"
    else addr

[<ReactComponent>]
let AccountPage (onLogout: unit -> unit) =
    let data, setData           = React.useState<Deferred<AccountDTO>> Loading
    let newWallet, setNewWallet = React.useState ""
    let donateAmt, setDonateAmt = React.useState "1000"
    let msg, setMsg             = React.useState<string option> None
    
    let wallet = useWallet ()

    React.useEffect((fun () ->
        async {
            let! r = Api.getAccount ()
            match r with
            | Ok d  ->
                setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])

    deferred data (fun account ->
        let acc = account.Account
        let isNewWallet = newWallet = ""
        Html.div [
            prop.className "acc-dashboard"
            prop.children [
                Html.section [
                    prop.className "block user-section"
                    prop.children [
                        Html.div [
                            prop.children [
                                Html.p [ prop.className "account-nickname"; prop.text acc.User.Nickname ]
                            ]
                        ]
                        Html.div [
                            prop.className "donate-row"
                            prop.children [
                                Html.input [ prop.type' "number"; prop.value donateAmt; prop.onChange setDonateAmt; prop.className "form-control" ]
                                match wallet.activeAddress with
                                | Some awallet ->
                                    Html.button [
                                        prop.className "btn btn-primary"
                                        prop.onClick (fun _ ->
                                            match Decimal.TryParse donateAmt with
                                            | true, v ->
                                                async {
                                                    "" |> Some |> setMsg
                                                    let tx = Tx.Donate (awallet, v) 
                                                    let! r = Api.createTx tx
                                                    match r with
                                                    | Ok txnb64 ->
                                                        let! sr =
                                                            UseWallet.signTx (Api.submitTx tx) wallet txnb64
                                                                (fun () -> "Processing request..." |> Some |> setMsg)
                                                        match sr with
                                                        | Ok m -> m
                                                        | Error err -> err
                                                        |> Some |> setMsg 
                                                    | Error err ->
                                                        setMsg (Some err)
                                                } |> Async.StartImmediate
                                            | _ -> ())
                                        prop.text "Donate"
                                    ]
                                | None ->
                                    Html.p [ prop.className "notice"; prop.text "Connect confirmed wallet on 'Account' page to donate some DarkCoins to rewards pool" ]
                            ]
                        ]
                        
                        Html.button [
                            prop.className "btn btn-secondary"
                            prop.onClick (fun _ -> 
                                async {
                                    "" |> Some |> setMsg
                                    let! _ = Api.logout ()
                                    if acc.User.IsWeb3 then
                                        try
                                            match wallet.activeWallet with
                                            | Some w ->
                                                do! w.disconnect() |> Async.AwaitPromise
                                            | None ->()
                                        with _ ->
                                            ()
                                    onLogout ()
                                } |> Async.StartImmediate)
                            prop.text "Log out"
                        ]
                    ]
                ]
                Html.section [
                    prop.className "block info-section"
                    prop.children [
                        Html.table [
                            Html.tbody [
                                Html.tr [
                                    let p = Page.MyChamps
                                    Html.td [ prop.text "Champs" ]
                                    Html.td [ Html.a [ prop.href p.Route; prop.onClick (Nav.navTo p.Route); prop.text (string acc.Champs) ] ]
                                ]
                                Html.tr [
                                    let p = Page.MyMonsters
                                    Html.td [ prop.text "Monsters" ]
                                    Html.td [ Html.a [ prop.href p.Route; prop.onClick (Nav.navTo p.Route); prop.text (string acc.Monsters) ] ]
                                ]
                                Html.tr [
                                    let p = Page.MyRequests
                                    Html.td [ prop.text "Requests" ]
                                    Html.td [ Html.a [ prop.href p.Route; prop.onClick (Nav.navTo p.Route); prop.text (string acc.Requests) ] ]
                                ]
                            ]
                        ]
                    ]
                ]

                
                if acc.User.IsWeb3 |> not then
                    Html.section [
                        prop.className "block wallets-section"
                        prop.children [
                            match msg with
                            | Some m -> Html.p [ prop.className "action-msg"; prop.text m ]
                            | None -> Html.none

                            if acc.Wallets.IsEmpty then
                                Html.p [ prop.text "No wallets registered yet." ]
                            else
                                Html.h3 [ prop.text "My wallets" ]
                                Html.div [
                                    prop.className "wallet-card-list"
                                    prop.children [
                                        for w in acc.Wallets do
                                            let isConnected = wallet.activeAddress = Some w.Wallet
                                            Html.div [
                                                prop.className "wallet-card"
                                                prop.children [
                                                    Html.div [
                                                        prop.className "wallet-card-header"
                                                        prop.children [
                                                            Html.span [ prop.className "wallet-address"; prop.text w.Wallet ]
                                                            Html.span [
                                                                prop.className (if w.IsConfirmed then "yes" else "no")
                                                                prop.text (if w.IsConfirmed then WebEmoji.CheckMark else WebEmoji.CrossMark)
                                                            ]
                                                        ]
                                                    ]
                                                    
                                                    Html.div [
                                                        prop.className "wallet-card-actions"
                                                        prop.children [
                                                            match wallet.activeAddress with
                                                            | _ when not w.IsConfirmed || (w.IsConfirmed && not isConnected) ->
                                                                match wallet.activeAddress with
                                                                | Some addr when addr = w.Wallet ->
                                                                    Html.button [
                                                                        prop.className "btn btn-primary btn-sm"
                                                                        prop.text "Confirm wallet"
                                                                        prop.onClick (fun _ ->
                                                                            async {
                                                                                "" |> Some |> setMsg
                                                                                let! r = Api.createTx (Tx.Confirm(w.Wallet, w.Code))
                                                                                match r with
                                                                                | Error e -> setMsg (Some ("Error: " + e))
                                                                                | Ok txnb64 ->
                                                                                    try
                                                                                        let txnBytes = System.Convert.FromBase64String txnb64
                                                                                        let! signedTxns =
                                                                                            wallet.signTransactions [| box txnBytes |]
                                                                                            |> Async.AwaitPromise
                                                                                        let signedB64 = System.Convert.ToBase64String signedTxns.[0]
                                                                                        let! result = Api.verifyTx signedB64
                                                                                        match result with
                                                                                        | Ok () ->
                                                                                            setMsg (Some "Wallet confirmed!")
                                                                                            let! r = Api.getAccount ()
                                                                                            match r with
                                                                                            | Ok d  -> setData (Loaded d)
                                                                                            | Error e -> setData (Failed e)
                                                                                        | Error e -> setMsg (Some ("Error: " + e))
                                                                                    with ex ->
                                                                                        setMsg (Some $"Signing cancelled: {ex.Message}")
                                                                            } |> Async.StartImmediate)
                                                                    ]
                                                                    Html.button [
                                                                        prop.className "btn btn-secondary btn-sm"
                                                                        prop.text "Disconnect"
                                                                        prop.onClick (fun _ ->
                                                                            "" |> Some |> setMsg
                                                                            match wallet.activeWallet with
                                                                            | Some aw -> aw.disconnect() |> Async.AwaitPromise |> Async.StartImmediate
                                                                            | None -> ())
                                                                    ]
                                                                | Some addr ->
                                                                    if w.IsConfirmed then
                                                                        Html.span [ prop.className "notice"; prop.text $"Connected: {shortAddr addr}" ]
                                                                        Html.button [
                                                                            prop.className "btn btn-secondary btn-sm"
                                                                            prop.text $"Switch to {shortAddr w.Wallet}"
                                                                            prop.onClick (fun _ ->
                                                                                "" |> Some |> setMsg
                                                                                match wallet.activeWallet with
                                                                                | Some aw -> aw.disconnect() |> Async.AwaitPromise |> Async.StartImmediate
                                                                                | None -> ())
                                                                        ]
                                                                    else
                                                                        Html.span [ prop.className "notice"; prop.text $"Connected wallet doesn't match. Current: {shortAddr addr}. Connect {shortAddr w.Wallet} to confirm." ]
                                                                        Html.button [
                                                                            prop.className "btn btn-secondary btn-sm"
                                                                            prop.text "Disconnect"
                                                                            prop.onClick (fun _ ->
                                                                                "" |> Some |> setMsg
                                                                                match wallet.activeWallet with
                                                                                | Some aw -> aw.disconnect() |> Async.AwaitPromise |> Async.StartImmediate
                                                                                | None -> ())
                                                                        ]
                                                                | None ->
                                                                    Html.span [ prop.text (if w.IsConfirmed then $"Connect to use for signing:" else "Connect your wallet to confirm:") ]
                                                                    for aw in wallet.wallets do
                                                                        Html.button [
                                                                            prop.className "btn btn-secondary btn-sm"
                                                                            prop.text $"{aw.id}"
                                                                            prop.onClick (fun _ ->
                                                                                "" |> Some |> setMsg
                                                                                aw.connect() |> Async.AwaitPromise |> Async.StartImmediate)
                                                                        ]
                                                            | _ ->
                                                                Html.button [
                                                                    prop.className "btn btn-secondary btn-sm"
                                                                    prop.text "Disconnect"
                                                                    prop.onClick (fun _ ->
                                                                        match wallet.activeWallet with
                                                                        | Some aw ->
                                                                            "" |> Some |> setMsg
                                                                            aw.disconnect() |> Async.AwaitPromise |> Async.StartImmediate
                                                                        | None -> ())
                                                                ]
                                                        ]
                                                    ]
                                                ]
                                            ]
                                    ]
                                ]

                            Html.div [
                                prop.className "wallet-form"
                                prop.children [
                                    Html.input [
                                        prop.type' "text"; prop.className "wallet-input"
                                        prop.placeholder "Algorand wallet address"
                                        prop.value newWallet; prop.onChange setNewWallet
                                    ]
                                    Html.button [
                                        prop.className "btn btn-primary"
                                        prop.disabled isNewWallet
                                        prop.onClick (fun _ ->
                                            async {
                                                "" |> Some |> setMsg
                                                let! r = Api.registerWallet newWallet
                                                match r with
                                                | Ok ()   -> setMsg (Some "Wallet registered!"); setNewWallet ""
                                                | Error e -> setMsg (Some ("Error: " + e))
                                            } |> Async.StartImmediate)
                                        prop.text "Register wallet"
                                    ]
                                ]
                            ]
                        ]
                    ]
            ]
        ])

[<ReactComponent>]
let StoragePage () =
    let data, setData = React.useState<Deferred<UserStorageDTO>> Loading
    let selChamps, setSelChamps = React.useState Map.empty<int, string>
    let msg, setMsg = React.useState<string option> None

    let getSelected i = selChamps |> Map.tryFind i |> Option.defaultValue ""
    let setSelected i v = selChamps |> Map.add i v |> setSelChamps

    let load () =
        async {
            let! r = Api.getStorage ()
            match r with
            | Ok d ->
                setData (Loaded d)
                let defaultChamp = d.Champs |> List.tryHead |> Option.map (fun c -> string c.ID) |> Option.defaultValue ""
                setSelChamps (d.Storage |> List.mapi (fun i _ -> i, defaultChamp) |> Map.ofList)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate

    React.useEffect(load, [||])

    deferred data (fun d ->
        Html.div [
            prop.className "storage"
            prop.children [
                match msg with
                | Some m -> Html.p [ prop.className "action-msg"; prop.text m ]
                | None -> Html.none
                if d.Storage.IsEmpty then
                    Html.p [ prop.className "muted"; prop.text "Storage is empty." ]
                else
                    Html.div [
                        prop.className "shop-grid"
                        prop.children [
                            for (i, (item, amount)) in d.Storage |> List.indexed ->
                                Cards.storageItemCard i item amount d.Champs (getSelected i) (setSelected i) setMsg load
                        ]
                    ]
            ]
        ])

[<ReactComponent>]
let MyChampsPage () =
    let data, setData = React.useState<Deferred<(ChampShortInfo * decimal) list>> Loading
    let msg, setMsg   = React.useState<string option> None

    let load () =
        async {
            let! r = Api.getMyChamps ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate

    React.useEffect((fun () -> load ()), [||])

    deferred data (fun champs ->
        Html.div [
            prop.className "my-champs"
            prop.children [
                match msg with
                | Some m -> Html.p [ prop.className "action-msg"; prop.text m ]
                | None -> Html.none
                if champs.IsEmpty then
                    Html.p [
                        Html.text "No champs found. Buy one "
                        Html.a [ prop.href Links.DarkChampCollection; prop.target.blank; prop.text "here" ]
                        Html.text " then click Rescan."
                    ]
                else
                    Html.div [
                        prop.className "monster-leaderboard"
                        prop.children [
                            for (i, (c, balance)) in champs |> List.indexed ->
                                Cards.myChampCard i c balance
                        ]
                    ]
            ]
        ])

[<ReactComponent>]
let ChampsEffectsPage () =
    let data, setData = React.useState<Deferred<ChampUnderEffect list>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getChampsEffects ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun champs ->
        Html.div [
            prop.className "champs-under-effects"
            prop.children [
                if champs.IsEmpty then
                    Html.p [ prop.className "muted"; prop.text "No champs under effects." ]
                else
                    Html.div [
                        prop.className "monster-leaderboard"
                        prop.children [
                            for (i, c) in champs |> List.sortByDescending (fun c -> c.RoundsLeft) |> List.indexed ->
                                Cards.champEffectCard i c
                        ]
                    ]
            ]
        ])

[<ReactComponent>]
let MyMonstersPage () =
    let data, setData = React.useState<Deferred<UserMonstersDTO>> Loading
    let selType, setSelType       = React.useState MonsterType.Zombie
    let selSubType, setSelSubType = React.useState MonsterSubType.None
    let msg, setMsg               = React.useState<string option> None

    let isNftMode, setIsNftMode   = React.useState true
    let assetIdInput, setAssetIdInput = React.useState ""
    let assetInfo, setAssetInfo   = React.useState<Types.AssetInfo option> None
    let nftName, setNftName       = React.useState ""
    let nftDesc, setNftDesc       = React.useState ""
    let nftExtUrl, setNftExtUrl   = React.useState ""

    let wallet = useWallet ()
    let load() =
        async {
            let! r = Api.getMyMonsters ()
            match r with
            | Ok d    -> 
                setData (Loaded d)
            | Error e -> setData (Failed e)
            setMsg None
            setAssetInfo None
            setNftName ""
            setNftDesc ""
            setNftExtUrl ""
        }

    let fetch() =
        async {
            match System.UInt64.TryParse(assetIdInput) with
            | false, _ -> setMsg (Some "Enter a valid asset ID")
            | true, aid ->
                setMsg (Some "Fetching...")
                let! r = Api.getAssetInfo aid
                match r with
                | Ok ai ->
                    setAssetInfo (Some ai)
                    setNftName ai.Name
                    setNftDesc ""
                    setNftExtUrl ai.ExternalUrl
                    setMsg None
                | Error e ->
                    setAssetInfo None
                    setMsg (Some $"Error: {e}")
        }

    React.useEffect((fun () -> load() |> Async.StartImmediate), [||])

    deferred data (fun d ->
        Html.div [
            prop.className "my-monsters"
            prop.children [
                match msg with
                | Some m -> Html.p [ prop.className "action-msg"; prop.text m ]
                | None   -> Html.none

                let amount = Math.Round(Shop.GenMonsterPrice / d.Price, 6)
                Html.div [
                    prop.className "create-monster"
                    prop.children [
                        Html.p [ prop.dangerouslySetInnerHTML (sprintf "Create custom monster: %s %s DarkCoins" (string amount) WebEmoji.DarkCoin) ]

                        Html.div [
                            prop.className "nft-mode-toggle"
                            prop.children [
                                Html.button [
                                    prop.className (if not isNftMode then "toggle-btn active" else "toggle-btn")
                                    prop.onClick (fun _ -> setIsNftMode false; setMsg None)
                                    prop.text "Generate"
                                ]
                                Html.button [
                                    prop.className (if isNftMode then "toggle-btn active" else "toggle-btn")
                                    prop.onClick (fun _ -> setIsNftMode true; setMsg None)
                                    prop.text "From NFT"
                                ]
                            ]
                        ]

                        if not isNftMode then
                            CustomSelectInput (DisplayEnum.MonsterType selType)
                                (fun (s: string) ->
                                    AllEnums.MonsterTypes
                                    |> List.tryFind (fun t -> DisplayEnum.MonsterType t = s)
                                    |> Option.iter setSelType)
                                [ for t in AllEnums.MonsterTypes -> DisplayEnum.MonsterType t, DisplayEnum.MonsterType t, None ]

                            CustomSelectInput (DisplayEnum.MonsterSubType selSubType)
                                (fun (s: string) ->
                                    AllEnums.MonsterSubTypes
                                    |> List.tryFind (fun t -> DisplayEnum.MonsterSubType t = s)
                                    |> Option.iter setSelSubType)
                                [ for t in AllEnums.MonsterSubTypes -> DisplayEnum.MonsterSubType t, DisplayEnum.MonsterSubType t, None ]

                        else
                            // --- NFT flow ---
                            Html.div [
                                prop.className "nft-section"
                                prop.children [

                                Html.div [
                                    prop.className "notice"
                                    prop.children [
                                        Html.p [ 
                                            Html.text "You can only create monster based on NFT from your wallet"
                                        ]
                                    ]
                                ]

                                Html.div [
                                    prop.className "nft-fetch-row"
                                    prop.children [
                                        Html.input [
                                            prop.className "input-assetid"
                                            prop.type' "text"
                                            prop.placeholder "Asset ID"
                                            prop.value assetIdInput
                                            prop.onChange (fun (v: string) ->
                                                let digits = v |> Seq.filter System.Char.IsDigit |> System.String.Concat
                                                setAssetIdInput digits)
                                        ]
                                        Html.button [
                                            prop.className "btn btn-secondary"
                                            prop.onClick (fun _ -> fetch() |> Async.StartImmediate)
                                            prop.text "Fetch"
                                        ]
                                    ]
                                ]

                                Html.div [
                                    prop.className "pera-attribution"
                                    prop.children [
                                        Html.img [ prop.src "pera-logo-white.png"; prop.alt "Pera" ]
                                        Html.span [
                                            prop.children [
                                                Html.text "Data provided by "
                                                Html.a [
                                                    prop.href "https://docs.perawallet.app/references/public-api#get-public-assets-asset_id"
                                                    prop.target "_blank"
                                                    prop.rel "noreferrer"
                                                    prop.text "Pera"
                                                ]
                                            ]
                                        ]
                                    ]
                                ]

                                match assetInfo with
                                | None -> Html.none
                                | Some ai ->
                                    Html.div [
                                        prop.className "nft-card"
                                        prop.children [
                                            Html.img [
                                                prop.className "nft-card-img"
                                                prop.src $"https://mainnet.api.perawallet.app/v1/ipfs-thumbnails/{ai.IPFS}"
                                                prop.alt ai.Name
                                            ]
                                            Html.div [
                                                prop.className "nft-card-fields"
                                                prop.children [
                                                    Html.div [
                                                        prop.className "nft-field"
                                                        prop.children [
                                                            Html.label [ prop.text "Name" ]
                                                            Html.input [ prop.type' "text"; prop.value nftName; prop.onChange setNftName ]
                                                        ]
                                                    ]
                                                    Html.div [
                                                        prop.className "nft-field"
                                                        prop.children [
                                                            Html.label [ prop.text "Description" ]
                                                            Html.textarea [ prop.value nftDesc; prop.onChange setNftDesc; prop.placeholder "Description" ]
                                                        ]
                                                    ]
                                                    Html.div [
                                                        prop.className "nft-field"
                                                        prop.children [
                                                            Html.label [ prop.text "External link" ]
                                                            Html.input [ prop.type' "text"; prop.value nftExtUrl; prop.onChange setNftExtUrl ]
                                                        ]
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]

                                Html.div [
                                    prop.className "notice"
                                    prop.children [
                                        Html.p [ 
                                            Html.text "Keep in mind: if you sell or transfer the NFT out of a confirmed wallet, the monster will eventually be deactivated"
                                        ]
                                    ]
                                ]

                            ]
                        ]
                        // --- wallet / action ---
                        match wallet.activeAddress with
                        | Some awallet ->
                            Html.button [
                                prop.className "btn btn-primary"
                                prop.onClick (fun _ ->
                                    async {
                                        "" |> Some |> setMsg
                                        if isNftMode then
                                            match assetInfo with
                                            | None -> do! fetch()
                                            | Some ai ->
                                                let! r = Api.createNFTBasedMonster (ai.AssetId, nftName, nftDesc, ai.IPFS, nftExtUrl)
                                                match r with
                                                | Ok reqId ->
                                                    let tx = Tx.CreateNFTBasedCustomMonster (awallet, reqId)
                                                    let! r = Api.createTx tx
                                                    match r with
                                                    | Ok txnb64 ->
                                                        let! sr =
                                                            UseWallet.signTx (Api.submitTx tx) wallet txnb64
                                                                (fun () -> "Processing request..." |> Some |> setMsg)
                                                        match sr with
                                                        | Ok m ->
                                                            match UInt64.TryParse m with
                                                            | true, mId ->
                                                                Nav.navigateTo (Page.MonsterDetail mId).Route
                                                            | false, _ ->
                                                                m + " reloading..."  |> Some |> setMsg
                                                                do! Async.Sleep(TimeSpan.FromSeconds(5.))
                                                                do! load()
                                                        | Error e -> $"Error: {e}" |> Some |> setMsg
                                                        
                                                    | Error err -> setMsg (Some err)                                                    
                                                | Error err ->
                                                    setMsg (Some $"Error: {err}")
                                        else
                                            let tx = Tx.CreateCustomMonster (awallet, selType, selSubType)
                                            let! r = Api.createTx tx
                                            match r with
                                            | Ok txnb64 ->
                                                let! sr =
                                                    UseWallet.signTx (Api.submitTx tx) wallet txnb64
                                                        (fun () -> "Processing request..." |> Some |> setMsg)
                                                match sr with
                                                | Ok m    -> m
                                                | Error e -> $"Error: {e}"
                                                |> Some |> setMsg
                                            | Error err -> setMsg (Some err)
                                    } |> Async.StartImmediate)
                                prop.text "Create monster"
                            ]
                        | None ->
                            Html.p [ prop.className "notice"; prop.text "Connect confirmed wallet on 'Account' page to proceed" ]
                    ]
                ]

                if d.Monsters.IsEmpty then
                    Html.p [ prop.text "No monsters yet." ]
                else
                    Html.div [
                        prop.className "monster-leaderboard"
                        prop.children [
                            for (i, m) in d.Monsters |> List.indexed ->
                                Cards.myMonsterCard i m
                        ]
                    ]
            ]
        ])

[<ReactComponent>]
let MyRequestsPage () =
    let data, setData = React.useState<Deferred<GenRequest list>> Loading
    React.useEffect((fun () ->
        async {
            let! r = Api.getMyRequests ()
            match r with
            | Ok d  -> setData (Loaded d)
            | Error e -> setData (Failed e)
        } |> Async.StartImmediate), [||])
    deferred data (fun reqs ->
        Html.div [
            prop.className "my-requests"
            prop.children [
                if reqs.IsEmpty then
                    Html.p [ prop.text "No pending requests." ]
                else
                    Html.table [
                        Html.thead [ Html.tr [ Html.th [prop.text "#"]; Html.th [prop.text "ID"]; Html.th [prop.text "Timestamp"]; Html.th [prop.text "Status"] ] ]
                        Html.tbody [
                            for (i, r) in reqs |> List.indexed ->
                                Html.tr [
                                    Html.td [prop.text (string (i+1))]
                                    Html.td [prop.text (string r.ID)]
                                    Html.td [prop.text (r.Timestamp.ToLocalTime().ToString("dd MMM yyyy HH:mm"))]
                                    Html.td [prop.text $"{DisplayEnum.GenStatus r.Status}"]
                                ]
                        ]
                    ]
            ]
        ])
