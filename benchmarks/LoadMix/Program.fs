module LoadMix.Program

open System
open System.Net
open System.Threading
open Hopac
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.RequestErrors

/// Sequential first-match routing (not Hopac `Alt.choose`).
let choose = fallback

/// Incremented when the losing `/race` arm is nacked.
let mutable nacks = 0L

let ok : WebPart =
  OK "PONG"

let sleep : WebPart =
  fun ctx ->
    timeOutMillis 10 |> Alt.afterJob (fun () -> OK "slept" ctx)

/// 5 ms cache vs 50 ms db. The loser is nacked.
let race : WebPart =
  fun ctx ->
    let cache = timeOutMillis 5 |> Alt.afterFun (fun () -> "fast")
    let db =
      timeOutMillis 50
      |> Alt.wrapAbortFun (fun () -> Interlocked.Increment(&nacks) |> ignore)
      |> Alt.afterFun (fun () -> "slow")
    Alt.choose [ cache; db ]
    |> Alt.afterJob (fun winner -> OK winner ctx)

/// Eight 5 ms waits in parallel; the request waits for all of them.
let fanin : WebPart =
  fun ctx ->
    ofJob (job {
      do! Job.conIgnore (Array.init 8 (fun _ -> timeOutMillis 5))
      return! OK "fanin" ctx
    })

let compute : WebPart =
  fun ctx ->
    let mutable h = 2166136261u
    for i = 1 to 8000 do
      h <- (h ^^^ uint32 i) * 16777619u
    OK (string h) ctx

let slowChild : WebPart =
  fun ctx ->
    timeOutMillis 200 |> Alt.afterJob (fun () -> OK "too late" ctx)

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
