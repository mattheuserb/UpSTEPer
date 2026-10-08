namespace UpSTEPer.Core.Drawing;

/// <summary>A point in a 2D view, in model units (mm). Double precision, unlike System.Numerics.Vector2.</summary>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate.</param>
public readonly record struct Point2(double X, double Y)
{
    /// <summary>Adds an offset.</summary>
    /// <param name="point">The point.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The moved point.</returns>
    public static Point2 operator +(Point2 point, Point2 offset) => new(point.X + offset.X, point.Y + offset.Y);

    /// <summary>Adds an offset (named alternative to the operator).</summary>
    /// <param name="left">The point.</param>
    /// <param name="right">The offset.</param>
    /// <returns>The moved point.</returns>
    public static Point2 Add(Point2 left, Point2 right) => left + right;
}

/// <summary>An axis-aligned 2D rectangle.</summary>
/// <param name="MinX">Left edge.</param>
/// <param name="MinY">Bottom edge.</param>
/// <param name="MaxX">Right edge.</param>
/// <param name="MaxY">Top edge.</param>
public readonly record struct Bounds2(double MinX, double MinY, double MaxX, double MaxY)
{
    /// <summary>Gets the width.</summary>
    public double Width => MaxX - MinX;

    /// <summary>Gets the height.</summary>
    public double Height => MaxY - MinY;

    /// <summary>Gets the bounds of <paramref name="points"/>, or a zero-size box at the origin when empty.</summary>
    /// <param name="points">The points.</param>
    /// <returns>The bounds.</returns>
    public static Bounds2 Of(IEnumerable<Point2> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var point in points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        return double.IsInfinity(minX) ? default : new Bounds2(minX, minY, maxX, maxY);
    }
}
