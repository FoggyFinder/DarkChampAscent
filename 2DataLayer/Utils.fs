module Utils

open System.Data

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