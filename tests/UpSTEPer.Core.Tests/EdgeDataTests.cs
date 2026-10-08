namespace UpSTEPer.Core.Tests;

public class EdgeDataTests
{
    [Fact]
    public void Constructor_ComputesCounts()
    {
        var edges = new EdgeData(new float[9], [0, 1, 1, 2], [4, 4]);

        Assert.Equal(3, edges.VertexCount);
        Assert.Equal(2, edges.SegmentCount);
    }

    [Fact]
    public void Constructor_RejectsPositionsNotDivisibleByThree()
    {
        Assert.Throws<ArgumentException>(() => new EdgeData(new float[4], [0, 1], [0]));
    }

    [Fact]
    public void Constructor_RejectsIndicesNotMatchingSegments()
    {
        Assert.Throws<ArgumentException>(() => new EdgeData(new float[6], [0, 1, 1], [0]));
    }

    [Fact]
    public void Empty_HasNoLines()
    {
        Assert.Equal(0, EdgeData.Empty.VertexCount);
        Assert.Equal(0, EdgeData.Empty.SegmentCount);
    }
}
