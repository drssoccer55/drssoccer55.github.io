module Doglas.Client.Personal

open System
open System.Net.Http
open Microsoft.AspNetCore.Components
open Elmish
open Bolero
open Bolero.Html
open Microsoft.JSInterop
open System.Text.Json

type Personal = Template<"wwwroot/personal.html">

let personalPage model dispatch =
    Personal.Greeting().Elt()

let rlbotPage model dispatch =
    Personal.RLBot().Elt()



