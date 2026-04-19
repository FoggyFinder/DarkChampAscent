module Utils

open System.Data
open GameLogic.Monsters
open System

let eps = 0.001M
let isCloseEnough (x:decimal) (y:decimal) = Math.Abs(x - y) < eps

let ofList (xs:Result<_,_> list) =
    if xs |> List.exists(fun r -> r.IsError) then
        None
    else
        xs |> List.map(function|Ok ok -> ok | _ -> failwith "invalid") |> Some

// https://stackoverflow.com/q/2983087
let getBytesData (reader : IDataReader) (i:int) = 
    let len = reader.GetBytes(i, int64 0, null, 0, 0);
    // Create a buffer to hold the bytes, and then
    // read the bytes from the DataTableReader.
    let buffer : byte array = Array.zeroCreate (int32 len)
    reader.GetBytes(i, int64 0, buffer, 0, int32 len) |> ignore
    buffer

[<RequireQualifiedAccess>]
module MonsterImg =
    let DefaultName(mtype:MonsterType, msubtype:MonsterSubType) =
        mtype.ToString().ToLower() + "_" + msubtype.ToString().ToLower() + ".png"
    let DefaultFile(monster:Monster) =
        let filename = DefaultName (monster.MType, monster.MSubType)
        System.IO.Path.Combine("Assets", filename) |> MonsterImg.File


type IReadOnlySignal<'a> =
    abstract Value: 'a
    abstract Publish: IEvent<'a>

type Signal<'a when 'a: equality>(initial: 'a) =
    let mutable current = initial
    let locker = obj()
    let changed = Event<'a>()

    member _.Value = lock locker (fun _ -> current)
    member _.Publish = changed.Publish

    member _.Set(value: 'a) =
        lock locker (fun _ ->
            if current <> value then
                current <- value
                changed.Trigger value)

    interface IReadOnlySignal<'a> with
        member this.Value = this.Value
        member this.Publish = this.Publish
