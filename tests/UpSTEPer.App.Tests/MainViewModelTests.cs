using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

public class MainViewModelTests
{
    private static readonly PaletteEntry _blue = new("Blue", ColorRgb.FromHex("#2271B3"));
    private static readonly PaletteEntry _green = new("Green", ColorRgb.FromHex("#57A639"));

    [Fact]
    public async Task LoadAsync_Success_SetsSceneTitleAndStatus()
    {
        using var viewModel = new MainViewModel(new FakeReader());

        var loaded = await viewModel.LoadAsync("bracket.stp");

        Assert.True(loaded);
        Assert.True(viewModel.HasDocument);
        Assert.NotNull(viewModel.Scene);
        Assert.Equal("bracket.stp - UpSTEPer", viewModel.Title);
        Assert.Contains("2 faces", viewModel.Status, StringComparison.Ordinal);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task LoadAsync_ImportError_ShowsErrorAndKeepsNoDocument()
    {
        using var viewModel = new MainViewModel(new FakeReader(_ => throw new StepImportException("not STEP")));

        var loaded = await viewModel.LoadAsync("broken.stp");

        Assert.False(loaded);
        Assert.False(viewModel.HasDocument);
        Assert.Null(viewModel.Scene);
        Assert.Equal("Error: not STEP", viewModel.Status);
    }

    [Fact]
    public async Task LoadAsync_SecondFile_DisposesFirstAndResetsSelectionAndHistory()
    {
        var reader = new FakeReader();
        using var viewModel = new MainViewModel(reader);
        await viewModel.LoadAsync("a.stp");
        viewModel.SelectedFace = new FaceId(1);
        await viewModel.ColorFaceCommand.ExecuteAsync(null);

        await viewModel.LoadAsync("b.stp");

        Assert.True(reader.Documents[0].IsDisposed);
        Assert.Null(viewModel.SelectedFace);
        Assert.False(viewModel.UndoCommand.CanExecute(null));
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task SelectedFace_DescribesFaceAndColour()
    {
        using var viewModel = await LoadedAsync(new FakeReader());

        viewModel.SelectedFace = new FaceId(0);
        Assert.Equal("Selected: face 0, colour #FF0000", viewModel.SelectionInfo);

        viewModel.SelectedFace = new FaceId(1);
        Assert.Equal("Selected: face 1, colour none (default grey)", viewModel.SelectionInfo);

        viewModel.SelectedFace = null;
        Assert.Empty(viewModel.SelectionInfo);
    }

    [Fact]
    public async Task ColourCommands_RequireASelection()
    {
        using var viewModel = await LoadedAsync(new FakeReader());

        Assert.False(viewModel.ColorFaceCommand.CanExecute(null));
        Assert.False(viewModel.ColorBodyCommand.CanExecute(null));
        Assert.False(viewModel.ClearFaceColorCommand.CanExecute(null));

        viewModel.SelectedFace = new FaceId(1);

        Assert.True(viewModel.ColorFaceCommand.CanExecute(null));
        Assert.True(viewModel.ColorBodyCommand.CanExecute(null));
        Assert.True(viewModel.ClearFaceColorCommand.CanExecute(null));
    }

    [Fact]
    public async Task ColorFace_UpdatesSceneColoursKeepsMeshAndMarksDirty()
    {
        using var viewModel = await LoadedAsync(new FakeReader());
        var mesh = viewModel.Scene!.Mesh;
        viewModel.SelectedFace = new FaceId(1);
        viewModel.SelectedPaletteEntry = _blue;

        await viewModel.ColorFaceCommand.ExecuteAsync(null);

        Assert.Equal(_blue.Color, viewModel.Scene!.GetFaceColor(new FaceId(1)));
        Assert.Same(mesh, viewModel.Scene.Mesh);
        Assert.True(viewModel.IsDirty);
        Assert.StartsWith("*", viewModel.Title, StringComparison.Ordinal);
        Assert.Equal("Selected: face 1, colour #2271B3", viewModel.SelectionInfo);
    }

    [Fact]
    public async Task ColorBody_ColoursFacesWithoutOwnColour()
    {
        using var viewModel = await LoadedAsync(new FakeReader());
        viewModel.SelectedFace = new FaceId(1);
        viewModel.SelectedPaletteEntry = _green;

        await viewModel.ColorBodyCommand.ExecuteAsync(null);

        Assert.Equal(FakeDocument.FileRed, viewModel.Scene!.GetFaceColor(new FaceId(0)));
        Assert.Equal(_green.Color, viewModel.Scene.GetFaceColor(new FaceId(1)));
    }

    [Fact]
    public async Task ClearFaceColor_ShowsBodyColourAgain()
    {
        using var viewModel = await LoadedAsync(new FakeReader());
        viewModel.SelectedFace = new FaceId(0);

        await viewModel.ClearFaceColorCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Scene!.GetFaceColor(new FaceId(0)));
    }

    [Fact]
    public async Task UndoRedo_RestoreExactAssignedColours()
    {
        using var viewModel = await LoadedAsync(new FakeReader());
        viewModel.SelectedFace = new FaceId(0);
        viewModel.SelectedPaletteEntry = _blue;
        await viewModel.ColorFaceCommand.ExecuteAsync(null);
        viewModel.SelectedPaletteEntry = _green;
        await viewModel.ColorBodyCommand.ExecuteAsync(null);

        await viewModel.UndoCommand.ExecuteAsync(null);
        Assert.Null(viewModel.Scene!.GetFaceColor(new FaceId(1)));
        Assert.Equal(_blue.Color, viewModel.Scene.GetFaceColor(new FaceId(0)));

        await viewModel.UndoCommand.ExecuteAsync(null);
        Assert.Equal(FakeDocument.FileRed, viewModel.Scene!.GetFaceColor(new FaceId(0)));
        Assert.False(viewModel.UndoCommand.CanExecute(null));

        await viewModel.RedoCommand.ExecuteAsync(null);
        await viewModel.RedoCommand.ExecuteAsync(null);
        Assert.Equal(_blue.Color, viewModel.Scene!.GetFaceColor(new FaceId(0)));
        Assert.Equal(_green.Color, viewModel.Scene.GetFaceColor(new FaceId(1)));
        Assert.False(viewModel.RedoCommand.CanExecute(null));
    }

    [Fact]
    public async Task NewEdit_ClearsRedo()
    {
        using var viewModel = await LoadedAsync(new FakeReader());
        viewModel.SelectedFace = new FaceId(1);
        await viewModel.ColorFaceCommand.ExecuteAsync(null);
        await viewModel.UndoCommand.ExecuteAsync(null);
        Assert.True(viewModel.RedoCommand.CanExecute(null));

        await viewModel.ColorBodyCommand.ExecuteAsync(null);

        Assert.False(viewModel.RedoCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveAsync_WritesFileAndClearsDirtyFlag()
    {
        var reader = new FakeReader();
        using var viewModel = await LoadedAsync(reader);
        viewModel.SelectedFace = new FaceId(1);
        await viewModel.ColorFaceCommand.ExecuteAsync(null);

        var saved = await viewModel.SaveAsync("painted.stp");

        Assert.True(saved);
        Assert.Equal("painted.stp", reader.Documents[0].SavedPath);
        Assert.False(viewModel.IsDirty);
        Assert.Equal("painted.stp - UpSTEPer", viewModel.Title);
    }

    [Fact]
    public async Task SaveAsync_WriteError_KeepsDirtyAndShowsError()
    {
        var reader = new FakeReader();
        using var viewModel = await LoadedAsync(reader);
        viewModel.SelectedFace = new FaceId(1);
        await viewModel.ColorFaceCommand.ExecuteAsync(null);
        reader.Documents[0].SaveFailure = new IOException("disk full");

        var saved = await viewModel.SaveAsync("painted.stp");

        Assert.False(saved);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("Error: disk full", viewModel.Status);
    }

    [Fact]
    public async Task SaveAsync_WithoutDocument_ReturnsFalse()
    {
        using var viewModel = new MainViewModel(new FakeReader());

        Assert.False(await viewModel.SaveAsync("out.stp"));
    }

    [Fact]
    public async Task Dispose_DisposesOpenDocument()
    {
        var reader = new FakeReader();
        var viewModel = await LoadedAsync(reader);

        viewModel.Dispose();

        Assert.True(reader.Documents[0].IsDisposed);
    }

    private static async Task<MainViewModel> LoadedAsync(FakeReader reader)
    {
        var viewModel = new MainViewModel(reader);
        await viewModel.LoadAsync("fake.stp");
        return viewModel;
    }
}
