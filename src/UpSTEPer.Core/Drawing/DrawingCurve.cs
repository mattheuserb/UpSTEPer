namespace UpSTEPer.Core.Drawing;

/// <summary>How an edge appears in a projected view.</summary>
public enum LineKind
{
    /// <summary>Visible sharp edge or silhouette (continuous line).</summary>
    Visible = 0,

    /// <summary>Hidden sharp edge or silhouette (dashed line).</summary>
    Hidden = 1,

    /// <summary>Visible smooth (tangent) edge between faces; often omitted in drawings.</summary>
    Tangent = 2,
}

/// <summary>A 2D curve of a projected view. Angles and parameters are radians, counter-clockwise.</summary>
/// <param name="Kind">Visible, hidden or tangent.</param>
public abstract record DrawingCurve(LineKind Kind)
{
    /// <summary>Gets points on the curve (exact end points, sampled interior), e.g. for bounds.</summary>
    /// <returns>The points.</returns>
    public abstract IEnumerable<Point2> Sample();

    /// <summary>Returns the same curve moved by <paramref name="offset"/>.</summary>
    /// <param name="offset">The translation.</param>
    /// <returns>The moved curve.</returns>
    public abstract DrawingCurve Translate(Point2 offset);

    /// <summary>Samples <paramref name="count"/> + 1 points of a parametric curve from t0 to t1.</summary>
    /// <param name="t0">Start parameter.</param>
    /// <param name="t1">End parameter.</param>
    /// <param name="point">The curve.</param>
    /// <param name="count">Number of segments.</param>
    /// <returns>The points.</returns>
    protected static IEnumerable<Point2> SampleParametric(double t0, double t1, Func<double, Point2> point, int count = 32)
    {
        ArgumentNullException.ThrowIfNull(point);
        for (var i = 0; i <= count; i++)
        {
            yield return point(t0 + ((t1 - t0) * i / count));
        }
    }
}

/// <summary>A straight segment.</summary>
/// <param name="Start">Start point.</param>
/// <param name="End">End point.</param>
/// <param name="Kind">Line kind.</param>
public sealed record LineSegment(Point2 Start, Point2 End, LineKind Kind) : DrawingCurve(Kind)
{
    /// <inheritdoc />
    public override IEnumerable<Point2> Sample() => [Start, End];

    /// <inheritdoc />
    public override DrawingCurve Translate(Point2 offset) => this with { Start = Start + offset, End = End + offset };
}

/// <summary>A full circle.</summary>
/// <param name="Center">Centre.</param>
/// <param name="Radius">Radius.</param>
/// <param name="Kind">Line kind.</param>
public sealed record CircleCurve(Point2 Center, double Radius, LineKind Kind) : DrawingCurve(Kind)
{
    /// <inheritdoc />
    public override IEnumerable<Point2> Sample() =>
        SampleParametric(0, 2 * Math.PI, a => new Point2(Center.X + (Radius * Math.Cos(a)), Center.Y + (Radius * Math.Sin(a))));

    /// <inheritdoc />
    public override DrawingCurve Translate(Point2 offset) => this with { Center = Center + offset };
}

/// <summary>A circular arc, counter-clockwise from <see cref="StartAngle"/> to <see cref="EndAngle"/>.</summary>
/// <param name="Center">Centre.</param>
/// <param name="Radius">Radius.</param>
/// <param name="StartAngle">Start angle in radians.</param>
/// <param name="EndAngle">End angle in radians.</param>
/// <param name="Kind">Line kind.</param>
public sealed record ArcCurve(Point2 Center, double Radius, double StartAngle, double EndAngle, LineKind Kind) : DrawingCurve(Kind)
{
    /// <summary>Gets the counter-clockwise sweep in radians, in (0, 2π].</summary>
    public double Sweep
    {
        get
        {
            var sweep = (EndAngle - StartAngle) % (2 * Math.PI);
            return sweep <= 0 ? sweep + (2 * Math.PI) : sweep;
        }
    }

    /// <inheritdoc />
    public override IEnumerable<Point2> Sample() =>
        SampleParametric(StartAngle, StartAngle + Sweep, a => new Point2(Center.X + (Radius * Math.Cos(a)), Center.Y + (Radius * Math.Sin(a))));

    /// <inheritdoc />
    public override DrawingCurve Translate(Point2 offset) => this with { Center = Center + offset };
}

/// <summary>
/// An elliptical arc, counter-clockwise from <see cref="StartParameter"/> to <see cref="EndParameter"/>
/// (full ellipse: 0 to 2π). Point(t) = Center + MajorAxis·cos t + MinorAxis·sin t.
/// </summary>
/// <param name="Center">Centre.</param>
/// <param name="MajorAxis">Major semi-axis vector (direction and length).</param>
/// <param name="Ratio">Minor / major radius, in (0, 1].</param>
/// <param name="StartParameter">Start parameter in radians.</param>
/// <param name="EndParameter">End parameter in radians.</param>
/// <param name="Kind">Line kind.</param>
public sealed record EllipseCurve(Point2 Center, Point2 MajorAxis, double Ratio, double StartParameter, double EndParameter, LineKind Kind)
    : DrawingCurve(Kind)
{
    /// <summary>Gets the minor semi-axis vector (major axis rotated by +90°, scaled by <see cref="Ratio"/>).</summary>
    public Point2 MinorAxis => new(-MajorAxis.Y * Ratio, MajorAxis.X * Ratio);

    /// <inheritdoc />
    public override IEnumerable<Point2> Sample()
    {
        var end = EndParameter > StartParameter ? EndParameter : EndParameter + (2 * Math.PI);
        var minor = MinorAxis;
        return SampleParametric(StartParameter, end, t => new Point2(
            Center.X + (MajorAxis.X * Math.Cos(t)) + (minor.X * Math.Sin(t)),
            Center.Y + (MajorAxis.Y * Math.Cos(t)) + (minor.Y * Math.Sin(t))));
    }

    /// <inheritdoc />
    public override DrawingCurve Translate(Point2 offset) => this with { Center = Center + offset };
}

/// <summary>A polyline approximating a free-form curve (B-spline, projected torus edge, ...).</summary>
/// <param name="Points">The vertices, at least two.</param>
/// <param name="Kind">Line kind.</param>
public sealed record PolylineCurve(IReadOnlyList<Point2> Points, LineKind Kind) : DrawingCurve(Kind)
{
    /// <inheritdoc />
    public override IEnumerable<Point2> Sample() => Points;

    /// <inheritdoc />
    public override DrawingCurve Translate(Point2 offset) => this with { Points = [.. Points.Select(p => p + offset)] };
}
