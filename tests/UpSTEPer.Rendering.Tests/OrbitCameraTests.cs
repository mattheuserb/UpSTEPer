using System.Numerics;

namespace UpSTEPer.Rendering.Tests;

public class OrbitCameraTests
{
    private static readonly BoundingBox _box = new(new Vector3(-10, -20, 0), new Vector3(30, 20, 50));

    [Fact]
    public void FitTo_TargetsCenterAndShowsWholeBox()
    {
        var camera = new OrbitCamera();

        camera.FitTo(_box);

        Assert.Equal(_box.Center, camera.Target);
        var viewProjection = camera.GetViewProjection(4f / 3f);
        foreach (var corner in Corners(_box))
        {
            var clip = Vector4.Transform(new Vector4(corner, 1f), viewProjection);
            var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
            Assert.InRange(ndc.X, -1f, 1f);
            Assert.InRange(ndc.Y, -1f, 1f);
            Assert.InRange(ndc.Z, -1f, 1f);
        }
    }

    [Fact]
    public void GetViewProjection_ProjectsTargetToViewportCenter()
    {
        var camera = new OrbitCamera();
        camera.FitTo(_box);
        camera.Orbit(37, -12);

        var clip = Vector4.Transform(new Vector4(camera.Target, 1f), camera.GetViewProjection(1f));

        Assert.Equal(0f, clip.X / clip.W, 4);
        Assert.Equal(0f, clip.Y / clip.W, 4);
    }

    [Fact]
    public void Position_IsDistanceAwayAgainstViewDirection()
    {
        var camera = new OrbitCamera();
        camera.FitTo(_box);

        Assert.Equal(camera.Distance, Vector3.Distance(camera.Position, camera.Target), 3);
        Assert.Equal(1f, camera.Forward.Length(), 5);
        Assert.True(Vector3.Dot(camera.Target - camera.Position, camera.Forward) > 0);
    }

    [Fact]
    public void Orbit_ClampsPitchBelowPoles()
    {
        var camera = new OrbitCamera();

        camera.Orbit(0, 100_000);
        Assert.True(camera.Pitch < MathF.PI / 2f);

        camera.Orbit(0, -200_000);
        Assert.True(camera.Pitch > -MathF.PI / 2f);
    }

    [Fact]
    public void Zoom_InMovesCloserAndIsClamped()
    {
        var camera = new OrbitCamera();
        camera.FitTo(_box);
        var start = camera.Distance;

        camera.Zoom(1);
        Assert.True(camera.Distance < start);

        camera.Zoom(1000);
        Assert.Equal(_box.Radius * 0.05f, camera.Distance, 3);

        camera.Zoom(-1000);
        Assert.Equal(_box.Radius * 100f, camera.Distance, 1);
    }

    [Fact]
    public void Pan_MovesTargetPerpendicularToViewDirection()
    {
        var camera = new OrbitCamera();
        camera.FitTo(_box);
        var before = camera.Target;

        camera.Pan(50, -20, 800);

        var moved = camera.Target - before;
        Assert.True(moved.Length() > 0);
        Assert.Equal(0f, Vector3.Dot(moved, camera.Forward), 3);
    }

    [Fact]
    public void ResetOrientation_RestoresDefaultView()
    {
        var camera = new OrbitCamera();
        var (yaw, pitch) = (camera.Yaw, camera.Pitch);

        camera.Orbit(123, 45);
        camera.ResetOrientation();

        Assert.Equal(yaw, camera.Yaw);
        Assert.Equal(pitch, camera.Pitch);
    }

    [Fact]
    public void CreateRay_ThroughViewportCenter_PointsAtTarget()
    {
        var camera = new OrbitCamera();
        camera.FitTo(_box);
        camera.Orbit(25, 10);

        var ray = camera.CreateRay(400, 300, 800, 600);

        Assert.Equal(1f, ray.Direction.Length(), 4);
        Assert.Equal(1f, Vector3.Dot(ray.Direction, camera.Forward), 3);
        var closest = ray.PointAt(Vector3.Dot(camera.Target - ray.Origin, ray.Direction));
        Assert.True(Vector3.Distance(closest, camera.Target) < 1e-2f * camera.Distance);
    }

    [Fact]
    public void CreateRay_RightOfCenter_PointsRight()
    {
        var camera = new OrbitCamera();
        camera.FitTo(_box);
        var right = Vector3.Normalize(Vector3.Cross(camera.Forward, Vector3.UnitZ));

        var ray = camera.CreateRay(700, 300, 800, 600);

        Assert.True(Vector3.Dot(ray.Direction, right) > 0);
    }

    private static IEnumerable<Vector3> Corners(BoundingBox box)
    {
        foreach (var x in new[] { box.Min.X, box.Max.X })
        {
            foreach (var y in new[] { box.Min.Y, box.Max.Y })
            {
                foreach (var z in new[] { box.Min.Z, box.Max.Z })
                {
                    yield return new Vector3(x, y, z);
                }
            }
        }
    }
}
