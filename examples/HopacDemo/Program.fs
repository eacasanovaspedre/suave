module HopacDemo.Program

open System
open System.Net
open System.Threading
open Hopac
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Writers
open Suave.RequestErrors

/// Sequential first-match routing (not Hopac `Alt.choose`).
let choose = fallback

let mutable private nextId = 0L

let private reqId () =
  Interlocked.Increment(&nextId)

let private stamp () =
  DateTimeOffset.Now.ToString("HH:mm:ss.fff")

let private log msg =
  printfn "%s  %s" (stamp ()) msg

let home =
  setMimeType "text/plain; charset=utf-8" >=> OK """
Hopac-native Suave demo
=======================

  GET  /hello              combinators (succeed / >=>)
  GET  /job                job { } handler
  GET  /race               Alt.choose of two backends (fast wins)
  GET  /add/<a>/<b>        pathScan + job
  GET  /timeout            timeoutWebPart nacks a 30s child after 2s -> 408
  GET  /long               20s Alt; nack it with GET /cancel from another shell
  GET  /cancel             nacks every waiter on /long

Nack demo (timeoutWebPart — no extra client):
  curl -i http://127.0.0.1:8080/timeout

Nack demo (channel — two shells):
  curl http://127.0.0.1:8080/long
  curl http://127.0.0.1:8080/cancel
"""

let hello =
  GET >=> path "/hello" >=> OK "hello from combinators"

let fromJob : WebPart =
  fun ctx ->
    ofJob (job {
      do! timeOutMillis 150
      return! OK "hello from job { }" ctx
    })

/// Two backends race; the 40ms cache wins and the 400ms db Alt is nacked.
let race : WebPart =
  fun ctx ->
    let cache =
      timeOutMillis 40 |> Alt.afterFun (fun () -> "cache")
    let db =
      timeOutMillis 400 |> Alt.afterFun (fun () -> "db")
    Alt.choose [ cache; db ]
    |> Alt.afterJob (fun winner ->
      log (sprintf "[race] winner=%s (the other backend was nacked)" winner)
      OK (sprintf "winner: %s\n" winner) ctx)

let add : WebPart =
  pathScan "/add/%d/%d" (fun (a, b) ->
    fun ctx ->
      ofJob (job {
        do! timeOutMillis 10
        return! OK (sprintf "%d" (a + b)) ctx
      }))

/// A 30-second wait that logs when nacked. `timeoutWebPart` races it against 2s.
let slowChild : WebPart =
  fun ctx ->
    let id = reqId ()
    log (sprintf "[timeout-child #%d] started (would run 30s)" id)
    Alt.withNackJob <| fun nack ->
      job {
        do! Job.start <| job {
          do! nack
          log (sprintf "[timeout-child #%d] nacked" id)
        }
        return
          timeOutMillis 30_000
          |> Alt.afterJob (fun () ->
            log (sprintf "[timeout-child #%d] finished (should not happen)" id)
            OK "slow child finished" ctx)
      }

let timed : WebPart =
  timeoutWebPart (TimeSpan.FromSeconds 2.0) slowChild

/// Signal used by /cancel to nack waiters on /long.
let private cancelLong = Ch<string>()

/// 20s wait. `GET /cancel` (or `ctx.abort`) nacks the timer.
let longRunning : WebPart =
  fun ctx ->
    let id = reqId ()
    log (sprintf "[long #%d] started — GET /cancel to nack, or wait 20s" id)
    let timer =
      timeOutMillis 20_000
      |> Alt.wrapAbortFun (fun () ->
        log (sprintf "[long #%d] timer nacked" id))
      |> Alt.afterJob (fun () ->
        log (sprintf "[long #%d] finished" id)
        OK (sprintf "long #%d completed after 20s\n" id) ctx)
    let cancelled =
      cancelLong
      |> Alt.afterJob (fun reason ->
        log (sprintf "[long #%d] cancelled (%s)" id reason)
        OK (sprintf "long #%d nacked: %s\n" id reason) ctx)
    let aborted =
      ctx.abort
      |> Alt.afterFun (fun () ->
        log (sprintf "[long #%d] ctx.abort committed" id)
        None)
    Alt.choose [ timer; cancelled; aborted ]

let cancelWaiters : WebPart =
  fun ctx ->
    ofJob (job {
      do! Ch.send cancelLong "GET /cancel"
      log "[cancel] sent nack to /long waiters"
      return! OK "nacked waiting /long requests\n" ctx
    })

let app =
  choose [
    GET >=> path "/" >=> home
    hello
    GET >=> path "/job" >=> fromJob
    GET >=> path "/race" >=> race
    GET >=> add
    GET >=> path "/timeout" >=> timed
    GET >=> path "/long" >=> longRunning
    GET >=> path "/cancel" >=> cancelWaiters
    NOT_FOUND "no such route"
  ]

[<EntryPoint>]
let main _ =
  let config =
    { defaultConfig with
        bindings = [ HttpBinding.create HTTP IPAddress.Loopback 8080us ] }
  printfn "Hopac Suave demo: http://127.0.0.1:8080/"
  printfn "Try:  curl http://127.0.0.1:8080/"
  printfn "Nack: curl -i http://127.0.0.1:8080/timeout"
  printfn "Nack: curl http://127.0.0.1:8080/long    (other shell: curl http://127.0.0.1:8080/cancel)"
  startWebServer config app
  0
