# Contributing to UpSTEPer

Thanks for your interest! This document describes how we work.

## Before you start

- Read [docs/architecture.md](docs/architecture.md) and skim [docs/adr/](docs/adr/).
- For anything larger than a bug fix, open an issue first so we can agree on the approach.
- Set up your machine with [docs/development.md](docs/development.md).

## Workflow

1. Fork and create a branch: `feature/<short-name>` or `fix/<short-name>`.
2. Keep changes focused; one concern per pull request.
3. `dotnet build` must be warning-free (warnings are errors) and `dotnet test` green.
4. If you touch native code, CI must pass on all three platforms.
5. Open a PR describing *what* and *why*; link the issue.

Commit messages: imperative mood, short subject (≤ 72 chars), body explaining why.

## PR checklist

- [ ] Tests added/updated (unit for Core, integration for native features)
- [ ] Public API has XML documentation
- [ ] Native API changes mirrored in `NativeMethods.cs` (see checklist in `CLAUDE.md`)
- [ ] Docs updated (`docs/`, `CLAUDE.md`, roadmap checkboxes)
- [ ] New decision that is hard to reverse? → add an ADR
- [ ] New test files: licence allows redistribution, recorded in `docs/test-data.md`

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
