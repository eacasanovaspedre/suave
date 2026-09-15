open Suave
open Suave.Filters
open Suave.Successful
open Hopac

open System.Net

/// Inspired by https://news.ycombinator.com/item?id=3067403

let fib n =
  let rec loop a b i =
    job {
      if i > n then
        return b
      else
        return! loop b (a + b) (i + 1I)
    }
  job {
    if n = 1I || n = 2I then
      return 1I
    else
      return! loop 1I 1I 3I
  }

let app =
  pathScan "/%d" (fun (n : int) -> fun x ->
    ofJob (job {
      let! r = fib (bigint n)
      return! OK (r.ToString()) x
    }))

let config =
  { defaultConfig with
     bindings = [ HttpBinding.create HTTP IPAddress.Loopback 3000us ] }

[<EntryPoint>]
let main _ =
  startWebServer config app
  0
