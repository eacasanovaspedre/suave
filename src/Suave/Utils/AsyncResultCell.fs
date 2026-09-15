namespace Suave.Utils

open Hopac

/// Write-once cell. Completing more than once is a no-op.
type AsyncResultCell<'T>() =
  let iv = IVar<'T>()

  /// Complete the cell. Returns true if this call set the value.
  member x.complete result =
    Hopac.start (Job.Ignore (IVar.tryFill iv result))

  /// Await the value as a Job.
  member x.awaitResult () : Job<'T> =
    iv :> Job<'T>
