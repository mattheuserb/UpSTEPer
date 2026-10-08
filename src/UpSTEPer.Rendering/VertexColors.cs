using UpSTEPer.Core;

namespace UpSTEPer.Rendering;

/// <summary>Expands per-face colours to per-vertex colours for the GPU.</summary>
public static class VertexColors
{
    /// <summary>Gets the colour of faces without an assigned colour (neutral grey).</summary>
    public static ColorRgb Default { get; } = ColorRgb.FromHex("#B4B9BE");

    /// <summary>
    /// Builds an RGB float array (three floats per vertex). Mesh vertices are never shared between
    /// faces, so every vertex takes the colour of the face of the triangles that use it.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="faceColor">Colour lookup per face; <see langword="null"/> means <see cref="Default"/>.</param>
    /// <returns>The vertex colours.</returns>
    public static float[] Build(MeshData mesh, Func<FaceId, ColorRgb?> faceColor)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(faceColor);

        var colors = new float[mesh.VertexCount * 3];
        var indices = mesh.Indices.Span;
        var faces = mesh.TriangleFaceIds.Span;
        var cache = new Dictionary<int, ColorRgb>();

        for (var triangle = 0; triangle < faces.Length; triangle++)
        {
            var faceId = faces[triangle];
            if (!cache.TryGetValue(faceId, out var color))
            {
                color = faceColor(new FaceId(faceId)) ?? Default;
                cache[faceId] = color;
            }

            for (var corner = 0; corner < 3; corner++)
            {
                var offset = (int)indices[(triangle * 3) + corner] * 3;
                colors[offset] = color.R;
                colors[offset + 1] = color.G;
                colors[offset + 2] = color.B;
            }
        }

        return colors;
    }
}
