module External

open System
open System.Net.Http
open System.Net.Http.Headers
open Newtonsoft.Json.Linq

[<RequireQualifiedAccess>]
module API =
    let [<Literal>] DarkCoin = 1088771340UL

    let private vClient = new HttpClient()
    let private pClient = new HttpClient()

    let private getJsonRequest(uri:string) =
        let request = new HttpRequestMessage()
        request.Method <- HttpMethod.Get
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"))
        request.RequestUri <- Uri(uri)
        request

    let private getUsdPrice(assetId: uint64) =
        task {
            try
                let uri = $"https://api.vestigelabs.org/assets/price?asset_ids={assetId}&network_id=0&denominating_asset_id=31566704"
                use request = getJsonRequest uri
                let! response = vClient.SendAsync(request)
                let! content = response.Content.ReadAsStringAsync()
                let jObj = JArray.Parse(content)
                return jObj.[0].["price"].Value<decimal>() |> Some
            with _ ->
                return None
        }

    let getDarkCoinPrice() = getUsdPrice(DarkCoin)
    
    let getAssetInfo(assetId: uint64) =
        task {
            try
                let uri = $"https://mainnet.api.perawallet.app/v1/public/assets/{assetId}/"
                use request = getJsonRequest uri
                let! response = pClient.SendAsync(request)
                let! content = response.Content.ReadAsStringAsync()
                let jObj = JObject.Parse(content)

                let isCollectible = jObj.["is_collectible"].Value<bool>()

                if not isCollectible then
                    return Error ($"Asset {assetId} is not a collectible")
                else
                    let collectible = jObj.["collectible"]
                    let ipfs = collectible.["thumbnail_ipfs_cid"].Value<string>()
                    let name = jObj.["name"].Value<string>()
                    let externalUrl =
                        let token = collectible.["metadata"].["external_url"]
                        if token = null || token.Type = JTokenType.Null then ""
                        else
                            let s = token.Value<string>()
                            if System.String.IsNullOrWhiteSpace(s) then ""
                            else s
                    let ai = Types.AssetInfo(assetId, name, ipfs, externalUrl)
                    return ai |> Ok
            with exn ->
                return Error (exn.Message)
        }