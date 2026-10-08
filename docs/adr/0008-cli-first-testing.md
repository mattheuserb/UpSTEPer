# 0008. CLI parity and test strategy

- Status: Accepted
- Date: 2026-10-08

## Context

Geometry features are hard to verify through a GUI, and AI assistants cannot operate a GUI.

## Decision

- Every feature is available in `UpSTEPer.Cli` before or together with the GUI. GUI and CLI share
  Core services; neither contains business logic the other lacks.
- Tests: xUnit v3 on Microsoft Testing Platform. Core = pure unit tests; Occt = integration tests
  on real STEP files; DXF/STEP outputs verified by approval-style comparisons with tolerances.
- Native-dependent tests skip locally when the native library is absent but fail in CI
  (`UPS_REQUIRE_NATIVE=1`), so a missing native build never looks green.

## Consequences

Each feature costs a little more (one command per feature) but is scriptable, batch-capable and
verifiable in CI and by Claude.
