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
- [ ] macOS 3D view — **deferred to the end of the roadmap** (decision 2026-10-08), see
  [macOS support](#macos-support-last).

Exit criterion: `upsteper info` and an Avalonia window showing the example file work on Windows,
Linux and macOS from CI-built binaries.

## M1 — Viewer

- [x] Avalonia app (MVVM, CommunityToolkit.Mvvm); background loading
- [x] Renderer: orbit/pan/zoom, fit (double-click)
- [x] Face selection by click: CPU ray casting (`MeshPicker`, Möller–Trumbore) instead of a GPU
  ID buffer — testable and independent of GPU quirks; highlight + face/colour in the status bar
- [ ] Edges overlay (B-Rep edges as lines)
- [ ] Standard views (front, top, right, iso) and view cube / toolbar
- [ ] Model tree (assemblies, parts, bodies) from XDE labels; body selection
- [ ] Display quality presets (re-mesh with `BRepTools::Clean`)
- [ ] Progress and cancellation for long loads; DI/logging (Microsoft.Extensions)
- [ ] Hover highlight (needs a BVH for picking performance)

## M2 — Colouring

- [x] Colour faces and bodies (native `ups_document_set_color`, NULL clears); face beats body
- [x] Undo/redo of colour edits (exact: restores the previously *assigned* colour)
- [x] Save STEP (AP214) with colours via `STEPCAFControl_Writer`; save → reload round-trip test
- [x] CLI `paint` command; app toolbar with RAL palette, Ctrl+Z/Y, Save as
- [x] Fix: concurrent STEP loads crashed (OCCT translators use global state) — now serialised
- [ ] Manual interoperability check of saved files in FreeCAD and one commercial CAD
- [ ] Colours in assemblies (faces/bodies inside instances are rejected today)
- [ ] AP242 output option; custom colour picker (any RGB) in the app

## M3 — DXF export (2D views)

- [x] Native: exact HLR (`HLRBRep_Algo`) per view; edges classified into line / arc / circle /
  ellipse / polyline; edge-on circles become lines (degenerate ellipses are invalid DXF)
- [x] Core: `ProjectedView`, first-angle `DrawingLayout`, `HiddenLineCleanup` (hidden edges lying
  on visible ones are dropped), `DrawingBuilder`
- [x] `UpSTEPer.Export.Dxf` (ACadSharp): mm, layers VISIBLE / HIDDEN (dashed) / TANGENT
- [x] CLI `export-dxf` (`--views`, `--no-hidden`, `--tangent`, `--gap`); app button "Export DXF"
- [x] Tests: DXF read-back with ACadSharp, end-to-end on the example file
- [ ] Split partially covered hidden edges (today they are kept whole)
- [x] Scale 1:1 in mm, no frame/title block by default (decision 2026-10-08, ADR 0006)
- [ ] Optional (opt-in only): other scales, frame/title block; section views, dimensions
- [ ] Manual check of exported DXF in a commercial CAD (AutoCAD / DraftSight / LibreCAD)

## M4 — Repair, tiers 1–2

- Healing pipeline with a change report; canonical recognition of spline geometry
- Colour preservation through `BRepTools_History`
- CLI: `upsteper repair <in> <out> --report`

## M5 — Repair, tier 3 (faceted → analytic)

- Interactive: select facets → fit cylinder/cone → preview deviation → apply
- Automatic detection of cylindrical facet strips (holes, shafts)
- **Needs real problem files** (see `docs/test-data.md`)

## macOS support (last)

Deliberately scheduled after all functional milestones; no Mac hardware is available to the
project. The CLI and native kernel already work on macOS (CI green); only the 3D view is affected.

**Problem.** On the GitHub-hosted macOS runner Avalonia 12 reports "Compositor backend doesn't
support GPU interop" for `OpenGlControlBase`, also with the OpenGL compositor forced. Unknown
whether this is the GPU-less runner VM or Avalonia 12 on macOS in general. The CI screenshot step
is non-blocking on macOS and emits a warning.

**Preferred approach: Google ANGLE (OpenGL ES on Metal), rendered offscreen.**

1. Build ANGLE with its `metal` feature via vcpkg (port `angle`, BSD-3-Clause).
2. Create our own EGL context, independent of Avalonia's compositor; render with the existing
   `MeshRenderer` (already GLSL ES 3.00 capable — Windows uses ANGLE today).
3. Hand frames to Avalonia: first by pixel copy into a `WriteableBitmap` (simple, ~6 MB per frame
   at 1600×1000, fine for on-demand redraws), later optionally by sharing a Metal texture.
4. If the runner VM has no Metal, use ANGLE's SwiftShader (CPU) backend for CI.

Benefits beyond macOS: one shader dialect on all platforms, and independence from Avalonia's
undocumented GL behaviour. Alternatives: a native Metal renderer (more work), or verification on a
rented Mac / by community testers once the repository is public.

## Later

- Packaging: VC++ Redistributable prerequisite on Windows, Velopack/self-contained publish, AppImage, signed/notarised macOS build (needs Apple developer account)
- Localisation (de, en)
- API docs with DocFX
