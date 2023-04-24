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
type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/graphs">] Graphs

/// The Elmish application's model.
type Model =
    {
        page: Page
        error: string option
        graphs: Graph list option // Todo break up this model why tf does it start like this
    }

and Book =
    {
        title: string
        author: string
        publishDate: DateTime
        isbn: string
    }

and Graph =
    {
        chart: Chart
        key: string
    }

let initModel =
    {
        page = Home
        error = None
        graphs = None
    }


/// The Elmish application's update messages.
type Message =
    | SetPage of Page
    | GetGraphs
    | GotGraphs of Graph list
    | RenderGraphs
    | Error of exn
    | ClearError

let log (js: IJSRuntime) (s:obj) = js.InvokeVoidAsync("console.log", s) |> ignore
let error (js: IJSRuntime) (s:obj) = js.InvokeVoidAsync("console.error", s) |> ignore
let strToDate (s:string) =
    match DateTime.TryParse s with
    | true, dt ->
        let t = dt - DateTime.UnixEpoch
        t.TotalSeconds |> float
    | _ -> 0 |> float // TODO

let update (http: HttpClient) (js: IJSRuntime) message model =
    log js message
    match message with
    | SetPage page ->
        let nextCmd =
            match page with
            | Graphs -> Cmd.ofMsg RenderGraphs // if navigating back to graphs need to re-render
            | _ -> Cmd.none
        { model with page = page }, nextCmd
    | GetGraphs ->
        let stringToJsonStr (s:string) =
            // Try to skip complexity for now and assume response is always same prefix and suffix
            let prefixToRemove = "google.visualization.Query.setResponse("
            let sIndex = s.IndexOf(prefixToRemove)
            // Need to be careful with substring bc errors silently ignored
            match sIndex with
            | -1 -> None
            | d -> s.Substring(d + prefixToRemove.Length, s.Length - d - prefixToRemove.Length - 2) |> Some

        let jsonToSpreadsheet (s:string) =
            try
                JsonSerializer.Deserialize<Spreadsheet>(s) |> Some
            with
                | :? System.ArgumentNullException -> error js "Cannot convert json to spreadsheet because argument is null"; None
                | :? System.Text.Json.JsonException as ex -> error js ("Cannot convert json to spreadsheet because the json is invalid: " + ex.ToString()); None
                | :? System.NotSupportedException as ex -> error js ("Cannot convert json to spreadsheet because no compatible deserializer: " + ex.ToString()); None

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
                    }

                    {
                        key = key
                        chart = chart
                    }
                |> List.map <| Map.toList keyRowsMap

        let strToGraphs = stringToJsonStr >> Option.bind jsonToSpreadsheet >> spreadsheetToGraphs >> GotGraphs
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
    | Error exn ->
        { model with error = Some exn.Message }, Cmd.none
    | ClearError ->
        { model with error = None }, Cmd.none

/// Connects the routing system to the Elmish application.
let router = Router.infer SetPage (fun model -> model.page)

type Main = Template<"wwwroot/main.html">

let homePage model dispatch =
    Main.Home().Elt()

let graphsPage js model dispatch =
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
        })
        .Body(
            cond model.page <| function
            | Home -> homePage model dispatch
            | Graphs -> graphsPage js model dispatch
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
        Program.mkProgram (fun _ -> initModel, Cmd.batch (seq {Cmd.ofMsg GetGraphs})) update view
        |> Program.withRouter router
