using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Occt.Native;

/// <summary>Converts native drawing curves into the Core drawing model.</summary>
internal static class DrawingConverter
{
    public static DrawingCurve ToCurve(in NativeDrawingCurve curve, ReadOnlySpan<double> points)
    {
        var kind = (LineKind)curve.Kind;
        return curve.Type switch
        {
            NativeCurveType.Line => new LineSegment(new Point2(curve.D0, curve.D1), new Point2(curve.D2, curve.D3), kind),
            NativeCurveType.Arc => new ArcCurve(new Point2(curve.D0, curve.D1), curve.D2, curve.D3, curve.D4, kind),
            NativeCurveType.Circle => new CircleCurve(new Point2(curve.D0, curve.D1), curve.D2, kind),
            NativeCurveType.Ellipse => new EllipseCurve(new Point2(curve.D0, curve.D1), new Point2(curve.D2, curve.D3), curve.D4, curve.D5, curve.D6, kind),
            NativeCurveType.Polyline => new PolylineCurve(ReadPoints(points, curve.FirstPoint, curve.PointCount), kind),
            _ => throw new InvalidOperationException($"Unknown native curve type {curve.Type}."),
        };
    }

    private static Point2[] ReadPoints(ReadOnlySpan<double> points, int first, int count)
    {
        var result = new Point2[count];
        for (var i = 0; i < count; i++)
        {
            var offset = (first + i) * 2;
            result[i] = new Point2(points[offset], points[offset + 1]);
        }

        return result;
    }
}
