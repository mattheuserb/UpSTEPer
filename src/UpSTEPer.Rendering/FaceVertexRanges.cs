using UpSTEPer.Core;

namespace UpSTEPer.Rendering;

/// <summary>
/// The contiguous range of mesh vertices that belongs to each face, so the colour of a single face
/// can be updated on the GPU without re-uploading every vertex colour (hover highlight).
/// </summary>
/// <remarks>
/// The native tessellator appends faces one after another and never shares vertices between faces,
/// so every face normally owns one contiguous range. Faces whose vertices are interleaved with
/// another face's report no range; callers then update all colours.
/// </remarks>
public sealed class FaceVertexRanges
{
    private readonly int[] _first;
    private readonly int[] _count;

    private FaceVertexRanges(int[] first, int[] count)
    {
        _first = first;
        _count = count;
    }

    /// <summary>Computes the ranges of <paramref name="mesh"/>.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The ranges.</returns>
    public static FaceVertexRanges Build(MeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var indices = mesh.Indices.Span;
        var faces = mesh.TriangleFaceIds.Span;
        var faceCount = 0;
        foreach (var face in faces)
        {
            faceCount = Math.Max(faceCount, face + 1);
        }

        // Owner face of every vertex (-1: unused), and the vertex span each face touches.
        var owner = new int[mesh.VertexCount];
        Array.Fill(owner, -1);
        var min = new int[faceCount];
        var max = new int[faceCount];
        Array.Fill(min, int.MaxValue);
        Array.Fill(max, -1);
        var shared = new bool[faceCount];
        for (var triangle = 0; triangle < faces.Length; triangle++)
        {
            var face = faces[triangle];
            for (var corner = 0; corner < 3; corner++)
            {
                var vertex = (int)indices[(triangle * 3) + corner];
                if (owner[vertex] >= 0 && owner[vertex] != face)
                {
                    shared[face] = shared[owner[vertex]] = true;
                }

                owner[vertex] = face;
                min[face] = Math.Min(min[face], vertex);
                max[face] = Math.Max(max[face], vertex);
            }
        }

        var first = new int[faceCount];
        var count = new int[faceCount];
        for (var face = 0; face < faceCount; face++)
        {
            first[face] = -1;
            if (max[face] < 0 || shared[face])
            {
                continue;
            }

            var contiguous = true;
            for (var vertex = min[face]; vertex <= max[face] && contiguous; vertex++)
            {
                contiguous = owner[vertex] == face || owner[vertex] < 0;
            }

            if (contiguous)
            {
                first[face] = min[face];
                count[face] = max[face] - min[face] + 1;
            }
        }

        return new FaceVertexRanges(first, count);
    }

    /// <summary>Gets the vertex range of <paramref name="face"/>.</summary>
    /// <param name="face">The face.</param>
    /// <param name="firstVertex">The first vertex of the face.</param>
    /// <param name="vertexCount">The number of vertices.</param>
    /// <returns><see langword="false"/> when the face has no triangles or no contiguous range.</returns>
    public bool TryGetRange(FaceId face, out int firstVertex, out int vertexCount)
    {
        if (face.Value >= 0 && face.Value < _first.Length && _first[face.Value] >= 0)
        {
            firstVertex = _first[face.Value];
            vertexCount = _count[face.Value];
            return true;
        }

        firstVertex = 0;
        vertexCount = 0;
        return false;
    }
}
