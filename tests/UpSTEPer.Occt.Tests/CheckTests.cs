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
        Assert.Equal(new PartCheck("Split cylinder", 1, 6, 0, 0, 0, 0, 3, 0), parts["Split cylinder"]);
        Assert.Equal(new PartCheck("Spline cylinder", 1, 3, 0, 0, 3, 3, 0, 0), parts["Spline cylinder"]);
    }

    [Fact]
    public void Check_HealthyFiles_ReportNoProblems()
    {
        TestFiles.RequireNativeLibrary();
        foreach (var file in new[] { TestFiles.CableGland, TestFiles.TestAssembly })
        {
            using var document = _reader.Load(file, cancellationToken: TestContext.Current.CancellationToken);

            var parts = document.Check();

            Assert.NotEmpty(parts);
            Assert.All(parts, part => Assert.False(part.HasProblems, $"{Path.GetFileName(file)}: {part}"));
        }
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
