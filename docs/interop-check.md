# Interoperability check: colours in other CAD systems

Manual check that STEP files written by UpSTEPer show the same colours in other CAD systems
(roadmap M2). Repeat it after changes to colouring or STEP export.

## Generate the files

The files go to `out/interop/` (ignored by Git; never commit them).

```bash
for s in ap214 ap242; do
  dotnet run --project src/UpSTEPer.Cli -- paint "examples/ESKV 12 RAL 7035 10066410.stp" \
    "out/interop/gland-face-body-$s.stp" --body 0=#3366CC --face 80=#FF0000 --schema $s
  dotnet run --project src/UpSTEPer.Cli -- paint examples/generated/test-assembly.stp \
    "out/interop/assembly-instance-$s.stp" --body 1=#FF0000 --body 5=#57A639 --face 5=#FFCC00 --schema $s
done
```

Reference images as UpSTEPer shows them:
`dotnet run --project src/UpSTEPer.App -- out/interop/<file>.stp --screenshot out/interop/expected-<name>.png`.
Body and face ids: `dotnet run --project src/UpSTEPer.Cli -- info <file> --tree`.

## Expected result

| File | Expected in the other CAD system |
|---|---|
| `gland-face-body-*.stp` | Whole cable gland blue (#3366CC), one small face in the side slot red (#FF0000). |
| `assembly-instance-*.stp` | Top face of the base plate yellow (#FFCC00), its other faces without colour. **Left** pin red (#FF0000), **right** pin without colour although both are instances of the same part. Lower washer of the **right** pin assembly green (#57A639), all other washers without colour. |

## Checklist

For each system, open all four files (AP214 and AP242) and record the result here.

| System, version | File | Colours as expected? | Notes |
|---|---|---|---|
| FreeCAD 1.1.1 | gland-face-body-ap214 | yes | 2026-10-13 |
| FreeCAD 1.1.1 | gland-face-body-ap242 | yes | 2026-10-13 |
| FreeCAD 1.1.1 | assembly-instance-ap214 | yes | 2026-10-13 |
| FreeCAD 1.1.1 | assembly-instance-ap242 | yes | 2026-10-13 |
| Commercial CAD (e.g. SolidWorks, Inventor, NX, eDrawings) | all four | not needed | Decision 2026-10-13: the FreeCAD check suffices |

Things to look for:

- Face colour wins over body colour (red slot face on the blue gland).
- Instance colours: only the left pin is red. If both pins are red, the system ignores the
  per-instance copy (see ADR 0009) or merges identical part definitions.
- Uncoloured faces show the system's default colour, not black.
- The AP242 files open at all (older systems may only read AP203/AP214).
