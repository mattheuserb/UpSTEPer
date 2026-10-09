namespace UpSTEPer.Core;

/// <summary>Axis-aligned bounding box of a model in millimetres.</summary>
/// <remarks>
/// Computed from the exact geometry and possibly slightly larger than the model (tolerances,
/// spline control points): suitable for scaling tolerances and views, not for measuring.
/// </remarks>
/// <param name="MinX">Smallest x.</param>
/// <param name="MinY">Smallest y.</param>
/// <param name="MinZ">Smallest z.</param>
/// <param name="MaxX">Largest x.</param>
/// <param name="MaxY">Largest y.</param>
/// <param name="MaxZ">Largest z.</param>
public sealed record ModelBounds(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)
{
    /// <summary>Gets the length of the box diagonal, a measure of the model size.</summary>
    public double Diagonal
    {
        get
        {
            var (dx, dy, dz) = (MaxX - MinX, MaxY - MinY, MaxZ - MinZ);
            return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }
    }
}
