using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

public class TessellationTests
{
    private static double Diagonal(float[] positions)
    {
        var (min, max) = (new double[] { double.MaxValue, double.MaxValue, double.MaxValue }, new double[] { double.MinValue, double.MinValue, double.MinValue });
        for (var i = 0; i < positions.Length; i++)
        {
            min[i % 3] = Math.Min(min[i % 3], positions[i]);
            max[i % 3] = Math.Max(max[i % 3], positions[i]);
        }

        return Math.Sqrt(Enumerable.Range(0, 3).Sum(axis => (max[axis] - min[axis]) * (max[axis] - min[axis])));
    }

    private readonly OcctStepReader _reader = new();

    [Fact]
    public void Tessellate_CoversEveryFace()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var mesh = document.Tessellate(TessellationOptions.Default, cancellationToken: TestContext.Current.CancellationToken);

        var facesWithTriangles = mesh.TriangleFaceIds.ToArray().Distinct().Order().ToArray();
        Assert.Equal(Enumerable.Range(0, document.Statistics.FaceCount), facesWithTriangles);
    }

    [Fact]
    public void Tessellate_ProducesValidIndicesAndUnitNormals()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var mesh = document.Tessellate(TessellationOptions.Default, cancellationToken: TestContext.Current.CancellationToken);

        Assert.All(mesh.Indices.ToArray(), index => Assert.True(index < mesh.VertexCount));
        var normals = mesh.Normals.Span;
        for (var v = 0; v < mesh.VertexCount; v++)
        {
            var length = MathF.Sqrt((normals[v * 3] * normals[v * 3]) + (normals[(v * 3) + 1] * normals[(v * 3) + 1]) + (normals[(v * 3) + 2] * normals[(v * 3) + 2]));
            Assert.InRange(length, 0.99f, 1.01f);
        }
    }

    [Fact]
    public void Tessellate_FinerOptions_ProduceMoreTriangles()
    {
        TestFiles.RequireNativeLibrary();

        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var coarse = document.Tessellate(TessellationOptions.Coarse, cancellationToken: TestContext.Current.CancellationToken);
        var fine = document.Tessellate(TessellationOptions.Fine, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(fine.TriangleCount > coarse.TriangleCount, $"fine={fine.TriangleCount}, coarse={coarse.TriangleCount}");
    }

    [Fact]
    public void Tessellate_CoarserAfterFiner_ReplacesTriangulation()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        var coarse = document.Tessellate(TessellationOptions.Coarse, cancellationToken: TestContext.Current.CancellationToken);
        var fine = document.Tessellate(TessellationOptions.Fine, cancellationToken: TestContext.Current.CancellationToken);

        var coarseAgain = document.Tessellate(TessellationOptions.Coarse, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(coarseAgain.TriangleCount < fine.TriangleCount, $"fine={fine.TriangleCount}, coarse again={coarseAgain.TriangleCount}");
        Assert.Equal(coarse.TriangleCount, coarseAgain.TriangleCount);
    }

    [Fact]
    public void Bounds_ContainTheMesh()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var bounds = document.Bounds;
        var positions = document.Tessellate(TessellationOptions.Default, cancellationToken: TestContext.Current.CancellationToken).Positions.ToArray();

        Assert.NotNull(bounds);
        Assert.InRange(bounds.Diagonal, 10, 1000);
        const double tolerance = 1e-3;
        for (var i = 0; i < positions.Length; i += 3)
        {
            Assert.InRange(positions[i], bounds.MinX - tolerance, bounds.MaxX + tolerance);
            Assert.InRange(positions[i + 1], bounds.MinY - tolerance, bounds.MaxY + tolerance);
            Assert.InRange(positions[i + 2], bounds.MinZ - tolerance, bounds.MaxZ + tolerance);
        }
    }

    [Fact]
    public void Tessellate_LargerMinSize_ProducesFewerTriangles()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        var fine = TessellationOptions.Fine;

        var unlimited = document.Tessellate(fine, cancellationToken: TestContext.Current.CancellationToken);
        var limited = document.Tessellate(fine with { MinSize = 1.0 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(limited.TriangleCount < unlimited.TriangleCount / 2, $"unlimited={unlimited.TriangleCount}, limited={limited.TriangleCount}");
    }

    [Fact]
    public void Bounds_MatchTheMeshTightly()
    {
        // The box comes from the edges; it must not be inflated by surface control points.
        TestFiles.RequireNativeLibrary();
        foreach (var file in new[] { TestFiles.CableGland, TestFiles.TestAssembly })
        {
            using var document = _reader.Load(file, cancellationToken: TestContext.Current.CancellationToken);
            var bounds = document.Bounds!;
            var positions = document.Tessellate(TessellationOptions.Fine, cancellationToken: TestContext.Current.CancellationToken).Positions.ToArray();

            var meshDiagonal = Diagonal(positions);
            Assert.InRange(bounds.Diagonal, meshDiagonal * 0.99, meshDiagonal * 1.01);
        }
    }

    [Theory]
    [InlineData(DisplayQuality.Coarse)]
    [InlineData(DisplayQuality.Normal)]
    [InlineData(DisplayQuality.Fine)]
    public void Tessellate_ForModel_StaysWithinReasonableTriangleCount(DisplayQuality quality)
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var mesh = document.Tessellate(TessellationOptions.ForModel(quality, document.Bounds), cancellationToken: TestContext.Current.CancellationToken);

        Assert.InRange(mesh.TriangleCount, 1_000, 500_000);
    }
}
