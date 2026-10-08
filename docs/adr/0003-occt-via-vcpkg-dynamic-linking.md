# 0003. Obtain OCCT via vcpkg, link dynamically

- Status: Accepted
- Date: 2026-10-08

## Context

OCCT must be built reproducibly for three platforms. OCCT is licensed LGPL-2.1 with an
additional exception; UpSTEPer is MIT.

## Decision

- Use vcpkg manifest mode (`native/vcpkg.json`) with a pinned `builtin-baseline` (OCCT 8.0.1 at
  the time of writing). CMake presets select dynamic triplets: `x64-windows`,
  `x64-linux-dynamic`, `arm64-osx-dynamic`.
- Link OCCT **dynamically** and ship its unmodified shared libraries next to ours, with its
  licence texts. This satisfies the LGPL (users can replace OCCT) without affecting our licence.
- `cmake --install` collects our library plus runtime dependencies into `artifacts/native/<rid>`;
  RPATH is `$ORIGIN` / `@loader_path`.
- CI caches vcpkg binaries and saves the cache right after configure.

## Alternatives considered

| Option | Why not |
|---|---|
| Static linking | LGPL relinking obligations become awkward; larger binaries |
| System packages (apt/brew) | Different OCCT versions per platform; not available on Windows |
| Vendor prebuilt OCCT binaries | Not available for all targets/versions; less reproducible |

## Consequences

- The first build is slow (OCCT from source); binary caching mitigates it.
- Packaging of dependent `.dylib`/`.so` files on macOS and Linux must be verified in CI (M0).
