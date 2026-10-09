using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Core.Tests.Drawing;

public class HiddenLineCleanupTests
{
    private static readonly LineSegment _visibleLine = new(new Point2(0, 0), new Point2(10, 0), LineKind.Visible);

    [Fact]
    public void HiddenLineOnVisibleLine_IsRemoved()
    {
        var hidden = new LineSegment(new Point2(2, 0), new Point2(8, 0), LineKind.Hidden);

        var cleaned = Clean(_visibleLine, hidden);

        Assert.Equal([_visibleLine], cleaned);
    }

    [Fact]
    public void PartlyCoveredHiddenLine_KeepsOnlyTheUncoveredPart()
    {
        var hidden = new LineSegment(new Point2(5, 0), new Point2(15, 0), LineKind.Hidden);

        var cleaned = Clean(_visibleLine, hidden);

        Assert.Equal(2, cleaned.Count);
        AssertLine(new Point2(10, 0), new Point2(15, 0), cleaned[1]);
    }

    [Fact]
    public void HiddenLineCoveredInTheMiddle_IsSplitInTwo()
    {
        var visible = new LineSegment(new Point2(4, 0), new Point2(6, 0), LineKind.Visible);
        var hidden = new LineSegment(new Point2(0, 0), new Point2(10, 0), LineKind.Hidden);

        var cleaned = Clean(visible, hidden);

        Assert.Equal(3, cleaned.Count);
        AssertLine(new Point2(0, 0), new Point2(4, 0), cleaned[1]);
        AssertLine(new Point2(6, 0), new Point2(10, 0), cleaned[2]);
    }

    [Fact]
    public void HiddenLineTouchingOrCrossingVisibleLines_StaysWhole()
    {
        // Ends on one visible edge, crosses another: overlaps far below the minimum overlap.
        var start = new LineSegment(new Point2(0, -5), new Point2(0, 5), LineKind.Visible);
        var crossing = new LineSegment(new Point2(5, -5), new Point2(5, 5), LineKind.Visible);
        var hidden = new LineSegment(new Point2(0, 0), new Point2(10, 0), LineKind.Hidden);

        Assert.Equal([start, crossing, hidden], Clean(start, crossing, hidden));
    }

    [Fact]
    public void ShortOverlapBelowMinimum_IsKept()
    {
        var visible = new LineSegment(new Point2(4, 0), new Point2(4.05, 0), LineKind.Visible);
        var hidden = new LineSegment(new Point2(0, 0), new Point2(10, 0), LineKind.Hidden);

        Assert.Equal([visible, hidden], Clean(visible, hidden));
    }

    [Fact]
    public void HiddenCirclePartlyUnderVisibleArc_BecomesTheRemainingArc()
    {
        var visible = new ArcCurve(new Point2(1, 1), 5, 0, Math.PI / 2, LineKind.Visible);
        var hidden = new CircleCurve(new Point2(1, 1), 5, LineKind.Hidden);

        var rest = Assert.IsType<ArcCurve>(Clean(visible, hidden)[1]);

        Assert.Equal(LineKind.Hidden, rest.Kind);
        Assert.Equal(Math.PI / 2, rest.StartAngle % (2 * Math.PI), 6);
        Assert.Equal(1.5 * Math.PI, rest.Sweep, 6);
    }

    [Fact]
    public void HiddenCircleUnderArcThroughZero_BecomesOneArc()
    {
        // The visible arc runs through angle 0, where the circle's parameter starts.
        var visible = new ArcCurve(default, 2, -Math.PI / 4, Math.PI / 4, LineKind.Visible);
        var hidden = new CircleCurve(default, 2, LineKind.Hidden);

        var rest = Assert.IsType<ArcCurve>(Assert.Single(Clean(visible, hidden), c => c.Kind == LineKind.Hidden));

        Assert.Equal(1.5 * Math.PI, rest.Sweep, 6);
    }

    [Fact]
    public void HiddenEllipticArcPartlyCovered_KeepsEllipseType()
    {
        var hidden = new EllipseCurve(default, new Point2(4, 0), 0.5, 0, Math.PI, LineKind.Hidden);
        var visible = new PolylineCurve([.. new EllipseCurve(default, new Point2(4, 0), 0.5, 0, Math.PI / 2, LineKind.Visible).Sample()], LineKind.Visible);

        var rest = Assert.IsType<EllipseCurve>(Clean(visible, hidden)[1]);

        Assert.InRange(rest.StartParameter, (Math.PI / 2) - 0.01, (Math.PI / 2) + 0.01);
        Assert.Equal(Math.PI, rest.EndParameter, 6);
    }

