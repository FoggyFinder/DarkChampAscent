module UseWallet

open Fable.Core
open Feliz

[<Erase>]
type UseWallet =
    [<ReactComponent("WalletProvider", "@txnlab/use-wallet-react")>]
    static member WalletProvider(manager: obj, children: ReactElement) = React.Imported()

[<Import("WalletManager", "@txnlab/use-wallet-react")>]
type WalletManager(config: obj) = class end

type IWallet =
    abstract id       : string
    abstract connect  : unit -> JS.Promise<unit>
    abstract disconnect : unit -> JS.Promise<unit>

type IUseWalletResult =
    abstract wallets          : IWallet[]
    abstract activeWallet     : IWallet option
    abstract activeAddress    : string option
    abstract signTransactions : obj[] -> JS.Promise<byte[][]>

[<Import("useWallet", "@txnlab/use-wallet-react")>]
let useWallet () : IUseWalletResult = jsNative