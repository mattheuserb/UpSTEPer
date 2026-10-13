using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

public class ModelTreeViewModelTests
{
    private static readonly PaletteEntry _blue = new("Blue", ColorRgb.FromHex("#2271B3"));

    [Fact]
    public async Task Load_ExposesModelTree()
    {
        using var viewModel = await LoadedAsync();

        var assembly = Assert.Single(viewModel.ModelTree);
        Assert.Equal(["Part A", "Part B"], assembly.Children.Select(c => c.Name));
    }

    [Fact]
    public async Task SelectingAssembly_HighlightsAllFacesOfItsBodies()
    {
        using var viewModel = await LoadedAsync();

        viewModel.SelectedNode = viewModel.ModelTree[0];

        Assert.Equal([0, 1], viewModel.HighlightedFaces.Select(f => f.Value).Order());
        Assert.Equal("Selected: Assembly (2 bodies, 2 faces)", viewModel.SelectionInfo);
    }

    [Fact]
    public async Task SelectingPart_HighlightsOnlyItsFaces()
    {
        using var viewModel = await LoadedAsync();

        viewModel.SelectedNode = viewModel.ModelTree[0].Children[1];

        Assert.Equal([new FaceId(1)], viewModel.HighlightedFaces);
    }

    [Fact]
    public async Task FaceAndTreeSelection_ExcludeEachOther()
    {
        using var viewModel = await LoadedAsync();

        viewModel.SelectedNode = viewModel.ModelTree[0];
        viewModel.SelectedFace = new FaceId(0);
        Assert.Null(viewModel.SelectedNode);
        Assert.Equal([new FaceId(0)], viewModel.HighlightedFaces);

        viewModel.SelectedNode = viewModel.ModelTree[0];
        Assert.Null(viewModel.SelectedFace);

        viewModel.SelectedNode = null;
        Assert.Empty(viewModel.HighlightedFaces);
        Assert.Empty(viewModel.SelectionInfo);
    }

    [Fact]
    public async Task ColorBody_WithTreeSelection_ColoursAllBodiesInOneUndoStep()
    {
        using var viewModel = await LoadedAsync();
        viewModel.SelectedNode = viewModel.ModelTree[0];
        viewModel.SelectedPaletteEntry = _blue;
        Assert.True(viewModel.ColorBodyCommand.CanExecute(null));
        Assert.False(viewModel.ColorFaceCommand.CanExecute(null));

        await viewModel.ColorBodyCommand.ExecuteAsync(null);

        Assert.Equal(FakeDocument.FileRed, viewModel.Scene!.GetFaceColor(new FaceId(0))); // own face colour wins
        Assert.Equal(_blue.Color, viewModel.Scene.GetFaceColor(new FaceId(1)));
        Assert.Equal("Body colour applied to 2 bodies: Blue", viewModel.Status);

        await viewModel.UndoCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Scene!.GetFaceColor(new FaceId(1)));
        Assert.False(viewModel.UndoCommand.CanExecute(null));
    }

    [Fact]
    public async Task ColorBody_WithFaceSelection_ColoursOnlyThatFacesBody()
    {
        using var viewModel = await LoadedAsync();
        viewModel.SelectedFace = new FaceId(1);
        viewModel.SelectedPaletteEntry = _blue;

        await viewModel.ColorBodyCommand.ExecuteAsync(null);

        Assert.Equal(_blue.Color, viewModel.Scene!.GetFaceColor(new FaceId(1)));
        Assert.Equal("Body colour applied: Blue", viewModel.Status);
    }

    private static async Task<MainViewModel> LoadedAsync()
    {
        var viewModel = new MainViewModel(new FakeReader(twoBodies: true));
        await viewModel.LoadAsync("assembly.stp");
        return viewModel;
    }
}
