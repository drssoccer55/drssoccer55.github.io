module Doglas.Client.Main

open System
open System.Net.Http
open Microsoft.AspNetCore.Components
open Elmish
open Bolero
open Bolero.Html
open Microsoft.JSInterop
open System.Text.Json
open Spreadsheet
open Chart
open Utils
open PartySketch
open Personal

/// Routing endpoints definition.
// I don't think the EndPoint attribute gets used now that I have a custom router.
type Page =
    | [<EndPoint "/">] Home
    | [<EndPoint "/graphs">] Graphs
    | [<EndPoint "/party">] Party
    | [<EndPoint "/personal">] Personal
    | [<EndPoint "/personal/rlbot">] RLBot
    | [<EndPoint "/personal/doglasRadio">] DoglasRadio
    | [<EndPoint "/personal/website">] Website

/// The Elmish application's model.
type Model =
    {
        page: Page
        error: string option
        graphs: Graph list option // Todo break up this model
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
        style: string
    }

let initModel =
    {
        page = Home
        error = None
        graphs = None
        partyComments = None
    }


/// The Elmish application's update messages. Todo break up these messages
type Message =
    | SetPage of Page
    | GetGraphs
    | GotGraphs of Graph list
    | RenderGraphs
    | GetPartyComments
    | GotPartyComments of PartyComment list
    | Error of exn
    | ClearError
    | SendPartyComment of PartySketch.Comment

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
        | Graphs -> { model with page = page }, Cmd.ofMsg GetGraphs
        | Party -> { model with page = page }, Cmd.ofMsg GetPartyComments
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
                        style = row.c.Item(1).v
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
    | SendPartyComment comment ->
        // Maybe I'll make an object for this at some point
        let postBody = "{\"name\":\"" + comment.name + "\",\"style\":\"" + comment.style + "\",\"content\":\"" + comment.comment + "\"}"
        let httpContent = new StringContent(postBody, System.Text.Encoding.UTF8, "text/plain");
        let getPostTask () =
            http.PostAsync("https://script.google.com/macros/s/AKfycbxjvgwXKmQcrkc3Vqju3C01u-wY47ie7NBlL3d979UjGEJ13raMaFKS27E0nW0iwVtX/exec", httpContent)

        // Not checking result of httpresponsemessage yet
        model, Cmd.OfTask.perform getPostTask () (fun _ -> GetPartyComments)

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
                | [""] -> Some Home // needed to set this bc navigating back to home refreshed the whole page!
                | ["graphs"] -> Some Graphs
                | ["party"] -> Some Party
                | ["personal"] -> Some Personal
                | ["personal"; "rlbot"] -> Some RLBot
                | ["personal"; "doglasRadio"] -> Some DoglasRadio
                | ["personal"; "website"] -> Some Website
                | _ -> None

            match Array.toList <| path.Trim('/').Split('/') with
            | "?p=" :: p -> basicPathList p
            | l -> basicPathList l

        // getRoute : Page -> string
        getRoute = function
            | Home -> "/"
            | Graphs -> "/graphs"
            | Party -> "/party"
            | Personal -> "/personal"
            | RLBot -> "/personal/rlbot"
            | DoglasRadio -> "/personal/doglasRadio"
            | Website -> "/personal/website"

        makeMessage = SetPage

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

let partyPage js model dispatch =

    let dispatchComment partySketch = SendPartyComment(partySketch) |> dispatch

    let paddedHeader str =
        th {
            attr.style "padding: 6px"
            text str
        }

    let paddedRowNode (node:Node) =
        td {
            attr.style "padding: 6px"
            node
        }

    let paddedRowData str =
        text str |> paddedRowNode

    let paddedRowDataImg (str: string) =
        match str with
        | a when str.StartsWith "data:image" ->
            img {
                attr.src str
            } |> paddedRowNode
        | _ -> paddedRowData str


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
                            attr.style (comment.style)
                            paddedRowData comment.name
                            paddedRowDataImg comment.comment
                        }
                    |> forEach comments
                }
        )
        .PartySketch(PartySketch.createCanvas(js, dispatchComment, "sketchCanvas"))
        .Elt()

let emptyPage model dispatch =
    empty()

let menuItem (model: Model) (page: Page) (text: string) =
    Main.MenuItem()
        .Active(if model.page = page then "is-active" else "")
        .Url(router.Link page)
        .Text(text)
        .Elt()

let personalPages =
    [(Personal, "Personal Projects"); (RLBot, "RLBot"); (DoglasRadio, "DoglasRadio"); (Website, "Website")]

let homePageMenuItem () =
    [(Home, "Home")]

// From the page which pages are available in the menu
let menuPages (page: Page) =
    match page with
    | Home -> List.concat [homePageMenuItem(); [(Personal, "Personal Projects")]]
    | Graphs -> homePageMenuItem()
    | Party -> homePageMenuItem()
    | Personal -> List.concat [homePageMenuItem(); personalPages]
    | RLBot -> List.concat [homePageMenuItem(); personalPages]
    | DoglasRadio -> List.concat [homePageMenuItem(); personalPages]
    | Website -> List.concat [homePageMenuItem(); personalPages]

let menuOfTuple model (page: Page, label: string) =
    menuItem model page label

let view js model dispatch =
    Main()
        .Menu(forEach (menuPages model.page) (menuOfTuple model))
        .Body(
            cond model.page <| function
            | Home -> homePage model dispatch
            | Graphs -> graphsPage model dispatch
            | Party -> partyPage js model dispatch
            | Personal -> personalPage model dispatch
            | RLBot -> rlbotPage model dispatch
            | DoglasRadio -> doglasRadioPage model dispatch
            | Website -> websitePage model dispatch
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
        Program.mkProgram (fun _ -> initModel, Cmd.Empty) update view
        |> Program.withRouter router
