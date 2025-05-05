module Doglas.Client.Model

open Chart
open Bolero

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

