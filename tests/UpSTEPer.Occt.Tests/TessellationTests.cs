using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

public class TessellationTests
{
    private readonly OcctStepReader _reader = new();

    [Fact]
    public void Tessellate_CoversEveryFace()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland);

        var mesh = document.Tessellate(TessellationOptions.Default);

        var facesWithTriangles = mesh.TriangleFaceIds.ToArray().Distinct().Order().ToArray();
        Assert.Equal(Enumerable.Range(0, document.Statistics.FaceCount), facesWithTriangles);
    }

    [Fact]
    public void Tessellate_ProducesValidIndicesAndUnitNormals()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland);

        var mesh = document.Tessellate(TessellationOptions.Default);

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

        using var document = _reader.Load(TestFiles.CableGland);

        var coarse = document.Tessellate(TessellationOptions.Coarse);
        var fine = document.Tessellate(TessellationOptions.Fine);

        Assert.True(fine.TriangleCount > coarse.TriangleCount, $"fine={fine.TriangleCount}, coarse={coarse.TriangleCount}");
    }

    [Fact]
    public void Tessellate_CoarserAfterFiner_ReplacesTriangulation()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland);
        var coarse = document.Tessellate(TessellationOptions.Coarse);
        var fine = document.Tessellate(TessellationOptions.Fine);

        var coarseAgain = document.Tessellate(TessellationOptions.Coarse);

        Assert.True(coarseAgain.TriangleCount < fine.TriangleCount, $"fine={fine.TriangleCount}, coarse again={coarseAgain.TriangleCount}");
        Assert.Equal(coarse.TriangleCount, coarseAgain.TriangleCount);
    }

    [Fact]
    public void Bounds_ContainTheMesh()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland);

        var bounds = document.Bounds;
        var positions = document.Tessellate(TessellationOptions.Default).Positions.ToArray();

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

    [Theory]
    [InlineData(DisplayQuality.Coarse)]
    [InlineData(DisplayQuality.Normal)]
    [InlineData(DisplayQuality.Fine)]
    public void Tessellate_ForModel_StaysWithinReasonableTriangleCount(DisplayQuality quality)
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland);

        var mesh = document.Tessellate(TessellationOptions.ForModel(quality, document.Bounds));

        Assert.InRange(mesh.TriangleCount, 1_000, 500_000);
    }
}
