using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

public class MainViewModelTests
{
    [Fact]
    public async Task LoadAsync_Success_SetsSceneTitleAndStatus()
    {
        var viewModel = new MainViewModel(new StubReader(_ => new StubDocument()));

        var loaded = await viewModel.LoadAsync("bracket.stp");

        Assert.True(loaded);
        Assert.NotNull(viewModel.Scene);
        Assert.Equal("bracket.stp - UpSTEPer", viewModel.Title);
        Assert.Contains("2 faces", viewModel.Status, StringComparison.Ordinal);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoadAsync_ImportError_ShowsErrorAndKeepsPreviousScene()
    {
        var viewModel = new MainViewModel(new StubReader(_ => throw new StepImportException("not STEP")));

        var loaded = await viewModel.LoadAsync("broken.stp");

        Assert.False(loaded);
        Assert.Null(viewModel.Scene);
        Assert.Equal("Error: not STEP", viewModel.Status);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SelectedFace_DescribesFaceAndColour()
    {
        var viewModel = new MainViewModel(new StubReader(_ => new StubDocument()));
        await viewModel.LoadAsync("bracket.stp");

        viewModel.SelectedFace = new FaceId(0);
        Assert.Equal("Selected: face 0, colour #FF0000", viewModel.SelectionInfo);

        viewModel.SelectedFace = new FaceId(1);
        Assert.Equal("Selected: face 1, colour none (default grey)", viewModel.SelectionInfo);

        viewModel.SelectedFace = null;
        Assert.Empty(viewModel.SelectionInfo);
    }

    [Fact]
    public async Task LoadAsync_ClearsSelection()
    {
        var viewModel = new MainViewModel(new StubReader(_ => new StubDocument()));
        await viewModel.LoadAsync("a.stp");
        viewModel.SelectedFace = new FaceId(1);

        await viewModel.LoadAsync("b.stp");

        Assert.Null(viewModel.SelectedFace);
    }

    private sealed class StubReader(Func<string, IStepDocument> load) : IStepReader
    {
        public IStepDocument Load(string path) => load(path);
    }

    // Two triangles on two faces; face 0 red, face 1 without colour.
    private sealed class StubDocument : IStepDocument
    {
        public string SourcePath => "stub.stp";

        public ModelStatistics Statistics { get; } = new(1, 1, 2, 6, 4);

        public ColorRgb? GetFaceColor(FaceId faceId) => faceId.Value == 0 ? ColorRgb.FromHex("#FF0000") : null;

        public MeshData Tessellate(TessellationOptions options) =>
            new(new float[18], new float[18], [0, 1, 2, 3, 4, 5], [0, 1]);

        public void Dispose()
        {
        }
    }
}
