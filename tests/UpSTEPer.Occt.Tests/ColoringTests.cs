using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

public sealed class ColoringTests : IDisposable
{
    private static readonly ColorRgb _red = ColorRgb.FromHex("#FF0000");
    private static readonly ColorRgb _blue = ColorRgb.FromHex("#3366CC");
    private static readonly FaceId _face = new(80);
    private static readonly BodyId _body = new(0);

    private readonly OcctStepReader _reader = new();
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void GetBodyOfFace_EveryFaceBelongsToTheSingleBody()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        for (var i = 0; i < document.Statistics.FaceCount; i++)
        {
            Assert.Equal(_body, document.GetBodyOfFace(new FaceId(i)));
        }
    }

    [Fact]
    public void GetAssignedColor_FileColourIsOnBodyNotOnFaces()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(document.GetAssignedColor(ColorTarget.Of(_face)));
        var bodyColor = document.GetAssignedColor(ColorTarget.Of(_body));
        Assert.NotNull(bodyColor);
        Assert.InRange(bodyColor.Value.R, 0.75f, 0.77f);
    }

    [Fact]
    public void SetColor_Face_ChangesOnlyThatFace_AndClearingRestoresBodyColour()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        var original = document.GetFaceColor(_face);

        document.SetColor(ColorTarget.Of(_face), _red);

        Assert.Equal(_red.ToHex(), document.GetFaceColor(_face)?.ToHex());
        Assert.Equal(_red.ToHex(), document.GetAssignedColor(ColorTarget.Of(_face))?.ToHex());
        Assert.Equal(1, CountFaces(document, _red));

        document.SetColor(ColorTarget.Of(_face), null);

        Assert.Equal(original, document.GetFaceColor(_face));
        Assert.Null(document.GetAssignedColor(ColorTarget.Of(_face)));
    }

    [Fact]
    public void SetColor_Body_ColoursFacesWithoutOwnColour()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        var faceCount = document.Statistics.FaceCount;
        document.SetColor(ColorTarget.Of(_face), _red);

        document.SetColor(ColorTarget.Of(_body), _blue);

        Assert.Equal(faceCount - 1, CountFaces(document, _blue));
        Assert.Equal(1, CountFaces(document, _red));
    }

    [Fact]
    public void SaveStep_ThenReload_KeepsFaceAndBodyColours()
    {
        TestFiles.RequireNativeLibrary();
        var path = TempPath();
        int faceCount;
        using (var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken))
        {
            faceCount = document.Statistics.FaceCount;
            document.SetColor(ColorTarget.Of(_face), _red);
            document.SetColor(ColorTarget.Of(_body), _blue);
            document.SaveStep(path);
        }

        using var reloaded = _reader.Load(path, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(faceCount, reloaded.Statistics.FaceCount);
        Assert.Equal(1, reloaded.Statistics.SolidCount);
        Assert.Equal(1, CountFaces(reloaded, _red));
        Assert.Equal(faceCount - 1, CountFaces(reloaded, _blue));
    }

    [Fact]
    public void SaveStep_UnwritablePath_ThrowsIOException()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "out.stp");

        Assert.Throws<IOException>(() => document.SaveStep(path));
    }

    [Theory]
    [InlineData(ColorTargetKind.Face, -1)]
    [InlineData(ColorTargetKind.Face, 109)]
    [InlineData(ColorTargetKind.Body, 1)]
    public void SetColor_UnknownTarget_Throws(ColorTargetKind kind, int id)
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Throws<ArgumentOutOfRangeException>(() => document.SetColor(new ColorTarget(kind, id), _red));
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            File.Delete(file);
        }
    }

    // Compares as hex: colours pass through OCCT's sRGB/linear conversion.
    private static int CountFaces(IStepDocument document, ColorRgb color) =>
        Enumerable.Range(0, document.Statistics.FaceCount)
            .Count(i => document.GetFaceColor(new FaceId(i))?.ToHex() == color.ToHex());

    private string TempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"upsteper-{Guid.NewGuid():N}.stp");
        _tempFiles.Add(path);
        return path;
    }
}
