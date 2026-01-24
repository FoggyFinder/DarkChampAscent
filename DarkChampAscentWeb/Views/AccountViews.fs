module AccountView

open Falco.Markup
open Types
open UI
open Display
open System

let private userSection (user:DiscordUser) (balance:decimal) (price: decimal option) =
    let usdc' = price |> Option.map(fun p -> Math.Round(p * balance, 6).ToString()) |> Option.defaultValue ""
    Elem.section [ 
        Attr.class' "block user-section"
        Attr.id "user-section"
        XmlAttribute.KeyValueAttr("aria-labelledby", "hb-user")
    ] [
        Elem.div [ ] [
            Elem.form [
                Attr.id "donateForm"
                Attr.action Route.donate
                Attr.method "post"
            ] [
                Elem.table [] [
                    Elem.tr [] [
                        Elem.td [] [
                            match user.Pic with
                            | Some pic ->
                                Elem.img [
                                    Attr.class' "picSmall"
                                    Attr.src pic
                                ]
                            | None -> ()
                        ]
                        Elem.td [] [ Text.raw $"{user.Nickname}" ]
                    ]

                    Elem.tr [] [
                        Elem.td [] [ Text.raw "Balance" ]
                        Elem.td [] [ Text.raw $"{Display.toRound6StrD balance} {WebEmoji.DarkCoin} DarkCoins (~{usdc'} {WebEmoji.USDC})" ]
                    ]

                    Elem.tr [] [
                        Elem.td [] [ 
                            Elem.input [
                                Attr.typeNumber
                                Attr.value "1000"
                                Attr.min "0"
                                Attr.step "1"
                                Attr.class' "form-control"
                                Attr.id "amount"
                                Attr.name "amount"
                                Attr.inputmode "numeric"
                                Attr.pattern "\d*"
                            ]
                        ]

                        Elem.td [] [ 
                            Elem.button [
                                Attr.class' "btn btn-primary"
                                Attr.typeSubmit
                            ] [
                                Text.raw "Donate"
                            ]
                        ]
                    ]
                ]
            ]

            Ui.howToDeposit

            Elem.form [
                Attr.id "logOutForm"
                Attr.action Route.logout
                Attr.method "post"
            ] [
                Elem.button [
                    Attr.class' "btn btn-primary float-end"
                    Attr.typeSubmit
                ] [
                    Text.raw "Log out"
                ]
            ]
        ]

        Elem.script [] [
            Text.raw """
        document.addEventListener('DOMContentLoaded', function () {
          var form = document.getElementById('donateForm');
          var amount = document.getElementById('amount');
          form.addEventListener('submit', function (ev) {
            if (!confirm('Are you sure you want to donate ' + amount.value + ' ?')) {
              ev.preventDefault(); // cancel submission
            }
          });
        });
        """
        ]
    ]   
    
let private walletsSection (wallets:Wallet list) =
    Elem.section [ 
        Attr.class' "block wallets-section"
        Attr.id "wallets-section"
        XmlAttribute.KeyValueAttr("aria-labelledby", "hb-wallets")
    ] [
        Elem.div [ ] [
            Text.raw "To confirm your registered wallet send 0-cost Algo tx to"
        
            Elem.span [
                Attr.create "class" "wallet-address"
                Attr.create "data-wallet" KnownWallets.DarkChampAscent
                Attr.create "title" KnownWallets.DarkChampAscent
            ] [
                Text.raw KnownWallets.DarkChampAscent
            ]
            
            Elem.button [
                Attr.create "type" "button"
                Attr.create "class" "copy-btn"
                Attr.create "data-copy" KnownWallets.DarkChampAscent
                Attr.create "aria-label" "Copy wallet address"
                Attr.create "title" "Copy address"
            ] [
                Text.raw "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' fill='none' aria-hidden='true'><rect x='9' y='9' width='13' height='13' rx='2'></rect><path d='M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1'></path></svg>"
            ]

            Text.raw "with confirmation code as a note"
        ]

        if wallets.IsEmpty then
            Text.raw $"You haven't registered any wallet yet."
        else
            Text.h2 "My wallets"
            Elem.table [] [
                Elem.tr [] [
                    Elem.th [] [ Text.raw "Address" ]
                    Elem.th [] [ Text.raw "IsConfirmed" ]
                    Elem.th [] [ Text.raw "Code" ]
                ]
                for wallet in wallets do
                    Elem.tr [] [
                        Elem.td [] [ Text.raw $"{wallet.Wallet}" ]
                        Elem.td [] [
                            if wallet.IsConfirmed then
                                Elem.div [ Attr.class' "yes" ] [
                                    Text.raw "✓"
                                ]
                            else
                                Elem.div [ Attr.class' "no" ] [
                                    Text.raw "✖"
                                ]
                        ]
                        Elem.td [] [ Text.raw wallet.Code ]
                    ]
            ]

        Elem.div [ ] [
            Elem.form [
                Attr.methodPost
                Attr.action Route.walletRegister
                Attr.class' "wallet-form"
            ] [
                Elem.input [
                    Attr.typeText
                    Attr.id "wallet"
                    Attr.name "wallet"
                    Attr.class' "wallet-input"
                    Attr.placeholder "Algorand wallet, e.g. G6YFTYHG5NGTRLUWYVZOY2OODHYJEFA4E57M4HN7NKP4NC3EUQJWMT5ZMA"
                ]

                Elem.input [
                    Attr.class' "btn btn-primary"
                    Attr.typeSubmit
                    Attr.value "Register new wallet"
                ]
            ]
        ]
    ]

let private infoSection (champs:int, monsters: int) =
    Elem.section [ 
        Attr.class' "block info-section"
        Attr.id "info-section"
        XmlAttribute.KeyValueAttr("aria-labelledby", "hb-info")
    ] [
        Elem.table [] [
            Elem.tr [] [
                Elem.td [] [ Text.raw $"Champs" ]
                Elem.td [] [ 
                    Elem.a [ Attr.href Route.mychamps ] [ Text.raw $"{champs}" ] 
                ]
            ]

            Elem.tr [] [
                Elem.td [] [ Text.raw $"Monsters" ]
                Elem.td [] [
                    Elem.a [ Attr.href Route.mymonsters ] [ Text.raw $"{monsters}" ]
                ]
            ]
        ]
    ]

let accountView (userAccount: UserAccount) (price:decimal option) =
    Elem.main [
        Attr.class' "acc-dashboard"
        Attr.role "main"
        XmlAttribute.KeyValueAttr("aria-label", "Account dashboard")
    ] [
        userSection userAccount.User userAccount.Balance price
        infoSection (userAccount.Champs, userAccount.Monsters)
        walletsSection userAccount.Wallets
    ]