using UpSTEPer.Core;

namespace UpSTEPer.Rendering;

/// <summary>Everything the renderer needs for one model: mesh, per-vertex colours and bounds.</summary>
public sealed class SceneData
{
    private SceneData(MeshData mesh, float[] vertexColors, BoundingBox bounds)
    {
        Mesh = mesh;
        VertexColors = vertexColors;
        Bounds = bounds;
    }

    /// <summary>Gets the display mesh.</summary>
    public MeshData Mesh { get; }

    /// <summary>Gets the RGB colour per vertex (three floats per vertex).</summary>
    public ReadOnlyMemory<float> VertexColors { get; }

    /// <summary>Gets the model bounds.</summary>
    public BoundingBox Bounds { get; }

    /// <summary>Creates scene data from a mesh and a per-face colour lookup.</summary>
    /// <param name="mesh">The display mesh.</param>
    /// <param name="faceColor">Colour per face; <see langword="null"/> uses <see cref="Rendering.VertexColors.Default"/>.</param>
    /// <returns>The scene data.</returns>
    public static SceneData Create(MeshData mesh, Func<FaceId, ColorRgb?> faceColor)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return new SceneData(mesh, Rendering.VertexColors.Build(mesh, faceColor), BoundingBox.FromPositions(mesh.Positions.Span));
    }
}
