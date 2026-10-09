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

## Addendum (2026-10-08, implementation)

- Layers are `VISIBLE`, `HIDDEN` (linetype `DASHED`) and `TANGENT` instead of `OUTLINE`:
  silhouettes are visible edges in drawing practice; smooth face transitions are the optional extra.
- Views are arranged in **first-angle projection** (ISO 128 method E, the European standard).
- **Scale 1:1 by default, no drawing frame and no title block** (decision by the project owner,
  2026-10-08): the DXF holds model coordinates in millimetres; framing, scaling and plotting are
  done in the CAD system. Other scales or a frame may come later as opt-in options only.
- HLR reports hidden edges that project exactly onto visible ones; those stretches are cut out
  before export (`HiddenLineCleanup`), otherwise contours appear doubled. Partly covered hidden
  edges are split and the rest keeps its curve type (2026-10-09; before, only fully covered edges
  were dropped, and a line was judged by its two end points alone, which wrongly deleted hidden
  edges running between two visible ones).
- Circles seen edge-on come out of HLR as ellipses with a vanishing minor axis, which DXF readers
  reject; the native layer emits them as lines.
