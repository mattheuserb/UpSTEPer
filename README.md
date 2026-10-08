# UpSTEPer

**View, colour, repair and convert STEP models — cross-platform, open source, built on Open CASCADE.**

[![CI](https://github.com/mattheuserb/UpSTEPer/actions/workflows/ci.yml/badge.svg)](https://github.com/mattheuserb/UpSTEPer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> **Status: pre-alpha (M2).** Load and view STEP models, select faces, colour faces and bodies
> (undo/redo) and save as STEP — in the app and via the CLI. See the [roadmap](docs/roadmap.md).

## Goals

| Feature | Description | Milestone |
|---|---|---|
| View | Load STEP (AP203/AP214/AP242) and display it in 3D | M1 |
| Colour | Colour individual bodies and individual faces, save back to STEP (app + `upsteper paint`) | M2 ✅ |
| Export | Generate 2D drawing views (front/top/side/iso, hidden lines) as DXF | M3 |
| Repair | Heal poorly exported models; turn faceted "polygon circles" back into true cylinders/circles | M4–M5 |

Platforms: Windows x64, Linux x64, macOS arm64.

## Quick start

```bash
# 1. Build the native bridge (once; first build compiles OCCT, ~1 h) — see docs/development.md
cd native && cmake --preset win-x64 && cmake --build --preset win-x64 && cmake --install build/win-x64 && cd ..

# 2. Build, test, run
dotnet build
dotnet test
dotnet run --project src/UpSTEPer.Cli -- info "examples/ESKV 12 RAL 7035 10066410.stp"
```

Without step 1 everything still builds; tests that need the geometry kernel are skipped.

Before pushing, run the same quality gate as CI (format, analyzers, tests, coverage):
`./scripts/check.ps1` — rules in [CONTRIBUTING.md](CONTRIBUTING.md#code-quality).

## Architecture in one picture

```
 UpSTEPer.App (Avalonia, planned)    UpSTEPer.Cli (System.CommandLine)
              \                          /
               └──── UpSTEPer.Core ─────┘     domain model + abstractions, no native deps
                          ▲
                  UpSTEPer.Occt               implements Core via P/Invoke + SafeHandles
                          │  C ABI (upsteper.h)
                  native/ upsteper_native     thin C++ facade, CMake + vcpkg
                          │
                  Open CASCADE Technology 8   (LGPL-2.1, dynamically linked)
```

Details: [docs/architecture.md](docs/architecture.md) · Decisions: [docs/adr/](docs/adr/)

## Documentation

| Document | For |
|---|---|
| [docs/development.md](docs/development.md) | Setting up a machine, building, testing, debugging |
| [docs/architecture.md](docs/architecture.md) | Layers, threading, memory ownership, extension points |
| [docs/roadmap.md](docs/roadmap.md) | Milestones and open work |
| [docs/adr/](docs/adr/) | Why things are the way they are |
| [docs/test-data.md](docs/test-data.md) | Test files and what they cover |
| [CLAUDE.md](CLAUDE.md) | Working on this repo with Claude Code (also a good human cheat-sheet) |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Workflow, conventions, PR checklist |

## License

UpSTEPer is MIT licensed. It dynamically links Open CASCADE Technology (LGPL-2.1 with
exception) and other third-party components; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
