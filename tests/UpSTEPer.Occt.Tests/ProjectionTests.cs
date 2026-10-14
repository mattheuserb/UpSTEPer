using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Occt.Tests;

public class ProjectionTests(ITestOutputHelper output)
{
    private readonly OcctStepReader _reader = new();

    [Theory]
    [InlineData(ViewDirection.Front)]
    [InlineData(ViewDirection.Top)]
    [InlineData(ViewDirection.Left)]
    [InlineData(ViewDirection.Iso)]
    public void Project_CableGland_KeepsCirclesAnalytic(ViewDirection direction)
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var view = document.Project(direction, ProjectionOptions.Default);

        var counts = view.Curves.GroupBy(c => $"{c.GetType().Name}/{c.Kind}").OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}");
        output.WriteLine($"{direction}: {string.Join(", ", counts)}; bounds {view.GetBounds()}");

        Assert.Equal(direction, view.Direction);
        Assert.Contains(view.Curves, c => c.Kind == LineKind.Visible);
        Assert.Contains(view.Curves, c => c.Kind == LineKind.Hidden);
        Assert.Contains(view.Curves, c => c is CircleCurve or ArcCurve or EllipseCurve);

        // Circles seen edge-on must come out as lines, not as degenerate ellipses (invalid in DXF).
        Assert.All(view.Curves.OfType<EllipseCurve>(), e => Assert.InRange(e.Ratio, 1e-6, 1.0));
    }

    [Theory]
    [InlineData(ViewDirection.Front)]
    [InlineData(ViewDirection.Top)]
    [InlineData(ViewDirection.Left)]
    [InlineData(ViewDirection.Iso)]
    public void Project_CableGland_ReturnsStraightSplinesAsLines(ViewDirection direction)
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var view = document.Project(direction, ProjectionOptions.Default);

        // HLR returns many straight edges as B-splines (degree-1 approximations with 15 poles); before
        // they were classified, about two thirds of all curves in these views were polylines.
        var polylines = view.Curves.OfType<PolylineCurve>().ToList();
        output.WriteLine($"{direction}: {polylines.Count} polylines of {view.Curves.Count} curves");
        Assert.DoesNotContain(polylines, IsStraight);
        Assert.InRange(polylines.Count, 0, view.Curves.Count / 5);
    }

    [Fact]
    public void Project_ViewsAgreeOnSharedDimensions()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var front = document.Project(ViewDirection.Front, ProjectionOptions.Default).GetBounds();
        var top = document.Project(ViewDirection.Top, ProjectionOptions.Default).GetBounds();
        var left = document.Project(ViewDirection.Left, ProjectionOptions.Default).GetBounds();

        // First-angle relationships: front and top share the width (model X),
        // front and left share the height (model Z).
        Assert.Equal(front.Width, top.Width, 3);
        Assert.Equal(front.Height, left.Height, 3);
    }

    [Fact]
    public void Project_WithoutHidden_HasNoHiddenLines()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var view = document.Project(ViewDirection.Front, ProjectionOptions.Default with { IncludeHidden = false });

        Assert.DoesNotContain(view.Curves, c => c.Kind == LineKind.Hidden);
        Assert.NotEmpty(view.Curves);
    }

    [Fact]
    public void Project_InvalidDeflection_Throws()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Throws<ArgumentException>(() => document.Project(ViewDirection.Front, ProjectionOptions.Default with { Deflection = 0 }));
    }

    private static bool IsStraight(PolylineCurve polyline)
    {
        var start = polyline.Points[0];
        var end = polyline.Points[^1];
        var length = Math.Sqrt(((end.X - start.X) * (end.X - start.X)) + ((end.Y - start.Y) * (end.Y - start.Y)));
        return length > 1e-9 && polyline.Points.All(p => Math.Abs(((end.X - start.X) * (p.Y - start.Y)) - ((end.Y - start.Y) * (p.X - start.X))) / length <= 1e-6);
    }
}
