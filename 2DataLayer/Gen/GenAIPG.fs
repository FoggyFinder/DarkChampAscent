module GenAIPG

open System.Text.Json.Serialization
open System.Text.Json
open System.Net.Http
open System.Text
open System
open Microsoft.Extensions.Options
open Conf

type TextRequestParams(?maxContextLength:int, ?maxLength:int, ?temperature:float, ?topP:float) =
    [<JsonPropertyName("max_context_length")>]
    member val MaxContextLength = maxContextLength
    [<JsonPropertyName("max_length")>]
    member val MaxLength = maxLength
    [<JsonPropertyName("temperature")>]
    member val Temperature = temperature
    [<JsonPropertyName("top_p")>]
    member val TopP = topP

type GenerateTextRequest(prompt:string, models: string array, tparams:TextRequestParams) =
    [<JsonPropertyName("prompt")>]
    member val Prompt = prompt
    [<JsonPropertyName("models")>]
    member val Models = models
    [<JsonPropertyName("params")>]
    member val Params = tparams

type TextGenMetadata = {
    [<JsonPropertyName("ref")>]
    Ref: string option
    [<JsonPropertyName("type")>]
    Type: string
    [<JsonPropertyName("value")>]
    Value: string
}

type TextGeneration = {
    [<JsonPropertyName("model")>]
    Model: string option
    [<JsonPropertyName("state")>]
    State: string
    [<JsonPropertyName("worker_id")>]
    WorkerId: string option
    [<JsonPropertyName("worker_name")>]
    WorkerName: string option
    [<JsonPropertyName("gen_metadata")>]
    GenMetadata: TextGenMetadata array option
    [<JsonPropertyName("seed")>]
    Seed: int option
    [<JsonPropertyName("text")>]
    Text: string option
}

type TextCompleteResponse = {
    [<JsonPropertyName("done")>]
    Done: bool
    [<JsonPropertyName("faulted")>]
    Faulted: bool
    [<JsonPropertyName("finished")>]
    Finished: int
    [<JsonPropertyName("is_possible")>]
    IsPossible: bool
    [<JsonPropertyName("kudos")>]
    Kudos: float
    [<JsonPropertyName("processing")>]
    Processing: int
    [<JsonPropertyName("queue_position")>]
    QueuePosition: int
    [<JsonPropertyName("restarted")>]
    Restarted: int
    [<JsonPropertyName("wait_time")>]
    WaitTime: int
    [<JsonPropertyName("waiting")>]
    Waiting: int
    [<JsonPropertyName("generations")>]
    Generations: TextGeneration array option
}

// Params immutable class with option fields
type Params
    (
        ?height: int,
        ?samplerName: string,
        ?width: int,
        ?n: int,
        ?steps: int
    ) =
    [<JsonPropertyName("height")>] 
    member val Height = height
    [<JsonPropertyName("sampler_name")>]
    member val SamplerName = samplerName
    [<JsonPropertyName("width")>]
    member val Width = width
    [<JsonPropertyName("n")>]
    member val N = n
    [<JsonPropertyName("steps")>]
    member val Steps = steps

// Main request immutable class with option fields
type GenerateRequest
    (
        prompt: string,
        models: string array,
        parameters: Params
    ) =
    [<JsonPropertyName("prompt")>]
    member val Prompt = prompt
    [<JsonPropertyName("models")>]
    member val Models = models
    [<JsonPropertyName("params")>]
    member val Parameters = parameters

type GenerateResponse =
    { id: string }

type StatusResponse =
    {   
        [<JsonPropertyName("done")>] done': bool
        [<JsonPropertyName("faulted")>] faulted: bool
        [<JsonPropertyName("finished")>] finished: int
        [<JsonPropertyName("is_possible")>] isPossible: bool
        [<JsonPropertyName("kudos")>] kudos: float
        [<JsonPropertyName("processing")>] processing: int
        [<JsonPropertyName("queue_position")>] queuePosition: int
        [<JsonPropertyName("restarted")>] restarted: int
        [<JsonPropertyName("wait_time")>] waitTime: int
        [<JsonPropertyName("waiting")>] waiting: int
    }

