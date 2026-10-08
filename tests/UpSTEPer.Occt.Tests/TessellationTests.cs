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

        // Separate documents: OCCT keeps an existing finer triangulation.
        using var coarseDocument = _reader.Load(TestFiles.CableGland);
        using var fineDocument = _reader.Load(TestFiles.CableGland);

        var coarse = coarseDocument.Tessellate(TessellationOptions.Coarse);
        var fine = fineDocument.Tessellate(TessellationOptions.Fine);

        Assert.True(fine.TriangleCount > coarse.TriangleCount, $"fine={fine.TriangleCount}, coarse={coarse.TriangleCount}");
    }
}
