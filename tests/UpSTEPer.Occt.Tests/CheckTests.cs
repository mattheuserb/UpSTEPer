using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

public class CheckTests
{
    private readonly OcctStepReader _reader = new();

    [Fact]
    public void Check_DamagedParts_FindsEachBuiltInDefect()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.DamagedParts, cancellationToken: TestContext.Current.CancellationToken);

        var parts = document.Check().ToDictionary(p => p.Name);

        Assert.Equal(["Healthy box", "Unsewn box", "Split cylinder", "Spline cylinder"], parts.Keys);
        Assert.False(parts["Healthy box"].HasProblems);
        Assert.Equal(new PartCheck("Unsewn box", 0, 6, 24, 0, 0, 0, 0, 0), parts["Unsewn box"]);
        // Splitting left B-spline curves where the cutting planes meet the cylinder: circles really.
        Assert.Equal(new PartCheck("Split cylinder", 1, 6, 0, 0, 0, 0, 3, 0, SplineEdges: 3, CanonicalEdges: 3), parts["Split cylinder"]);
        Assert.Equal(new PartCheck("Spline cylinder", 1, 3, 0, 0, 3, 3, 0, 0, SplineEdges: 3, CanonicalEdges: 3), parts["Spline cylinder"]);
    }

    [Fact]
    public void Check_HealthyFile_ReportsNoProblems()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly, cancellationToken: TestContext.Current.CancellationToken);

        var parts = document.Check();

        Assert.NotEmpty(parts);
        Assert.All(parts, part => Assert.False(part.HasProblems, part.ToString()));
    }

    [Fact]
    public void Check_CableGland_FindsCirclesAndLinesStoredAsSplines()
    {
        // 38 spline edges; 26 are exact circles and lines, 12 only resemble circles (0.04 mm off,
        // beyond their edge tolerance) and stay splines.
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var part = document.Check().Single();

        Assert.Equal(38, part.SplineEdges);
        Assert.Equal(26, part.CanonicalEdges);
        Assert.Equal(0, part.FreeEdges + part.InvalidShapes + part.SplineFaces + part.MergeableFaces + part.SmallEdges + part.FailedChecks);
    }

    [Fact]
    public void Check_AssemblyListsEachPartDefinitionOnce()
    {
        // The pin assembly is placed twice; its parts must not be checked twice.
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["Base plate", "Pin", "Washer"], document.Check().Select(p => p.Name));
    }

    [Fact]
    public void Check_TightTolerance_RecognisesNoCanonicalSplines()
    {
        // Recognition is tolerance-driven; the converted cylinder is exact, so even 1e-6 mm fits.
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.DamagedParts, cancellationToken: TestContext.Current.CancellationToken);

        var spline = document.Check(1e-6).Single(p => p.Name == "Spline cylinder");

        Assert.Equal(3, spline.SplineFaces);
        Assert.InRange(spline.CanonicalFaces, 0, 3);
        Assert.Equal(3, spline.SplineEdges);
        Assert.InRange(spline.CanonicalEdges, 0, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Check_InvalidTolerance_Throws(double tolerance)
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.Check(tolerance));
    }
}
