using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Types.Units;
using CSMath;
using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Export.Dxf;

/// <summary>
/// Writes arranged 2D views as an ASCII DXF in millimetres at scale 1:1 (model coordinates, no
/// scaling), without drawing frame or title block (project decision, ADR 0006). Each line kind
/// goes to its own layer:
/// <c>VISIBLE</c> (continuous, 0.5 mm), <c>HIDDEN</c> (dashed, 0.25 mm), <c>TANGENT</c> (thin).
/// Circles, arcs and ellipses are written as true DXF entities, never as polylines.
/// </summary>
public static class DxfExporter
{
    /// <summary>Layer name for visible edges.</summary>
    public const string VisibleLayer = "VISIBLE";

    /// <summary>Layer name for hidden edges.</summary>
    public const string HiddenLayer = "HIDDEN";

    /// <summary>Layer name for tangent edges.</summary>
    public const string TangentLayer = "TANGENT";

    /// <summary>Writes the views to <paramref name="path"/> (overwritten).</summary>
    /// <param name="path">The DXF file.</param>
    /// <param name="views">Views in sheet coordinates, e.g. from <see cref="DrawingBuilder.Build"/>.</param>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public static void Write(string path, IReadOnlyList<ProjectedView> views)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(views);

        var document = CreateDocument(views);
        try
        {
            using var writer = new DxfWriter(path, document, binary: false);
            writer.Write();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or DirectoryNotFoundException)
        {
            throw new IOException($"Cannot write DXF file '{path}': {exception.Message}", exception);
        }
    }

    /// <summary>Builds the DXF document in memory (exposed for tests).</summary>
    /// <param name="views">Views in sheet coordinates.</param>
    /// <returns>The document.</returns>
    internal static CadDocument CreateDocument(IReadOnlyList<ProjectedView> views)
    {
        var document = new CadDocument();
        document.Header.InsUnits = UnitsType.Millimeters;

        var dashed = new LineType("DASHED") { Description = "Dashed __ __ __" };
        dashed.AddSegment(new LineType.Segment { Length = 3.0 });
        dashed.AddSegment(new LineType.Segment { Length = -1.5 });
        document.LineTypes.Add(dashed);

        var layers = new Dictionary<LineKind, Layer>
        {
            [LineKind.Visible] = new Layer(VisibleLayer) { Color = new Color(7), LineWeight = LineWeightType.W50 },
            [LineKind.Hidden] = new Layer(HiddenLayer) { Color = new Color(8), LineWeight = LineWeightType.W25, LineType = dashed },
            [LineKind.Tangent] = new Layer(TangentLayer) { Color = new Color(9), LineWeight = LineWeightType.W13 },
        };
        foreach (var layer in layers.Values)
        {
            document.Layers.Add(layer);
        }

        foreach (var curve in views.SelectMany(v => v.Curves))
        {
            var entity = ToEntity(curve);
            entity.Layer = layers[curve.Kind];
            document.Entities.Add(entity);
        }

        return document;
    }

    private static Entity ToEntity(DrawingCurve curve) => curve switch
    {
        LineSegment line => new Line(ToXyz(line.Start), ToXyz(line.End)),
        CircleCurve circle => new Circle(ToXyz(circle.Center), circle.Radius),
        ArcCurve arc => new Arc(ToXyz(arc.Center), arc.Radius, Normalize(arc.StartAngle), Normalize(arc.EndAngle)),
        EllipseCurve ellipse => new Ellipse
        {
            Center = ToXyz(ellipse.Center),
            MajorAxisEndPoint = ToXyz(ellipse.MajorAxis),
            RadiusRatio = ellipse.Ratio,
            StartParameter = Normalize(ellipse.StartParameter),
            EndParameter = ellipse.EndParameter - ellipse.StartParameter >= (2 * Math.PI) - 1e-9
                ? Normalize(ellipse.StartParameter) + (2 * Math.PI)
                : Normalize(ellipse.EndParameter),
        },
        PolylineCurve polyline => new LwPolyline(polyline.Points.Select(p => new LwPolyline.Vertex(p.X, p.Y))),
        _ => throw new NotSupportedException($"Curve type {curve.GetType().Name} is not supported."),
    };

    private static XYZ ToXyz(Point2 point) => new(point.X, point.Y, 0);

    // DXF angles and ellipse parameters are in [0, 2π).
    private static double Normalize(double angle)
    {
        var normalized = angle % (2 * Math.PI);
        return normalized < 0 ? normalized + (2 * Math.PI) : normalized;
    }
}
