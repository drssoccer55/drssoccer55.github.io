module Doglas.Client.Personal

open Bolero

// html has to be different than page routing bc otherwise does not trigger 404 redirect and page cannot be refreshed. i.e cannot be personal.html when Page personal exists.
type Personal = Template<"wwwroot/personal_templates.html">

let personalPage model dispatch =
    Personal.Greeting().Elt()

let rlbotPage model dispatch =
    Personal.RLBot().Elt()

let doglasRadioPage model dispatch =
    Personal.DoglasRadio().Elt()

let websitePage model dispatch =
    Personal.Website().Elt()
