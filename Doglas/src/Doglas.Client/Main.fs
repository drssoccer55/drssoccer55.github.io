module Doglas.Client.Main

open System
open System.Net.Http
open System.Net.Http.Json
open Microsoft.AspNetCore.Components
open Elmish
open Bolero
open Bolero.Html
open Microsoft.JSInterop
open System.Text.Json
open Spreadsheet
open Chart
open Utils

/// Routing endpoints definition.
// I don't think the EndPoint attribute gets used now that I have a custom router.
type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/graphs">] Graphs
    | [<EndPoint "/party">] Party

/// The Elmish application's model.
type Model =
    {
        page: Page
        error: string option
        graphs: Graph list option // Todo break up this model why tf does it start like this
        partyComments: PartyComment list option
    }

and Graph =
    {
        chart: Chart
        key: string
    }
and PartyComment =
    {
        name: string
        comment: string
        htmlColor: string
    }

let initModel =
    {
        page = Home
        error = None
        graphs = None
        partyComments = None
    }


/// The Elmish application's update messages.
type Message =
    | SetPage of Page
    | GetGraphs
    | GotGraphs of Graph list
    | RenderGraphs
    | GetPartyComments
    | GotPartyComments of PartyComment list
    | Error of exn
    | ClearError

let log (js: IJSRuntime) (s:obj) = js.InvokeVoidAsync("console.log", s) |> ignore
let error (js: IJSRuntime) (s:obj) = js.InvokeVoidAsync("console.error", s) |> ignore
let strToDate (s:string) =
    match DateTime.TryParse s with
    | true, dt ->
        let t = dt - DateTime.UnixEpoch
        t.TotalMilliseconds |> float
    | _ -> 0 |> float // TODO

let stringToJsonStr (s:string) =
    // Try to skip complexity for now and assume response is always same prefix and suffix
    let prefixToRemove = "google.visualization.Query.setResponse("
    let sIndex = s.IndexOf(prefixToRemove)
    // Need to be careful with substring bc errors silently ignored
    match sIndex with
    | -1 -> None
    | d -> s.Substring(d + prefixToRemove.Length, s.Length - d - prefixToRemove.Length - 2) |> Some

let jsonToSpreadsheet js (s:string) =
    try
        JsonSerializer.Deserialize<Spreadsheet>(s) |> Some
    with
        | :? System.ArgumentNullException -> error js "Cannot convert json to spreadsheet because argument is null"; None
        | :? System.Text.Json.JsonException as ex -> error js ("Cannot convert json to spreadsheet because the json is invalid: " + ex.ToString()); None
        | :? System.NotSupportedException as ex -> error js ("Cannot convert json to spreadsheet because no compatible deserializer: " + ex.ToString()); None

let update (http: HttpClient) (js: IJSRuntime) message model =
    log js message
    match message with
    | SetPage page ->
        match page with
        | Graphs -> { model with page = page }, Cmd.ofMsg RenderGraphs // if navigating to graphs need to render
        | _ -> { model with page = page }, Cmd.none
    | GetGraphs ->
        let spreadsheetToGraphs (s:Spreadsheet option) =
            match s with
            | None -> []
            | Some s ->
                let keys = Spreadsheet.getKeys s |> Set.remove "Key" |> Set.toList  // key is a reserved work
                let keyRows =
                    List.map (fun k -> k, Spreadsheet.getRowsFilteredForKey s k) keys
                    |> Map.ofList

                let keyRowsMap =
                    fun (r:Row) ->
                        {
                            x = r.c.Item(1).v |> strToDate
                            y = r.c.Item(2).v |> float
                        }
                    |> List.map
                    |> konst
                    |> Map.map <| keyRows

                fun (key, dataRow) ->
                    let chart =
                        {
                            ``type`` = "scatter"
                            data = {
                                    datasets = [{
                                        DataSet.label = key
                                        DataSet.data = dataRow
                                    }]
                                }
                            options = {
                                scales = {
                                    x = {
                                        ticks = {
                                            callback = """return moment(label).format("MM/DD/YY hh:mm");"""
                                        }
                                    }
                                }
                            } |> Some
                    }

                    {
                        key = key
                        chart = chart
                    }
                |> List.map <| Map.toList keyRowsMap

        let strToGraphs = stringToJsonStr >> Option.bind (jsonToSpreadsheet js) >> spreadsheetToGraphs >> GotGraphs
        let getSpreadsheetTask () = 
            http.GetStringAsync("https://docs.google.com/spreadsheets/u/0/d/1y0eRzRaPnncd5ckQdI6tgbhHH5g9fGwNI-Tbq-7SMWY/gviz/tq?tqx=out:json&tq=select+*")
            |> Task.map strToGraphs

        let cmd = Cmd.OfTask.either getSpreadsheetTask () id Error
        { model with graphs = None }, cmd
    | GotGraphs graphs ->
        { model with graphs = Some graphs }, Cmd.ofMsg RenderGraphs
    | RenderGraphs ->
        fun (graphs: Graph list) ->
            fun g ->
                // Check if element exists
                js.InvokeVoidAsync("createChart", g.key, g.chart)
                |> ignore
            |> List.iter <| graphs
        |> Option.iter <| model.graphs
            
        model, Cmd.none
    | GetPartyComments ->
        let spreadsheetToPartyComments (s:Spreadsheet option) =
            match s with
            | None -> []
            | Some s ->
                fun (row:Row) ->
                    {
                        name = row.c.Item(0).v
                        htmlColor = row.c.Item(1).v
                        comment = row.c.Item(2).v
                    }
                |> List.map <| s.table.rows

        let strToParty = stringToJsonStr >> Option.bind (jsonToSpreadsheet js) >> spreadsheetToPartyComments >> GotPartyComments
        let getSpreadsheetTask () = 
            http.GetStringAsync("https://docs.google.com/spreadsheets/u/0/d/1OCiqaic-CHct2ltSjTvEaY4AevekwrP4sX8BavScWhQ/gviz/tq?tqx=out:json&tq=select+*")
            |> Task.map strToParty

        let cmd = Cmd.OfTask.either getSpreadsheetTask () id Error
        { model with graphs = None }, cmd
    | GotPartyComments partyComments ->
        { model with partyComments = Some partyComments }, Cmd.none
    | Error exn ->
        { model with error = Some exn.Message }, Cmd.none
    | ClearError ->
        { model with error = None }, Cmd.none

