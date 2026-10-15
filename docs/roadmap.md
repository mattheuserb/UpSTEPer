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
- [x] Redirect OCCT console diagnostics (`Message::DefaultMessenger`) into our logging (M1)
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
- [x] Edges overlay: B-Rep edges as lines (seams skipped), polygon offset against z-fighting, toggle in the toolbar
- [x] Fix: the 3D view received no mouse input (Avalonia hit-tests only what a control draws itself)
- [x] Standard views (front, back, left, right, top, bottom, iso) in toolbar and on the numeric
  keypad, oriented like the DXF views; exact top/bottom views; orthographic projection toggle
- [ ] Optional: view cube in the 3D view
- [x] Model tree (assemblies, parts, bodies) from XDE labels; selecting a node highlights its faces;
  "Colour body" acts on the tree selection (one undo step for several bodies)
- [x] Display quality (coarse / normal / fine) in the toolbar: re-meshes in the background and keeps
  colours, selection and camera; chord tolerance and minimum triangle size scale with the model
  size (bounding box diagonal, from the edges), the previous triangulation is discarded
  (`BRepTools::Clean`). Measured on a 72,000-face engine assembly (`docs/test-data.md`)
- [x] Progress and cancellation for loading and meshing: native callback (ADR 0010), progress bar
  and Cancel (Esc) in the app, progress line and Ctrl+C in the CLI
- [x] DI/logging (Microsoft.Extensions): containers in app and CLI, daily log file in the app,
  `--verbose` in the CLI, OCCT messages routed into the log
- [x] Hover highlight: the face under the pointer is tinted blue and named in the status bar;
  picking through a per-mesh BVH (`TriangleBvh`, µs instead of ms per pick at 10⁵ triangles),
  only the two affected faces are re-uploaded (`FaceVertexRanges`, `glBufferSubData`)
