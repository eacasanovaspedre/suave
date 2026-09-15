namespace Suave.Utils

open Hopac

/// Unbounded producer/consumer queue implemented with a Hopac channel.
type BlockingQueueAgent<'T>() =
  let incoming = Ch<'T>()

  /// Enqueue. Completes without waiting for a consumer.
  member x.Add (v: 'T) : Job<unit> =
    Ch.send incoming v

  /// Dequeue as an Alt (nackable).
  member x.Get () : Alt<'T> =
    incoming :> Alt<'T>
