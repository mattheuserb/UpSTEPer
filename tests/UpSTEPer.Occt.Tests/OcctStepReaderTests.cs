using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

public class OcctStepReaderTests
{
    private readonly OcctStepReader _reader = new();

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => _reader.Load("does-not-exist.stp", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Load_NonStepFile_ThrowsStepImportException()
    {
        TestFiles.RequireNativeLibrary();
        var path = Path.Combine(Path.GetTempPath(), $"upsteper-{Guid.NewGuid():N}.stp");
        File.WriteAllText(path, "this is not ISO-10303-21");
        try
        {
            Assert.Throws<StepImportException>(() => _reader.Load(path, cancellationToken: TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_CableGland_ReportsTopology()
    {
        TestFiles.RequireNativeLibrary();

        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, document.Statistics.SolidCount);
        Assert.Equal(109, document.Statistics.FaceCount);
    }

    [Fact]
    public void Load_CableGland_EveryFaceHasBodyColour()
    {
        TestFiles.RequireNativeLibrary();
        // COLOUR_RGB('',0.76,0.79,0.77) applied to the solid in the file.
        const float Tolerance = 0.01f;

        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        for (var i = 0; i < document.Statistics.FaceCount; i++)
        {
            var colour = document.GetFaceColor(new FaceId(i));
            Assert.NotNull(colour);
            Assert.InRange(colour.Value.R, 0.76f - Tolerance, 0.76f + Tolerance);
            Assert.InRange(colour.Value.G, 0.79f - Tolerance, 0.79f + Tolerance);
            Assert.InRange(colour.Value.B, 0.77f - Tolerance, 0.77f + Tolerance);
        }
    }

    [Fact]
    public void GetFaceColor_OutOfRange_Throws()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetFaceColor(new FaceId(document.Statistics.FaceCount)));
    }

    [Fact]
    public void Dispose_ThenAccess_ThrowsObjectDisposed()
    {
        TestFiles.RequireNativeLibrary();
        var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        document.Dispose();

        Assert.Throws<ObjectDisposedException>(() => document.Statistics);
    }
}