type GenMetadata =
    {   
        [<JsonPropertyName("ref")>] ref: string option
        [<JsonPropertyName("type")>] gtype: string
        [<JsonPropertyName("value")>] value: string
    }

type Generation =
    {   
        [<JsonPropertyName("model")>] model: string option
        [<JsonPropertyName("state")>] state: string
        [<JsonPropertyName("worker_id")>] workerId: string option
        [<JsonPropertyName("worker_name")>] workerName: string option
        [<JsonPropertyName("censored")>] censored: bool option
        [<JsonPropertyName("gen_metadata")>] genMetadata: GenMetadata array option
        [<JsonPropertyName("id")>] id: string option
        [<JsonPropertyName("img")>] img: string option
        [<JsonPropertyName("seed")>] seed: string option
    }

type CompleteResponse =
    {   
        [<JsonPropertyName("done")>] done': bool
        [<JsonPropertyName("faulted")>] faulted: bool
        [<JsonPropertyName("finished")>] finished: int
        [<JsonPropertyName("is_possible")>] isPossible: bool
        [<JsonPropertyName("kudos")>] kudos: float
        [<JsonPropertyName("processing")>] processing: int
        [<JsonPropertyName("queue_position")>] queuePosition: int
        [<JsonPropertyName("restarted")>] restarted: int
        [<JsonPropertyName("wait_time")>] waitTime: int
        [<JsonPropertyName("waiting")>] waiting: int
        [<JsonPropertyName("generations")>] generations: Generation array
        [<JsonPropertyName("shared")>] shared: bool option
    }

let jsonOptions =
    let opts = JsonSerializerOptions()
    opts.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    opts.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    opts

let serialize obj = JsonSerializer.Serialize(obj, jsonOptions)
let deserialize<'T> (json: string) = JsonSerializer.Deserialize<'T>(json, jsonOptions)

