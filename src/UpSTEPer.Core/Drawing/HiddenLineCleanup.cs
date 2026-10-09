namespace UpSTEPer.Core.Drawing;

/// <summary>
/// Removes the parts of hidden curves that lie on visible curves. Hidden-line removal reports edges
/// behind a visible edge even when they project onto it; drawing practice (ISO 128) shows only the
/// visible line there.
/// </summary>
/// <remarks>
/// <para>
/// Every hidden curve is sampled along its length; samples within the tolerance of a visible (or
/// tangent) curve are covered. Covered stretches are cut out and the remaining stretches are
/// returned as curves of the same type (segment, arc, elliptical arc, polyline), so the output
/// stays analytic. Stretch boundaries are refined by bisection.
/// </para>
/// <para>
/// Only overlaps of at least the minimum overlap length are cut: a hidden edge that merely touches
/// a visible one at its end, or crosses it, stays whole, so the joints of the drawing do not open.
/// </para>
/// <para>
/// Polylines only approximate free-form edges (within the projection deflection), so a point on a
/// polyline chord may lie that far from the exact curve: the deflection is added to the tolerance
/// whenever a polyline is involved.
/// </para>
/// </remarks>
public static class HiddenLineCleanup
{
    /// <summary>Default tolerance in mm; exact HLR output coincides far more closely.</summary>
    public const double DefaultTolerance = 1e-3;

    /// <summary>Default minimum length (mm) of a hidden stretch on a visible curve for it to be cut out.</summary>
    public const double DefaultMinimumOverlap = 0.1;

    // Sample spacing along a hidden curve (mm), and limits for the number of samples.
    private const double SampleSpacing = 0.05;
    private const int MinimumSamples = 64;
    private const int MaximumSamples = 8192;

