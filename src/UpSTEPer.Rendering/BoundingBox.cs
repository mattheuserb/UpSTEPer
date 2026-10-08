using System.Numerics;

namespace UpSTEPer.Rendering;

/// <summary>An axis-aligned bounding box in model coordinates (mm).</summary>
/// <param name="Min">The minimum corner.</param>
/// <param name="Max">The maximum corner.</param>
public readonly record struct BoundingBox(Vector3 Min, Vector3 Max)
{
    /// <summary>Gets a unit box around the origin, used when there is no geometry.</summary>
    public static BoundingBox Unit { get; } = new(new Vector3(-0.5f), new Vector3(0.5f));

    /// <summary>Gets the centre of the box.</summary>
    public Vector3 Center => (Min + Max) * 0.5f;

    /// <summary>Gets the edge lengths of the box.</summary>
    public Vector3 Size => Max - Min;

    /// <summary>Gets the radius of the sphere enclosing the box.</summary>
    public float Radius => Size.Length() * 0.5f;

    /// <summary>Computes the bounds of interleaved x, y, z positions.</summary>
    /// <param name="positions">Positions, three floats per vertex.</param>
    /// <returns>The bounds, or <see cref="Unit"/> when <paramref name="positions"/> is empty.</returns>
    public static BoundingBox FromPositions(ReadOnlySpan<float> positions)
    {
        if (positions.Length < 3)
        {
            return Unit;
        }

        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        for (var i = 0; i + 2 < positions.Length; i += 3)
        {
            var point = new Vector3(positions[i], positions[i + 1], positions[i + 2]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return new BoundingBox(min, max);
    }
}
