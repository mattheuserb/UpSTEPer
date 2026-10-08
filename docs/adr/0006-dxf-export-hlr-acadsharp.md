# 0006. DXF only, 2D views via OCCT HLR and ACadSharp

- Status: Accepted
- Date: 2026-10-08

## Context

Users need drawings from STEP models. DWG is proprietary; 3D solids in DWG/DXF are stored as
ACIS (SAT/SAB) data, which is not realistically producible. The project owner confirmed that
DXF with 2D views is sufficient.

## Decision

- Output **DXF only**, as **2D drawing views** (front, top, side, isometric; sections later).
- Projection and hidden-line removal with OCCT `HLRBRep_Algo` in the native layer; edges are
  returned classified (line, circle, arc, ellipse, B-spline) with visibility.
- Writing with **ACadSharp** (MIT, pure C#). Circles and arcs are written as `CIRCLE`/`ARC`,
  never as polylines. Layers: `VISIBLE`, `HIDDEN`, `OUTLINE`.
- Dimensions and annotations are out of scope until requested.

## Alternatives considered

| Option | Why not |
|---|---|
| DWG via ODA/Teigha | Commercial membership, incompatible with open source |
| LibreDWG | GPL-3.0 would force the whole project to GPL |
| netDxf | Viable (MIT); ACadSharp chosen for active development and optional DWG later |

## Consequences

ACadSharp can also write DWG; enabling it later is a small change if ever needed.