- [x] UI redesign, main window (2026-10-14): menu bar, two grouped toolbars that never wrap
  (narrow windows: icons only, palette in the colour panel), colour panel with active colour,
  right-click menu in the 3D view, collapsible model panel, status bar with counts, optional hint
  line, keyboard shortcut list (F1); design tokens and Lucide icons. Selection stays orange (#FF8C1A)
- [x] UI redesign, check and repair dialog (2026-10-14): summary of the current report, status per
  part as icon + text + colour (changed is not success by itself, unchanged not a failure), one-line
  findings / changes with details of the selected part, usable down to 560×440
- [x] Settings file (`settings.json` next to the logs), File → Recent files (last 5 opened), Help → About
  (versions, system, log folder, third-party notices) (2026-10-14)
- [x] Multi-language UI: English and German, switchable at run time (View → Language); first start
  follows the system language; numbers keep the regional format; CLI and logs stay English (2026-10-14)
- [x] RAL Classic list in the colour panel (211 colours from Wikidata, CC0, `scripts/update-ral.py`):
  search by number or name, names in the UI language; the toolbar palette uses the same values.
  Favourite colours with own names in their own section (rename, reorder, remove), stored in the
  settings (2026-10-14). RAL 6021 is missing: Wikidata has no value for it

## M2 — Colouring

- [x] Colour faces and bodies (native `ups_document_set_color`, NULL clears); face beats body
- [x] Undo/redo of colour edits (exact: restores the previously *assigned* colour)
- [x] Save STEP (AP214) with colours via `STEPCAFControl_Writer`; save → reload round-trip test
- [x] CLI `paint` command; app toolbar with RAL palette, Ctrl+Z/Y, Save as
- [x] Fix: concurrent STEP loads crashed (OCCT translators use global state) — now serialised
- [x] Manual interoperability check of saved files in FreeCAD 1.1.1 (2026-10-13): colours as expected,
  results in `docs/interop-check.md`
- [x] Same check in one commercial CAD: not needed, the FreeCAD check suffices (decision 2026-10-13)
- [x] Colours in assemblies: only the clicked instance changes; shared definitions are copied
  for it ("make independent", ADR 0009), ids stay stable, save → reload tested
- [x] AP242 output option: "Save as" file type in the app, `paint --schema ap242` in the CLI;
  `info --tree` prints the body ids `paint --body` expects
- [x] Custom colour picker (any RGB) in the app: "Custom..." flyout with `ColorView`; picked colours
  join the palette (up to 6, per session)

## M3 — DXF export (2D views)

- [x] Native: exact HLR (`HLRBRep_Algo`) per view; edges classified into line / arc / circle /
  ellipse / polyline; edge-on circles become lines (degenerate ellipses are invalid DXF)
- [x] Core: `ProjectedView`, first-angle `DrawingLayout`, `HiddenLineCleanup` (hidden edges lying
  on visible ones are dropped), `DrawingBuilder`
- [x] `UpSTEPer.Export.Dxf` (ACadSharp): mm, layers VISIBLE / HIDDEN (dashed) / TANGENT
- [x] CLI `export-dxf` (`--views`, `--no-hidden`, `--tangent`, `--gap`); app button "Export DXF"
- [x] Tests: DXF read-back with ACadSharp, end-to-end on the example file
- [x] Split partially covered hidden edges: covered stretches (>= 0.1 mm) are cut out, the rest
  keeps its curve type; at most 30 ms per view on the example files
- [x] Scale 1:1 in mm, no frame/title block by default (decision 2026-10-08, ADR 0006)
- [x] Default views front, top, left; isometric only on request (`--views ...,iso`; decision 2026-10-13)
- [ ] Optional (opt-in only): other scales, frame/title block; section views, dimensions
- [x] Manual check of exported DXF in a commercial CAD: not needed (decision 2026-10-13)
- [x] Fewer polylines from HLR: HLR returns many straight edges as B-splines (degree-1, 15 poles);
  straight ones (poles on the chord) are written as lines. Cable gland, three views: polylines in
  the DXF 204 → 31, the rest are genuinely curved (2026-10-14)

## M4 — Repair, tiers 1–2

- [x] Generated test file with one defect per part (`examples/generated/damaged-parts.stp`)
- [x] Check per part definition (free edges, invalid shapes, canonical splines, mergeable faces,
  small edges; failures isolated per sub-check): native `ups_document_check`, CLI `check`
- [x] Healing (tier 1, ADR 0011): ShapeFix, sewing (closed shells become solids), merging faces on
  the same surface; per part definition, all-or-nothing, cancellable, colours through the histories
  (largest area wins on conflicts); CLI `repair <in> <out> [--steps] [--tolerance] [--schema]`
- [x] Canonical recognition (tier 2): spline faces that are planes, cylinders, cones or spheres
  get the exact surface (orientation kept, deviation reported); `--steps canonical`
- [x] Canonical recognition of edge curves (lines, circles, ellipses), so DXF views of converted
  parts become analytic too; only exact ones (within the edge's own tolerance, ADR 0011)
- [x] App: repair dialog ("Repair..."): check report, options (steps, tolerance), result report, undo of the
  last repair via a temporary STEP snapshot; manually tested (2026-10-13)

## M5 — Repair, tier 3 (faceted → analytic)

- Interactive: select facets → fit cylinder/cone → preview deviation → apply
- Automatic detection of cylindrical facet strips (holes, shafts)
- **Needs real problem files** (see `docs/test-data.md`); none available yet (2026-10-13), so M5
  waits until one turns up

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

## Public release (plan of 2026-10-14)

Goal: publish the repository on GitHub as a presentable open source project with a first
downloadable release (v0.1.0-alpha).

Decisions (2026-10-14): keep the history, cleaned (see below); first release with app and CLI for
Windows and Linux, CLI only for macOS.

**Phase 0 — blockers**

- [x] `THIRD-PARTY-NOTICES.md` complete for all shipped NuGet packages and native libraries;
  `ThirdPartyNoticesTests` fails when a package of the app or the CLI is missing (2026-10-14)
- [x] Clean Git history (2026-10-15): `examples/G_I202_XX_64331V.stp` (Siemens, licence unclear)
  removed and author addresses mapped to the GitHub `noreply` one with `git filter-repo`, pushed
  to a **new** repository (a force push is not enough: GitHub keeps the commits of old PRs under
  `refs/pull/*`). The old repository is private as `UpSTEPer-private`; PRs #1–#31 live there.
  Public since 2026-10-15
- [ ] Check that the name "UpSTEPer" is free: GitHub, NuGet and a web search found nothing
  (2026-10-14); trademark registers (DPMA, EUIPO) not checked yet

**Phase 1 — first impression**

- [x] README: current status and features, screenshots (`scripts/update-screenshots.ps1`, app option
  `--screenshot <png> --window`), CLI examples, platforms, known limitations (2026-10-14); download section
  (2026-10-15)
- [x] `CHANGELOG.md` (Keep a Changelog), issue templates (bug, feature), PR template (2026-10-14)
- [x] App icon (isometric cube in the app colours, `scripts/update-app-icon.py`): windows and the
  Windows executable (2026-10-15); `.icns` and Linux desktop entry follow with packaging
- [x] Repository settings (2026-10-15): description, topics, area/platform labels, private
  vulnerability reporting, Dependabot alerts, security updates and weekly grouped version updates
  (NuGet, Actions), CodeQL default setup; `main` protected: changes only through pull requests,
  the four CI jobs required, no force pushes (admins may bypass)
- [ ] Social preview image
- [ ] Open roadmap items as GitHub issues and milestones

**Phase 2 — CI**

- [x] Full CI matrix green on the public repository (2026-10-15), macOS on every run. The first
  runs found three flaky tests: a UI language switch off the UI thread (macOS), a progress report
  of 100 % after a cancel (a real bug, Linux) and a BVH timing test without warm-up (Windows)

**Phase 3 — first release**

- [x] Release workflow on tags `v*` (`docs/releasing.md`, 2026-10-15): checks that tag,
  `Directory.Build.props`, CHANGELOG and Flatpak metainfo agree, runs the full CI with packaging and
  smoke tests, and creates a draft release with checksums and the CHANGELOG section as notes.
  Every CI run builds and tests the release files
- [x] Windows installer (Inno Setup, `scripts/build-installer.ps1`, `docs/packaging.md`): per user
  without admin rights or for all users, "Open with" for .stp/.step, optional PATH entry for the
  CLI, MSVC runtime app-local; 45 MB. Silent install/uninstall tested locally (2026-10-15)
- [x] Windows ZIP; Linux: Flatpak (prebuilt binaries, runtime from Flathub) and tar.gz; macOS: CLI
  only (the app is unsigned and its 3D view unverified) (2026-10-15)
- [ ] Flathub: needs a source build of OCCT and the NuGet packages inside flatpak-builder
- [ ] Manual installer test in Windows Sandbox and as all-users installation
- [ ] Smoke test of the packages on a clean machine (Windows Sandbox, fresh Linux container)
- [ ] Later: installer (Velopack), AppImage, code signing (Windows SmartScreen, macOS notarisation)

## Later

- API docs with DocFX
