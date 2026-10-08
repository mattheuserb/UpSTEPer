using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Types.Units;
using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Export.Dxf.Tests;

public sealed class DxfExporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"upsteper-{Guid.NewGuid():N}.dxf");

    private static readonly ProjectedView _view = new(ViewDirection.Front,
    [
        new LineSegment(new Point2(0, 0), new Point2(10, 0), LineKind.Visible),
        new CircleCurve(new Point2(5, 5), 2, LineKind.Hidden),
        new ArcCurve(new Point2(0, 0), 3, -Math.PI / 2, Math.PI / 2, LineKind.Visible),
        new EllipseCurve(new Point2(1, 2), new Point2(0, 4), 0.25, 0, 2 * Math.PI, LineKind.Tangent),
        new EllipseCurve(new Point2(1, 2), new Point2(4, 0), 0.5, -1, 1, LineKind.Visible),
        new PolylineCurve([new Point2(0, 0), new Point2(1, 1), new Point2(2, 0)], LineKind.Hidden),
    ]);

    [Fact]
    public void Write_ThenRead_KeepsTypesLayersAndGeometry()
    {
        DxfExporter.Write(_path, [_view]);

        var document = DxfReader.Read(_path);
        var entities = document.Entities.ToList();

        Assert.Equal(UnitsType.Millimeters, document.Header.InsUnits);
        Assert.Equal(6, entities.Count);

        var line = Assert.Single(entities.OfType<Line>());
        Assert.Equal(DxfExporter.VisibleLayer, line.Layer.Name);
        Assert.Equal(10, line.EndPoint.X, 9);

        var circle = Assert.Single(entities.OfType<Circle>(), e => e is not Arc);
        Assert.Equal(DxfExporter.HiddenLayer, circle.Layer.Name);
        Assert.Equal(2, circle.Radius, 9);

        var arc = Assert.Single(entities.OfType<Arc>());
        Assert.Equal(3 * Math.PI / 2, arc.StartAngle, 9); // -π/2 normalised into [0, 2π)
        Assert.Equal(Math.PI / 2, arc.EndAngle, 9);

        var ellipses = entities.OfType<Ellipse>().ToList();
        Assert.Equal(2, ellipses.Count);
        var full = Assert.Single(ellipses, e => e.Layer.Name == DxfExporter.TangentLayer);
        Assert.True(full.IsFullEllipse);
        Assert.Equal(0.25, full.RadiusRatio, 9);
        var partial = Assert.Single(ellipses, e => e.Layer.Name == DxfExporter.VisibleLayer);
        Assert.Equal((2 * Math.PI) - 1, partial.StartParameter, 9);
        Assert.Equal(1, partial.EndParameter, 9);

        var polyline = Assert.Single(entities.OfType<LwPolyline>());
        Assert.Equal(3, polyline.Vertices.Count);
        Assert.Equal(DxfExporter.HiddenLayer, polyline.Layer.Name);
    }

    [Fact]
    public void CreateDocument_HiddenLayerIsDashed()
    {
        var document = DxfExporter.CreateDocument([_view]);

        var hidden = document.Layers[DxfExporter.HiddenLayer];
        Assert.Equal("DASHED", hidden.LineType.Name);
        Assert.True(hidden.LineType.Segments.Count() >= 2);
    }

    [Fact]
    public void Write_MissingDirectory_ThrowsIOException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "out.dxf");

        Assert.ThrowsAny<IOException>(() => DxfExporter.Write(path, [_view]));
    }

    public void Dispose() => File.Delete(_path);
}
