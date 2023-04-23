module Doglas.Client.Task 

open System
open System.Threading.Tasks

// Stolen from here: https://github.com/fsprojects/FSharpPlus/blob/357e2d2b85e046f50a6ba0de4a4c4b547679ebc3/src/FSharpPlus/Extensions/Task.fs#L18
// backup link: https://stackoverflow.com/questions/59061122/how-to-implement-task-map

let private (|Canceled|Faulted|Completed|) (t: Task<'a>) =
    if t.IsCanceled then Canceled
    else if t.IsFaulted then Faulted t.Exception
    else Completed t.Result

let map (f: 'T -> 'U) (source: Task<'T>) : Task<'U> =
        if source.Status = TaskStatus.RanToCompletion then
            try Task.FromResult (f source.Result)
            with e ->
                let tcs = TaskCompletionSource<'U> ()
                tcs.SetException e
                tcs.Task
        else
            let tcs = TaskCompletionSource<'U> ()
            if source.Status = TaskStatus.Faulted then
                tcs.SetException source.Exception.InnerExceptions
                tcs.Task
            elif source.Status = TaskStatus.Canceled then
                tcs.SetCanceled ()
                tcs.Task
            else
                let k = function
                    | Canceled    -> tcs.SetCanceled ()
                    | Faulted e   -> tcs.SetException e.InnerExceptions
                    | Completed r ->
                        try tcs.SetResult (f r)
                        with e -> tcs.SetException e
                source.ContinueWith k |> ignore
                tcs.Task

