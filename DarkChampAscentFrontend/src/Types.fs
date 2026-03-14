module Types

[<RequireQualifiedAccess>]
module Links =
    let [<Literal>] Github    = "https://github.com/FoggyFinder/DarkChampAscent"
    let [<Literal>] Discord   = "https://discord.gg/bYPtQhYKwN"
    let [<Literal>] IPFS      = "https://ipfs.dark-coin.io/ipfs/"
    let [<Literal>] DarkChampCollection = "https://www.randgallery.com/collections/Dark%20Coin%20Champions?sort=price_asc&buy_now=true"
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
    | MonstersEffects
    | ChampDetail of uint64
    | MonsterDetail of uint64
    | TopGeneral
    | TopChamps
    | TopMonsters
    | TopDonaters
    | TopUnknownDonaters
    | Traits
    | FAQ
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
        | MonstersEffects    -> "#/monstersundereffects"
        | ChampDetail id     -> $"#/champ/{id}"
        | MonsterDetail id   -> $"#/monster/{id}"
        | TopGeneral         -> "#/top"
        | TopChamps          -> "#/top/champs"
        | TopMonsters        -> "#/top/monsters"
        | TopDonaters        -> "#/top/donaters"
        | TopUnknownDonaters -> "#/top/donaters/unknown"
        | Traits             -> "#/traits"
        | FAQ                -> "#/faq"
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
