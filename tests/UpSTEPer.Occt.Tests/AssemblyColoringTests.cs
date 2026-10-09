using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

/// <summary>
/// Colouring inside assemblies colours only the clicked instance (project decision 2026-10-08):
/// a shared definition is copied for that instance first ("make independent").
/// </summary>
public sealed class AssemblyColoringTests : IDisposable
{
    private static readonly ColorRgb _red = ColorRgb.FromHex("#FF0000");
    private readonly OcctStepReader _reader = new();
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"upsteper-{Guid.NewGuid():N}.stp");

    [Fact]
    public void ColorFace_InSharedInstance_ChangesOnlyThatFace()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly, cancellationToken: TestContext.Current.CancellationToken);
        var (left, right) = PinBodies(document);
        var face = FacesOf(document, left)[0];
        var before = Snapshot(document);

        document.SetColor(ColorTarget.Of(face), _red);

        var after = Snapshot(document);
        Assert.Equal(_red.ToHex(), after[face.Value]);
        Assert.Equal(1, Enumerable.Range(0, after.Length).Count(i => after[i] != before[i]));
        Assert.All(FacesOf(document, right), f => Assert.Equal(before[f.Value], after[f.Value]));
        Assert.Equal(_red.ToHex(), document.GetAssignedColor(ColorTarget.Of(face))?.ToHex());
    }

    [Fact]
    public void ColorBody_InSharedInstance_ChangesOnlyThatBody()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly, cancellationToken: TestContext.Current.CancellationToken);
        var (left, right) = PinBodies(document);

        document.SetColor(ColorTarget.Of(left), _red);

        Assert.All(FacesOf(document, left), f => Assert.Equal(_red.ToHex(), document.GetFaceColor(f)?.ToHex()));
        Assert.All(FacesOf(document, right), f => Assert.NotEqual(_red.ToHex(), document.GetFaceColor(f)?.ToHex()));
    }

    [Fact]
    public void Colouring_KeepsIdsTopologyAndTree()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly, cancellationToken: TestContext.Current.CancellationToken);
        var statistics = document.Statistics;
        var bodiesBefore = Enumerable.Range(0, statistics.FaceCount).Select(i => document.GetBodyOfFace(new FaceId(i))).ToList();
        var (left, _) = PinBodies(document);

        document.SetColor(ColorTarget.Of(FacesOf(document, left)[0]), _red);

        Assert.Equal(statistics, document.Statistics);
        Assert.Equal(bodiesBefore, Enumerable.Range(0, statistics.FaceCount).Select(i => document.GetBodyOfFace(new FaceId(i))));
        Assert.Equal(Enumerable.Range(0, 7), document.GetModelTree().SelectMany(n => n.GetBodies()).Select(b => b.Value).Order());
    }

    [Fact]
    public void ClearColour_InInstance_RestoresInheritedColour()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly, cancellationToken: TestContext.Current.CancellationToken);
        var (left, _) = PinBodies(document);
        var face = FacesOf(document, left)[0];
        var original = document.GetFaceColor(face);

        document.SetColor(ColorTarget.Of(face), _red);
        document.SetColor(ColorTarget.Of(face), null);

        Assert.Equal(original, document.GetFaceColor(face));
        Assert.Null(document.GetAssignedColor(ColorTarget.Of(face)));
    }

    [Fact]
    public void SaveAndReload_KeepsInstanceColours()
    {
        TestFiles.RequireNativeLibrary();
        int faceCount;
        using (var document = _reader.Load(TestFiles.TestAssembly, cancellationToken: TestContext.Current.CancellationToken))
        {
            faceCount = document.Statistics.FaceCount;
            var (left, _) = PinBodies(document);
            document.SetColor(ColorTarget.Of(left), _red);
            document.SaveStep(_path);
        }

        using var reloaded = _reader.Load(_path, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(faceCount, reloaded.Statistics.FaceCount);
        Assert.Equal(7, reloaded.Statistics.SolidCount);
        var redFaces = Enumerable.Range(0, faceCount).Count(i => reloaded.GetFaceColor(new FaceId(i))?.ToHex() == _red.ToHex());
        var (reloadedLeft, _) = PinBodies(reloaded);
        Assert.Equal(FacesOf(reloaded, reloadedLeft).Count, redFaces);
    }

    public void Dispose() => File.Delete(_path);

    // The "Pin" bodies of the left and right pin assembly, located via the model tree.
    private static (BodyId Left, BodyId Right) PinBodies(IStepDocument document)
    {
        var top = Assert.Single(document.GetModelTree());
        BodyId Pin(string assembly) =>
            top.Children.Single(c => c.Name == assembly).Children.Single(c => c.Name == "Pin").GetBodies().Single();
        return (Pin("Pin assembly (left)"), Pin("Pin assembly (right)"));
    }

    private static List<FaceId> FacesOf(IStepDocument document, BodyId body) =>
        [.. Enumerable.Range(0, document.Statistics.FaceCount).Select(i => new FaceId(i)).Where(f => document.GetBodyOfFace(f) == body)];

    private static string?[] Snapshot(IStepDocument document) =>
        [.. Enumerable.Range(0, document.Statistics.FaceCount).Select(i => document.GetFaceColor(new FaceId(i))?.ToHex())];
}
