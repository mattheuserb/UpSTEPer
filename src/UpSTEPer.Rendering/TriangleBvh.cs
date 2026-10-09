using System.Numerics;
using UpSTEPer.Core;

namespace UpSTEPer.Rendering;

/// <summary>
/// Bounding volume hierarchy over the triangles of a mesh for fast ray picking: a pick visits
/// O(log n) boxes instead of every triangle, which makes picking on every mouse move (hover) cheap
/// even for meshes with 10⁵–10⁶ triangles.
/// </summary>
/// <remarks>
/// Built once per mesh (median split on the longest axis of the centroid bounds, up to
/// <see cref="LeafSize"/> triangles per leaf) and immutable afterwards, so it can be shared between
/// threads. Results are identical to <see cref="MeshPicker.Pick"/>.
/// </remarks>
public sealed class TriangleBvh
{
    /// <summary>Maximum number of triangles in a leaf.</summary>
    public const int LeafSize = 4;

    private readonly MeshData _mesh;

    // Triangle indices, reordered so every node covers a contiguous range.
    private readonly int[] _triangles;

    // Flattened nodes: bounds, plus either the right child (inner node; the left child follows
    // directly) or the first triangle (leaf, Count > 0).
    private readonly Node[] _nodes;

    private TriangleBvh(MeshData mesh, int[] triangles, Node[] nodes)
    {
        _mesh = mesh;
        _triangles = triangles;
        _nodes = nodes;
    }

    /// <summary>Gets the number of nodes (for diagnostics and tests).</summary>
    public int NodeCount => _nodes.Length;

    /// <summary>Builds the hierarchy for <paramref name="mesh"/>.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The hierarchy.</returns>
    public static TriangleBvh Build(MeshData mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var count = mesh.TriangleCount;
        var triangles = new int[count];
        var bounds = new Box[count];
        var centroids = new Vector3[count];
        var positions = mesh.Positions.Span;
        var indices = mesh.Indices.Span;
        for (var triangle = 0; triangle < count; triangle++)
        {
            var a = Vertex(positions, indices[triangle * 3]);
            var b = Vertex(positions, indices[(triangle * 3) + 1]);
            var c = Vertex(positions, indices[(triangle * 3) + 2]);
            triangles[triangle] = triangle;
            bounds[triangle] = new Box(Vector3.Min(a, Vector3.Min(b, c)), Vector3.Max(a, Vector3.Max(b, c)));
            centroids[triangle] = (a + b + c) / 3f;
        }

        var nodes = new List<Node>(Math.Max(1, (2 * count / LeafSize) + 1));
        if (count > 0)
        {
            BuildNode(nodes, triangles, bounds, centroids, 0, count);
        }

        return new TriangleBvh(mesh, triangles, [.. nodes]);
    }

    /// <summary>Returns the closest face hit by <paramref name="ray"/>, or <see langword="null"/>.</summary>
    /// <param name="ray">The pick ray.</param>
    /// <returns>The hit, or <see langword="null"/> when the ray misses the mesh.</returns>
    public PickResult? Pick(Ray ray)
    {
        if (_nodes.Length == 0)
        {
            return null;
        }

        var positions = _mesh.Positions.Span;
        var indices = _mesh.Indices.Span;

        var bestDistance = float.PositiveInfinity;
        var bestTriangle = -1;
        Span<int> stack = stackalloc int[64];
        var depth = 0;
        stack[depth++] = 0;
        while (depth > 0)
        {
            var index = stack[--depth];
            var node = _nodes[index];
            if (!node.Bounds.Intersects(ray, bestDistance))
            {
                continue;
            }

            if (node.Count > 0)
            {
                for (var i = node.Start; i < node.Start + node.Count; i++)
                {
                    var triangle = _triangles[i];
                    if (MeshPicker.Intersect(
                            ray,
                            Vertex(positions, indices[triangle * 3]),
                            Vertex(positions, indices[(triangle * 3) + 1]),
                            Vertex(positions, indices[(triangle * 3) + 2]),
                            out var distance)
                        && (distance < bestDistance || (distance == bestDistance && triangle < bestTriangle)))
                    {
                        bestDistance = distance;
                        bestTriangle = triangle;
                    }
                }
            }
            else
            {
                // Inner node: the left child follows it, Start is the right child. Median splits
                // keep the depth at log2(n / LeafSize), far below the stack size.
                stack[depth++] = node.Start;
                stack[depth++] = index + 1;
            }
        }

        return bestTriangle < 0
            ? null
            : new PickResult(new FaceId(_mesh.TriangleFaceIds.Span[bestTriangle]), bestDistance, ray.PointAt(bestDistance));
    }

