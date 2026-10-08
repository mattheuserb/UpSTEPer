# Development guide

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.100+ | pinned via `global.json` (`latestFeature` roll-forward) |
| C++ compiler | MSVC 2022 (v143) / GCC 11+ / Apple Clang 15+ | Windows: Visual Studio workload **"Desktop development with C++"** |
| CMake | 3.24+ | bundled with the VS C++ workload |
| Ninja | any recent | bundled with the VS C++ workload; `apt install ninja-build`, `brew install ninja` |
| vcpkg | baseline in `native/vcpkg.json` | `git clone https://github.com/microsoft/vcpkg && ./vcpkg/bootstrap-vcpkg.(sh|bat)`, set `VCPKG_ROOT` |

Linux additionally needs: `pkg-config autoconf autoconf-archive automake libtool bison libgl1-mesa-dev libx11-dev libxext-dev libxi-dev libxrandr-dev libxinerama-dev libxcursor-dev`
(the exact list is maintained in `.github/workflows/ci.yml`).

## Building the native bridge

The .NET code builds without it; only kernel-dependent tests and the CLI need it.

**Windows (recommended):** the script finds Visual Studio/Build Tools via `vswhere`, loads the
MSVC developer environment and runs configure, build and install:

```powershell
git clone https://github.com/microsoft/vcpkg C:\vcpkg; C:\vcpkg\bootstrap-vcpkg.bat -disableMetrics
./scripts/build-native.ps1 -VcpkgRoot C:\vcpkg
```

Keep `VCPKG_ROOT` on a short path without spaces. The presets build vcpkg ports in
`VCPKG_ROOT/b` (`--x-buildtrees-root`), because OCCT object file paths exceed the Windows
260-character limit when built inside a deep project folder. Windows uses the
`x64-windows-release` triplet (dynamic, release only), which halves the OCCT build time.

The MSVC runtime (`vcruntime140*.dll`, `msvcp140*.dll`) is intentionally not copied to
`artifacts/native`; end users need the VC++ Redistributable (to be handled by the installer).

**Manually (all platforms):**

```bash
cd native
cmake --preset win-x64          # or linux-x64 / osx-arm64; Windows: run from a "Developer PowerShell for VS"
cmake --build --preset win-x64
cmake --install build/win-x64   # -> artifacts/native/win-x64/ (our library + OCCT + deps)
```

The first configure builds OCCT and its dependencies from source through vcpkg (roughly 30–90
minutes). Results are cached in vcpkg's binary cache, so later builds take seconds.

`src/UpSTEPer.Occt/UpSTEPer.Occt.csproj` copies everything in `artifacts/native/<rid>/` to the
output of every project that references it. The RID defaults to the host
(`win-x64`, `linux-x64`, `osx-arm64`); override with `-p:UpsNativeRid=...` or point at any folder
with `-p:UpsNativeDir=...`.

Check the result with:

```bash
dotnet run --project src/UpSTEPer.Cli -- runtime
```

## Tests

```bash
dotnet test                                   # all
dotnet test --project tests/UpSTEPer.Occt.Tests
UPS_REQUIRE_NATIVE=1 dotnet test              # fail instead of skip when native is missing (CI does this)
```

- Framework: xUnit v3 on Microsoft Testing Platform (`global.json` → `test.runner`).
- `UpSTEPer.Core.Tests`: pure unit tests, no native code.
- `UpSTEPer.Occt.Tests`: integration tests against real STEP files in `examples/`.

## Debugging native code

- **Windows / Visual Studio:** open `native/` as a CMake folder, or attach to the `dotnet`/`upsteper`
  process with *Mixed (.NET + Native)* debugging enabled. Build with `-DCMAKE_BUILD_TYPE=Debug`
  (add a `CMakeUserPresets.json`, which is git-ignored).
- **Linux/macOS:** `lldb -- dotnet bin/.../upsteper.dll info file.stp`.
- Every native failure carries a message (`ups_last_error_message`) that ends up in the .NET
  exception text — read it before attaching a debugger.

## CI

`.github/workflows/ci.yml` runs on every push and pull request:

| Job | Runs | Fails on |
|---|---|---|
| `format + analyzers` | Ubuntu, ~2 min, no native build | `dotnet format` differences, any analyzer/compiler warning |
| `win-x64`, `linux-x64` | always | build, native or .NET test failures |
| `osx-arm64` | pushes to `main`, manual runs | same |

`linux-x64` also collects code coverage: summary on the run page, HTML report as artifact
`coverage-report`. Locally: `./scripts/check.ps1` (see CONTRIBUTING.md).

Two cache layers keep runs short:

| Cache | Key | Effect |
|---|---|---|
| `native-<preset>` | hash of everything under `native/` | Unchanged native code → the installed `upsteper_native` + OCCT are restored; vcpkg/CMake steps are skipped entirely |
| `vcpkg-<preset>` | hash of `native/vcpkg.json` + `native/triplets/` | Native code changed → only our library is compiled; OCCT comes from the vcpkg binary cache |

OCCT is rebuilt from source only when the vcpkg baseline or triplets change, when a runner-image
compiler update changes vcpkg's ABI hash, or when GitHub evicts the cache (7 days unused, 10 GB
per repository). The vcpkg cache is saved right after configure, so a later failure does not
lose that work. All triplets are release-only (`native/triplets/` overlays for Linux/macOS).

macOS runs only on pushes to `main` and manual runs (10× minute billing on private
repositories). Native binaries per platform are uploaded as artifacts (`native-<rid>`).

## Updating OCCT / vcpkg

1. Pick a new vcpkg commit and set it as `builtin-baseline` in `native/vcpkg.json`.
2. Push a branch; CI rebuilds OCCT (cache key changes with the manifest).
3. Read the OCCT release notes for deprecations (OCCT 8.0 deprecated the collection typedefs).
