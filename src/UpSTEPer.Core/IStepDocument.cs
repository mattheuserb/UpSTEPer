namespace UpSTEPer.Core;

/// <summary>
/// A loaded STEP model. Implementations wrap native geometry and must be disposed.
/// </summary>
/// <remarks>
/// Instances are not thread-safe. Callers serialise access, typically by running all
/// geometry work on one dedicated worker (see docs/architecture.md, "Threading").
/// </remarks>
public interface IStepDocument : IDisposable
{
    /// <summary>Gets the path the document was loaded from.</summary>
    string SourcePath { get; }

    /// <summary>Gets topology counts of the model.</summary>
    ModelStatistics Statistics { get; }

    /// <summary>Gets the bounding box of the model, or <see langword="null"/> when it has no geometry.</summary>
    ModelBounds? Bounds { get; }

    /// <summary>Gets the colour assigned to a face in the file, falling back to its solid's colour.</summary>
    /// <param name="faceId">The face.</param>
    /// <returns>The colour, or <see langword="null"/> when the file assigns none.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="faceId"/> does not exist.</exception>
    ColorRgb? GetFaceColor(FaceId faceId);

    /// <summary>Checks every part definition for typical defects (read-only).</summary>
    /// <param name="tolerance">Tolerance (mm) for recognising canonical geometry and small edges.</param>
    /// <returns>One entry per part definition, in document order.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> is not positive.</exception>
    IReadOnlyList<PartCheck> Check(double tolerance = 0.01);

    /// <summary>
    /// Heals every part definition (sewing gaps, merging split faces, general fixes) and keeps
    /// colours: faces merged from differently coloured ones take the colour covering the largest area.
    /// </summary>
    /// <remarks>
    /// Changes topology: afterwards face and body ids are renumbered, so re-read
    /// <see cref="Statistics"/>, the model tree, colours and the mesh. Either all parts are repaired
    /// or (on cancellation) none.
    /// </remarks>
    /// <param name="options">Steps and tolerance.</param>
    /// <param name="progress">Receives progress reports, possibly from another thread; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the repair; the model is unchanged then.</param>
    /// <returns>One entry per part definition.</returns>
    /// <exception cref="OperationCanceledException">The repair was cancelled.</exception>
    IReadOnlyList<PartRepair> Repair(RepairOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Gets the product structure: assemblies, parts and their bodies, with names.</summary>
    /// <returns>The top-level nodes.</returns>
    IReadOnlyList<ModelNode> GetModelTree();

    /// <summary>Gets the body (solid) a face belongs to.</summary>
    /// <param name="faceId">The face.</param>
    /// <returns>The body, or <see langword="null"/> for faces outside any solid.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="faceId"/> does not exist.</exception>
    BodyId? GetBodyOfFace(FaceId faceId);

    /// <summary>
    /// Gets the colour assigned directly to a face or body, ignoring inheritance. Unlike
    /// <see cref="GetFaceColor"/>, a face without its own colour returns <see langword="null"/>
    /// even when its body is coloured. Needed to undo colour changes exactly.
    /// </summary>
    /// <param name="target">The face or body.</param>
    /// <returns>The assigned colour, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The target does not exist.</exception>
    ColorRgb? GetAssignedColor(ColorTarget target);

    /// <summary>Assigns a colour to a face or body, or removes it with <see langword="null"/>.</summary>
    /// <remarks>Face colours take precedence over body colours. Meshes stay valid (topology is unchanged).</remarks>
    /// <param name="target">The face or body.</param>
    /// <param name="color">The colour, or <see langword="null"/> to remove the assigned colour.</param>
    /// <exception cref="ArgumentOutOfRangeException">The target does not exist.</exception>
    /// <exception cref="GeometryOperationException">The target cannot be coloured (e.g. inside an assembly instance).</exception>
    void SetColor(ColorTarget target, ColorRgb? color);

    /// <summary>Writes the model as STEP including colours, names and layers.</summary>
    /// <param name="path">The output path; an existing file is overwritten.</param>
    /// <param name="schema">The application protocol; AP214 by default.</param>
    /// <exception cref="IOException">The file cannot be written.</exception>
    void SaveStep(string path, StepSchema schema = StepSchema.Ap214);

    /// <summary>Projects the model into a 2D view with exact hidden-line removal.</summary>
    /// <remarks>Lines, circles, arcs and ellipses stay analytic; other curves become polylines.</remarks>
    /// <param name="direction">The viewing direction.</param>
    /// <param name="options">What to include and the polyline tolerance.</param>
    /// <returns>The view in its own coordinates (mm).</returns>
    /// <exception cref="GeometryOperationException">Hidden-line removal failed.</exception>
    Drawing.ProjectedView Project(Drawing.ViewDirection direction, Drawing.ProjectionOptions options);

    /// <summary>Discretises the model's edges into display lines (seams excluded).</summary>
    /// <param name="options">Uses <see cref="TessellationOptions.LinearDeflection"/> as chord tolerance.</param>
    /// <returns>The lines.</returns>
    /// <exception cref="GeometryOperationException">Discretisation failed.</exception>
    EdgeData TessellateEdges(TessellationOptions options);

    /// <summary>Triangulates the model for display, replacing any previous triangulation.</summary>
    /// <param name="options">Tessellation quality.</param>
    /// <param name="progress">Receives progress reports, possibly from other threads; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels meshing; the document keeps no partial triangulation then.</param>
    /// <returns>The mesh.</returns>
    /// <exception cref="GeometryOperationException">Tessellation failed.</exception>
    /// <exception cref="OperationCanceledException">Meshing was cancelled.</exception>
    MeshData Tessellate(TessellationOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
}
