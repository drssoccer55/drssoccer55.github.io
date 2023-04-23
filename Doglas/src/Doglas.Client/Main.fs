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

/// Routing endpoints definition.
type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/counter">] Counter
    | [<EndPoint "/data">] Data
    | [<EndPoint "/graphs">] Graphs

/// The Elmish application's model.
type Model =
    {
        page: Page
        counter: int
        books: Book[] option
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
        counter = 0
        books = None
        error = None
        graphs = None
    }


/// The Elmish application's update messages.
type Message =
    | SetPage of Page
    | Increment
    | Decrement
    | SetCounter of int
    | GetBooks
    | GotBooks of Book[]
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
        { model with page = page }, Cmd.none

    | Increment ->
        { model with counter = model.counter + 1 }, Cmd.none
    | Decrement ->
        { model with counter = model.counter - 1 }, Cmd.none
    | SetCounter value ->
        { model with counter = value }, Cmd.none

    | GetBooks ->
        let getBooks() = http.GetFromJsonAsync<Book[]>("/books.json")
        let cmd = Cmd.OfTask.either getBooks () GotBooks Error
        { model with books = None }, cmd
    | GotBooks books ->
        { model with books = Some books }, Cmd.none

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
                let rows = Spreadsheet.getRowsFilteredForKey s "w"
                let dataRows =
                    fun (r:Row) ->
                        {
                            x = r.c.Item(1).v |> strToDate
                            y = r.c.Item(2).v |> float
                        }
                    |> List.map <| rows

                let chart =
                    {
                        ``type`` = "scatter"
                        data = {
                                datasets = [{
                                    DataSet.label = "w"
                                    DataSet.data = dataRows
                                }]
                            }
                    }

                let graph =
                    {
                        key = "w"
                        chart = chart
                    }
                
                List.singleton graph

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

let counterPage model dispatch =
    Main.Counter()
        .Decrement(fun _ -> dispatch Decrement)
        .Increment(fun _ -> dispatch Increment)
        .Value(model.counter, fun v -> dispatch (SetCounter v))
        .Elt()

let dataPage model dispatch =
    Main.Data()
        .Reload(fun _ -> dispatch GetBooks)
        .Rows(cond model.books <| function
            | None ->
                Main.EmptyData().Elt()
            | Some books ->
                forEach books <| fun book ->
                    tr {
                        td { book.title }
                        td { book.author }
                        td { book.publishDate.ToString("yyyy-MM-dd") }
                        td { book.isbn }
                    })
        .Elt()

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
            menuItem model Counter "Counter"
            menuItem model Data "Download data"
            menuItem model Graphs "Graphs"
        })
        .Body(
            cond model.page <| function
            | Home -> homePage model dispatch
            | Counter -> counterPage model dispatch
            | Data -> dataPage model dispatch
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
        Program.mkProgram (fun _ -> initModel, Cmd.batch (seq {Cmd.ofMsg GetGraphs; Cmd.ofMsg GetBooks})) update view
        |> Program.withRouter router
