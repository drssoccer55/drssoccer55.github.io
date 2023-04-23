module Doglas.Client.Spreadsheet

type Spreadsheet =
    {
        version: string
        reqId: string
        status: string
        ``sig``: string
        table: Table
    }
and Table =
    {
        cols: Column list
        rows: Row list
        parsedNumHeaders: int
    }
and Column =
    {
        id: string
        label: string
        ``type``: string
    }
and Row =
    {
        c: RowValue list
    }
and RowValue =
    {
        v: string // Values are always strings and need to mark dates/numbers as plain text in sheets
    }

let getRowsFilteredForKey (spreadsheet:Spreadsheet) (filter:string) =
    fun (r: Row) ->
        (List.tryHead r.c |> Option.map (fun k -> k.v = filter)) = Some(true)
    |> List.filter <| spreadsheet.table.rows
