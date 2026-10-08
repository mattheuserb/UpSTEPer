using System.Numerics;

namespace UpSTEPer.Rendering;

/// <summary>A half-line in model space.</summary>
/// <param name="Origin">The start point.</param>
/// <param name="Direction">The unit direction.</param>
public readonly record struct Ray(Vector3 Origin, Vector3 Direction)
{
    /// <summary>Gets the point at <paramref name="distance"/> along the ray.</summary>
    /// <param name="distance">Distance from <see cref="Origin"/>.</param>
    /// <returns>The point.</returns>
    public Vector3 PointAt(float distance) => Origin + (Direction * distance);
}
