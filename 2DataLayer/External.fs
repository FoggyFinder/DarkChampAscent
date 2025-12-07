module External

open System.Net.Http
open Newtonsoft.Json.Linq

[<RequireQualifiedAccess>]
module API =
    let [<Literal>] DarkCoin = 1088771340UL
    let private getUsdPrice(assetId: uint64) =
        let client = new HttpClient()
        async {
            try
                let uri = $"https://api.vestigelabs.org/assets/price?asset_ids={assetId}&network_id=0&denominating_asset_id=31566704"
                use request = new System.Net.Http.HttpRequestMessage()
                request.Method <- System.Net.Http.HttpMethod.Get
                request.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"))
                request.RequestUri <- System.Uri(uri)
                let! response = client.SendAsync(request) |> Async.AwaitTask
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                let jObj = JArray.Parse(content)
                return jObj.[0].SelectToken("price").Value<decimal>() |> Some
            with exp ->
                return None
        } |> Async.RunSynchronously

    let getDarkCoinPrice() = getUsdPrice(DarkCoin)