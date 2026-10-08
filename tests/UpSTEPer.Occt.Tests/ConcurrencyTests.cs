using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

/// <summary>
/// Regression tests for concurrent use of independent documents. OCCT's STEP translators use
/// global state; without serialisation in the native layer parallel loads crashed the process.
/// </summary>
public class ConcurrencyTests
{
    private const int Parallelism = 8;

    [Fact]
    public void LoadAndSave_IndependentDocumentsInParallel_DoNotCrash()
    {
        TestFiles.RequireNativeLibrary();
        var reader = new OcctStepReader();
        var paths = Enumerable.Range(0, Parallelism)
            .Select(_ => Path.Combine(Path.GetTempPath(), $"upsteper-{Guid.NewGuid():N}.stp"))
            .ToArray();

        try
        {
            var faceCounts = new int[Parallelism];
            Parallel.For(0, Parallelism, new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, i =>
            {
                using var document = reader.Load(TestFiles.CableGland);
                document.SetColor(ColorTarget.Of(new FaceId(i)), ColorRgb.FromHex("#FF0000"));
                _ = document.Tessellate(TessellationOptions.Coarse);
                document.SaveStep(paths[i]);
                faceCounts[i] = document.Statistics.FaceCount;
            });

            Assert.All(faceCounts, count => Assert.Equal(109, count));
            Assert.All(paths, path => Assert.True(new FileInfo(path).Length > 0));
        }
        finally
        {
            foreach (var path in paths)
            {
                File.Delete(path);
            }
        }
    }
}
