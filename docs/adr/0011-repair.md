# 0011. Repair per part definition, all-or-nothing, colours through histories

- Status: Accepted
- Date: 2026-10-09

## Context

ADR 0007 sets the repair tiers, ADR 0005 how colours survive topology changes. Implementing tier 1
(healing) raised further questions:

- The geometry lives in an XDE document. A part definition can be placed many times in assemblies;
  colours sit on sub-shape labels of the definition (and, after ADR 0009, on copies of it).
- Real files contain parts on which OCCT algorithms throw (the 374 MB engine assembly in
  `docs/test-data.md`: "Courbes non jointives").
- Repairing a large model takes a while and must be cancellable.

## Decision

- **Unit of repair is the part definition** (simple-shape label that is not an assembly), each
  once however often it is placed. The repaired shape replaces the definition
  (`XCAFDoc_ShapeTool::SetShape`), assemblies are updated (`UpdateAssemblies`), and roots, face
  and body maps and display colours are rebuilt. Face and body ids are renumbered; callers re-read
  statistics, tree, colours and mesh.
- **Steps** (flags, all by default), in this order: `ShapeFix_Shape` (precision and maximum
  tolerance = repair tolerance), sewing (`BRepBuilderAPI_Sewing`, only for parts with free edges;
  closed shells become solids), canonical recognition (tier 2, below), merging faces on the same
  surface (`ShapeUpgrade_UnifySameDomain`). Default tolerance 0.05 mm.
- **Canonical recognition (tier 2)**: B-spline/Bezier faces that `ShapeAnalysis_CanonicalRecognition`
  identifies as plane, cylinder, cone or sphere within the tolerance get the exact surface. The new
  face keeps the old wires (shared edges stay shared); `ShapeFix_Shape` then adds the edges'
  parameter curves on the new surface. The exact surface is oriented like the spline (plane: normal
  reversed, others: left-handed frame) so the material side does not flip. Edges keep their curves
  (B-splines stay B-splines) for now. It runs before merging, so pieces of one cylinder can merge.
  The report gives the number of converted faces and the largest deviation. Faces whose measured
  deviation (`GetGap`) exceeds the tolerance are skipped although the recognition accepted them
  (seen on the engine assembly: 0.18 mm at 0.05 mm). If converting a part's faces fails the
  never-worse rule, the conversion of the whole part is discarded; retrying face by face is future
  work.
- **All or nothing**: every part is computed first; only if the whole run completes are the
  results written to the document. Cancellation (between parts) or an unexpected failure leaves the
  model unchanged.
- **Failures are isolated per step**: if an algorithm throws on a part, that step is skipped for
  the part (reported as failed); its other steps and all other parts are still repaired.
- **Never worse, per step**: a step whose result has more invalid sub-shapes, free edges or
  inside-out solids (negative volume), or changes the volume of the same solids by more than 1 %,
  is discarded (reported as reverted); the other steps still apply. The volume rule catches
  replacement surfaces with the material side flipped, which BRepCheck does not report. Measured on
  the engine assembly: merging faces alone raised invalid sub-shapes from 22 to 40 in 8 of 159
  parts, while ShapeFix alone lowered them to 21; discarding whole parts would also have lost the
  ShapeFix improvement. Steps that change nothing (ShapeFix without `ShapeExtend_DONE`, merging
  without fewer faces or edges) keep the shape as it is.
- **Colours** are taken from the part's sub-shape labels before repair and followed through each
  step's history (`BRepTools_History` of ShapeFix's re-shape context and of UnifySameDomain;
  `ModifiedSubShape` of sewing). A new face that several coloured faces became takes the colour
  covering the largest total area; if their colours differ, the part reports a colour conflict.
  Uncoloured contributors do not outvote a colour, so painted areas are not lost.
- **Report** per part: changed, failed, faces / solids / free edges / invalid sub-shapes before and
  after, colour conflicts. CLI `upsteper repair <in> <out> [--steps] [--tolerance] [--schema]`.

## Consequences

- Names on sub-shapes (rare) are not carried over; part names and part colours are.
- Parts placed several times are repaired once, which is both faster and keeps instances identical.
- Canonical recognition (tier 2) is a separate step and not part of this decision's steps yet.
- Covered by `RepairTests` on `examples/generated/damaged-parts.stp` (sewing, merging with colour
  conflict, cancellation, healthy files unchanged, save -> reload, instance colours of ADR 0009).
