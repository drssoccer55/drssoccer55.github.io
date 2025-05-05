module Doglas.Client.Party

open Bolero
open Bolero.Html
open Model

// html has to be different than page routing bc otherwise does not trigger 404 redirect and page cannot be refreshed. i.e cannot be personal.html when Page personal exists.
type PartyTemplate = Template<"wwwroot/party_templates.html">

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


    PartyTemplate.Party()
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



