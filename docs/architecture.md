# Architecture

## Context

UpSTEPer is a desktop and command-line tool. It reads STEP files, shows them, lets users
assign colours to bodies and faces, repairs poor geometry, and exports 2D DXF drawings.
All geometry work is done by Open CASCADE Technology (OCCT), a C++ library.

## Layers

```
┌──────────────────────────────┐   ┌───────────────────────────┐
│ UpSTEPer.App (Avalonia)      │   │ UpSTEPer.Cli              │  presentation / composition roots
│ Avalonia, MVVM, renderer     │   │ System.CommandLine        │
└──────────────┬───────────────┘   └─────────────┬─────────────┘
               └───────────────┬─────────────────┘
                     ┌─────────▼─────────┐
                     │ UpSTEPer.Core     │  domain model, interfaces, commands (no native deps)
                     └─────────▲─────────┘
                     ┌─────────┴─────────┐
                     │ UpSTEPer.Occt     │  adapter: P/Invoke, SafeHandles, error translation
                     └─────────┬─────────┘
                               │ C ABI: native/include/upsteper/upsteper.h
                     ┌─────────▼─────────┐
                     │ upsteper_native   │  C++17 facade: STEP I/O, meshing, colours, (repair, HLR)
                     └─────────┬─────────┘
                     ┌─────────▼─────────┐
                     │ OCCT 8 (LGPL)     │
                     └───────────────────┘
```

Planned additional projects (see roadmap): `UpSTEPer.Rendering` (OpenGL via Silk.NET),
`UpSTEPer.Export.Dxf` (ACadSharp), `UpSTEPer.Repair` (orchestration; algorithms live native).

### Why a narrow C ABI

OCCT has no maintained cross-platform .NET binding. C++/CLI is Windows-only; wrapping all of
OCCT with SWIG is unmaintainable. A purpose-built C API of a few dozen functions keeps
marshalling trivial, keeps OCCT types out of .NET, and is easy to test. See ADR 0002.

## Key concepts

| Concept | Where | Notes |
|---|---|---|
| Document | `IStepDocument` / `ups_document` | Owns an OCCT XDE document (shapes, names, colours, assemblies). |
| Face id | `FaceId` | 0-based index into the document's face map. Valid until topology changes (ADR 0005). |
| Mesh | `MeshData` / `ups_mesh` | Display-only triangulation; vertices duplicated per face; `TriangleFaceIds` enables picking/colouring per face. |
| Tessellation options | `TessellationOptions` | Display quality only — never changes the model. |

## Memory ownership

- Every native object is created by a `ups_*` function and freed by the matching `ups_*_free`.
- .NET wraps each pointer in a `SafeHandle` (`DocumentSafeHandle`, `MeshSafeHandle`), so native
  memory is freed on `Dispose` or, as a fallback, by the finaliser.
- Bulk data (meshes) is copied once into managed arrays (`ups_mesh_copy`), then the native mesh
  is freed immediately. Copying a few MB is cheaper than managing shared lifetimes.

## Error handling

- Native: every exported function runs inside `ups::detail::Guarded`, which converts OCCT and C++
  exceptions into a `ups_status` plus a thread-local message. Nothing propagates across the ABI.
- .NET: `NativeError.ThrowIfFailed` maps statuses to `ArgumentException`, `StepImportException`
  or `GeometryOperationException`, including the native message.

## Threading

OCCT objects are not safe for concurrent access. Rules:

1. A document is only used by one thread at a time. The app enforces this with
   `DocumentSession`, which queues every operation and runs it on the thread pool.
2. Different documents may be used concurrently, **except** that OCCT's STEP translators and the
   XDE application singleton use process-global state: the native layer serialises STEP
   reading/writing and document creation/destruction with one mutex (`TranslatorMutex`).
   `ConcurrencyTests` guards this; without it parallel loads crashed with an access violation.
3. OCCT may parallelise internally (e.g. meshing with `isInParallel`), which is safe.
4. Planned: long operations accept `CancellationToken` and report `IProgress<T>` (mapped to
   OCCT's `Message_ProgressIndicator`).

## Colours (M2)

- Import uses OCCT XDE (`STEPCAFControl_Reader`) with colour, name and layer modes enabled.
- Resolution order for display: face colour → solid colour → single root colour.
- Edits go straight into the XDE document via `XCAFDoc_ColorTool` (surface colour on the face or
  solid label; sub-shape labels are created on demand) and are written by
  `STEPCAFControl_Writer`. Clearing removes the label's colour, so the face falls back to its body.
- Undo/redo (`EditHistory` in the app) stores the *assigned* colour before and after
  (`GetAssignedColor`), not the displayed one, so undo restores inheritance exactly.
- After an edit the app builds `SceneData.WithFaceColors` on the same mesh: no re-tessellation,
  the view keeps its camera and only re-uploads the colour buffer.
- Known gap: faces/bodies inside assembly instances are rejected (`UPS_ERROR_OPERATION`).

## Repair (M4–M5 design)

Tiered, each tier a separate, testable pipeline step with a report of what changed:

1. **Heal**: `ShapeFix_Shape`, sewing, `ShapeUpgrade_UnifySameDomain`.
2. **Canonical recognition**: B-spline curves/surfaces that are really lines, circles, planes,
   cylinders, cones, spheres (OCCT `ShapeAnalysis_CanonicalRecognition`).
3. **Faceted → analytic**: groups of planar facets that approximate a cylinder are fitted
   (least squares) and replaced. Semi-automatic first (user picks facets), automatic later.

Colours survive repair by mapping through `BRepTools_History` (ADR 0005).

## DXF export (M3 design)

`HLRBRep_Algo` computes visible/hidden edges for each view direction; edges are classified
(line, circle, arc, ellipse, spline) and written with ACadSharp to layers `VISIBLE`, `HIDDEN`,
`OUTLINE`. True circles are emitted as `CIRCLE`/`ARC`, not polylines. Dimensions are out of scope.
