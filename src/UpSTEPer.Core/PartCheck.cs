namespace UpSTEPer.Core;

/// <summary>Defects found in one part definition by <see cref="IStepDocument.Check"/>.</summary>
/// <remarks>A part placed several times in assemblies is checked (and listed) once.</remarks>
/// <param name="Name">Part name from the STEP file; may be empty.</param>
/// <param name="SolidCount">Number of solids (bodies).</param>
/// <param name="FaceCount">Number of faces.</param>
/// <param name="FreeEdges">Edges bounding only one face: gaps between faces or an open shell; repaired by sewing.</param>
/// <param name="InvalidShapes">Faces, edges and vertices the OCCT checker reports as invalid.</param>
/// <param name="SplineFaces">Faces on B-spline or Bezier surfaces (not a defect in itself).</param>
/// <param name="CanonicalFaces">Spline faces that are really planes, cylinders, cones or spheres within the tolerance.</param>
/// <param name="MergeableFaces">Faces that disappear when neighbouring faces on the same surface are merged.</param>
/// <param name="SmallEdges">Edges shorter than the tolerance.</param>
/// <param name="FailedChecks">Checks that failed on broken geometry; the other checks of the part still ran.</param>
/// <param name="SplineEdges">Edges with B-spline or Bezier curves (not a defect in itself).</param>
/// <param name="CanonicalEdges">Spline edges that are really lines, circles or ellipses within the tolerance.</param>
public sealed record PartCheck(
    string Name,
    int SolidCount,
    int FaceCount,
    int FreeEdges,
    int InvalidShapes,
    int SplineFaces,
    int CanonicalFaces,
    int MergeableFaces,
    int SmallEdges,
    int FailedChecks = 0,
    int SplineEdges = 0,
    int CanonicalEdges = 0)
{
    /// <summary>Gets a value indicating whether anything in this part would benefit from repair.</summary>
    public bool HasProblems =>
        FreeEdges > 0 || InvalidShapes > 0 || CanonicalFaces > 0 || CanonicalEdges > 0 || MergeableFaces > 0 || SmallEdges > 0
        || FailedChecks > 0;

    /// <summary>Gets the name, or a placeholder for unnamed parts.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(unnamed part)" : Name;
}
