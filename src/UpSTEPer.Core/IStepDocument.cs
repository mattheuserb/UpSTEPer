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

    /// <summary>Gets the colour assigned to a face in the file, falling back to its solid's colour.</summary>
    /// <param name="faceId">The face.</param>
    /// <returns>The colour, or <see langword="null"/> when the file assigns none.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="faceId"/> does not exist.</exception>
    ColorRgb? GetFaceColor(FaceId faceId);

    /// <summary>Triangulates the model for display.</summary>
    /// <param name="options">Tessellation quality.</param>
    /// <returns>The mesh.</returns>
    /// <exception cref="GeometryOperationException">Tessellation failed.</exception>
    MeshData Tessellate(TessellationOptions options);
}
