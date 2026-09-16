module Suave.Tests.HttpApplicatives

open System
open System.IO

open Suave
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.ServerErrors

open Suave.Tests.TestUtilities
open Suave.Testing

open Expecto
open Hopac

[<Tests>]
let applicativeTests cfg =
  let runWithConfig = runWith cfg
  let ip, port =
    let binding = SuaveConfig.firstBinding cfg
    string binding.socketBinding.ip,
    int binding.socketBinding.port

  testList "primitives: Host applicative" [
    testCase "url with spaces: path" <| fun _ ->
      let res = runWithConfig (path "/get by" >=> OK "A") |> req HttpMethod.GET "/get by" None
      Expect.equal res "A" "Should return A"

    testCase "url with spaces: pathScan" <| fun _ ->
      let res = runWithConfig (pathScan "/foo/%s" OK) |> req HttpMethod.GET "/foo/get by" None
      Expect.equal res "get by" "Should return 'get buy'"

    testCase "when not matching on Host" <| fun _ ->
      let app = request (fun r -> OK r.host)

      let res = runWithConfig app |> req HttpMethod.GET "/" None
      Expect.equal res ip "Should be what config says the IP is"

    testCase "when matching on Host but is forwarded" <| fun _ ->
      let app =
        host ip >=> request (fun r -> OK r.host)
        <|> warbler (fun ctx -> INTERNAL_ERROR (sprintf "host: %s" ctx.request.clientHostTrustProxy))

      let res = runWithConfig app |> req HttpMethod.GET "/" None
      Expect.equal res ip "Should be what the config says the IP is"
    ]

[<Tests>]
let ofJobTests =
  testList "ofJob" [
    testCase "returns the job result" <| fun _ ->
      let r = Hopac.run (ofJob (Job.result (Some 42)))
      Expect.equal r (Some 42) "ofJob should yield the job's value"

    testCase "does not commit during prepare; ready Alt wins choose" <| fun _ ->
      let sw = Diagnostics.Stopwatch.StartNew()
      let slow = ofJob (Job.map (fun () -> Some "slow") (timeOutMillis 500))
      let winner = Hopac.run (Alt.choose [ slow; Alt.always (Some "fast") ])
      sw.Stop()
      Expect.equal winner (Some "fast")
        "Alt.always must win; wrapping the job in Alt.always inside prepare commits too soon"
      Expect.isLessThan sw.ElapsedMilliseconds 200L
        "must not wait for the slow job inside prepareJob"
    ]