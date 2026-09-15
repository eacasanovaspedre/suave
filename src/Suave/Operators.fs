module Suave.Operators

let (>=>) a b =
  WebPart.compose a b

let orElse a b =
  WebPart.tryThen a b

/// Sequential try-then. Not Hopac `<|>` (concurrent Alt). Prefer `orElse` when
/// `Hopac.Infixes` is also open.
let inline (<|>) a b =
  WebPart.tryThen a b

let inline (@@) a b =
  WebPart.concatenate a b

let (|@) c errorMsg =
  match c with
  | Choice1Of2 x -> Choice1Of2 x
  | Choice2Of2 y -> Choice2Of2 errorMsg

let (||@) opt errorMsg =
  match opt with
  | Some x ->
    Choice1Of2 x
  | None ->
    Choice2Of2 errorMsg