type AipgGen(options:IOptions<GenConfiguration>) =
    let apiBase = "https://api.aipowergrid.io/api/v2/generate"
    let httpClient = new HttpClient()
    do
        httpClient.DefaultRequestHeaders.Add("apikey", options.Value.AIPG)
    
    let generateTextAsync (req: GenerateTextRequest) : Async<Result<string, string>> =
        async {
            try
                let url = $"{apiBase}/text/async"
                let json = serialize req
                use content = new StringContent(json, Encoding.UTF8, "application/json")
                let! response = httpClient.PostAsync(url, content) |> Async.AwaitTask
                let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                if response.IsSuccessStatusCode then
                    let result = deserialize<GenerateResponse>(body)
                    return Ok(result.id)
                else
                    return Error($"Text generation request failed: {body}")
            with e ->
                return Error(e.ToString())
        }

    let rec getGenerateTextAsync (id: string) (attempt:int) : Async<Result<string, string>> =
        if attempt >= 10 then async { return Error "Max attempts reached" }
        else
            async {
                try
                    let url = $"{apiBase}/text/status/{id}"
                    let! response = httpClient.GetAsync(url) |> Async.AwaitTask
                    let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    if response.IsSuccessStatusCode then
                        let cresponse = deserialize<TextCompleteResponse>(body)
                        if cresponse.Faulted then
                            return Error("Internal server error, could not complete request (faulted=true)")
                        elif not cresponse.IsPossible then
                            return Error("Request is not possible with current pool of workers (is_possible=false)")
                        elif cresponse.Done then
                            match cresponse.Generations with
                            | Some arr when arr.Length > 0 ->
                                return Ok(arr.[0].Text |> Option.defaultValue "")
                            | _ -> return Error($"Unexpected response - generations array was empty: {body}")
                        else
                            let ts = TimeSpan.FromSeconds(int64 cresponse.WaitTime)
                            // to not spam with request
                            let ts' = if ts.TotalSeconds < 60.0 then TimeSpan.FromSeconds(60L) else ts
                            do! Async.Sleep(ts')
                            return! getGenerateTextAsync id (attempt + 1)
                    else
                        return Error($"Status check request failed: {body}")
                with e ->
                    return Error(e.ToString())
            }

    let generateImageAsync (req: GenerateRequest) : Async<Result<string, string>> =
        async {
            try
                let url = $"{apiBase}/async"
                let json = serialize req
                use content = new StringContent(json, Encoding.UTF8, "application/json")
                let! response = httpClient.PostAsync(url, content) |> Async.AwaitTask
                let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                if response.IsSuccessStatusCode then
                    let result = deserialize<GenerateResponse>(body)
                    return Ok(result.id)
                else
                    return Error($"Image generation request failed: {body}")
            with e ->
                return Error(e.ToString())
        }

    let rec pollStatusAsync (id: string) : Async<Result<unit, string>> =
        async {
            try
                let url = $"{apiBase}/check/{id}"
                let! response = httpClient.GetAsync(url) |> Async.AwaitTask
                let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                if response.IsSuccessStatusCode then
                    let status = deserialize<StatusResponse>(body)
                    if status.faulted then
                        return Error("Internal server error, could not complete request (faulted=true)")
                    elif not status.isPossible then
                        return Error("Request is not possible with current pool of workers (is_possible=false)")
                    elif status.done' then
                        return Ok()
                    else
                        let ts = TimeSpan.FromSeconds(int64 status.waitTime)
                        // to not spam with request
                        let ts' = if ts.TotalSeconds < 45.0 then TimeSpan.FromSeconds(45L) else ts
                        do! Async.Sleep(ts')
                        return! pollStatusAsync id
                else
                    return Error($"Status check request failed: {body}")
            with e ->
                return Error(e.ToString())
        }

    let fetchCompleteResponseAsync (id: string) : Async<Result<Generation, string>> =
        async {
            try
                let url = $"{apiBase}/status/{id}"
                let! response = httpClient.GetAsync(url) |> Async.AwaitTask
                let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                if response.IsSuccessStatusCode then
                    let result = deserialize<CompleteResponse>(body)
                    if result.done' then
                        if result.generations.Length > 0 then
                            return result.generations.[0] |> Ok
                        else
                            return Error($"Empty generation: {body}")
                    else return Error($"Unexpected response: {body}")
                else
                    return Error($"Image complete request failed: {body}")
            with e ->
                return Error(e.ToString())
        }

    let downloadImageAsync (url: string)  : Async<Result<byte[], string>> =
        async {
            try
                let! response = httpClient.GetAsync(url) |> Async.AwaitTask
                if response.IsSuccessStatusCode then
                    let! bytes = response.Content.ReadAsByteArrayAsync() |> Async.AwaitTask
                    return Ok(bytes)
                else
                    let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    return Error($"Failed to download image: {body}")
            with e ->
                return Error(e.ToString())
        }

    member _.GenerateTextAsync = generateTextAsync
    member _.GetGeneratedTextAsync id = getGenerateTextAsync id 0
    member _.GenerateImageAsync = generateImageAsync
    member _.PollStatusAsync = pollStatusAsync
   
    member _.FetchCompleteResponseAsync (id:string) =
        async {
            try
                let! res = pollStatusAsync id
                let! res' = async {
                    match res with
                    | Ok() ->
                        let! cr = fetchCompleteResponseAsync id
                        match cr with
                        | Ok gen when gen.img.IsSome ->
                            return! downloadImageAsync gen.img.Value
                        | Ok gen ->
                            return Error $"No image found in CompleteResponse generations: {gen}"
                        | Error err -> return Error err
                    | Error err -> return Error err
                    }
                return res'
            with e ->
                return Error(e.ToString())
    }
        
