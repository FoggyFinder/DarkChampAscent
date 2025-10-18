namespace Gen

type TextPayload = {
    Name: string
    Description: string
}

type ImgPayload = {
    Data: byte []
}

[<RequireQualifiedAccess>]
type GenPayload =
    | TextReqCreated of prompt:string
    | TextReqReceived of id:string
    | TextPayloadReceived of TextPayload
    | ImgReqReceived of id:string * TextPayload
    | ImgPayloadReceived of ImgPayload * TextPayload

type GenStatus =
    | RequstCreated = 0 // coins charged from user's balance and added to locked amount
    | TextRequestReceived = 1 // text request send to API and its id is returned
    | TextPayloadReceived = 2 // name and description was generated
    | ImgRequestReceived = 3 // img request sent to API and its id is returned
    | ImgPayloadReceived = 4 // complete data is received
    | Success = 5 // coins are removed from locked amount and added to rewards
    | Failure = 6 // indicates that request must be repeated

[<RequireQualifiedAccess>]
module Prompt =
    open GameLogic.Monsters
    let createMonsterNameDesc (mtype:MonsterType) (subtype:MonsterSubType) =
        let subtype' = if subtype = MonsterSubType.None then "" else $" and {subtype} subtype"
        $"""Please, generate a single, unique name and description for a monster in discord game in "sword and magic" settings. This monster has {mtype} type{subtype'}.
    For example, name for a monster with zombie type - Rotting Cadaver and for a monster with demon type and frost subtype - Glacial Abomination.
    Return reply in form of json with 2 fields - name and description without leading json tag".
    """
