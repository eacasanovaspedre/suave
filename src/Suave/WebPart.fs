[<AutoOpen>]
module Suave.WebPart

open Hopac
open Hopac.Infixes

type WebPart<'a> = 'a -> Alt<'a option>

let inline succeed x = Alt.always (Some x)

let fail<'a> : Alt<'a option> = Alt.always (Option<'a>.None)

/// Immediate routing miss. Not Hopac `Alt.never`, which hangs.
let never : WebPart<'a> = fun _ -> fail

let ofJob (j : Job<'a option>) : Alt<'a option> = Alt.prepare (Promise.start j)

let ofAsync (a : Async<'a option>) : Alt<'a option> =
  Alt.fromAsync a

let toAsync (x : Alt<'a>) : Async<'a> =
  Job.toAsync (x :> Job<_>)

let bind (f: 'a -> Alt<'b option>) (a: Alt<'a option>) : Alt<'b option> =
  Alt.prepareJob <| fun () ->
    (a :> Job<_>) >>= function
    | None -> Job.result fail
    | Some q -> Job.result (f q)

let compose (first : 'a -> Alt<'b option>) (second : 'b -> Alt<'c option>)
            : 'a -> Alt<'c option> =
  fun x -> bind second (first x)

type WebPartBuilder() =
  member this.Return(x:'a) : Alt<'a option> = succeed x
  member this.Zero() : Alt<unit option> = this.Return()
  member this.ReturnFrom(x : Alt<'a option>) = x
  member this.Delay(f: unit -> Alt<'a option>) = Alt.prepareFun f
  member this.Bind(x : Alt<'a option>, f : 'a -> Alt<'b option>) : Alt<'b option> = bind f x
  member this.Bind(x : 'a option, f : 'a -> Alt<'b option>) : Alt<'b option> = bind f (Alt.always x)

let webPart = WebPartBuilder()

let rec fallback (options : WebPart<'a> list) : WebPart<'a> =
  fun arg ->
    match options with
    | [] -> fail
    | p :: tail ->
      Alt.prepareJob <| fun () ->
        (p arg :> Job<_>) >>= function
        | Some x -> Job.result (succeed x)
        | None -> Job.result (fallback tail arg)

let choose options = fallback options

let rec inject (postOp : WebPart<'a>) (pairs : (WebPart<'a> * WebPart<'a>) list) : WebPart<'a> =
  fun arg ->
    match pairs with
    | [] -> fail
    | (p,q) :: tail ->
      Alt.prepareJob <| fun () ->
        (p arg :> Job<_>) >>= function
        | Some x -> Job.result ((compose postOp q) x)
        | None -> Job.result (inject postOp tail arg)

let inline warbler f a = f a a

let inline cnst x = fun _ -> x

let cond item f g a =
  match item with
  | Choice1Of2 x -> f x a
  | Choice2Of2 _ -> g a

let inline tryThen (first : WebPart<'a>) (second : WebPart<'a>) : WebPart<'a> =
  fun x ->
    Alt.prepareJob <| fun () ->
      (first x :> Job<_>) >>= function
      | None -> Job.result (second x)
      | r -> Job.result (Alt.always r)

let inline concatenate first second = fun x ->
  match first x with
  | None   -> second x
  | r      -> r