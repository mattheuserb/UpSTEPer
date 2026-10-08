namespace UpSTEPer.Core;

/// <summary>
/// A triangle mesh for display. Vertices are not shared between faces, so every
/// triangle maps to exactly one <see cref="FaceId"/> via <see cref="TriangleFaceIds"/>.
/// </summary>
public sealed class MeshData
{
    /// <summary>Initializes a new instance of the <see cref="MeshData"/> class.</summary>
    /// <param name="positions">Vertex positions, 3 floats (x, y, z) per vertex.</param>
    /// <param name="normals">Unit vertex normals, 3 floats per vertex.</param>
    /// <param name="indices">Vertex indices, 3 per triangle, counter-clockwise seen from outside.</param>
    /// <param name="triangleFaceIds">Face index per triangle.</param>
    /// <exception cref="ArgumentException">The array lengths are inconsistent.</exception>
    public MeshData(float[] positions, float[] normals, uint[] indices, int[] triangleFaceIds)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(normals);
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(triangleFaceIds);

        if (positions.Length % 3 != 0 || normals.Length != positions.Length)
        {
            throw new ArgumentException("Positions and normals must have equal length divisible by 3.", nameof(normals));
        }

        if (indices.Length != triangleFaceIds.Length * 3)
        {
            throw new ArgumentException("Expected exactly three indices per triangle face id.", nameof(indices));
        }

        Positions = positions;
        Normals = normals;
        Indices = indices;
        TriangleFaceIds = triangleFaceIds;
    }

    /// <summary>Gets an empty mesh.</summary>
    public static MeshData Empty { get; } = new([], [], [], []);

    /// <summary>Gets the vertex positions (x, y, z per vertex).</summary>
    public ReadOnlyMemory<float> Positions { get; }

    /// <summary>Gets the unit vertex normals (x, y, z per vertex).</summary>
    public ReadOnlyMemory<float> Normals { get; }

    /// <summary>Gets the triangle vertex indices (3 per triangle).</summary>
    public ReadOnlyMemory<uint> Indices { get; }

    /// <summary>Gets the 0-based face index of each triangle.</summary>
    public ReadOnlyMemory<int> TriangleFaceIds { get; }

    /// <summary>Gets the number of vertices.</summary>
    public int VertexCount => Positions.Length / 3;

    /// <summary>Gets the number of triangles.</summary>
    public int TriangleCount => TriangleFaceIds.Length;
}
