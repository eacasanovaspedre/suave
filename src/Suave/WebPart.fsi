[<AutoOpen>]
module Suave.WebPart

open Hopac

/// Takes `'a` and returns an alternative of `'a option`.
/// `None` means this part did not handle the input (try the next part).
/// Returning `Alt.never ()` from a WebPart hangs the request unless an outer
/// concurrent choice exists — use `fail` for a routing miss.
type WebPart<'a> = 'a -> Alt<'a option>

val inline succeed : WebPart<'a>

val fail<'a> : Alt<'a option>

/// Immediate routing miss. Not Hopac `Alt.never`, which hangs.
val never : WebPart<'a>

/// Sequential first-`Some` over a list of WebParts. This is not Hopac `Alt.choose`.
val fallback : options:WebPart<'a> list -> WebPart<'a>

/// Alias for `fallback`. Prefer `fallback` in new code.
val choose : options:WebPart<'a> list -> WebPart<'a>

/// Classic bind (for the option-inside-Alt).
val bind : f:('a -> Alt<'b option>) -> a:Alt<'a option> -> Alt<'b option>

/// Left-to-right Kleisli composition over the option-inside-Alt.
val compose : first:('a -> Alt<'b option>) -> second:('b -> Alt<'c option>) -> 'a -> Alt<'c option>

/// Lift a Job of option into a WebPart result.
val ofJob : Job<'a option> -> Alt<'a option>

/// Lift an F# Async of option into a WebPart result (cancellable via Alt).
val ofAsync : Async<'a option> -> Alt<'a option>

/// Run an Alt as F# Async (interop for leftover Async workflows).
val toAsync : Alt<'a> -> Async<'a>

type WebPartBuilder =
  new : unit -> WebPartBuilder
  member Return : 'a -> Alt<'a option>
  member Zero : unit -> Alt<unit option>
  member ReturnFrom : Alt<'a option> -> Alt<'a option>
  member Delay : (unit -> Alt<'a option>) -> Alt<'a option>
  member Bind : Alt<'a option> * ('a -> Alt<'b option>) -> Alt<'b option>
  member Bind : ('a option) * ('a -> Alt<'b option>) -> Alt<'b option>

/// Computation expression for option-short-circuiting WebParts.
///
///  let part ctx = webPart {
///    let! _ = GET ctx
///    let! ctx = Writers.setHeader "foo" "bar" ctx
///    return ctx
///  }
val webPart : WebPartBuilder

/// Inject a webPart
val inject : postOp:WebPart<'a> -> pairs:(WebPart<'a> * WebPart<'a>) list -> WebPart<'a>

val inline warbler : f:('t -> 't -> 'u) -> 't -> 'u

val inline cnst : x:'t -> 'u -> 't

val cond : item:Choice<'T, _> -> f:('T -> 'U -> 'V) -> g:('U -> 'V) -> 'U -> 'V

val inline tryThen : first:WebPart<'a> -> second:WebPart<'a> -> WebPart<'a>

val inline concatenate : first:('a -> 'b option) -> second:('a -> 'b option)
                       -> 'a -> 'b option
