module Doglas.Client.PartySketch

open Bolero
open Bolero.Html
open Microsoft.AspNetCore.Components
open Microsoft.JSInterop

type PartySketch =
    {
        canvas: Node
    }
and DrawContext =
    {
        mutable prevX: float
        mutable prevY: float
        mutable curX: float
        mutable curY: float
        mutable mouseDown: bool
        mutable style: string
        mutable width: int
    }
and CommentContext =
    {
        mutable name: string
        mutable style: string
    }
and Sketch = 
    {
        dataUrl: string
    }
and Comment =
    {
        name: string
        style: string
        comment: string
    }

let createCanvas (js: IJSRuntime, dispatchComment: (Comment -> unit), key: string) : Node =

    let drawContext = {
        prevX = 0
        prevY = 0
        curX = 0
        curY = 0
        mouseDown = false
        style = "black"
        width = 2
    }

    let commentContext = {
        name = "anon"
        style = "color: black"
    }

    let mouseDown = fun (mea : Web.MouseEventArgs) -> 
        drawContext.prevX <- mea.ClientX
        drawContext.prevY <- mea.ClientY
        drawContext.mouseDown <- true

    let touchDown = fun (tea : Web.TouchEventArgs) -> 
        drawContext.prevX <- tea.Touches[0].ClientX
        drawContext.prevY <- tea.Touches[0].ClientY
        drawContext.mouseDown <- true

    let mouseUp = fun _ -> 
        drawContext.mouseDown <- false

    let mouseMove = fun (mea : Web.MouseEventArgs) -> 
        drawContext.curX <- mea.ClientX
        drawContext.curY <- mea.ClientY
        js.InvokeVoidAsync("draw", key, drawContext) |> ignore
        drawContext.prevX <- mea.ClientX
        drawContext.prevY <- mea.ClientY

    let touchMove = fun (tea : Web.TouchEventArgs) -> 
        drawContext.curX <- tea.Touches[0].ClientX
        drawContext.curY <- tea.Touches[0].ClientY
        js.InvokeVoidAsync("draw", key, drawContext) |> ignore
        drawContext.prevX <- tea.Touches[0].ClientX
        drawContext.prevY <- tea.Touches[0].ClientY

    let saveSketch = fun _ ->
        // Async.RunSynchronously hangs, see https://github.com/fsbolero/Bolero/issues/14
        async {
            let! canvasData = Async.AwaitTask <| js.InvokeAsync("getCanvasData", key).AsTask()
            dispatchComment { name = commentContext.name; style = commentContext.style; comment = canvasData}
        } |> Async.Start

    let optionColor color =
        option {
            attr.value color
            attr.style ("color: " + color)
            text color
        }

    let optionWidth width =
        option {
            attr.value width
            text (string width)
        }

    div {
        div {
            text "Name: "
            input {
                on.change (fun e -> commentContext.name <- (unbox e.Value))
            }
        }
        div {
            text "Color: "
            select {
                on.change (fun e -> 
                    commentContext.style <- "color: " + (unbox e.Value)
                    drawContext.style <- (unbox e.Value)
                )
                optionColor "Black"
                optionColor "Red"
                optionColor "Orange"
                optionColor "Yellow"
                optionColor "Green"
                optionColor "Blue"
                optionColor "Purple"
                optionColor "FireBrick"
                optionColor "Sienna"
                optionColor "OrangeRed"
                optionColor "Gold"
                optionColor "GreenYellow"
                optionColor "PaleGreen"
                optionColor "DarkOliveGreen"
                optionColor "MidnightBlue"
                optionColor "DodgerBlue"
                optionColor "Aqua"
                optionColor "MediumOrchid"
                optionColor "Plum"
                optionColor "DeepPink"
            }
            text "Width: "
            select {
                on.change (fun e -> 
                    drawContext.width <- (unbox<string> e.Value |> int)
                )
                optionWidth 2
                optionWidth 5
                optionWidth 20
            }
        }

        div {
            button {
                on.click saveSketch
                "Save Sketch"
            }
            button {
                on.click (fun _ -> js.InvokeVoidAsync("clear", key) |> ignore)
                "Clear Sketch"
            }
        }
        canvas {
            attr.id key
            attr.style "border-style: solid; touch-action: none"
            on.mousedown mouseDown
            on.mouseup mouseUp
            on.mousemove mouseMove
            on.mouseout mouseUp
            on.touchstart touchDown
            on.touchend mouseUp
            on.touchcancel mouseUp
            on.touchmove touchMove
        }
    }