using Microsoft.Extensions.Logging;
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
        Assert.Equal(StepSchema.Ap214, reader.Documents[0].SavedSchema);
        Assert.False(viewModel.IsDirty);
        Assert.Equal("painted.stp - UpSTEPer", viewModel.Title);
    }

    [Fact]
    public async Task SaveAsync_Ap242_PassesSchemaAndReportsIt()
    {
        var reader = new FakeReader();
        using var viewModel = await LoadedAsync(reader);

        await viewModel.SaveAsync("painted.stp", StepSchema.Ap242);

        Assert.Equal(StepSchema.Ap242, reader.Documents[0].SavedSchema);
        Assert.Equal("Saved painted.stp (Ap242)", viewModel.Status);
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
    public async Task ExportDxfAsync_WritesFileAndReportsCurves()
    {
        using var viewModel = await LoadedAsync(new FakeReader());
        var path = Path.Combine(Path.GetTempPath(), $"upsteper-{Guid.NewGuid():N}.dxf");
        try
        {
            var exported = await viewModel.ExportDxfAsync(path);

            Assert.True(exported);
            Assert.True(File.Exists(path));
            Assert.StartsWith("Exported 0 curves", viewModel.Status, StringComparison.Ordinal);
            Assert.False(viewModel.IsBusy);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportDxfAsync_UnwritablePath_ShowsError()
    {
        using var viewModel = await LoadedAsync(new FakeReader());
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "out.dxf");

        Assert.False(await viewModel.ExportDxfAsync(path));
        Assert.StartsWith("Error:", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportDxfAsync_WithoutDocument_ReturnsFalse()
    {
        using var viewModel = new MainViewModel(new FakeReader());

        Assert.False(await viewModel.ExportDxfAsync("out.dxf"));
    }

    [Fact]
    public async Task Dispose_DisposesOpenDocument()
    {
        var reader = new FakeReader();
        var viewModel = await LoadedAsync(reader);

        viewModel.Dispose();

        Assert.True(reader.Documents[0].IsDisposed);
    }

    [Fact]
    public async Task LoadAsync_MeshesWithSelectedQualityScaledToModel()
    {
        var reader = new FakeReader();
        using var viewModel = new MainViewModel(reader) { DisplayQuality = DisplayQuality.Fine };

        await viewModel.LoadAsync("fake.stp");

        var bounds = reader.Documents[0].Bounds;
        Assert.Equal([TessellationOptions.ForModel(DisplayQuality.Fine, bounds)], reader.Documents[0].TessellationRequests);
    }

    [Fact]
    public async Task DisplayQuality_Changed_RemeshesAndKeepsModelColoursAndSelection()
    {
        var reader = new FakeReader();
        using var viewModel = await LoadedAsync(reader);
        var before = viewModel.Scene!;
        viewModel.SelectedFace = new FaceId(1);

        viewModel.DisplayQuality = DisplayQuality.Coarse;
        await viewModel.RemeshCommand.ExecutionTask!;

        var document = reader.Documents[0];
        Assert.Equal(TessellationOptions.ForModel(DisplayQuality.Coarse, document.Bounds), document.TessellationRequests[^1]);
        Assert.NotSame(before, viewModel.Scene);
        Assert.True(viewModel.Scene!.IsSameModel(before));
        Assert.Equal(FakeDocument.FileRed, viewModel.Scene.GetFaceColor(new FaceId(0)));
        Assert.Equal(new FaceId(1), viewModel.SelectedFace);
        Assert.Contains("Coarse", viewModel.Status, StringComparison.Ordinal);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void DisplayQuality_ChangedWithoutModel_DoesNothing()
    {
        using var viewModel = new MainViewModel(new FakeReader());

        viewModel.DisplayQuality = DisplayQuality.Fine;

        Assert.Null(viewModel.Scene);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task CancelCommand_DuringLoad_KeepsPreviousModel()
    {
        var reader = new FakeReader();
        using var viewModel = await LoadedAsync(reader);
        var scene = viewModel.Scene;
        reader.BlockUntilCancelled = true;

        var loading = viewModel.LoadAsync("big.stp");
        Assert.True(viewModel.IsCancellable);
        Assert.True(viewModel.CancelCommand.CanExecute(null));
        viewModel.CancelCommand.Execute(null);
        var loaded = await loading;

        Assert.False(loaded);
        Assert.Same(scene, viewModel.Scene);
        Assert.False(reader.Documents[0].IsDisposed);
        Assert.Contains("cancelled", viewModel.Status, StringComparison.Ordinal);
        Assert.False(viewModel.IsCancellable);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Remesh_Cancelled_RestoresQualityWithoutRemeshing()
    {
        var reader = new FakeReader();
        using var viewModel = await LoadedAsync(reader);
        var document = reader.Documents[0];
        var scene = viewModel.Scene;
        document.CancelNextTessellation = true;

        viewModel.DisplayQuality = DisplayQuality.Fine;
        await viewModel.RemeshCommand.ExecutionTask!;

        Assert.Equal(DisplayQuality.Normal, viewModel.DisplayQuality);
        Assert.Same(scene, viewModel.Scene);
        Assert.Equal(2, document.TessellationRequests.Count);
        Assert.Contains("cancelled", viewModel.Status, StringComparison.Ordinal);
        Assert.False(viewModel.IsCancellable);
    }

    [Fact]
    public void CancelCommand_WithoutOperation_CannotExecute()
    {
        using var viewModel = new MainViewModel(new FakeReader());

        Assert.False(viewModel.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void ScaledProgress_MapsFractionIntoSection()
    {
        var reports = new List<OperationProgress>();
        var scaled = new ScaledProgress(new SynchronousProgress(reports.Add), 0.8, 1);

        scaled.Report(new OperationProgress(0.5, "Meshing"));
        scaled.Report(new OperationProgress(2, "Meshing"));

        Assert.Equal(0.9, reports[0].Fraction, 12);
        Assert.Equal("Meshing", reports[0].Phase);
        Assert.Equal(1.0, reports[1].Fraction, 12);
    }

    [Fact]
    public async Task LoadAsync_LogsSuccessAndFailure()
    {
        var logger = new RecordingLogger<MainViewModel>();
        var calls = 0;
        var reader = new FakeReader(_ => ++calls == 1 ? new FakeDocument() : throw new StepImportException("not STEP"));
        using var viewModel = new MainViewModel(reader, logger);

        await viewModel.LoadAsync("good.stp");
        await viewModel.LoadAsync("bad.stp");

        Assert.Collection(
            logger.Entries,
            opened =>
            {
                Assert.Equal(LogLevel.Information, opened.Level);
                Assert.Contains("good.stp at Normal display quality: 2 triangles", opened.Message, StringComparison.Ordinal);
            },
            failed =>
            {
                Assert.Equal(LogLevel.Warning, failed.Level);
                Assert.Contains("bad.stp failed", failed.Message, StringComparison.Ordinal);
                Assert.IsType<StepImportException>(failed.Exception);
            });
    }

    [Fact]
    public async Task HoveredFace_DescribesFaceInStatusBar()
    {
        using var viewModel = await LoadedAsync(new FakeReader());

        viewModel.HoveredFace = new FaceId(0);
        Assert.Equal("Face 0: #FF0000", viewModel.HoverInfo);

        viewModel.HoveredFace = new FaceId(1);
        Assert.Equal("Face 1: no colour", viewModel.HoverInfo);

        viewModel.HoveredFace = null;
        Assert.Empty(viewModel.HoverInfo);
    }

    [Fact]
    public async Task LoadAsync_LateProgressReports_DoNotOverwriteFinalStatus()
    {
        // Without a UI thread Progress<T> delivers on the thread pool, possibly after LoadAsync
        // returned; reports may only update the progress display, never the status line.
        using var viewModel = new MainViewModel(new FakeReader());

        await viewModel.LoadAsync("bracket.stp");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Contains("2 faces", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_SetsPartFaceAndTriangleCounts()
    {
        using var viewModel = new MainViewModel(new FakeReader());
        Assert.Equal((0, 0, 0), (viewModel.PartCount, viewModel.FaceCount, viewModel.TriangleCount));

        await viewModel.LoadAsync("bracket.stp");

        Assert.Equal(1, viewModel.PartCount);
        Assert.Equal(2, viewModel.FaceCount);
        Assert.Equal(viewModel.Scene!.Mesh.TriangleCount, viewModel.TriangleCount);
        Assert.True(viewModel.TriangleCount > 0);
    }

    [Fact]
    public async Task ColourCommands_ReportWhatWasApplied()
    {
        using var viewModel = await LoadedAsync(new FakeReader());
        viewModel.SelectedFace = new FaceId(1);
        viewModel.SelectedPaletteEntry = _blue;

        await viewModel.ColorFaceCommand.ExecuteAsync(null);
        Assert.Equal("Face colour applied: Blue", viewModel.Status);

        await viewModel.ClearFaceColorCommand.ExecuteAsync(null);
        Assert.Equal("Face colour removed; the face shows its body colour.", viewModel.Status);

        // Undo describes the restored state technically, as before.
        await viewModel.UndoCommand.ExecuteAsync(null);
        Assert.Equal("Face 1: #2271B3", viewModel.Status);
    }

    [Fact]
    public void PanelAndHintToggles_FlipTheirState()
    {
        using var viewModel = new MainViewModel(new FakeReader());
        Assert.True(viewModel.IsModelPanelVisible);
        Assert.True(viewModel.ShowShortcutHints);

        viewModel.ToggleModelPanelCommand.Execute(null);
        viewModel.ToggleShortcutHintsCommand.Execute(null);
        Assert.False(viewModel.IsModelPanelVisible);
        Assert.False(viewModel.ShowShortcutHints);

        viewModel.ToggleModelPanelCommand.Execute(null);
        Assert.True(viewModel.IsModelPanelVisible);
    }

    [Fact]
    public async Task SetDisplayQualityCommand_RemeshesLikeTheToolbar()
    {
        var reader = new FakeReader();
        using var viewModel = await LoadedAsync(reader);

        viewModel.SetDisplayQualityCommand.Execute(DisplayQuality.Fine);

        Assert.Equal(DisplayQuality.Fine, viewModel.DisplayQuality);
        await viewModel.RemeshCommand.ExecutionTask!;
        Assert.Equal(TessellationOptions.ForModel(DisplayQuality.Fine, reader.Documents[0].Bounds), reader.Documents[0].TessellationRequests[^1]);
    }

    [Fact]
    public void PaletteEntry_Description_AddsTheValueOnce()
    {
        Assert.Equal("Blue (#2271B3)", _blue.Description);
        Assert.Equal("Custom #ABCDEF", PaletteEntry.Custom(ColorRgb.FromHex("#ABCDEF")).Description);
    }

    private static async Task<MainViewModel> LoadedAsync(FakeReader reader)
    {
        var viewModel = new MainViewModel(reader);
        await viewModel.LoadAsync("fake.stp");
        return viewModel;
    }
}
