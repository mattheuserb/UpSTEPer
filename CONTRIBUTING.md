# Contributing to UpSTEPer

Thanks for your interest! This document describes how we work.

## Before you start

- Read [docs/architecture.md](docs/architecture.md) and skim [docs/adr/](docs/adr/).
- For anything larger than a bug fix, open an issue first so we can agree on the approach.
- Set up your machine with [docs/development.md](docs/development.md).

## Workflow

1. Fork and create a branch: `feature/<short-name>` or `fix/<short-name>`.
2. Keep changes focused; one concern per pull request.
3. `./scripts/check.ps1` must pass (see [Code quality](#code-quality)).
4. If you touch native code, CI must pass on all three platforms.
5. Open a PR describing *what* and *why*; link the issue.

Commit messages: imperative mood, short subject (≤ 72 chars), body explaining why.

## Code quality

All checks run locally with one command, exactly as in CI:

```powershell
./scripts/check.ps1          # verify: format, analyzers, tests, coverage report
./scripts/check.ps1 -Fix     # apply formatting/code-style fixes first
./scripts/check.ps1 -NoCoverage
```

(Windows PowerShell 5.1 or PowerShell 7 on any OS. Without the native library, kernel tests are
skipped and the coverage minimum is only reported, not enforced; set `UPS_REQUIRE_NATIVE=1` to
make kernel tests mandatory as in CI.)

### Rules

| Area | Rule | Where configured |
|---|---|---|
| Analyzers | All .NET analyzers (`AnalysisLevel=latest-all`) | `Directory.Build.props` |
| Warnings | Every compiler/analyzer warning is an error | `TreatWarningsAsErrors` |
| Code style | Naming, `var`, braces, usings, file-scoped namespaces ... enforced in the build | `.editorconfig` + `EnforceCodeStyleInBuild` |
| Formatting | `dotnet format --verify-no-changes` must pass | CI job `format + analyzers` |
| Nullability | Nullable reference types enabled everywhere | `Directory.Build.props` |
| Docs | XML documentation on all public API (CS1591), not required in tests | `GenerateDocumentationFile` |
| Tests | All tests green on Windows, Linux and macOS; native tests mandatory in CI | `.github/workflows/ci.yml` |
| Coverage | Minimum **90 % line / 70 % branch** (generated code excluded); report on every CI run (job summary + `coverage-report` artifact). Thresholds only go up; lowering one needs a reason in the commit message | `scripts/coverage-report.ps1` |

### When a rule does not fit

1. Prefer fixing the code.
2. If a rule is wrong for the whole project, relax it in `.editorconfig` with a reason in the
   same line. Never use `<NoWarn>`.
3. For a single, justified spot use `[SuppressMessage(..., Justification = "...")]`, not
   `#pragma warning disable`.
4. Temporary relaxations for legacy code: set the rule to `suggestion` and add
   `# TODO(#<issue>): ...` with a tracking issue. New code must not add to that debt.

StyleCop.Analyzers is intentionally not used: it largely duplicates the IDE rules in
`.editorconfig`, and its defaults (`this.` prefixes, no `_` field prefix, file headers) contradict
the .NET conventions this repository follows.

## PR checklist

- [ ] Tests added/updated (unit for Core, integration for native features)
- [ ] Public API has XML documentation
- [ ] Native API changes mirrored in `NativeMethods.cs` (see checklist in `CLAUDE.md`)
- [ ] Docs updated (`docs/`, `CLAUDE.md`, roadmap checkboxes)
- [ ] New decision that is hard to reverse? → add an ADR
- [ ] New test files: licence allows redistribution, recorded in `docs/test-data.md`
- [ ] New NuGet package shipped by the app or CLI: listed in `THIRD-PARTY-NOTICES.md`
- [ ] User-visible change: entry in `CHANGELOG.md` under "Unreleased"

## Coding conventions

- C#: [.NET Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/),
  enforced via `.editorconfig` and analyzers.
- C++: C++17, no exceptions across the C ABI, all exported functions wrapped in `Guarded`.
- Dependencies: versions only in `Directory.Packages.props`; new dependencies need a licence
  compatible with MIT and an entry in `THIRD-PARTY-NOTICES.md`.

## Using Claude Code

`CLAUDE.md` contains the commands, boundaries and gotchas an AI assistant needs. Review
AI-generated changes like any other contribution; the PR author is responsible for them.

## Code of conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md).
