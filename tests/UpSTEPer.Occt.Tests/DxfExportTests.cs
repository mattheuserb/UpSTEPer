using ACadSharp.Entities;
using ACadSharp.IO;
using UpSTEPer.Core.Drawing;
using UpSTEPer.Export.Dxf;

namespace UpSTEPer.Occt.Tests;

/// <summary>End to end: STEP → hidden-line views → DXF → read back with ACadSharp.</summary>
public sealed class DxfExportTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"upsteper-{Guid.NewGuid():N}.dxf");

    [Fact]
    public void CableGland_StandardViews_ProduceValidDxfWithTrueCircles()
    {
        TestFiles.RequireNativeLibrary();
        using (var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken))
        {
            var views = DrawingBuilder.Build(document, DrawingBuilder.StandardViews, ProjectionOptions.Default);
            DxfExporter.Write(_path, views);
        }

        var entities = DxfReader.Read(_path).Entities.ToList();

        // The front view looks along the gland's axis: its six visible outlines are exact circles.
        Assert.Equal(6, entities.OfType<Circle>().Count(e => e is not Arc && e.Layer.Name == DxfExporter.VisibleLayer));
        Assert.Contains(entities, e => e is Line && e.Layer.Name == DxfExporter.HiddenLayer);
        Assert.Contains(entities, e => e is Ellipse);
        Assert.All(entities.OfType<Ellipse>(), e => Assert.InRange(e.RadiusRatio, 1e-6, 1.0));

        // Scale 1:1 in millimetres: the gland's outer diameter is 18 mm.
        Assert.Equal(9.0, entities.OfType<Circle>().Where(e => e is not Arc).Max(c => c.Radius), 6);
    }

    [Fact]
    public void Export_HasNoFrameTitleBlockOrPaperSpaceContent()
    {
        TestFiles.RequireNativeLibrary();
        using (var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken))
        {
            DxfExporter.Write(_path, DrawingBuilder.Build(document, [ViewDirection.Front], ProjectionOptions.Default));
        }

        var dxf = DxfReader.Read(_path);

        Assert.DoesNotContain(dxf.Entities, e => e is TextEntity or MText or Insert);
        Assert.All(dxf.Entities, e => Assert.Contains(e.Layer.Name, new[] { DxfExporter.VisibleLayer, DxfExporter.HiddenLayer, DxfExporter.TangentLayer }));
    }

    [Fact]
    public void Cleanup_RemovesHiddenEdgesCoveredByVisibleOnes()
    {
        TestFiles.RequireNativeLibrary();
        using var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        var raw = document.Project(ViewDirection.Top, ProjectionOptions.Default);

        var cleaned = HiddenLineCleanup.RemoveCoveredHidden(raw);

        Assert.True(cleaned.Curves.Count(c => c.Kind == LineKind.Hidden) < raw.Curves.Count(c => c.Kind == LineKind.Hidden));
        Assert.Equal(raw.Curves.Count(c => c.Kind == LineKind.Visible), cleaned.Curves.Count(c => c.Kind == LineKind.Visible));
    }

    public void Dispose() => File.Delete(_path);
}
