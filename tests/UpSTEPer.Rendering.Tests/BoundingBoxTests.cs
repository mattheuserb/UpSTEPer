using System.Numerics;

namespace UpSTEPer.Rendering.Tests;

public class BoundingBoxTests
{
    [Fact]
    public void FromPositions_ComputesMinMaxCenterAndRadius()
    {
        float[] positions = [-1, 0, 2, 3, 4, -2, 1, 2, 0];

        var box = BoundingBox.FromPositions(positions);

        Assert.Equal(new Vector3(-1, 0, -2), box.Min);
        Assert.Equal(new Vector3(3, 4, 2), box.Max);
        Assert.Equal(new Vector3(1, 2, 0), box.Center);
        Assert.Equal(new Vector3(4, 4, 4).Length() / 2f, box.Radius, 5);
    }

    [Fact]
    public void FromPositions_Empty_ReturnsUnitBox()
    {
        Assert.Equal(BoundingBox.Unit, BoundingBox.FromPositions([]));
    }
}
