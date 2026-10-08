# 0001. Record architecture decisions

- Status: Accepted
- Date: 2026-10-08

## Context

The project is meant to be continued by people who were not part of the original discussions,
and by AI assistants that only see the repository. Rationale that lives only in chat history is lost.

## Decision

Every decision that is expensive to reverse (dependencies, layering, file formats, public API
shape) gets an ADR in `docs/adr/`. Accepted ADRs are not rewritten; a change of mind is a new ADR
that supersedes the old one.

## Consequences

Small overhead per decision; newcomers and Claude can read *why*, not just *what*.