/// Connects the routing system to the Elmish application.
// https://fsbolero.io/docs/Routing#format
let router :Router<Page, Model, Message> =
    {
        // getEndPoint : Model -> Page
        getEndPoint = fun m -> m.page
        // setRoute : string -> option<Message>
        setRoute = fun path ->
            let basicPathList l =
                match l with
                | [] -> Some Home
                | ["graphs"] -> Some Graphs
                | ["party"] -> Some Party
                | _ -> None

            match Array.toList <| path.Trim('/').Split('/') with
            | "?p=" :: p -> basicPathList p
            | l -> basicPathList l

        // getRoute : Page -> string
        getRoute = function
            | Home -> "/"
            | Graphs -> "/graphs"
            | Party -> "/party"

        makeMessage = function
            | Home -> SetPage Home
            | Graphs -> SetPage Graphs
            | Party -> SetPage Party

        notFound = Some Home
    }

type Main = Template<"wwwroot/main.html">

let homePage model dispatch =
    Main.Home().Elt()

let graphsPage model dispatch =
    Main.Graphs()
        .Reload(fun _ -> dispatch GetGraphs)
        .WGraph(cond model.graphs <| function
            | None -> empty()
            | Some graphs ->
                fun graph ->
                    div {
                        canvas {
                            attr.id graph.key
                        }
                    }
                |> forEach graphs
        )
        .Elt()

let partyPage model dispatch =
    let paddedHeader str =
        th {
            attr.style "padding: 6px"
            text str
        }

    let paddedRowData str =
        td {
            attr.style "padding: 6px"
            text str
        }

    Main.Party()
        .PartyComments(cond model.partyComments <| function
            | None -> empty()
            | Some comments ->
                table {
                    attr.border "1px solid black"
                    tr {
                        paddedHeader "Name"
                        paddedHeader "Comment"
                    }
                    fun comment ->
                        tr {
                            attr.style ("color: " + comment.htmlColor)
                            paddedRowData comment.name
                            paddedRowData comment.comment
                        }
                    |> forEach comments
                }
        )
        .Elt()

let emptyPage model dispatch =
    empty()

let menuItem (model: Model) (page: Page) (text: string) =
    Main.MenuItem()
        .Active(if model.page = page then "is-active" else "")
        .Url(router.Link page)
        .Text(text)
        .Elt()

let view js model dispatch =
    Main()
        .Menu(concat {
            menuItem model Home "Home"
            menuItem model Graphs "Graphs"
            menuItem model Party "Party"
        })
        .Body(
            cond model.page <| function
            | Home -> homePage model dispatch
            | Graphs -> graphsPage model dispatch
            | Party -> partyPage model dispatch
        )
        .Error(
            cond model.error <| function
            | None -> empty()
            | Some err ->
                Main.ErrorNotification()
                    .Text(err)
                    .Hide(fun _ -> dispatch ClearError)
                    .Elt()
        )
        .Elt()

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    [<Inject>]
    member val HttpClient = Unchecked.defaultof<HttpClient> with get, set

    [<Inject>]
    member val JSRuntime = Unchecked.defaultof<IJSRuntime> with get, set

    override this.Program =
        let update = update this.HttpClient this.JSRuntime
        let view = view this.JSRuntime
        Program.mkProgram (fun _ -> initModel, Cmd.batch (seq {Cmd.ofMsg GetGraphs; Cmd.ofMsg GetPartyComments})) update view
        |> Program.withRouter router