    /// <summary>Returns <paramref name="view"/> with the parts of hidden curves on visible curves removed.</summary>
    /// <param name="view">The view.</param>
    /// <param name="tolerance">Maximum distance (mm) of a hidden point from a visible curve to count as covered.</param>
    /// <param name="minimumOverlap">Minimum length (mm) of a covered stretch to be removed.</param>
    /// <param name="polylineDeflection">
    /// Chord error (mm) of polylines, i.e. <see cref="ProjectionOptions.Deflection"/> of the projection.
    /// </param>
    /// <returns>The cleaned view (the same instance if nothing changed).</returns>
    public static ProjectedView RemoveCoveredHidden(
        ProjectedView view,
        double tolerance = DefaultTolerance,
        double minimumOverlap = DefaultMinimumOverlap,
        double polylineDeflection = 0.01)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tolerance);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumOverlap);
        ArgumentOutOfRangeException.ThrowIfNegative(polylineDeflection);

        double Slack(DrawingCurve curve) => curve is PolylineCurve ? polylineDeflection : 0;

        // Each visible curve as a coverage test with its own allowance for chord errors.
        var visible = view.Curves
            .Where(c => c.Kind != LineKind.Hidden)
            .Select(c => (Bounds: Box.Of(c), Distance: Distance.For(c), Slack: Slack(c)))
            .ToList();
        if (visible.Count == 0)
        {
            return view;
        }

        var result = new List<DrawingCurve>(view.Curves.Count);
        var changed = false;
        foreach (var curve in view.Curves)
        {
            if (curve.Kind != LineKind.Hidden)
            {
                result.Add(curve);
                continue;
            }

            var bounds = Box.Of(curve);
            var reach = tolerance + Slack(curve) + polylineDeflection;
            var candidates = visible.Where(v => v.Bounds.Overlaps(bounds, reach)).ToList();
            var near = candidates
                .Select(v =>
                {
                    var allowance = tolerance + Slack(curve) + v.Slack;
                    return (Func<Point2, bool>)(p => v.Bounds.Contains(p, allowance) && v.Distance(p) <= allowance);
                })
                .ToList();

            // How far coverage reaches beyond the end of a visible curve, to pull cuts back by.
            var inset = tolerance + Slack(curve) + (candidates.Count == 0 ? 0 : candidates.Max(v => v.Slack));
            var pieces = near.Count == 0 ? null : Uncovered(Parametric.For(curve), near, tolerance, inset, minimumOverlap);
            if (pieces is null)
            {
                result.Add(curve);
            }
            else
            {
                result.AddRange(pieces);
                changed = true;
            }
        }

        return changed ? view with { Curves = result } : view;
    }

    /// <summary>
    /// The stretches of a hidden curve not covered by visible curves, or <see langword="null"/> when
    /// the curve stays unchanged.
    /// </summary>
    private static List<DrawingCurve>? Uncovered(
        Parametric curve, List<Func<Point2, bool>> visible, double tolerance, double inset, double minimumOverlap)
    {
        bool Covered(double t)
        {
            var point = curve.At(t);
            return visible.Exists(isOn => isOn(point));
        }

        var samples = (int)Math.Clamp(Math.Ceiling(curve.Length / SampleSpacing), MinimumSamples, MaximumSamples);
        var flags = new bool[samples + 1];
        for (var i = 0; i <= samples; i++)
        {
            flags[i] = Covered((double)i / samples);
        }

        if (Array.TrueForAll(flags, f => !f))
        {
            return null;
        }

        if (Array.TrueForAll(flags, f => f))
        {
            return [];
        }

        // Covered intervals in curve parameter, boundaries refined between neighbouring samples.
        double Boundary(int i) => Bisect((double)i / samples, (double)(i + 1) / samples, flags[i], Covered);
        var covered = new List<(double Start, double End)>();
        for (var i = 0; i <= samples; i++)
        {
            if (!flags[i] || (i > 0 && flags[i - 1]))
            {
                continue;
            }

            var end = i;
            while (end < samples && flags[end + 1])
            {
                end++;
            }

            // A point counts as covered up to the inset beyond the end of a visible curve; pull the
            // cut back by that much so the remaining piece starts where the visible curve ends.
            var back = inset / curve.Length;
            covered.Add((i == 0 ? 0 : Boundary(i - 1) + back, end == samples ? 1 : Boundary(end) - back));
            i = end;
        }

        // A closed curve's covered stretch may run through t = 0.
        if (curve.IsClosed && covered.Count > 1 && covered[0].Start == 0 && covered[^1].End == 1)
        {
            covered[0] = (covered[^1].Start - 1, covered[0].End);
            covered.RemoveAt(covered.Count - 1);
        }

        covered.RemoveAll(c => (c.End - c.Start) * curve.Length < minimumOverlap);
        if (covered.Count == 0)
        {
            return null;
        }

        // The complement, as pieces of the original curve type; slivers are dropped.
        var pieces = new List<DrawingCurve>();
        void AddPiece(double start, double end)
        {
            if ((end - start) * curve.Length >= 10 * tolerance)
            {
                pieces.Add(curve.Piece(start, end));
            }
        }

        if (curve.IsClosed)
        {
            covered.Sort((a, b) => a.Start.CompareTo(b.Start));
            for (var i = 0; i < covered.Count; i++)
            {
                var next = i + 1 < covered.Count ? covered[i + 1].Start : covered[0].Start + 1;
                AddPiece(covered[i].End, next);
            }
        }
        else
        {
            var position = 0.0;
            foreach (var (start, end) in covered)
            {
                AddPiece(position, start);
                position = end;
            }

            AddPiece(position, 1);
        }

        return pieces;
    }

    // Finds the parameter between t0 and t1 where Covered changes from coveredAtT0.
    private static double Bisect(double t0, double t1, bool coveredAtT0, Func<double, bool> covered)
    {
        for (var i = 0; i < 40; i++)
        {
            var middle = (t0 + t1) / 2;
            if (covered(middle) == coveredAtT0)
            {
                t0 = middle;
            }
            else
            {
                t1 = middle;
            }
        }

        return (t0 + t1) / 2;
    }

    /// <summary>A curve parameterised over t in [0, 1] (t beyond 1 wraps for closed curves).</summary>
    private sealed record Parametric(Func<double, Point2> At, double Length, bool IsClosed, Func<double, double, DrawingCurve> Piece)
    {
        public static Parametric For(DrawingCurve curve) => curve switch
        {
            LineSegment line => Line(line),
            CircleCurve circle => Circle(circle),
            ArcCurve arc => Arc(arc),
            EllipseCurve ellipse => Ellipse(ellipse),
            PolylineCurve polyline => Polyline(polyline),
            _ => Polyline(new PolylineCurve([.. curve.Sample()], curve.Kind)),
        };

        private static Parametric Line(LineSegment line)
        {
            Point2 At(double t) => new(line.Start.X + ((line.End.X - line.Start.X) * t), line.Start.Y + ((line.End.Y - line.Start.Y) * t));
            return new(At, Distance.Length(line.Start, line.End), false, (t0, t1) => line with { Start = At(t0), End = At(t1) });
        }

        private static Parametric Circle(CircleCurve circle) => new(
            t => OnCircle(circle.Center, circle.Radius, 2 * Math.PI * t),
            2 * Math.PI * circle.Radius,
            true,
            (t0, t1) => new ArcCurve(circle.Center, circle.Radius, 2 * Math.PI * t0, 2 * Math.PI * t1, circle.Kind));

        private static Parametric Arc(ArcCurve arc)
        {
            var sweep = arc.Sweep;
            return new(
                t => OnCircle(arc.Center, arc.Radius, arc.StartAngle + (sweep * t)),
                sweep * arc.Radius,
                false,
                (t0, t1) => arc with { StartAngle = arc.StartAngle + (sweep * t0), EndAngle = arc.StartAngle + (sweep * t1) });
        }

        private static Parametric Ellipse(EllipseCurve ellipse)
        {
            var span = ellipse.EndParameter > ellipse.StartParameter
                ? ellipse.EndParameter - ellipse.StartParameter
                : ellipse.EndParameter + (2 * Math.PI) - ellipse.StartParameter;
            var minor = ellipse.MinorAxis;
            Point2 At(double t)
            {
                var u = ellipse.StartParameter + (span * t);
                return new(
                    ellipse.Center.X + (ellipse.MajorAxis.X * Math.Cos(u)) + (minor.X * Math.Sin(u)),
                    ellipse.Center.Y + (ellipse.MajorAxis.Y * Math.Cos(u)) + (minor.Y * Math.Sin(u)));
            }

            // Ramanujan's approximation of the circumference, scaled to the span.
            var (a, b) = (Distance.Length(default, ellipse.MajorAxis), Distance.Length(default, minor));
            var circumference = Math.PI * ((3 * (a + b)) - Math.Sqrt(((3 * a) + b) * (a + (3 * b))));
            return new(
                At,
                circumference * span / (2 * Math.PI),
                Math.Abs(span - (2 * Math.PI)) < 1e-9,
                (t0, t1) => ellipse with
                {
                    StartParameter = Normalize(ellipse.StartParameter + (span * t0)),
                    EndParameter = Normalize(ellipse.StartParameter + (span * t1)),
                });
        }

        private static Parametric Polyline(PolylineCurve polyline)
        {
            var points = polyline.Points;
            var cumulative = new double[points.Count];
            for (var i = 1; i < points.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] + Distance.Length(points[i - 1], points[i]);
            }

            var length = cumulative[^1];
            Point2 At(double t)
            {
                var s = Math.Clamp(t, 0, 1) * length;
                var i = Array.BinarySearch(cumulative, s);
                if (i >= 0)
                {
                    return points[i];
                }

                i = Math.Clamp(~i, 1, points.Count - 1);
                var segment = cumulative[i] - cumulative[i - 1];
                var f = segment == 0 ? 0 : (s - cumulative[i - 1]) / segment;
                return new(points[i - 1].X + ((points[i].X - points[i - 1].X) * f), points[i - 1].Y + ((points[i].Y - points[i - 1].Y) * f));
            }

            List<Point2> Between(double t0, double t1)
            {
                // Vertices that (nearly) coincide with an end are left out: no duplicate points.
                var epsilon = 1e-9 * length;
                var stretch = new List<Point2> { At(t0) };
                stretch.AddRange(points.Where((_, i) => cumulative[i] > (t0 * length) + epsilon && cumulative[i] < (t1 * length) - epsilon));
                stretch.Add(At(t1));
                return stretch;
            }

            DrawingCurve Piece(double t0, double t1)
            {
                // A stretch of a closed polyline may run through its start point.
                var stretch = t1 <= 1 ? Between(t0, t1) : [.. Between(t0, 1), .. Between(0, t1 - 1).Skip(1)];
                return polyline with { Points = stretch };
            }

            var closed = points.Count > 2 && Distance.Length(points[0], points[^1]) < 1e-9;
            return new(At, length, closed, Piece);
        }

        private static Point2 OnCircle(Point2 center, double radius, double angle) =>
            new(center.X + (radius * Math.Cos(angle)), center.Y + (radius * Math.Sin(angle)));

        private static double Normalize(double angle)
        {
            var normalized = angle % (2 * Math.PI);
            return normalized < 0 ? normalized + (2 * Math.PI) : normalized;
        }
    }

    /// <summary>Axis-aligned bounds of a curve's samples, to skip far-away visible curves cheaply.</summary>
    private readonly record struct Box(double MinX, double MinY, double MaxX, double MaxY)
    {
        public static Box Of(DrawingCurve curve)
        {
            var points = curve switch
            {
                PolylineCurve polyline => polyline.Points,
                _ => [.. curve.Sample()],
            };
            var box = new Box(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));

            // Curved samples (32 chords) can miss the true extreme by the chord sagitta,
            // r(1 - cos(pi/32)) < 0.5 % of the radius; 1 % is on the safe side.
            var margin = curve switch
            {
                CircleCurve circle => circle.Radius * 0.01,
                ArcCurve arc => arc.Radius * 0.01,
                EllipseCurve ellipse => Distance.Length(default, ellipse.MajorAxis) * 0.01,
                _ => 0,
            };
            return new Box(box.MinX - margin, box.MinY - margin, box.MaxX + margin, box.MaxY + margin);
        }

        public bool Contains(Point2 point, double margin) =>
            point.X >= MinX - margin && point.X <= MaxX + margin && point.Y >= MinY - margin && point.Y <= MaxY + margin;

        public bool Overlaps(Box other, double tolerance) =>
            MinX - tolerance <= other.MaxX && other.MinX - tolerance <= MaxX
            && MinY - tolerance <= other.MaxY && other.MinY - tolerance <= MaxY;
    }

    /// <summary>Point-to-curve distance functions.</summary>
    private static class Distance
    {
        public static Func<Point2, double> For(DrawingCurve curve) => curve switch
        {
            LineSegment line => p => ToSegment(p, line.Start, line.End),
            CircleCurve circle => p => Math.Abs(Length(p, circle.Center) - circle.Radius),
            ArcCurve arc => p => ToArc(p, arc),
            EllipseCurve ellipse => ToEllipse(ellipse),
            PolylineCurve polyline => ToPolyline(polyline.Points),
            _ => ToPolyline([.. curve.Sample()]),
        };

        public static double Length(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

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

        // Closest point by Newton's method in the ellipse frame, started from the point's eccentric
        // angle; converges in a few steps for the points that matter (those near the curve).
        private static Func<Point2, double> ToEllipse(EllipseCurve ellipse)
        {
            var a = Length(default, ellipse.MajorAxis);
            var b = a * ellipse.Ratio;
            var (ux, uy) = (ellipse.MajorAxis.X / a, ellipse.MajorAxis.Y / a);
            var span = ellipse.EndParameter > ellipse.StartParameter
                ? ellipse.EndParameter - ellipse.StartParameter
                : ellipse.EndParameter + (2 * Math.PI) - ellipse.StartParameter;
            var full = Math.Abs(span - (2 * Math.PI)) < 1e-9;

            return p =>
            {
                var (dx, dy) = (p.X - ellipse.Center.X, p.Y - ellipse.Center.Y);
                var x = (dx * ux) + (dy * uy);
                var y = (-dx * uy) + (dy * ux);
                double Gap(double t) => Math.Sqrt(Math.Pow((a * Math.Cos(t)) - x, 2) + Math.Pow((b * Math.Sin(t)) - y, 2));

                var t = Math.Atan2(y * a, x * b);
                for (var i = 0; i < 8; i++)
                {
                    var (sin, cos) = Math.SinCos(t);
                    var f = (((b * b) - (a * a)) * sin * cos) + (a * x * sin) - (b * y * cos);
                    var df = (((b * b) - (a * a)) * ((cos * cos) - (sin * sin))) + (a * x * cos) + (b * y * sin);
                    if (Math.Abs(df) < 1e-12)
                    {
                        break;
                    }

                    t -= f / df;
                }

                if (full)
                {
                    return Gap(t);
                }

                var offset = (t - ellipse.StartParameter) % (2 * Math.PI);
                if (offset < 0)
                {
                    offset += 2 * Math.PI;
                }

                return offset <= span
                    ? Gap(t)
                    : Math.Min(Gap(ellipse.StartParameter), Gap(ellipse.StartParameter + span));
            };
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
    }
}
