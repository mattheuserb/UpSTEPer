namespace UpSTEPer.Core;

/// <summary>
/// Display lines for the model's B-Rep edges: vertices plus index pairs (one pair per segment).
/// Seam and degenerated edges are not included.
/// </summary>
public sealed class EdgeData
{
    /// <summary>Initializes a new instance of the <see cref="EdgeData"/> class.</summary>
    /// <param name="positions">Vertex positions, 3 floats (x, y, z) per vertex.</param>
    /// <param name="indices">Vertex index pairs, 2 per segment.</param>
    /// <param name="segmentEdgeIds">The 0-based edge id of each segment.</param>
    /// <exception cref="ArgumentException">The array lengths are inconsistent.</exception>
    public EdgeData(float[] positions, uint[] indices, int[] segmentEdgeIds)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(segmentEdgeIds);

        if (positions.Length % 3 != 0)
        {
            throw new ArgumentException("Positions must contain three floats per vertex.", nameof(positions));
        }

        if (indices.Length != segmentEdgeIds.Length * 2)
        {
            throw new ArgumentException("Expected exactly two indices per segment edge id.", nameof(indices));
        }

        Positions = positions;
        Indices = indices;
        SegmentEdgeIds = segmentEdgeIds;
    }

    /// <summary>Gets an instance without lines.</summary>
    public static EdgeData Empty { get; } = new([], [], []);

    /// <summary>Gets the vertex positions (x, y, z per vertex).</summary>
    public ReadOnlyMemory<float> Positions { get; }

    /// <summary>Gets the vertex index pairs (for GL_LINES).</summary>
    public ReadOnlyMemory<uint> Indices { get; }

    /// <summary>Gets the 0-based edge id of each segment.</summary>
    public ReadOnlyMemory<int> SegmentEdgeIds { get; }

    /// <summary>Gets the number of vertices.</summary>
    public int VertexCount => Positions.Length / 3;

    /// <summary>Gets the number of line segments.</summary>
    public int SegmentCount => SegmentEdgeIds.Length;
}