    private static int BuildNode(List<Node> nodes, int[] triangles, Box[] bounds, Vector3[] centroids, int start, int count)
    {
        var index = nodes.Count;
        nodes.Add(default);

        var nodeBounds = Box.Empty;
        var centroidBounds = Box.Empty;
        for (var i = start; i < start + count; i++)
        {
            nodeBounds = nodeBounds.Union(bounds[triangles[i]]);
            centroidBounds = centroidBounds.Include(centroids[triangles[i]]);
        }

        var extent = centroidBounds.Max - centroidBounds.Min;
        if (count <= LeafSize || extent == Vector3.Zero)
        {
            nodes[index] = new Node(nodeBounds, start, count);
            return index;
        }

        var axis = extent.X >= extent.Y && extent.X >= extent.Z ? 0 : extent.Y >= extent.Z ? 1 : 2;
        var half = count / 2;

        // Only "left <= median <= right" matters for the split: a selection is O(n) per level,
        // where a full sort was O(n log n) (16 s instead of ~1 s for 7.8 million triangles).
        SelectNth(triangles, centroids, axis, start, start + count - 1, start + half);

        BuildNode(nodes, triangles, bounds, centroids, start, half);
        var right = BuildNode(nodes, triangles, bounds, centroids, start + half, count - half);
        nodes[index] = new Node(nodeBounds, right, 0);
        return index;
    }

    // Quickselect (Hoare partition, median-of-three pivot): afterwards items[nth] holds the element
    // a sort would put there, with no larger key before it and no smaller key after it.
    internal static void SelectNth(int[] items, Vector3[] centroids, int axis, int left, int right, int nth)
    {
        while (right > left)
        {
            var a = Key(items[left]);
            var b = Key(items[left + ((right - left) / 2)]);
            var c = Key(items[right]);
            var pivot = MathF.Max(MathF.Min(a, b), MathF.Min(MathF.Max(a, b), c));

            var i = left;
            var j = right;
            while (i <= j)
            {
                while (Key(items[i]) < pivot)
                {
                    i++;
                }

                while (Key(items[j]) > pivot)
                {
                    j--;
                }

                if (i <= j)
                {
                    (items[i], items[j]) = (items[j], items[i]);
                    i++;
                    j--;
                }
            }

            if (nth <= j)
            {
                right = j;
            }
            else if (nth >= i)
            {
                left = i;
            }
            else
            {
                return;
            }
        }

        float Key(int triangle) => axis switch
        {
            0 => centroids[triangle].X,
            1 => centroids[triangle].Y,
            _ => centroids[triangle].Z,
        };
    }

    private static Vector3 Vertex(ReadOnlySpan<float> positions, uint index)
    {
        var offset = (int)index * 3;
        return new Vector3(positions[offset], positions[offset + 1], positions[offset + 2]);
    }

    // Inner node: Start = index of the right child, the left child is the next node.
    // Leaf: Start = first entry in _triangles, Count > 0.
    private readonly record struct Node(Box Bounds, int Start, int Count);

    private readonly record struct Box(Vector3 Min, Vector3 Max)
    {
        public static Box Empty { get; } = new(new Vector3(float.PositiveInfinity), new Vector3(float.NegativeInfinity));

        public Box Union(Box other) => new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

        public Box Include(Vector3 point) => new(Vector3.Min(Min, point), Vector3.Max(Max, point));

        // Slab test over the closed box, for ray parameters 0..maxDistance.
        public bool Intersects(Ray ray, float maxDistance)
        {
            var enter = 0f;
            var exit = maxDistance;
            return Slab(ray.Origin.X, ray.Direction.X, Min.X, Max.X, ref enter, ref exit)
                && Slab(ray.Origin.Y, ray.Direction.Y, Min.Y, Max.Y, ref enter, ref exit)
                && Slab(ray.Origin.Z, ray.Direction.Z, Min.Z, Max.Z, ref enter, ref exit)
                && enter <= exit;
        }

        // A ray parallel to a slab (direction 0) is inside for every t or for none; computing
        // 1/0 instead would give 0 * infinity = NaN, or a wrong interval, for rays lying exactly
        // in a box plane, e.g. through mesh edges in a front view.
        private static bool Slab(float origin, float direction, float min, float max, ref float enter, ref float exit)
        {
            // Below 1e-20 the inverse overflows to infinity; such a ray is parallel for all purposes.
            if (MathF.Abs(direction) < 1e-20f)
            {
                return origin >= min && origin <= max;
            }

            var inverse = 1f / direction;
            var t1 = (min - origin) * inverse;
            var t2 = (max - origin) * inverse;
            enter = MathF.Max(enter, MathF.Min(t1, t2));
            exit = MathF.Min(exit, MathF.Max(t1, t2));
            return true;
        }
    }
}
