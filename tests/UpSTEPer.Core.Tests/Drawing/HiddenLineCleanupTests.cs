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
    public void PartlyCoveredHiddenLine_IsKept()
    {
        var hidden = new LineSegment(new Point2(5, 0), new Point2(15, 0), LineKind.Hidden);

        Assert.Contains(hidden, Clean(_visibleLine, hidden));
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
        var ellipse = new EllipseCurve(default, new Point2(4, 0), 0.5, 0, 2 * Math.PI, LineKind.Visible);
        var hidden = new PolylineCurve([.. ellipse.Sample().Take(5)], LineKind.Hidden);

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

    private static List<DrawingCurve> Clean(params DrawingCurve[] curves) =>
        [.. HiddenLineCleanup.RemoveCoveredHidden(new ProjectedView(ViewDirection.Front, curves)).Curves];
}