    [Fact]
    public void HiddenPolylinePartlyOnVisibleLine_KeepsTheRestAsPolyline()
    {
        var hidden = new PolylineCurve([new Point2(0, 0), new Point2(10, 0), new Point2(10, 5)], LineKind.Hidden);

        var rest = Assert.IsType<PolylineCurve>(Clean(_visibleLine, hidden)[1]);

        Assert.Equal([new Point2(10, 0), new Point2(10, 5)], rest.Points.Select(p => new Point2(Math.Round(p.X, 6), Math.Round(p.Y, 6))));
    }

    [Fact]
    public void HiddenEllipseUnderRotatedVisibleEllipse_IsRemoved_SlightlyLargerKept()
    {
        var axis = new Point2(30 * Math.Cos(0.7), 30 * Math.Sin(0.7));
        var visible = new EllipseCurve(new Point2(5, -3), axis, 0.35, 0, 2 * Math.PI, LineKind.Visible);
        var same = visible with { Kind = LineKind.Hidden };
        var larger = same with { MajorAxis = new Point2(axis.X * (30.01 / 30), axis.Y * (30.01 / 30)) };

        Assert.Equal([visible, larger], Clean(visible, same, larger));
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        var view = new ProjectedView(ViewDirection.Front, []);

        Assert.Throws<ArgumentOutOfRangeException>(() => HiddenLineCleanup.RemoveCoveredHidden(view, tolerance: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => HiddenLineCleanup.RemoveCoveredHidden(view, minimumOverlap: -1));
    }

    [Fact]
    public void HiddenLineBesideVisibleLine_IsKept()
    {
        var hidden = new LineSegment(new Point2(0, 0.01), new Point2(10, 0.01), LineKind.Hidden);

        Assert.Contains(hidden, Clean(_visibleLine, hidden));
    }

    [Fact]
    public void HiddenCircleUnderVisibleCircle_IsRemoved_DifferentRadiusKept()
    {
        var visible = new CircleCurve(new Point2(1, 1), 5, LineKind.Visible);
        var same = new CircleCurve(new Point2(1, 1), 5, LineKind.Hidden);
        var smaller = new CircleCurve(new Point2(1, 1), 4, LineKind.Hidden);

        Assert.Equal([visible, smaller], Clean(visible, same, smaller));
    }

    [Fact]
    public void HiddenArcInsideVisibleArc_IsRemoved_OutsideKept()
    {
        var visible = new ArcCurve(default, 3, 0, Math.PI, LineKind.Visible);
        var inside = new ArcCurve(default, 3, 0.5, 2.5, LineKind.Hidden);
        var outside = new ArcCurve(default, 3, Math.PI + 0.5, Math.PI + 1, LineKind.Hidden);

        Assert.Equal([visible, outside], Clean(visible, inside, outside));
    }

    [Fact]
    public void HiddenPolylineOnVisibleEllipse_IsRemoved()
    {
        // Like HLR output: a polyline within the projection deflection (0.01 mm) of the exact curve.
        var ellipse = new EllipseCurve(default, new Point2(4, 0), 0.5, 0, 2 * Math.PI, LineKind.Visible);
        var hidden = new PolylineCurve([.. new EllipseCurve(default, new Point2(4, 0), 0.5, 0, 0.5, LineKind.Hidden).Sample()], LineKind.Hidden);

        Assert.Equal([ellipse], Clean(ellipse, hidden));
    }

    [Fact]
    public void NoVisibleCurves_ReturnsSameInstance()
    {
        var view = new ProjectedView(ViewDirection.Front, [new LineSegment(default, new Point2(1, 1), LineKind.Hidden)]);

        Assert.Same(view, HiddenLineCleanup.RemoveCoveredHidden(view));
    }

    [Fact]
    public void TangentEdgesAlsoCoverHiddenOnes()
    {
        var tangent = _visibleLine with { Kind = LineKind.Tangent };
        var hidden = new LineSegment(new Point2(1, 0), new Point2(2, 0), LineKind.Hidden);

        Assert.Equal([tangent], Clean(tangent, hidden));
    }

    private static void AssertLine(Point2 start, Point2 end, DrawingCurve actual)
    {
        var line = Assert.IsType<LineSegment>(actual);
        Assert.Equal(LineKind.Hidden, line.Kind);
        Assert.Equal(start.X, line.Start.X, 4);
        Assert.Equal(start.Y, line.Start.Y, 4);
        Assert.Equal(end.X, line.End.X, 4);
        Assert.Equal(end.Y, line.End.Y, 4);
    }

    private static List<DrawingCurve> Clean(params DrawingCurve[] curves) =>
        [.. HiddenLineCleanup.RemoveCoveredHidden(new ProjectedView(ViewDirection.Front, curves)).Curves];
}
