namespace UpSTEPer.Core.Tests;

public class MeshDataTests
{
    [Fact]
    public void Constructor_ComputesCounts()
    {
        var mesh = new MeshData(new float[9], new float[9], [0, 1, 2], [7]);

        Assert.Equal(3, mesh.VertexCount);
        Assert.Equal(1, mesh.TriangleCount);
    }

    [Fact]
    public void Constructor_RejectsMismatchedNormals()
    {
        Assert.Throws<ArgumentException>(() => new MeshData(new float[9], new float[6], [0, 1, 2], [0]));
    }

    [Fact]
    public void Constructor_RejectsIndicesNotMatchingTriangleCount()
    {
        Assert.Throws<ArgumentException>(() => new MeshData(new float[9], new float[9], [0, 1], [0]));
    }

    [Fact]
    public void Empty_HasNoGeometry()
    {
        Assert.Equal(0, MeshData.Empty.VertexCount);
        Assert.Equal(0, MeshData.Empty.TriangleCount);
    }
}
