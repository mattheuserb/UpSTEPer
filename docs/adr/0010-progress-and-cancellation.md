# 0010. Progress reporting and cancellation across the C ABI

- Status: Accepted
- Date: 2026-10-09

## Context

Loading and meshing large STEP models takes seconds to minutes. The app and the CLI need a
progress indication and a way to stop. OCCT reports progress through `Message_ProgressIndicator`
and polls `UserBreak()` for cancellation; .NET code uses `IProgress<T>` and `CancellationToken`.
Both must meet at the C ABI (ADR 0002), where no C++ types, exceptions or managed objects may
cross.

Facts that shaped the design (OCCT 8.0.1, measured on the example files):

- `STEPCAFControl_Reader::Transfer` and `BRepMesh_IncrementalMesh` accept a
  `Message_ProgressRange`; `ReadFile` (parsing) does not, so parsing reports nothing and cannot be
  interrupted.
- `BRepMesh` runs in parallel and calls `Show`/`UserBreak` from worker threads.
- Transfer reports fine-grained progress for shapes, then jumps to the end while colours and
  names are processed.

## Decision

- **One callback type** `ups_progress_callback(user_data, fraction, phase) -> int32_t`; a non-zero
  return cancels. The fraction is overall (0..1, monotonic, exactly 1 on success), the phase a
  short English label. A single callback both reports and polls, so there is no second function
  pointer to keep alive.
- **New entry points** `ups_document_load_step_with_progress` and
  `ups_document_tessellate_with_progress` (callback may be NULL); the existing functions call
  them with NULL. Cancellation returns the new status `UPS_ERROR_CANCELLED` and produces no
  output: no document, no mesh and no partial triangulation left on the shape.
- **`CallbackProgress`** (`native/src/progress.cpp`) derives from `Message_ProgressIndicator`,
  serialises calls with its own mutex, throttles them to one per 50 ms (forced on phase changes
  and completion) and remembers a cancel request. `UserBreak()` doubles as a throttled report,
  so cancellation is noticed even while the fraction does not move.
- **Loading** splits the bar: parsing 20 % (no intermediate reports), transfer 80 %.
- **.NET**: `IStepReader.Load` and `IStepDocument.Tessellate` take optional
  `IProgress<OperationProgress>` and `CancellationToken`. `NativeProgress` passes a static
  `[UnmanagedCallersOnly]` function pointer plus a `GCHandle` as `user_data`, so no delegate can be
  collected while native code holds it. Exceptions from the progress handler are caught in the
  callback (they must not unwind into native code), cancel the operation and are rethrown after
  the native call returns. `UPS_ERROR_CANCELLED` becomes `OperationCanceledException`.
- **App**: progress bar and Cancel button (Esc) in the status line for loading and re-meshing.
  Loading maps to 0..80 %, meshing to 80..100 % of the bar. A cancelled load keeps the previous
  model; a cancelled re-mesh keeps the mesh on screen and resets the quality selector.
- **CLI**: a self-overwriting progress line on stderr when it is a terminal; Ctrl+C cancels
  through System.CommandLine's token and exits with 130.

## Consequences

- Cancelling during parsing takes effect only when parsing ends. For very large files a
  stream-based reader with its own progress would be needed; not worth it now.
- Progress handlers must be thread-safe and fast; `Progress<T>` in the app marshals to the UI
  thread. Late reports after an operation ended are dropped by the view model.
- Covered by `ProgressTests` (phases, monotonic fraction ending at 1, cancel from the handler,
  pre-cancelled token, handler exception, cancelled meshing followed by a full re-mesh), view
  model tests (cancel keeps the previous model, cancelled re-mesh restores the quality) and CLI
  tests (exit code 130, progress line).
