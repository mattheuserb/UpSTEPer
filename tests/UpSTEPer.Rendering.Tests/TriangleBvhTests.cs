using System.Diagnostics;
using System.Numerics;
using UpSTEPer.Core;

namespace UpSTEPer.Rendering.Tests;

public class TriangleBvhTests
{
    private static readonly float[] _unitTriangle = [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f];

    [Fact]
    public void Pick_RandomRays_MatchesBruteForce()
    {
        var mesh = RandomTriangles(new Lcg(42), 3_000, faceCount: 50);
        var bvh = TriangleBvh.Build(mesh);
        var random = new Lcg(7);
        var hits = 0;

        for (var i = 0; i < 2_000; i++)
        {
            var origin = RandomPoint(random, 30f);
            var ray = new Ray(origin, Vector3.Normalize(RandomPoint(random, 5f) - origin));

            var expected = MeshPicker.Pick(mesh, ray);
            var actual = bvh.Pick(ray);

            Assert.Equal(expected, actual);
            hits += expected is null ? 0 : 1;
        }

        Assert.InRange(hits, 200, 1_999); // the rays exercise both hits and misses
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, 1)]
    public void Pick_AxisParallelRaysThroughBoxPlanes_MatchesBruteForce(float x, float y, float z)
    {
        // A grid of unit squares: many node boxes share planes at integer coordinates, and the rays
        // start exactly in those planes (the 0 * infinity case of the slab test).
        var mesh = Grid(16, normalAxis: x != 0 ? 0 : y != 0 ? 1 : 2);
        var bvh = TriangleBvh.Build(mesh);
        var direction = new Vector3(x, y, z);

        for (var u = 0; u <= 16; u++)
        {
            for (var v = 0; v <= 16; v++)
            {
                var origin = (direction * -10f) + Place(direction, u * 0.5f, v * 0.5f);
                var ray = new Ray(origin, direction);
                Assert.Equal(MeshPicker.Pick(mesh, ray), bvh.Pick(ray));
            }
        }
    }

    [Fact]
    public void Pick_EmptyMesh_ReturnsNull()
    {
        var bvh = TriangleBvh.Build(MeshData.Empty);

        Assert.Null(bvh.Pick(new Ray(Vector3.Zero, Vector3.UnitZ)));
        Assert.Equal(0, bvh.NodeCount);
    }

    [Fact]
    public void Build_SmallMesh_IsSingleLeaf()
    {
        var mesh = RandomTriangles(new Lcg(1), TriangleBvh.LeafSize, faceCount: 1);

        Assert.Equal(1, TriangleBvh.Build(mesh).NodeCount);
    }

    [Fact]
    public void Build_IdenticalTriangles_TerminatesAndPicks()
    {
        // All centroids equal: the split has no extent and must stop instead of recursing forever.
        var positions = Enumerable.Repeat(_unitTriangle, 100).SelectMany(p => p).ToArray();
        var mesh = new MeshData(positions, new float[positions.Length], [.. Enumerable.Range(0, 300).Select(i => (uint)i)], new int[100]);

        var hit = TriangleBvh.Build(mesh).Pick(new Ray(new Vector3(0.2f, 0.2f, 5f), -Vector3.UnitZ));

        Assert.Equal(new FaceId(0), hit?.Face);
    }

    [Fact]
    public void Pick_LargeMesh_IsMuchFasterThanBruteForce()
    {
        var mesh = RandomTriangles(new Lcg(3), 200_000, faceCount: 1_000);
        var bvh = TriangleBvh.Build(mesh);
        var random = new Lcg(11);
        var rays = Enumerable.Range(0, 50)
            .Select(_ => RandomPoint(random, 30f))
            .Select(origin => new Ray(origin, Vector3.Normalize(RandomPoint(random, 5f) - origin)))
            .ToArray();

        var bruteForce = Stopwatch.StartNew();
        foreach (var ray in rays)
        {
            MeshPicker.Pick(mesh, ray);
        }

        bruteForce.Stop();
        var hierarchy = Stopwatch.StartNew();
        foreach (var ray in rays)
        {
            bvh.Pick(ray);
        }

        hierarchy.Stop();

        // Typically a factor of several hundred; 10 keeps the test stable on slow CI machines.
        Assert.True(
            hierarchy.Elapsed * 10 < bruteForce.Elapsed,
            $"BVH {hierarchy.Elapsed.TotalMilliseconds:F1} ms, brute force {bruteForce.Elapsed.TotalMilliseconds:F1} ms for {rays.Length} rays");
    }

    private static MeshData RandomTriangles(Lcg random, int count, int faceCount)
    {
        var positions = new float[count * 9];
        for (var triangle = 0; triangle < count; triangle++)
        {
            var center = RandomPoint(random, 10f);
            for (var corner = 0; corner < 3; corner++)
            {
                var vertex = center + RandomPoint(random, 0.6f);
                positions[(triangle * 9) + (corner * 3)] = vertex.X;
                positions[(triangle * 9) + (corner * 3) + 1] = vertex.Y;
                positions[(triangle * 9) + (corner * 3) + 2] = vertex.Z;
            }
        }

        var indices = Enumerable.Range(0, count * 3).Select(i => (uint)i).ToArray();
        var faces = Enumerable.Range(0, count).Select(i => i % faceCount).ToArray();
        return new MeshData(positions, new float[positions.Length], indices, faces);
    }

    // n x n unit squares (two triangles each) in the plane through the origin perpendicular to normalAxis.
    private static MeshData Grid(int n, int normalAxis)
    {
        var positions = new List<float>();
        var faces = new List<int>();
        var normal = normalAxis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };
        for (var u = 0; u < n; u++)
        {
            for (var v = 0; v < n; v++)
            {
                var p00 = Place(normal, u, v);
                var p10 = Place(normal, u + 1, v);
                var p01 = Place(normal, u, v + 1);
                var p11 = Place(normal, u + 1, v + 1);
                foreach (var p in new[] { p00, p10, p11, p00, p11, p01 })
                {
                    positions.AddRange([p.X, p.Y, p.Z]);
                }

                faces.AddRange([(u * n) + v, (u * n) + v]);
            }
        }

        var indices = Enumerable.Range(0, positions.Count / 3).Select(i => (uint)i).ToArray();
        return new MeshData([.. positions], new float[positions.Count], indices, [.. faces]);
    }

    // Point with coordinates (u, v) in the plane perpendicular to the axis of normal.
    private static Vector3 Place(Vector3 normal, float u, float v) =>
        MathF.Abs(normal.X) > 0 ? new Vector3(0, u, v) : MathF.Abs(normal.Y) > 0 ? new Vector3(u, 0, v) : new Vector3(u, v, 0);

    private static Vector3 RandomPoint(Lcg random, float extent) =>
        new(((random.NextSingle() * 2) - 1) * extent, ((random.NextSingle() * 2) - 1) * extent, ((random.NextSingle() * 2) - 1) * extent);

    // Deterministic pseudo-random numbers for test geometry (linear congruential generator).
    private sealed class Lcg(uint seed)
    {
        private uint _state = seed;

        public float NextSingle()
        {
            _state = unchecked((_state * 1664525u) + 1013904223u);
            return (_state >> 8) / 16777216f;
        }
    }
}
