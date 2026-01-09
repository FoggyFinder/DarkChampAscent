module GenView

open Falco.Markup
open System
open Gen

let myRequests (requests: (int64 * DateTime * GenStatus) list) =
    Elem.main [
        Attr.class' "my-champs"
        Attr.role "main"
    ] [
        if requests.IsEmpty then
            Text.raw $"No pending requests."
        else
            Elem.table [] [
                Elem.tr [] [
                    Elem.th [] [ Text.raw "" ]
                    Elem.th [] [ Text.raw "ID" ]
                    Elem.th [] [ Text.raw "Timestamp" ]
                    Elem.th [] [ Text.raw "Status" ]
                ]
                for (i, (rId, dt, status)) in requests |> List.indexed do
                    Elem.tr [] [
                        Elem.td [] [ Text.raw $"{i + 1}" ]
                        Elem.td [] [ Text.raw $"{rId}" ]
                        Elem.td [] [ Text.raw $"{dt}" ]
                        Elem.td [] [ Text.raw $"{status}" ]
                    ]
            ]
    ]