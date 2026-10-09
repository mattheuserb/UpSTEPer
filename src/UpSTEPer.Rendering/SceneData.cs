using UpSTEPer.Core;

namespace UpSTEPer.Rendering;

/// <summary>Everything the renderer needs for one model: mesh, colours and bounds.</summary>
public sealed class SceneData
{
    private readonly ColorRgb?[] _faceColors;
    private readonly ReadOnlyMemory<float> _vertexColors;

    // Identifies the model across re-meshing and colour edits (see IsSameModel).
    private readonly object _model;

    private SceneData(MeshData mesh, ColorRgb?[] faceColors, BoundingBox bounds, EdgeData edges, object model)
    {
        _model = model;
        Mesh = mesh;
        _faceColors = faceColors;
        _vertexColors = VertexColors.Build(mesh, GetFaceColor);
        Bounds = bounds;
        Edges = edges;
    }

    /// <summary>Gets the edge lines drawn on top of the mesh (may be empty).</summary>
    public EdgeData Edges { get; }

    /// <summary>Gets the colour used to highlight the selected face.</summary>
    public static ColorRgb HighlightColor { get; } = ColorRgb.FromHex("#FF8C1A");

    /// <summary>Gets the display mesh.</summary>
    public MeshData Mesh { get; }

    /// <summary>Gets the model bounds.</summary>
    public BoundingBox Bounds { get; }

    /// <summary>Creates scene data from a mesh and a per-face colour lookup.</summary>
    /// <param name="mesh">The display mesh.</param>
    /// <param name="faceColor">Colour per face; <see langword="null"/> uses <see cref="VertexColors.Default"/>.</param>
    /// <param name="edges">Edge lines to draw on top, or <see langword="null"/> for none.</param>
    /// <returns>The scene data.</returns>
    public static SceneData Create(MeshData mesh, Func<FaceId, ColorRgb?> faceColor, EdgeData? edges = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(faceColor);
        return new SceneData(mesh, ReadFaceColors(mesh, faceColor), BoundingBox.FromPositions(mesh.Positions.Span), edges ?? EdgeData.Empty, new object());
    }

    /// <summary>
    /// Creates scene data for the same mesh with different face colours (e.g. after an edit). The
    /// mesh instance is shared, so views can keep their camera and only update colours.
    /// </summary>
    /// <param name="faceColor">Colour per face; <see langword="null"/> uses <see cref="VertexColors.Default"/>.</param>
    /// <returns>The new scene data.</returns>
    public SceneData WithFaceColors(Func<FaceId, ColorRgb?> faceColor)
    {
        ArgumentNullException.ThrowIfNull(faceColor);
        return new SceneData(Mesh, ReadFaceColors(Mesh, faceColor), Bounds, Edges, _model);
    }

    /// <summary>
    /// Creates scene data for the same model with a new mesh and edges (e.g. another display
    /// quality). Face ids must be unchanged; the face colours are kept.
    /// </summary>
    /// <param name="mesh">The new display mesh.</param>
    /// <param name="edges">The new edge lines.</param>
    /// <returns>The new scene data; <see cref="IsSameModel"/> is <see langword="true"/> for this instance.</returns>
    public SceneData WithGeometry(MeshData mesh, EdgeData edges)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(edges);
        return new SceneData(mesh, ReadFaceColors(mesh, GetFaceColor), BoundingBox.FromPositions(mesh.Positions.Span), edges, _model);
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="other"/> shows the same model, derived by
    /// <see cref="WithFaceColors"/> or <see cref="WithGeometry"/>; views keep their camera then.
    /// </summary>
    /// <param name="other">The other scene.</param>
    /// <returns><see langword="true"/> for the same model.</returns>
    public bool IsSameModel(SceneData? other) => other is not null && ReferenceEquals(_model, other._model);

    /// <summary>Gets the colour assigned to a face in the model.</summary>
    /// <param name="face">The face.</param>
    /// <returns>The colour, or <see langword="null"/> when the face has none or is not in the mesh.</returns>
    public ColorRgb? GetFaceColor(FaceId face) =>
        face.Value >= 0 && face.Value < _faceColors.Length ? _faceColors[face.Value] : null;

    /// <summary>Gets vertex colours with the <paramref name="highlighted"/> faces drawn in <see cref="HighlightColor"/>.</summary>
    /// <param name="highlighted">The faces to highlight; empty for none.</param>
    /// <returns>RGB per vertex.</returns>
    public ReadOnlyMemory<float> GetVertexColors(IReadOnlySet<FaceId> highlighted)
    {
        ArgumentNullException.ThrowIfNull(highlighted);
        return highlighted.Count == 0
            ? _vertexColors
            : VertexColors.Build(Mesh, f => highlighted.Contains(f) ? HighlightColor : GetFaceColor(f));
    }

    private static ColorRgb?[] ReadFaceColors(MeshData mesh, Func<FaceId, ColorRgb?> faceColor)
    {
        var faceCount = 0;
        foreach (var id in mesh.TriangleFaceIds.Span)
        {
            faceCount = Math.Max(faceCount, id + 1);
        }

        var colors = new ColorRgb?[faceCount];
        for (var face = 0; face < faceCount; face++)
        {
            colors[face] = faceColor(new FaceId(face));
        }

        return colors;
    }
}
