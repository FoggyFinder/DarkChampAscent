module Pages.Login

open Feliz
open DarkChampAscent.Account
open UseWallet
open Fable.Core
open DarkChampAscent.Api

[<ReactComponent>]
let LoginPage (onLogin: Account -> unit) =
    let tab, setTab           = React.useState "login"
    let nickname, setNickname = React.useState ""
    let password, setPassword = React.useState ""
    let confirm, setConfirm   = React.useState ""
    let error, setError       = React.useState<string option> None
    let busy, setBusy         = React.useState false

    let wallet = useWallet ()

    let run op =
        setBusy true; setError None
        async {
            let! r = op
            match r with
            | Ok user  -> onLogin user
            | Error e  -> setError (Some e); setBusy false
        } |> Async.StartImmediate

    let handleWeb3Login () =
        setBusy true; setError None
        async {
            match wallet.activeAddress with
            | None ->
                setError (Some "No wallet connected"); setBusy false
            | Some address ->
                let! challengeResult = Api.getWeb3Challenge address
                match challengeResult with
                | Error e ->
                    setError (Some e); setBusy false
                | Ok challenge ->
                    try
                        let txnBytes : byte[] = challenge.TxnB64 |> System.Convert.FromBase64String
                        let! signedTxns =
                            wallet.signTransactions [| box txnBytes |]
                            |> Async.AwaitPromise
                        let signedTxnB64 = System.Convert.ToBase64String(signedTxns.[0])
                        let! result = Api.loginWeb3 address signedTxnB64 challenge.Nonce
                        match result with
                        | Ok user  -> onLogin user
                        | Error e  -> setError (Some e); setBusy false
                    with ex ->
                        setError (Some $"Signing cancelled: {ex.Message}")
                        setBusy false
        } |> Async.StartImmediate

    let connectAndSign (walletId: string) =
        setBusy true; setError None
        async {
            try
                let target = wallet.wallets |> Array.tryFind (fun w -> w.id = walletId)
                match target with
                | None -> setError (Some $"Wallet '{walletId}' not found"); setBusy false
                | Some w ->
                    do! w.connect() |> Async.AwaitPromise
                    setBusy false
            with ex ->
                setError (Some $"Connection failed: {ex.Message}")
                setBusy false
        } |> Async.StartImmediate

    Html.div [
        prop.className "auth-page"
        prop.children [
            Html.div [
                prop.className "auth-container"
                prop.children [
                    Html.h2 [ prop.className "auth-header"; prop.text "Welcome Back" ]
                    Html.p  [ prop.className "auth-subtitle"; prop.text "Sign in to continue your adventure" ]

                    match error with
                    | Some msg -> Html.div [ prop.className "error-alert"; prop.text msg ]
                    | None -> Html.none

                    Html.a [ prop.href (Api.baseUrl + Pattern.AuthLoginDiscord.Str); prop.className "btn btn-discord"
                             prop.text "Continue with Discord" ]

                    Html.div [ prop.className "separator"; prop.children [ Html.span [ prop.text "or" ] ] ]

                    match wallet.activeAddress with
                    | Some addr ->
                        Html.div [
                            prop.className "auth-form"
                            prop.children [
                                Html.p [ prop.className "wallet-address"
                                         prop.text $"Connected: {addr[..5]}...{addr[addr.Length-4..]}" ]
                                
                                Html.p [ prop.className "notice"; prop.text "It's not web3 app so you have to sign-in txn to verify your wallet, it's only submitted to the server and doesn't go to blockchain." ]

                                Html.div [
                                    prop.className "wallet-action-row"
                                    prop.children [
                                        Html.button [
                                            prop.type' "button"; prop.className "btn btn-web3"
                                            prop.disabled busy
                                            prop.onClick (fun _ -> handleWeb3Login ())
                                            prop.text (if busy then "Signing..." else "Sign in with Wallet")
                                        ]
                                        Html.button [
                                            prop.type' "button"; prop.className "btn btn-secondary"
                                            prop.disabled busy
                                            prop.onClick (fun _ ->
                                                async {
                                                    match wallet.activeWallet with
                                                    | Some w -> do! w.disconnect() |> Async.AwaitPromise
                                                    | None -> ()
                                                } |> Async.StartImmediate)
                                            prop.text "Disconnect"
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    | None ->
                        Html.div [
                            prop.className "auth-form"
                            prop.children [
                                Html.button [
                                    prop.type' "button"; prop.className "btn btn-pera"
                                    prop.disabled busy
                                    prop.onClick (fun _ -> connectAndSign "pera" )
                                    prop.text "Pera Wallet"
                                ]

                                Html.button [
                                    prop.type' "button"; prop.className "btn btn-defly"
                                    prop.disabled busy
                                    prop.onClick (fun _ -> connectAndSign "defly" )
                                    prop.text "Defly Wallet"
                                ]

                                Html.button [
                                    prop.type' "button"; prop.className "btn btn-lute"
                                    prop.disabled busy
                                    prop.onClick (fun _ -> connectAndSign "lute" )
                                    prop.text "Lute Wallet"
                                ]
                            ]
                        ]

                    Html.div [ prop.className "separator"; prop.children [ Html.span [ prop.text "or" ] ] ]

                    Html.div [
                        prop.className "auth-toggle"
                        prop.children [
                            Html.button [ prop.type' "button"
                                          prop.className (if tab = "login" then "toggle-btn active" else "toggle-btn")
                                          prop.onClick (fun _ -> setTab "login"; setError None)
                                          prop.text "Login" ]
                            Html.button [ prop.type' "button"
                                          prop.className (if tab = "register" then "toggle-btn active" else "toggle-btn")
                                          prop.onClick (fun _ -> setTab "register"; setError None)
                                          prop.text "Register" ]
                        ]
                    ]

                    if tab = "login" then
                        Html.div [
                            prop.className "auth-form"
                            prop.children [
                                Html.input [ prop.type' "text"; prop.className "form-input"
                                             prop.placeholder "Nickname"; prop.value nickname; prop.onChange setNickname ]
                                Html.input [ prop.type' "password"; prop.className "form-input"
                                             prop.placeholder "Password"; prop.value password; prop.onChange setPassword ]
                                Html.button [
                                    prop.type' "button"; prop.className "btn btn-primary"
                                    prop.disabled (busy || nickname = "" || password = "")
                                    prop.onClick (fun _ -> run (Api.loginCustom nickname password))
                                    prop.text (if busy then "Logging in..." else "Login")
                                ]
                            ]
                        ]
                    else
                        Html.div [
                            prop.className "auth-form"
                            prop.children [
                                Html.input [ prop.type' "text"; prop.className "form-input"
                                             prop.placeholder "Nickname (3-16)"; prop.minLength 3; prop.maxLength 16
                                             prop.value nickname; prop.onChange setNickname ]
                                Html.input [ prop.type' "password"; prop.className "form-input"
                                             prop.placeholder "Password (8-24)"; prop.minLength 8; prop.maxLength 24
                                             prop.value password; prop.onChange setPassword ]
                                Html.input [ prop.type' "password"; prop.className "form-input"
                                             prop.placeholder "Confirm password"
                                             prop.value confirm; prop.onChange setConfirm ]
                                Html.button [
                                    prop.type' "button"; prop.className "btn btn-primary"
                                    prop.disabled (busy || nickname.Length < 3 || password.Length < 8 || password <> confirm)
                                    prop.onClick (fun _ -> run (Api.register nickname password))
                                    prop.text (if busy then "Registering..." else "Register")
                                ]
                            ]
                        ]
                ]
            ]
        ]
    ]