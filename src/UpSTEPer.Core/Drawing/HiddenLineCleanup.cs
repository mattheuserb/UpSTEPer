namespace UpSTEPer.Core.Drawing;

/// <summary>
/// Removes hidden curves that lie entirely on visible curves. Hidden-line removal reports edges
/// behind a visible edge even when they project onto it; drawing practice (ISO 128) shows only the
/// visible line there.
/// </summary>
/// <remarks>
/// A hidden curve is dropped when all of its sample points are within the tolerance of some visible
/// curve. Partially covered hidden curves are kept unchanged (splitting them is future work).
/// </remarks>
public static class HiddenLineCleanup
{
    /// <summary>Default tolerance in mm; exact HLR output coincides far more closely.</summary>
    public const double DefaultTolerance = 1e-3;

    /// <summary>Returns <paramref name="view"/> without hidden curves covered by visible ones.</summary>
    /// <param name="view">The view.</param>
    /// <param name="tolerance">Maximum distance (mm) of a hidden sample point from a visible curve.</param>
    /// <returns>The cleaned view (the same instance if nothing was removed).</returns>
    public static ProjectedView RemoveCoveredHidden(ProjectedView view, double tolerance = DefaultTolerance)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tolerance);

        var visible = view.Curves.Where(c => c.Kind != LineKind.Hidden).Select(Distance.For).ToList();
        if (visible.Count == 0)
        {
            return view;
        }

        var kept = view.Curves
            .Where(c => c.Kind != LineKind.Hidden || !c.Sample().All(p => visible.Any(d => d(p) <= tolerance)))
            .ToList();
        return kept.Count == view.Curves.Count ? view : view with { Curves = kept };
    }

    /// <summary>Point-to-curve distance functions.</summary>
    private static class Distance
    {
        public static Func<Point2, double> For(DrawingCurve curve) => curve switch
        {
            LineSegment line => p => ToSegment(p, line.Start, line.End),
            CircleCurve circle => p => Math.Abs(Length(p, circle.Center) - circle.Radius),
            ArcCurve arc => p => ToArc(p, arc),
            _ => ToPolyline(Densify(curve)),
        };

        private static double ToArc(Point2 p, ArcCurve arc)
        {
            var angle = Math.Atan2(p.Y - arc.Center.Y, p.X - arc.Center.X);
            var offset = (angle - arc.StartAngle) % (2 * Math.PI);
            if (offset < 0)
            {
                offset += 2 * Math.PI;
            }

            if (offset <= arc.Sweep)
            {
                return Math.Abs(Length(p, arc.Center) - arc.Radius);
            }

            Point2 At(double a) => new(arc.Center.X + (arc.Radius * Math.Cos(a)), arc.Center.Y + (arc.Radius * Math.Sin(a)));
            return Math.Min(Length(p, At(arc.StartAngle)), Length(p, At(arc.StartAngle + arc.Sweep)));
        }

        // Ellipses: 512 segments keep the chord error well below the tolerance for typical part sizes.
        private static IReadOnlyList<Point2> Densify(DrawingCurve curve) => curve switch
        {
            EllipseCurve ellipse => [.. Resample(ellipse, 512)],
            PolylineCurve polyline => polyline.Points,
            _ => [.. curve.Sample()],
        };

        private static IEnumerable<Point2> Resample(EllipseCurve ellipse, int count)
        {
            var end = ellipse.EndParameter > ellipse.StartParameter ? ellipse.EndParameter : ellipse.EndParameter + (2 * Math.PI);
            var minor = ellipse.MinorAxis;
            for (var i = 0; i <= count; i++)
            {
                var t = ellipse.StartParameter + ((end - ellipse.StartParameter) * i / count);
                yield return new Point2(
                    ellipse.Center.X + (ellipse.MajorAxis.X * Math.Cos(t)) + (minor.X * Math.Sin(t)),
                    ellipse.Center.Y + (ellipse.MajorAxis.Y * Math.Cos(t)) + (minor.Y * Math.Sin(t)));
            }
        }

        private static Func<Point2, double> ToPolyline(IReadOnlyList<Point2> points) => p =>
        {
            var best = double.PositiveInfinity;
            for (var i = 0; i + 1 < points.Count; i++)
            {
                best = Math.Min(best, ToSegment(p, points[i], points[i + 1]));
            }

            return best;
        };

        private static double ToSegment(Point2 p, Point2 a, Point2 b)
        {
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            var lengthSquared = (dx * dx) + (dy * dy);
            var t = lengthSquared == 0 ? 0 : Math.Clamp((((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lengthSquared, 0, 1);
            return Length(p, new Point2(a.X + (t * dx), a.Y + (t * dy)));
        }

        private static double Length(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
    }
}
