using UpSTEPer.Core;

namespace UpSTEPer.Rendering.Tests;

public class FaceVertexRangesTests
{
    [Fact]
    public void Build_ConsecutiveFaces_ReportsTheirRanges()
    {
        // Face 0: vertices 0..5 (two triangles), face 1: vertices 6..8.
        var mesh = new MeshData(new float[27], new float[27], [0, 1, 2, 3, 4, 5, 6, 7, 8], [0, 0, 1]);

        var ranges = FaceVertexRanges.Build(mesh);

        Assert.True(ranges.TryGetRange(new FaceId(0), out var first0, out var count0));
        Assert.Equal((0, 6), (first0, count0));
        Assert.True(ranges.TryGetRange(new FaceId(1), out var first1, out var count1));
        Assert.Equal((6, 3), (first1, count1));
    }

    [Fact]
    public void Build_InterleavedFaces_ReportsNoRange()
    {
        // Face 0 uses vertices 0..2 and 6..8, face 1 sits in between.
        var mesh = new MeshData(new float[27], new float[27], [0, 1, 2, 3, 4, 5, 6, 7, 8], [0, 1, 0]);

        var ranges = FaceVertexRanges.Build(mesh);

        Assert.False(ranges.TryGetRange(new FaceId(0), out _, out _));
        Assert.True(ranges.TryGetRange(new FaceId(1), out var first, out var count));
        Assert.Equal((3, 3), (first, count));
    }

    [Fact]
    public void Build_SharedVertex_ReportsNoRangeForBothFaces()
    {
        // Vertex 2 belongs to both faces: colouring one would recolour the other.
        var mesh = new MeshData(new float[15], new float[15], [0, 1, 2, 2, 3, 4], [0, 1]);

        var ranges = FaceVertexRanges.Build(mesh);

        Assert.False(ranges.TryGetRange(new FaceId(0), out _, out _));
        Assert.False(ranges.TryGetRange(new FaceId(1), out _, out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void TryGetRange_UnknownFace_ReturnsFalse(int face)
    {
        var mesh = new MeshData(new float[18], new float[18], [0, 1, 2, 3, 4, 5], [0, 1]);

        Assert.False(FaceVertexRanges.Build(mesh).TryGetRange(new FaceId(face), out _, out _));
    }
}
