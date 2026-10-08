using System.Numerics;
using UpSTEPer.Core;

namespace UpSTEPer.Rendering.Tests;

public class MeshPickerTests
{
    // Two parallel unit triangles in the XY plane: face 3 at z = 0, face 8 at z = 5.
    private static readonly MeshData _stack = new(
        positions: [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 5, 1, 0, 5, 0, 1, 5],
        normals: new float[18],
        indices: [0, 1, 2, 3, 4, 5],
        triangleFaceIds: [3, 8]);

    [Fact]
    public void Pick_ReturnsClosestFaceAlongRay()
    {
        var fromAbove = new Ray(new Vector3(0.25f, 0.25f, 10f), -Vector3.UnitZ);

        var hit = MeshPicker.Pick(_stack, fromAbove);

        Assert.NotNull(hit);
        Assert.Equal(new FaceId(8), hit.Face);
        Assert.Equal(5f, hit.Distance, 4);
        Assert.Equal(new Vector3(0.25f, 0.25f, 5f), hit.Point);
    }

    [Fact]
    public void Pick_HitsBackFacesToo()
    {
        var fromBelow = new Ray(new Vector3(0.25f, 0.25f, -10f), Vector3.UnitZ);

        Assert.Equal(new FaceId(3), MeshPicker.Pick(_stack, fromBelow)?.Face);
    }

    [Theory]
    [InlineData(2f, 2f, 10f)] // outside the triangles
    [InlineData(0.25f, 0.25f, -10f, true)] // pointing away
    public void Pick_Miss_ReturnsNull(float x, float y, float z, bool pointAway = false)
    {
        var direction = pointAway ? -Vector3.UnitZ : -Vector3.UnitZ * MathF.Sign(z);

        Assert.Null(MeshPicker.Pick(_stack, new Ray(new Vector3(x, y, z), direction)));
    }

    [Fact]
    public void Pick_IgnoresRayParallelToTriangle()
    {
        var alongPlane = new Ray(new Vector3(-1f, 0.25f, 0f), Vector3.UnitX);

        Assert.Null(MeshPicker.Pick(_stack, alongPlane));
    }

    [Fact]
    public void Pick_ThroughCameraCenter_HitsModelAtTarget()
    {
        // One large triangle whose interior contains the bounding-box centre (0, 0, 0).
        var plate = new MeshData([-10, -10, 0, 10, -10, 0, 0, 10, 0], new float[9], [0, 1, 2], [5]);
        var camera = new OrbitCamera();
        camera.FitTo(BoundingBox.FromPositions(plate.Positions.Span));

        var hit = MeshPicker.Pick(plate, camera.CreateRay(400, 300, 800, 600));

        Assert.NotNull(hit);
        Assert.Equal(new FaceId(5), hit.Face);
        Assert.True(hit.Point.Length() < 0.05f, $"hit at {hit.Point}");
    }
}
