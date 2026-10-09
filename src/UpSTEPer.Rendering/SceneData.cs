using UpSTEPer.Core;

namespace UpSTEPer.Rendering;

/// <summary>
/// Everything the renderer needs for one model: mesh, colours, bounds, and the picking hierarchy.
/// </summary>
/// <remarks>
/// Creating an instance for a new mesh builds a <see cref="TriangleBvh"/> (tens of milliseconds for
/// 10⁵ triangles), so do it off the UI thread; <see cref="WithFaceColors"/> reuses it.
/// </remarks>
public sealed class SceneData
{
    private readonly ColorRgb?[] _faceColors;
    private readonly ReadOnlyMemory<float> _vertexColors;

    // Identifies the model across re-meshing and colour edits (see IsSameModel).
    private readonly object _model;

    // Per-mesh acceleration structures, shared by scenes with the same mesh.
    private readonly TriangleBvh _bvh;

    private SceneData(MeshData mesh, ColorRgb?[] faceColors, BoundingBox bounds, EdgeData edges, object model, TriangleBvh bvh, FaceVertexRanges ranges)
    {
        _model = model;
        _bvh = bvh;
        FaceVertexRanges = ranges;
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

    /// <summary>
    /// Gets the tint mixed into the colour of the face under the pointer: blue reads well on grey
    /// and coloured faces alike and cannot be confused with the orange selection.
    /// </summary>
    public static ColorRgb HoverTint { get; } = ColorRgb.FromHex("#4DA3FF");

    /// <summary>Gets the share of <see cref="HoverTint"/> in the colour of the face under the pointer (0..1).</summary>
    public static float HoverTintAmount => 0.5f;

    /// <summary>Gets the vertex range of every face, for updating the colours of single faces.</summary>
    public FaceVertexRanges FaceVertexRanges { get; }

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
        return new SceneData(
            mesh,
            ReadFaceColors(mesh, faceColor),
            BoundingBox.FromPositions(mesh.Positions.Span),
            edges ?? EdgeData.Empty,
            new object(),
            TriangleBvh.Build(mesh),
            FaceVertexRanges.Build(mesh));
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
        return new SceneData(Mesh, ReadFaceColors(Mesh, faceColor), Bounds, Edges, _model, _bvh, FaceVertexRanges);
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
        return new SceneData(
            mesh,
            ReadFaceColors(mesh, GetFaceColor),
            BoundingBox.FromPositions(mesh.Positions.Span),
            edges,
            _model,
            TriangleBvh.Build(mesh),
            FaceVertexRanges.Build(mesh));
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

    /// <summary>Returns the face under <paramref name="ray"/> (closest hit), or <see langword="null"/>.</summary>
    /// <param name="ray">The pick ray in model space.</param>
    /// <returns>The hit, or <see langword="null"/>.</returns>
    public PickResult? Pick(Ray ray) => _bvh.Pick(ray);

    /// <summary>
    /// Gets the colour a face is drawn in: <see cref="HighlightColor"/> when highlighted, otherwise
    /// its model colour (or <see cref="VertexColors.Default"/>), tinted with <see cref="HoverTint"/>
    /// when it is under the pointer.
    /// </summary>
    /// <param name="face">The face.</param>
    /// <param name="highlighted">The highlighted faces (selection).</param>
    /// <param name="hovered">The face under the pointer, if any.</param>
    /// <returns>The display colour.</returns>
    public ColorRgb GetDisplayColor(FaceId face, IReadOnlySet<FaceId> highlighted, FaceId? hovered)
    {
        ArgumentNullException.ThrowIfNull(highlighted);
        if (highlighted.Contains(face))
        {
            return HighlightColor;
        }

        var color = GetFaceColor(face) ?? VertexColors.Default;
        return face == hovered ? Mix(color, HoverTint, HoverTintAmount) : color;
    }

    /// <summary>
    /// Gets vertex colours with the <paramref name="highlighted"/> faces drawn in <see cref="HighlightColor"/>
    /// and the <paramref name="hovered"/> face tinted (see <see cref="GetDisplayColor"/>).
    /// </summary>
    /// <param name="highlighted">The faces to highlight; empty for none.</param>
    /// <param name="hovered">The face under the pointer, or <see langword="null"/>.</param>
    /// <returns>RGB per vertex.</returns>
    public ReadOnlyMemory<float> GetVertexColors(IReadOnlySet<FaceId> highlighted, FaceId? hovered = null)
    {
        ArgumentNullException.ThrowIfNull(highlighted);
        return highlighted.Count == 0 && hovered is null
            ? _vertexColors
            : VertexColors.Build(Mesh, f => GetDisplayColor(f, highlighted, hovered));
    }

    private static ColorRgb Mix(ColorRgb color, ColorRgb tint, float amount) =>
        new(color.R + ((tint.R - color.R) * amount), color.G + ((tint.G - color.G) * amount), color.B + ((tint.B - color.B) * amount));

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
