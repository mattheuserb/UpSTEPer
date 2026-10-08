# 0002. Access OCCT through a narrow C ABI and P/Invoke

- Status: Accepted
- Date: 2026-10-08

## Context

OCCT is a large C++ library (thousands of classes). The tool must run on Windows, Linux and
macOS and is written in C#. There is no maintained, cross-platform, open-source .NET binding.

## Decision

Write `upsteper_native`, a small C++ shared library that exposes a **use-case oriented C API**
(`native/include/upsteper/upsteper.h`): opaque handles, plain structs, status codes, UTF-8
strings, caller-allocated buffers for bulk data. .NET calls it via `[LibraryImport]` (source
generated, trimming/AOT friendly) and wraps handles in `SafeHandle`.

## Alternatives considered

| Option | Why not |
|---|---|
| C++/CLI wrapper (as in the OCCT C# sample, Macad\|3D) | Windows only |
| SWIG over all of OCCT | Huge generated surface, no maintained C# bindings, leaky abstractions, hard to debug |
| Commercial OCCT .NET wrapper | Not compatible with an open-source project |
| Another language (Python + pythonOCC, C++ + Qt) | Excluded by the project requirement (C#) |

## Consequences

- + Small, stable, testable boundary; OCCT upgrades rarely touch .NET code.
- + No C++ types or exceptions leak; identical behaviour on all platforms.
- − Every new feature needs native + interop + .NET work (checklist in `CLAUDE.md`).
- − Native binaries must be built and shipped per platform (ADR 0003).
