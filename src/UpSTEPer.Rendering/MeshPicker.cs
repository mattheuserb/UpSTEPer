using System.Numerics;
using UpSTEPer.Core;

namespace UpSTEPer.Rendering;

/// <summary>Result of a successful pick.</summary>
/// <param name="Face">The face hit first.</param>
/// <param name="Distance">Distance from the ray origin to the hit point.</param>
/// <param name="Point">The hit point in model space.</param>
public sealed record PickResult(FaceId Face, float Distance, Vector3 Point);

/// <summary>Finds the face under the mouse by intersecting a ray with the display mesh.</summary>
/// <remarks>
/// Brute force over all triangles (Möller–Trumbore, two-sided): a few milliseconds for 10⁵
/// triangles, which is enough for click selection. Add a BVH if hover picking is needed.
/// </remarks>
public static class MeshPicker
{
    private const float Epsilon = 1e-7f;

    /// <summary>Returns the closest face hit by <paramref name="ray"/>, or <see langword="null"/>.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="ray">The pick ray.</param>
    /// <returns>The hit, or <see langword="null"/> when the ray misses the mesh.</returns>
    public static PickResult? Pick(MeshData mesh, Ray ray)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var positions = mesh.Positions.Span;
        var indices = mesh.Indices.Span;
        var faces = mesh.TriangleFaceIds.Span;

        var bestDistance = float.PositiveInfinity;
        var bestTriangle = -1;
        for (var triangle = 0; triangle < faces.Length; triangle++)
        {
            var a = Vertex(positions, indices[triangle * 3]);
            var b = Vertex(positions, indices[(triangle * 3) + 1]);
            var c = Vertex(positions, indices[(triangle * 3) + 2]);
            if (Intersect(ray, a, b, c, out var distance) && distance < bestDistance)
            {
                bestDistance = distance;
                bestTriangle = triangle;
            }
        }

        return bestTriangle < 0
            ? null
            : new PickResult(new FaceId(faces[bestTriangle]), bestDistance, ray.PointAt(bestDistance));
    }

    private static Vector3 Vertex(ReadOnlySpan<float> positions, uint index)
    {
        var offset = (int)index * 3;
        return new Vector3(positions[offset], positions[offset + 1], positions[offset + 2]);
    }

    // Möller–Trumbore ray/triangle intersection, accepting both windings.
    private static bool Intersect(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0;
        var edge1 = b - a;
        var edge2 = c - a;
        var p = Vector3.Cross(ray.Direction, edge2);
        var determinant = Vector3.Dot(edge1, p);
        if (MathF.Abs(determinant) < Epsilon)
        {
            return false;
        }

        var inverse = 1f / determinant;
        var t = ray.Origin - a;
        var u = Vector3.Dot(t, p) * inverse;
        if (u is < 0f or > 1f)
        {
            return false;
        }

        var q = Vector3.Cross(t, edge1);
        var v = Vector3.Dot(ray.Direction, q) * inverse;
        if (v < 0f || u + v > 1f)
        {
            return false;
        }

        distance = Vector3.Dot(edge2, q) * inverse;
        return distance > Epsilon;
    }
}
