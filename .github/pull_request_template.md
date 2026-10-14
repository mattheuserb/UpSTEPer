## What and why

<!-- What does this change, and which problem does it solve? Link the issue: "Fixes #123". -->

## How it was tested

<!-- Commands you ran, files you tried. Say so if the native library was not built locally. -->

## Checklist

- [ ] `./scripts/check.ps1` passes locally
- [ ] Tests added/updated (unit for Core, integration for native features)
- [ ] Public API has XML documentation
- [ ] Native API changes mirrored in `NativeMethods.cs` (see checklist in `CLAUDE.md`)
- [ ] Docs updated (`docs/`, `CLAUDE.md`, roadmap checkboxes)
- [ ] User-visible change: entry in `CHANGELOG.md` under "Unreleased"
- [ ] New decision that is hard to reverse? → add an ADR
- [ ] New test files: licence allows redistribution, recorded in `docs/test-data.md`
- [ ] New NuGet package shipped by the app or CLI: listed in `THIRD-PARTY-NOTICES.md`
