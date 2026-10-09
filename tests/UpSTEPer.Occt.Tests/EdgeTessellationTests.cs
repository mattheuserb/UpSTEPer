using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

public class EdgeTessellationTests
{
    [Fact]
    public void TessellateEdges_CableGland_ProducesConsistentLines()
    {
        TestFiles.RequireNativeLibrary();
        using var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var edges = document.TessellateEdges(TessellationOptions.Default);

        Assert.True(edges.SegmentCount > 0);
        Assert.All(edges.Indices.ToArray(), index => Assert.True(index < edges.VertexCount));
        Assert.All(edges.SegmentEdgeIds.ToArray(), id => Assert.InRange(id, 0, document.Statistics.EdgeCount - 1));
    }

    [Fact]
    public void TessellateEdges_SkipsCylinderSeams()
    {
        TestFiles.RequireNativeLibrary();
        using var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var drawnEdges = document.TessellateEdges(TessellationOptions.Default).SegmentEdgeIds.ToArray().Distinct().Count();

        // The gland has dozens of cylindrical/conical faces, each with a seam edge that must not be drawn.
        Assert.True(drawnEdges < document.Statistics.EdgeCount, $"{drawnEdges} of {document.Statistics.EdgeCount} edges drawn");
    }

    [Fact]
    public void TessellateEdges_FinerDeflection_ProducesMoreSegments()
    {
        TestFiles.RequireNativeLibrary();
        using var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var coarse = document.TessellateEdges(TessellationOptions.Coarse).SegmentCount;
        var fine = document.TessellateEdges(TessellationOptions.Fine).SegmentCount;

        Assert.True(fine > coarse, $"fine={fine}, coarse={coarse}");
    }
}
