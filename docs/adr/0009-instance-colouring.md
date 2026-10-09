# 0009. Colouring faces and bodies inside assembly instances

- Status: Accepted
- Date: 2026-10-08

## Context

In an assembly the same part (or sub-assembly) definition can be placed several times. XDE stores
colours on labels: on the definition, on its sub-shape labels, or on a component (instance)
label. A face or body clicked in the viewer belongs to exactly one *occurrence*, but its
sub-shape label belongs to the shared definition, so colouring it there would recolour every
occurrence.

OCCT's per-occurrence mechanism (`XCAFDoc_ColorTool::SetInstanceColor`, SHUO) only addresses
whole component instances, not individual faces or bodies of an instance, and its support in
other CAD systems varies.

Options considered:

1. Colour the definition: every occurrence changes. Simple, but not what the user clicked.
2. SHUO instance colours: limited to whole components, no face granularity.
3. **Make the occurrence independent**: give the clicked occurrence its own copies of every
   shared definition on its path, then colour the copy's sub-shape label.

The project owner chose "only this instance".

## Decision

Option 3, implemented in `native/src/instances.cpp` (`MakeOccurrenceUnique`):

- Walking from the root to the part, every component whose definition has more than one user is
  re-pointed to a copy: assemblies are copied as new assembly labels with the same components,
  parts with `BRepBuilderAPI_Copy` plus name and colours (including sub-shape colours).
- The new component keeps the old placement; the old component is removed.
- Face and body ids are preserved: the face/solid maps are rebuilt in the old order through the
  copier's history, and a mismatch is an internal error instead of silently renumbered ids
  (see ADR 0005).
- Display colour precedence per occurrence: face colour → body colour → instance → definition →
  parent assembly.
- Clearing a colour that is not assigned is a no-op and does not copy anything.

## Consequences

- A saved STEP file contains an extra product definition for each part that was coloured
  individually (for example "Pin" twice). Geometry, names and placements are unchanged; other
  CAD systems show two identical parts instead of two instances of one.
- Undo restores the colour but not the shared definition; the copies stay until the file is
  reloaded. This is acceptable because the visible result is identical.
- Covered by `AssemblyColoringTests` (face, body, ids/tree preserved, clear, save → reload) on
  `examples/generated/test-assembly.stp`.
