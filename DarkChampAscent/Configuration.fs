module Conf

type WalletConfiguration() =
    member val GameWallet: string = "" with get, set
    member val DAOWallet: string = "" with get, set
    member val BurnWallet: string = "" with get, set
    member val DevsWallet: string = "" with get, set
    member val ReserveWallet: string = "" with get, set

type ChainConfiguration() =
    member val GameWalletKeys: string = "" with get, set

type DbConfiguration() =
    member val BackupFolder: string = "" with get, set

type GenConfiguration() = 
    member val AIPG: string = "" with get, set
    member val ImgFolder: string = "" with get, set

type Configuration() =
    member val Wallet: WalletConfiguration = WalletConfiguration() with get, set
    member val Chain: ChainConfiguration = ChainConfiguration() with get, set
    member val Db: DbConfiguration = DbConfiguration() with get, set
    member val Gen: GenConfiguration = GenConfiguration() with get, set
