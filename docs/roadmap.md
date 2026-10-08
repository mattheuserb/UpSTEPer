# Roadmap

Ordered by risk: the riskiest unknowns come first so that a dead end is found early.

## M0 — Feasibility (in progress)

Goal: prove that the native bridge builds and runs on all three platforms.

- [x] Solution layout, Central Package Management, analyzers, `.editorconfig`
- [x] Native C ABI: load STEP (XDE), statistics, face colours, tessellation with face ids
- [x] .NET adapter with SafeHandles and error translation
- [x] CLI: `runtime`, `info`
- [x] Unit and integration tests; CI matrix (Windows, Linux, macOS)
- [x] Native build and all tests green locally on Windows (OCCT 8.0.1, MSVC 14.51)
- [x] CI green on all three platforms (native build, tests, CLI smoke test)
- [ ] Redirect OCCT console diagnostics (`Message::DefaultMessenger`) into our error reporting
- [x] Spike: Avalonia window with OpenGL control rendering `MeshData` (verified locally on Windows/ANGLE)
- [x] Viewer screenshot smoke test green in CI on Windows (ANGLE) and Linux (Mesa llvmpipe, Xvfb)
- [ ] **macOS 3D view unverified.** On the GitHub-hosted macOS runner Avalonia 12 reports
  "Compositor backend doesn't support GPU interop", also with the OpenGL compositor forced. Unknown
  whether this is the GPU-less runner VM or Avalonia 12 on macOS in general. Next step: run
  `UpSTEPer --screenshot` on a real Mac; if it fails there too, evaluate a Metal backend or an
  offscreen GL context (ADR 0004). The CI step is non-blocking on macOS until then.

Exit criterion: `upsteper info` and an Avalonia window showing the example file work on Windows,
Linux and macOS from CI-built binaries.

## M1 — Viewer

- Avalonia app (MVVM, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection/Logging)
- Renderer (Silk.NET OpenGL 3.3 core): orbit/pan/zoom, fit-all, standard views, edges overlay
- Picking via ID buffer (triangle → `FaceId`), hover highlight, selection
- Model tree (assemblies, parts, bodies) from XDE labels
- Display quality presets (re-mesh with `BRepTools::Clean`)
- Background loading with progress and cancellation

## M2 — Colouring

- Colour bodies and faces; undo/redo (command pattern)
- Save STEP (AP214/AP242) with colours via `STEPCAFControl_Writer`
- Round-trip tests; manual interoperability check in FreeCAD and one commercial CAD
- Assembly instance colours

## M3 — DXF export (2D views)

- Native: HLR per view direction, edge classification
- `UpSTEPer.Export.Dxf` with ACadSharp: layers, line types, view placement, scale
- CLI: `upsteper export-dxf <file> --views front,top,right,iso`
- Approval tests on DXF output

## M4 — Repair, tiers 1–2

- Healing pipeline with a change report; canonical recognition of spline geometry
- Colour preservation through `BRepTools_History`
- CLI: `upsteper repair <in> <out> --report`

## M5 — Repair, tier 3 (faceted → analytic)

- Interactive: select facets → fit cylinder/cone → preview deviation → apply
- Automatic detection of cylindrical facet strips (holes, shafts)
- **Needs real problem files** (see `docs/test-data.md`)

## Later

- Packaging: VC++ Redistributable prerequisite on Windows, Velopack/self-contained publish, AppImage, signed/notarised macOS build (needs Apple developer account)
- Localisation (de, en)
- API docs with DocFX
