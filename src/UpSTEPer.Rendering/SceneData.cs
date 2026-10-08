using UpSTEPer.Core;

namespace UpSTEPer.Rendering;

/// <summary>Everything the renderer needs for one model: mesh, colours and bounds.</summary>
public sealed class SceneData
{
    private readonly ColorRgb?[] _faceColors;
    private readonly ReadOnlyMemory<float> _vertexColors;

    private SceneData(MeshData mesh, ColorRgb?[] faceColors)
    {
        Mesh = mesh;
        _faceColors = faceColors;
        _vertexColors = VertexColors.Build(mesh, GetFaceColor);
        Bounds = BoundingBox.FromPositions(mesh.Positions.Span);
    }

    /// <summary>Gets the colour used to highlight the selected face.</summary>
    public static ColorRgb HighlightColor { get; } = ColorRgb.FromHex("#FF8C1A");

    /// <summary>Gets the display mesh.</summary>
    public MeshData Mesh { get; }

    /// <summary>Gets the model bounds.</summary>
    public BoundingBox Bounds { get; }

    /// <summary>Creates scene data from a mesh and a per-face colour lookup.</summary>
    /// <param name="mesh">The display mesh.</param>
    /// <param name="faceColor">Colour per face; <see langword="null"/> uses <see cref="VertexColors.Default"/>.</param>
    /// <returns>The scene data.</returns>
    public static SceneData Create(MeshData mesh, Func<FaceId, ColorRgb?> faceColor)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(faceColor);

        var faceIds = mesh.TriangleFaceIds.Span;
        var faceCount = 0;
        foreach (var id in faceIds)
        {
            faceCount = Math.Max(faceCount, id + 1);
        }

        var colors = new ColorRgb?[faceCount];
        for (var face = 0; face < faceCount; face++)
        {
            colors[face] = faceColor(new FaceId(face));
        }

        return new SceneData(mesh, colors);
    }

    /// <summary>Gets the colour assigned to a face in the model.</summary>
    /// <param name="face">The face.</param>
    /// <returns>The colour, or <see langword="null"/> when the face has none or is not in the mesh.</returns>
    public ColorRgb? GetFaceColor(FaceId face) =>
        face.Value >= 0 && face.Value < _faceColors.Length ? _faceColors[face.Value] : null;

    /// <summary>Gets vertex colours with <paramref name="highlighted"/> drawn in <see cref="HighlightColor"/>.</summary>
    /// <param name="highlighted">The face to highlight, or <see langword="null"/> for none.</param>
    /// <returns>RGB per vertex.</returns>
    public ReadOnlyMemory<float> GetVertexColors(FaceId? highlighted) =>
        highlighted is { } face
            ? VertexColors.Build(Mesh, f => f == face ? HighlightColor : GetFaceColor(f))
            : _vertexColors;
}
