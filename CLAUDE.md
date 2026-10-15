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
dotnet run --project src/UpSTEPer.Cli -- info <file.stp> [--tree] [--verbose] # stats + colours + mesh; --tree: model tree with body ids
dotnet run --project src/UpSTEPer.Cli -- paint <in.stp> <out.stp> --face 80=#FF0000 --body 0=#3366CC [--schema ap242]   # colour + save
dotnet run --project src/UpSTEPer.Cli -- check <file.stp>   # defects per part (gaps, invalid, canonical splines, mergeable faces); exit 3 if any
dotnet run --project src/UpSTEPer.Cli -- repair <in.stp> <out.stp> [--steps fix,sew,canonical,merge]   # heal + exact surfaces/edges, keep colours, report
dotnet run --project src/UpSTEPer.Cli -- export-dxf <in.stp> <out.dxf> [--views front,top,left]  # 2D views; add ",iso" for the isometric one
dotnet run --project src/UpSTEPer.App -- <file.stp>       # desktop viewer
dotnet run --project src/UpSTEPer.App -- <file.stp> --screenshot out.png [--select-face N] [--view front|top|iso|...] [--ortho] [--quality coarse|normal|fine] [--hover-face N] [--window] [--language en|de]   # render 1 frame to PNG and exit; --window: whole window
./scripts/publish.ps1 [-Rid win-x64] [-Version 0.1.0-alpha]   # self-contained app + CLI + native libs → artifacts/publish/<rid>
./scripts/build-installer.ps1 [-Version ...] [-SkipPublish]      # Windows installer (Inno Setup 6) → artifacts/installer; docs/packaging.md
./scripts/package.ps1 -Rid <rid> [-Version ...]                  # release files of one platform → artifacts/release
scripts/build-flatpak.sh <version>                               # Linux only: Flatpak bundle from artifacts/publish/linux-x64
./scripts/update-screenshots.ps1   # regenerate docs/images (README pictures) after visible UI changes

# Native bridge (needs C++ toolchain, CMake >= 3.24, Ninja, VCPKG_ROOT) — see docs/development.md
powershell -File scripts/build-native.ps1 -VcpkgRoot C:\vcpkg [-Tools]   # Windows: sets up MSVC env itself; -Tools: test data generators
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
| `src/UpSTEPer.App` | Avalonia desktop app (MVVM), `ModelView` hosts the renderer; assembly/exe `UpSTEPer.Desktop` | Core, Occt, Rendering |
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

## Tools

- `.mcp.json` registers the Avalonia documentation MCP server (`avalonia-docs`,
  https://docs-mcp.avaloniaui.net/mcp). Consult it for Avalonia APIs and behaviour (Avalonia 12 changed
  much) before guessing or decompiling; approve it once when Claude Code asks.

## Conventions

- .NET: Framework Design Guidelines; file-scoped namespaces; nullable enabled; XML docs on all
  public API; `TreatWarningsAsErrors`; analyzers at `latest-all`. Never silence a rule with
  `NoWarn`/`#pragma`; follow "When a rule does not fit" in CONTRIBUTING.md.
- P/Invokes: `upsteper_native` is loaded only by absolute path from the app directory
  (`NativeLibraryResolver`); do not add other native libraries without the same treatment.
- Package versions only in `Directory.Packages.props` (Central Package Management).
  A new package the app or the CLI ships (also a transitive one) needs a row in
  `THIRD-PARTY-NOTICES.md`; `ThirdPartyNoticesTests` checks this.
- App UI: colours, spacing and type sizes only from `src/UpSTEPer.App/Themes/Tokens.axaml`; icons are
  Lucide geometries (`Themes/Icons.axaml`, regenerate with `scripts/update-icons.py`, never edit by
  hand) drawn by the `Icon` control. The app icon (`Assets/upsteper.*`) is generated by
  `scripts/update-app-icon.py`; change its geometry or colours there, never the files. Every command must stay reachable from the menu; toolbars only
  hide labels or the inline palette in narrow windows (container queries), they never wrap.
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
- `BRepMesh_IncrementalMesh` keeps an existing finer triangulation; `ups_document_tessellate`
  therefore clears it (`BRepTools::Clean`) first. Display quality scales the chord tolerance with
  the model size (`TessellationOptions.ForModel`); fixed millimetre values explode on large models.
- Face ids are indices into a `ShapeIndexedMap` and become invalid after any topology change;
  see `docs/adr/0005-topology-identity.md`.
- Windows native build: `Enter-VsDevShell` overwrites `VCPKG_ROOT` with the VS-bundled vcpkg;
  `scripts/build-native.ps1` re-sets it afterwards. OCCT object paths exceed MAX_PATH unless
  vcpkg buildtrees live on a short path (`VCPKG_INSTALL_OPTIONS` in `CMakePresets.json`).
- App texts are localised (English, German): `src/UpSTEPer.App/Localization/Strings.resx` and
  `Strings.de.resx`, every key in both (`LocalizationTests`). XAML uses `{loc:Tr Key}`, C# the generated
  `Strings.Key` and `Localizer.Format`; never a literal UI text. Logs, exceptions and the CLI stay
  English. Tests run in English; tests that switch the language belong to the `LanguageSwitching` collection.
- RAL colours come only from `src/UpSTEPer.App/Colours/ral-classic.json` (Wikidata, CC0), regenerated by
  `scripts/update-ral.py`; never copy values from other lists (licences, e.g. Wikipedia is CC BY-SA).
- Releases: tag `v<version>` after setting it in `Directory.Build.props`, `CHANGELOG.md` and the Flatpak
  metainfo; the release workflow checks that they agree (`docs/releasing.md`).
- The app keeps user settings (recent files, language, favourite colours) in `settings.json` next
  to the log folder (`SettingsStore`); add members with defaults so older files still load. Tests and
  `--screenshot` runs must use their own settings path, never the user's file.
- Parse numbers in the CLI with `Options.InvariantDouble`, never culture-dependent: on a German
  system `0.05` would otherwise become 5.
- OCCT never prints to the console: `messages.cpp` routes its messages to `OcctMessageRouter`
  (logger category `OCCT`). OCCT reports real file problems ("Incorrect record ...") at its lowest
  level, mapped to Debug: use `upsteper <command> --verbose`, or read the app's log file
  (`%LOCALAPPDATA%/UpSTEPer/logs`, `~/.local/share/UpSTEPer/logs`), when debugging import/export.
- Progress callbacks (`ups_progress_callback`) can arrive on OCCT worker threads (parallel
  meshing); keep handlers thread-safe and never let an exception unwind into native code (ADR 0010).
- Never call OCCT STEP readers/writers or create/close XDE documents without holding
  `ups::detail::TranslatorMutex()` (global OCCT state; concurrent use crashes).
- `dotnet test` on .NET 10 requires the MTP runner (`global.json` → `test.runner`); do not
  re-add `Microsoft.NET.Test.Sdk`.
