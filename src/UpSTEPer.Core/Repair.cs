namespace UpSTEPer.Core;

/// <summary>Steps of <see cref="IStepDocument.Repair"/> (repair tiers 1 and 2, ADR 0007).</summary>
[Flags]
public enum RepairSteps
{
    /// <summary>No step.</summary>
    None = 0,

    /// <summary>General fixes (OCCT ShapeFix): tolerances, orientation, wires, small defects.</summary>
    Fix = 1,

    /// <summary>Sew faces along free edges; closed shells become solids.</summary>
    Sew = 2,

    /// <summary>Merge neighbouring faces that lie on the same surface.</summary>
    MergeFaces = 4,

    /// <summary>
    /// Replace spline faces that are planes, cylinders, cones or spheres, and spline edges that are
    /// lines, circles or ellipses, within the tolerance by the exact geometry (tier 2); runs before
    /// merging, so split pieces of one cylinder can merge.
    /// </summary>
    Canonical = 8,

    /// <summary>All steps.</summary>
    All = Fix | Sew | MergeFaces | Canonical,
}

/// <summary>Options for <see cref="IStepDocument.Repair"/>.</summary>
/// <param name="Steps">The healing steps to run.</param>
/// <param name="Tolerance">
/// Tolerance in mm: gaps up to this size are sewn, faces within it merged, splines within it
/// replaced by exact geometry.
/// </param>
public sealed record RepairOptions(RepairSteps Steps = RepairSteps.All, double Tolerance = 0.05)
{
    /// <summary>Gets the default options: all steps, 0.05 mm.</summary>
    public static RepairOptions Default { get; } = new();
}

/// <summary>Outcome of <see cref="IStepDocument.Repair"/> for one part definition.</summary>
/// <param name="Name">Part name; may be empty.</param>
/// <param name="Changed">Whether the part's geometry was replaced.</param>
/// <param name="Failed">Whether a step failed on this part; it was skipped, the other steps still ran.</param>
/// <param name="FacesBefore">Faces before repair.</param>
/// <param name="FacesAfter">Faces after repair.</param>
/// <param name="SolidsBefore">Solids (bodies) before repair.</param>
/// <param name="SolidsAfter">Solids after repair.</param>
/// <param name="FreeEdgesBefore">Free edges (gaps) before repair.</param>
/// <param name="FreeEdgesAfter">Free edges after repair.</param>
/// <param name="InvalidBefore">Invalid sub-shapes before repair.</param>
/// <param name="InvalidAfter">Invalid sub-shapes after repair.</param>
/// <param name="ColourConflicts">Merged faces whose sources had different colours; the colour covering the largest area won.</param>
/// <param name="Reverted">
/// Whether a step made the part worse (more invalid sub-shapes, free edges or inside-out solids, or a
/// volume change above 1 %) and was discarded; the other steps still ran.
/// </param>
/// <param name="CanonicalFaces">Spline faces replaced by planes, cylinders, cones or spheres.</param>
/// <param name="MaxDeviation">Largest distance (mm) between a replaced spline and its exact surface or curve.</param>
/// <param name="CanonicalEdges">Spline edges replaced by lines, circles or ellipses.</param>
public sealed record PartRepair(
    string Name,
    bool Changed,
    bool Failed,
    int FacesBefore,
    int FacesAfter,
    int SolidsBefore,
    int SolidsAfter,
    int FreeEdgesBefore,
    int FreeEdgesAfter,
    int InvalidBefore,
    int InvalidAfter,
    int ColourConflicts,
    bool Reverted = false,
    int CanonicalFaces = 0,
    double MaxDeviation = 0,
    int CanonicalEdges = 0)
{
    /// <summary>Gets the name, or a placeholder for unnamed parts.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(unnamed part)" : Name;
}
