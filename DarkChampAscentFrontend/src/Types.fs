module Types

[<RequireQualifiedAccess>]
module Links =
    let [<Literal>] Github    = "https://github.com/FoggyFinder/DarkChampAscent"
    let [<Literal>] Discord   = "https://discord.gg/bYPtQhYKwN"
    let [<Literal>] IPFS      = "https://ipfs.dark-coin.io/ipfs/"
    let [<Literal>] DarkCoinIo = "https://dark-coin.io/"
    let [<Literal>] DarkChampCollection = "https://www.downbad.farm/collection/dark-coin-champions"
    let [<Literal>] AIPG      = "https://aipowergrid.io/"

[<RequireQualifiedAccess>]
module KnownWallets =
    let [<Literal>] DarkChampAscent    = "SZYECJF52SCJYMDQG4M3RGGLDTQPEEPTD36SW4PABEGZ6MCA4KH67QKRAU"
    let [<Literal>] DarkChampAscentNFD = "darkchampascent.algo"

[<RequireQualifiedAccess>]
type Page =
    | Home
    | Login
    | Account
    | Battle
    | Shop
    | Storage
    | MyChamps
    | MyChampsEffects
    | MyMonsters
    | MyRequests
    | DefeatedChamps
    | DefeatedMonsters
    | ChampDetail of uint64
    | MonsterDetail of uint64
    | UserDetail of uint64
    | TopGeneral
    | TopChamps
    | TopMonsters
    | TopDonaters
    | Traits
    | Stats
    | NotFound
    | Tokenomics
    member t.Route =
        match t with
        | Home               -> "#/"
        | Login              -> "#/login"
        | Account            -> "#/account"
        | Battle             -> "#/battle"
        | Shop               -> "#/shop"
        | Storage            -> "#/storage"
        | MyChamps           -> "#/mychamps"
        | MyChampsEffects    -> "#/mychampsundereffects"
        | MyMonsters         -> "#/mymonsters"
        | MyRequests         -> "#/myrequests"
        | DefeatedMonsters    -> "#/monstersdefeated"
        | DefeatedChamps    -> "#/champsdefeated"
        | ChampDetail id     -> $"#/champ/{id}"
        | MonsterDetail id   -> $"#/monster/{id}"
        | UserDetail id -> $"#/user/{id}"
        | TopGeneral         -> "#/top"
        | TopChamps          -> "#/top/champs"
        | TopMonsters        -> "#/top/monsters"
        | TopDonaters        -> "#/top/donaters"
        | Traits             -> "#/traits"
        | Stats              -> "#/stats"
        | NotFound           -> "#/notfound"
        | Tokenomics -> "#/tokenomics"
    member t.Title =
        t.ToString()

type Deferred<'T> =
    | NotStarted
    | Loading
    | Loaded of 'T
    | Failed of string

open Browser.Dom
[<RequireQualifiedAccess>]
module Nav =
    let navigateTo (route: string) =
        window.location.hash <- route.TrimStart('#')

    let navTo (route:string) (e: Browser.Types.MouseEvent) =
        e.preventDefault()
        navigateTo route
