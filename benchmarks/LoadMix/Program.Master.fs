module LoadMix.Program

open System
open System.Net
open System.Threading
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors

/// Incremented when the losing `/race` arm is cancelled.
let mutable nacks = 0L

let ok : WebPart =
  OK "PONG"

let sleep : WebPart =
  fun ctx -> async {
    do! Async.Sleep 10
    return! OK "slept" ctx
  }

/// 5 ms cache vs 50 ms db. `Async.Choice` cancels the loser.
let race : WebPart =
  fun ctx -> async {
    let cache = async {
      do! Async.Sleep 5
      return Some "fast"
    }
    let db = async {
      use! _c = Async.OnCancel (fun () -> Interlocked.Increment(&nacks) |> ignore)
      do! Async.Sleep 50
      return Some "slow"
    }
    match! Async.Choice [ cache; db ] with
    | Some winner -> return! OK winner ctx
    | None -> return None
  }

/// Eight 5 ms waits in parallel; the request waits for all of them.
let fanin : WebPart =
  fun ctx -> async {
    do! Async.Parallel [ for _ in 1 .. 8 -> Async.Sleep 5 ] |> Async.Ignore
    return! OK "fanin" ctx
  }

let compute : WebPart =
  fun ctx ->
    let mutable h = 2166136261u
    for i = 1 to 8000 do
      h <- (h ^^^ uint32 i) * 16777619u
    OK (string h) ctx

let slowChild : WebPart =
  fun ctx -> async {
    do! Async.Sleep 200
    return! OK "too late" ctx
  }

let timed : WebPart =
  timeoutWebPart (TimeSpan.FromMilliseconds 20.0) slowChild

let stats : WebPart =
  fun ctx ->
    OK (sprintf "nacks %d" (Interlocked.Read(&nacks))) ctx

let app : WebPart =
  choose [
    GET >=> path "/ok" >=> ok
    GET >=> path "/sleep" >=> sleep
    GET >=> path "/race" >=> race
    GET >=> path "/fanin" >=> fanin
    GET >=> path "/compute" >=> compute
    GET >=> path "/timeout" >=> timed
    GET >=> path "/stats" >=> stats
    GET >=> path "/" >=> OK "ok sleep race fanin compute timeout stats"
    NOT_FOUND "no such route"
  ]

[<EntryPoint>]
let main argv =
  let acceptors =
    match argv with
    | [| n |] ->
        match Int32.TryParse n with
        | true, v when v >= 0 -> v
        | _ -> 1
    | _ -> 1
  let config =
    { defaultConfig with
        bindings = [ HttpBinding.create HTTP IPAddress.Loopback 3000us ]
        bufferSize = 8192
        maxOps = 10000
        acceptorCount = acceptors }
  startWebServer config app
  0
