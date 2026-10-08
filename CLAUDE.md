# CLAUDE.md

Guidance for Claude Code (and humans) working in this repository. Keep it current: when a
command, convention or boundary changes, update this file in the same PR.

## What this is

UpSTEPer: a cross-platform C#/.NET 10 tool to view, colour, repair and convert STEP CAD models,
using Open CASCADE Technology (OCCT 8) through a thin native C ABI. Status and plan:
`docs/roadmap.md`. Rationale for every structural choice: `docs/adr/`.

## Commands

```bash
./scripts/check.ps1                # full CI quality gate locally; run before every commit
./scripts/check.ps1 -Fix           # apply dotnet format fixes first
dotnet build                       # whole solution (UpSTEPer.slnx); warnings are errors
dotnet test                        # Microsoft Testing Platform (configured in global.json)
dotnet test --project tests/UpSTEPer.Core.Tests          # one project
dotnet run --project src/UpSTEPer.Cli -- runtime         # is the native library loadable?
dotnet run --project src/UpSTEPer.Cli -- info <file.stp> # load + stats + colours + mesh
dotnet run --project src/UpSTEPer.App -- <file.stp>       # desktop viewer
dotnet run --project src/UpSTEPer.App -- <file.stp> --screenshot out.png [--select-face N]   # render 1 frame (optionally with face N highlighted) to PNG and exit

# Native bridge (needs C++ toolchain, CMake >= 3.24, Ninja, VCPKG_ROOT) — see docs/development.md
powershell -File scripts/build-native.ps1 -VcpkgRoot C:\vcpkg   # Windows: sets up MSVC env itself
cd native && cmake --preset <win-x64|linux-x64|osx-arm64> && cmake --build --preset <preset> && cmake --install build/<preset>
```

OCCT-dependent tests **skip** when the native library is missing, and **fail** when
`UPS_REQUIRE_NATIVE=1` (set in CI). If you cannot build native code locally, say so explicitly
rather than claiming those tests pass; CI (`.github/workflows/ci.yml`) is the source of truth.

## Layout and dependency rules

| Path | Role | May depend on |
|---|---|---|
| `src/UpSTEPer.Core` | Domain types (`ColorRgb`, `FaceId`, `MeshData`, ...) and interfaces (`IStepReader`, `IStepDocument`) | BCL only |
| `src/UpSTEPer.Occt` | Implements Core via P/Invoke (`Native/`) | Core |
| `src/UpSTEPer.Rendering` | OpenGL renderer (Silk.NET), `OrbitCamera`, `SceneData`; no UI framework | Core |
| `src/UpSTEPer.App` | Avalonia desktop app (MVVM), `ModelView` hosts the renderer | Core, Occt, Rendering |
| `src/UpSTEPer.Cli` | Composition root + commands | Core, Occt |
| `native/` | C++ facade over OCCT, public header `native/include/upsteper/upsteper.h` | OCCT |
| `tests/*` | xUnit v3 tests, one project per production project | project under test |

Never reference OCCT or native types from Core. UI projects (planned) depend on Core, never on
`UpSTEPer.Occt` internals.

## Changing the native API (checklist)

1. Edit `native/include/upsteper/upsteper.h` (C only; document ownership, units, 0-based ids).
2. Implement in `native/src/*.cpp`; wrap the body in `ups::detail::Guarded(...)` so no exception
   crosses the ABI; report errors with `Fail(status, message)`.
3. Mirror the declaration in `src/UpSTEPer.Occt/Native/NativeMethods.cs` (struct layouts must match
   exactly, field by field).
4. Expose it through a Core interface; translate status codes with `NativeError.ThrowIfFailed`.
5. Add an integration test in `tests/UpSTEPer.Occt.Tests` using a file from `examples/`.

## Conventions

- .NET: Framework Design Guidelines; file-scoped namespaces; nullable enabled; XML docs on all
  public API; `TreatWarningsAsErrors`; analyzers at `latest-all`. Never silence a rule with
  `NoWarn`/`#pragma`; follow "When a rule does not fit" in CONTRIBUTING.md.
- P/Invokes: `upsteper_native` is loaded only by absolute path from the app directory
  (`NativeLibraryResolver`); do not add other native libraries without the same treatment.
- Package versions only in `Directory.Packages.props` (Central Package Management).
- C++17, OCCT naming in native code (`PascalCase` functions inside `ups::detail`), `ups_` prefix
  and `snake_case` for exported C symbols.
- OCCT 8 deprecated the `TopTools_*`/`TDF_LabelSequence` typedefs: use the aliases in
  `native/src/internal.hpp` (`ShapeIndexedMap`, `ShapeAncestorMap`, `LabelSequence`).
- Units: millimetres, radians, colours as sRGB floats 0..1.
- Geometry is not thread-safe: one document is only touched from one thread at a time.
- Tests: `Method_Condition_Expectation` names; tolerances for floating point; never commit
  generated or modified CAD files (`examples/` files are licensed No-Derivatives).

## Gotchas

- "Circles look like polygons" is usually **display tessellation**, not a bad file. Check the
  STEP entities first (`CIRCLE`, `CYLINDRICAL_SURFACE` present ⇒ geometry is exact). See
  `docs/test-data.md`.
- `BRepMesh_IncrementalMesh` keeps an existing finer triangulation; re-meshing coarser requires
  clearing it (`BRepTools::Clean`) first.
- Face ids are indices into a `ShapeIndexedMap` and become invalid after any topology change;
  see `docs/adr/0005-topology-identity.md`.
- Windows native build: `Enter-VsDevShell` overwrites `VCPKG_ROOT` with the VS-bundled vcpkg;
  `scripts/build-native.ps1` re-sets it afterwards. OCCT object paths exceed MAX_PATH unless
  vcpkg buildtrees live on a short path (`VCPKG_INSTALL_OPTIONS` in `CMakePresets.json`).
- Parse numbers in the CLI with `Options.InvariantDouble`, never culture-dependent: on a German
  system `0.05` would otherwise become 5.
- OCCT prints parser diagnostics (`**** ERR StepFile ...`) to stdout; not yet redirected.
- `dotnet test` on .NET 10 requires the MTP runner (`global.json` → `test.runner`); do not
  re-add `Microsoft.NET.Test.Sdk`.
