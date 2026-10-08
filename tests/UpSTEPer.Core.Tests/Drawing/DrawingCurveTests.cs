using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Core.Tests.Drawing;

public class DrawingCurveTests
{
    [Theory]
    [InlineData(0, Math.PI / 2, Math.PI / 2)]
    [InlineData(3 * Math.PI / 2, Math.PI / 2, Math.PI)] // wraps through 0
    [InlineData(1, 1, 2 * Math.PI)] // equal angles: full turn
    public void Arc_Sweep_IsCounterClockwise(double start, double end, double expected)
    {
        var arc = new ArcCurve(default, 1, start, end, LineKind.Visible);

        Assert.Equal(expected, arc.Sweep, 9);
    }

    [Fact]
    public void Arc_Sample_StartsAndEndsOnTheArc()
    {
        var arc = new ArcCurve(new Point2(1, 1), 2, 0, Math.PI / 2, LineKind.Visible);

        var points = arc.Sample().ToList();

        Assert.Equal(3, points[0].X, 9);
        Assert.Equal(1, points[0].Y, 9);
        Assert.Equal(1, points[^1].X, 9);
        Assert.Equal(3, points[^1].Y, 9);
    }

    [Fact]
    public void Ellipse_Sample_FollowsMajorAndMinorAxis()
    {
        var ellipse = new EllipseCurve(default, new Point2(4, 0), 0.5, 0, 2 * Math.PI, LineKind.Visible);

        var bounds = Bounds2.Of(ellipse.Sample());

        Assert.Equal(new Point2(0, 2), ellipse.MinorAxis);
        Assert.Equal(-4, bounds.MinX, 6);
        Assert.Equal(4, bounds.MaxX, 6);
        Assert.Equal(-2, bounds.MinY, 2);
        Assert.Equal(2, bounds.MaxY, 2);
    }

    [Fact]
    public void Translate_MovesEveryCurveType()
    {
        var offset = new Point2(10, -5);
        DrawingCurve[] curves =
        [
            new LineSegment(new Point2(0, 0), new Point2(1, 0), LineKind.Visible),
            new CircleCurve(new Point2(0, 0), 1, LineKind.Hidden),
            new ArcCurve(new Point2(0, 0), 1, 0, 1, LineKind.Visible),
            new EllipseCurve(new Point2(0, 0), new Point2(2, 0), 0.5, 0, 1, LineKind.Visible),
            new PolylineCurve([new Point2(0, 0), new Point2(1, 1)], LineKind.Tangent),
        ];

        foreach (var curve in curves)
        {
            var before = Bounds2.Of(curve.Sample());
            var after = Bounds2.Of(curve.Translate(offset).Sample());
            Assert.Equal(before.MinX + 10, after.MinX, 9);
            Assert.Equal(before.MinY - 5, after.MinY, 9);
            Assert.Equal(curve.Kind, curve.Translate(offset).Kind);
        }
    }

    [Fact]
    public void Bounds_OfNoPoints_IsEmptyAtOrigin()
    {
        Assert.Equal(default, Bounds2.Of([]));
    }

    [Fact]
    public void Point_Add_IsComponentWise()
    {
        Assert.Equal(new Point2(4, 6), Point2.Add(new Point2(1, 2), new Point2(3, 4)));
    }
}
