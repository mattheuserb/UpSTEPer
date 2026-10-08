# 0005. Face identity and colour preservation across edits

- Status: Accepted
- Date: 2026-10-08

## Context

Users colour individual faces. OCCT has no persistent face ids; ids derived from iteration order
change whenever topology changes (for example when repair merges or splits faces).

## Decision

- A `FaceId` is the 0-based index of a face in the document's `ShapeIndexedMap`, valid only for
  one *revision* of the geometry.
- User colour assignments are stored against the OCCT shape (in XDE), not against the integer id.
- Every topology-changing operation (repair, healing) must use OCCT algorithms that provide a
  history (`BRepTools_History`, `Modified()`/`Generated()`); the native layer re-applies colours
  to the resulting faces. Merged faces with conflicting colours take the colour covering the
  largest area, and the conflict is listed in the operation report.
- After a topology change the face map, face ids and mesh are rebuilt; UI selection is mapped
  through the same history.

## Consequences

- Repair steps without history support cannot be used unless a geometric mapping is computed.
- Tests must verify colours before and after each repair step.
