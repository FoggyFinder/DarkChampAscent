namespace Gen
open GameLogic.Monsters
open System.Text.Json.Serialization

type TextPayload = {
    [<JsonPropertyName("name")>] Name: string
    [<JsonPropertyName("description")>] Description: string
}

type ImgPayload = {
    Data: byte []
}

type GenStatus =
    | RequstCreated = 0 // coins charged from user's balance and added to locked amount
    | TextRequestReceived = 1 // text request send to API and its id is returned
    | TextPayloadReceived = 2 // name and description was generated
    | ImgRequestReceived = 3 // img request sent to API and its id is returned
    | Failure = 4 // failure
    | Success = 5

[<RequireQualifiedAccess>]
type GenPayload =
    | TextReqCreated of prompt:string
    | TextReqReceived of id:string
    | TextPayloadReceived of TextPayload
    | ImgReqReceived of id:string * TextPayload
    | Failure of prevStep:GenFailure
    | Success
    member t.Status =
        match t with
        | TextReqCreated _ -> GenStatus.RequstCreated
        | TextReqReceived _ -> GenStatus.TextRequestReceived
        | TextPayloadReceived _ -> GenStatus.TextPayloadReceived
        | ImgReqReceived _ -> GenStatus.ImgRequestReceived
        | Failure _ -> GenStatus.Failure
        | Success -> GenStatus.Success
    member t.IsFinished =
        match t with
        | Success -> true
        | Failure f ->
            match f with
            | GenFailure.Final _ -> true
            | GenFailure.Repeat _ -> false
        | _ -> false
    member t.IsFinalError =
        match t with
        | Failure f ->
            match f with
            | GenFailure.Final _ -> true
            | GenFailure.Repeat _ -> false
        | _ -> false

and [<RequireQualifiedAccess>]
GenFailure =
    | Final of string
    | Repeat of GenPayload

[<RequireQualifiedAccess>]
module Prompt =
    
    let createMonsterNameDesc (mtype:MonsterType) (subtype:MonsterSubType) =
        let subtype' = if subtype = MonsterSubType.None then "" else $" and {subtype} subtype"
        let v1 = $"""Please, generate a single, unique name and description for a monster in discord game in "sword and magic" settings. This monster has {mtype} type{subtype'}.
            For example, name for a monster with zombie type - Rotting Cadaver and for a monster with demon type and frost subtype - Glacial Abomination."""
        let v2 = $"""Generate a unique name and description for a monster in a fantasy Discord game. The monster is a {mtype} with {subtype'} subtype.
            Create a title that is 2 words and a description that is 2-3 sentences. The tone should be dark, mystical, and ominous. For example, a zombie monster could be named 'Rotting Cadaver' and a demon with frost subtype could be named 'Glacial Abomination'. Use a similar style to create a name and description for this monster."""
        $"{v2}. Return reply in form of json with 2 fields - name and description without leading json tag"
