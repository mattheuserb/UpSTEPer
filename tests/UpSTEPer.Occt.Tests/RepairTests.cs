using UpSTEPer.Core;
using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Occt.Tests;

public sealed class RepairTests : IDisposable
{
    private static readonly ColorRgb _red = ColorRgb.FromHex("#FF0000");
    private static readonly ColorRgb _blue = ColorRgb.FromHex("#3366CC");
    private static readonly ColorRgb _yellow = ColorRgb.FromHex("#FFCC00");

    private readonly OcctStepReader _reader = new();
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"upsteper-repair-{Guid.NewGuid():N}.stp");

    [Fact]
    public void Repair_DamagedParts_SewsMergesConvertsAndReports()
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.DamagedParts);

        var report = document.Repair(RepairOptions.Default, cancellationToken: TestContext.Current.CancellationToken).ToDictionary(p => p.Name);

        Assert.False(report["Healthy box"].Changed);
        Assert.Equal(new PartRepair("Unsewn box", true, false, 6, 6, 0, 1, 24, 0, 0, 0, 0), report["Unsewn box"]);
        Assert.Equal(new PartRepair("Split cylinder", true, false, 6, 3, 1, 1, 0, 0, 0, 0, 1, CanonicalEdges: 3), report["Split cylinder"]);

        var spline = report["Spline cylinder"];
        Assert.True(spline.Changed);
        Assert.Equal(3, spline.CanonicalFaces);
        Assert.Equal(3, spline.CanonicalEdges); // two circles and the seam line
        Assert.InRange(spline.MaxDeviation, 0, 1e-6); // converted from an exact cylinder
        Assert.False(spline.Reverted);
    }

    [Fact]
    public void Repair_CanonicalOnly_LeavesNoCanonicalSplinesAndKeepsColour()
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.DamagedParts);

        document.Repair(new RepairOptions(RepairSteps.Canonical), cancellationToken: TestContext.Current.CancellationToken);

        var check = document.Check().Single(p => p.Name == "Spline cylinder");
        Assert.Equal(0, check.SplineFaces);
        Assert.Equal(0, check.SplineEdges);
        Assert.Equal(0, check.InvalidShapes);
        Assert.Equal(1, CountFaces(document, _yellow));
    }

    [Theory]
    [InlineData(ViewDirection.Top)]
    [InlineData(ViewDirection.Iso)]
    public void Repair_Canonical_MakesProjectedSplineEdgesAnalytic(ViewDirection direction)
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.DamagedParts);
        Assert.Contains(document.Project(direction, ProjectionOptions.Default).Curves, IsCurvedPolyline);

        document.Repair(new RepairOptions(RepairSteps.Canonical), cancellationToken: TestContext.Current.CancellationToken);

        // Circles and arcs of the converted edges now come out as circles, arcs or ellipses.
        var view = document.Project(direction, ProjectionOptions.Default);
        Assert.DoesNotContain(view.Curves, IsCurvedPolyline);
        Assert.Contains(view.Curves, c => c is CircleCurve or ArcCurve or EllipseCurve);
    }

    [Fact]
    public void Repair_CanonicalOnHealthyAnalyticModel_ChangesNothing()
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.TestAssembly);

        var report = document.Repair(new RepairOptions(RepairSteps.Canonical), cancellationToken: TestContext.Current.CancellationToken);

        Assert.All(report, part => Assert.Equal(0, part.CanonicalFaces));
        Assert.All(report, part => Assert.Equal(0, part.CanonicalEdges));
    }

    [Fact]
    public void Repair_RenumbersTopologyAndKeepsColours()
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.DamagedParts);
        Assert.Equal(21, document.Statistics.FaceCount);
        Assert.Equal(3, document.Statistics.SolidCount);

        document.Repair(RepairOptions.Default, cancellationToken: TestContext.Current.CancellationToken);

        // 6 + 6 + 3 + 3 faces; the unsewn box became a solid.
        Assert.Equal(18, document.Statistics.FaceCount);
        Assert.Equal(4, document.Statistics.SolidCount);
        Assert.Equal(4, document.GetModelTree().Single().Children.Sum(part => part.GetBodies().Count()));
        Assert.Equal(1, CountFaces(document, _red));    // top of the sewn box
        Assert.Equal(1, CountFaces(document, _blue));   // the merged cylinder side: blue covered the largest area
        Assert.Equal(1, CountFaces(document, _yellow)); // spline cylinder side, now an exact cylinder
        Assert.All(document.Check(), part => Assert.False(part.HasProblems, part.ToString()));
    }

    [Fact]
    public void Repair_ThenSaveAndReload_KeepsResultAndColours()
    {
        TestFiles.RequireNativeLibrary();
        using (var document = Load(TestFiles.DamagedParts))
        {
            document.Repair(RepairOptions.Default, cancellationToken: TestContext.Current.CancellationToken);
            document.SaveStep(_path);
        }

        using var reloaded = Load(_path);

        Assert.Equal(18, reloaded.Statistics.FaceCount);
        Assert.Equal(4, reloaded.Statistics.SolidCount);
        Assert.Equal(1, CountFaces(reloaded, _red));
        Assert.Equal(1, CountFaces(reloaded, _blue));
    }

    [Fact]
    public void Repair_OnlySewing_LeavesSplitFaces()
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.DamagedParts);

        var report = document.Repair(new RepairOptions(RepairSteps.Sew), cancellationToken: TestContext.Current.CancellationToken).ToDictionary(p => p.Name);

        Assert.True(report["Unsewn box"].Changed);
        Assert.False(report["Split cylinder"].Changed);
    }

    [Fact]
    public void Repair_HealthyFile_ChangesNothing()
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.TestAssembly);
        var faces = document.Statistics.FaceCount;
        var colours = Enumerable.Range(0, faces).Select(i => document.GetFaceColor(new FaceId(i))).ToArray();

        var report = document.Repair(RepairOptions.Default, cancellationToken: TestContext.Current.CancellationToken);

        Assert.All(report, part => Assert.False(part.Changed, part.ToString()));
        Assert.Equal(colours, Enumerable.Range(0, faces).Select(i => document.GetFaceColor(new FaceId(i))));
    }

    [Fact]
    public void Repair_CableGland_ConvertsOnlyExactSplineEdgesAndKeepsEverythingElse()
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.CableGland);
        var faces = document.Statistics.FaceCount;
        var colours = Enumerable.Range(0, faces).Select(i => document.GetFaceColor(new FaceId(i))).ToArray();

        var part = document.Repair(RepairOptions.Default, cancellationToken: TestContext.Current.CancellationToken).Single();

        Assert.True(part.Changed);
        Assert.False(part.Reverted);
        Assert.Equal(0, part.CanonicalFaces);
        Assert.Equal(26, part.CanonicalEdges);
        Assert.InRange(part.MaxDeviation, 0, 1e-4);
        Assert.Equal((faces, 0), (part.FacesAfter, part.InvalidAfter));
        Assert.Equal(colours, Enumerable.Range(0, faces).Select(i => document.GetFaceColor(new FaceId(i))));
        Assert.False(document.Check().Single().HasProblems);
    }

    [Fact]
    public void Repair_AfterColouringOneInstance_KeepsItsColourOnly()
    {
        // ADR 0009 copies the definition for the coloured instance; repair must not undo that.
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.TestAssembly);
        var top = document.GetModelTree().Single();
        var leftPin = top.Children.Single(c => c.Name == "Pin assembly (left)").Children.Single(c => c.Name == "Pin").GetBodies().Single();
        document.SetColor(ColorTarget.Of(leftPin), _red);
        var redFaces = CountFaces(document, _red);

        document.Repair(RepairOptions.Default, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, redFaces); // the left pin's three faces
        Assert.Equal(redFaces, CountFaces(document, _red));
    }

    [Fact]
    public void Repair_Cancelled_LeavesModelUnchanged()
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.DamagedParts);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var progress = new CancellingProgress(cancellation);

        Assert.ThrowsAny<OperationCanceledException>(() => document.Repair(RepairOptions.Default, progress, cancellation.Token));

        Assert.Equal(21, document.Statistics.FaceCount);
        Assert.Equal(24, document.Check().Single(p => p.Name == "Unsewn box").FreeEdges);
    }

    [Theory]
    [InlineData(RepairSteps.All, 0)]
    [InlineData(RepairSteps.All, double.NaN)]
    [InlineData((RepairSteps)16, 0.05)]
    public void Repair_InvalidOptions_Throw(RepairSteps steps, double tolerance)
    {
        TestFiles.RequireNativeLibrary();
        using var document = Load(TestFiles.TestAssembly);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Repair(new RepairOptions(steps, tolerance), cancellationToken: TestContext.Current.CancellationToken));
    }

    public void Dispose() => File.Delete(_path);

    private static bool IsCurvedPolyline(DrawingCurve curve)
    {
        if (curve is not PolylineCurve polyline || polyline.Points.Count < 3)
        {
            return false;
        }

        var start = polyline.Points[0];
        var end = polyline.Points[^1];
        var length = Math.Sqrt(((end.X - start.X) * (end.X - start.X)) + ((end.Y - start.Y) * (end.Y - start.Y)));
        return length < 1e-9 || polyline.Points.Any(p => Math.Abs(((end.X - start.X) * (p.Y - start.Y)) - ((end.Y - start.Y) * (p.X - start.X))) / length > 1e-3);
    }

    private static int CountFaces(IStepDocument document, ColorRgb color) =>
        Enumerable.Range(0, document.Statistics.FaceCount).Count(i => document.GetFaceColor(new FaceId(i))?.ToHex() == color.ToHex());

    private IStepDocument Load(string path) => _reader.Load(path, cancellationToken: TestContext.Current.CancellationToken);

    private sealed class CancellingProgress(CancellationTokenSource cancellation) : IProgress<OperationProgress>
    {
        public void Report(OperationProgress value) => cancellation.Cancel();
    }
}
