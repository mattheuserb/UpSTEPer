# 0007. Tiered geometry repair

- Status: Accepted
- Date: 2026-10-08

## Context

"Circles that look like polygons" has three causes with very different effort:

1. Coarse display tessellation — the file is fine (see `docs/test-data.md`).
2. Circles/cylinders were exported as B-splines.
3. The model is faceted: a cylinder consists of N planar faces (mesh-based exports).

Fully automatic recognition of case 3 is what specialised commercial products (e.g. CADfix) sell.

## Decision

Implement repair as independent tiers, each producing a report and preserving colours (ADR 0005):

| Tier | Problem | Approach |
|---|---|---|
| 0 | Display only | Finer `TessellationOptions` — no repair needed |
| 1 | Gaps, tolerances, split faces | `ShapeFix_Shape`, `BRepBuilderAPI_Sewing`, `ShapeUpgrade_UnifySameDomain` |
| 2 | Canonical geometry stored as splines | `ShapeAnalysis_CanonicalRecognition` / `ShapeCustom` |
| 3 | Faceted cylinders/cones | Own algorithm: group adjacent planar facets whose normals are perpendicular to a common axis, least-squares fit, replace faces, rebuild topology. **Semi-automatic first.** |

## Consequences

- Tiers 1–2 deliver value quickly with mostly existing OCCT functionality.
- Tier 3 is research-grade; its scope depends on real problem files, which are still needed.
