using Avalonia.Controls;
using Avalonia.VisualTree;
using UpSTEPer.App.ViewModels;
using UpSTEPer.App.Views;
using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

public class RepairViewModelTests
{
    [Fact]
    public async Task CheckModelAsync_ReturnsFindingsAndSummarises()
    {
        using var main = await LoadedAsync(new FakeReader());

        var parts = await main.CheckModelAsync();

        Assert.Single(parts!);
        Assert.Equal("Check: 1 of 1 part(s) have problems.", main.Status);
    }

    [Fact]
    public async Task RepairModelAsync_RepairsKeepsUndoPointAndResetsSelection()
    {
        var reader = new FakeReader();
        using var main = await LoadedAsync(reader);
        main.SelectedFace = new FaceId(1);
        await main.ColorFaceCommand.ExecuteAsync(null);
        var options = new RepairOptions(RepairSteps.Sew, 0.1);

        var report = await main.RepairModelAsync(options);

        var document = reader.Documents[0];
        Assert.Equal(options, document.RepairedWith);
        Assert.Single(report!);
        Assert.NotNull(document.SavedPath); // undo snapshot written before the repair
        Assert.Contains("upsteper-before-repair-", document.SavedPath, StringComparison.Ordinal);
        Assert.Null(main.SelectedFace);
        Assert.False(main.UndoCommand.CanExecute(null)); // colour history no longer matches the faces
        Assert.True(main.IsDirty);
        Assert.True(main.CanUndoRepair);
        Assert.True(main.UndoRepairCommand.CanExecute(null));
        Assert.StartsWith("Repair: 1 of 1 part(s) changed", main.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UndoRepair_ReloadsTheSnapshotAndRestoresDirtyFlag()
    {
        var reader = new FakeReader();
        using var main = await LoadedAsync(reader);
        await main.RepairModelAsync(RepairOptions.Default);

        await main.UndoRepairCommand.ExecuteAsync(null);

        Assert.Equal(2, reader.Documents.Count);   // the snapshot was loaded as a new document
        Assert.True(reader.Documents[0].IsDisposed);
        Assert.False(main.IsDirty);                // was clean before the repair
        Assert.False(main.CanUndoRepair);
        Assert.Equal("Repair undone.", main.Status);
    }

    [Fact]
    public async Task RepairModelAsync_Cancelled_KeepsModelAndOffersNoUndo()
    {
        var reader = new FakeReader();
        using var main = await LoadedAsync(reader);
        var scene = main.Scene;
        reader.Documents[0].CancelRepair = true;

        var report = await main.RepairModelAsync(RepairOptions.Default);

        Assert.Null(report);
        Assert.Same(scene, main.Scene);
        Assert.False(main.CanUndoRepair);
        Assert.False(main.IsDirty);
        Assert.Equal("Repair cancelled; the model is unchanged.", main.Status);
    }

    [Fact]
    public async Task LoadingAnotherFile_DropsTheUndoPoint()
    {
        using var main = await LoadedAsync(new FakeReader());
        await main.RepairModelAsync(RepairOptions.Default);

        await main.LoadAsync("other.stp");

        Assert.False(main.CanUndoRepair);
    }

    [Theory]
    [InlineData(true, true, true, true, RepairSteps.All)]
    [InlineData(false, true, false, false, RepairSteps.Sew)]
    [InlineData(false, false, true, true, RepairSteps.Canonical | RepairSteps.MergeFaces)]
    public void SelectedSteps_FollowTheCheckBoxes(bool fix, bool sew, bool canonical, bool merge, RepairSteps expected)
    {
        using var main = new MainViewModel(new FakeReader());
        var dialog = new RepairViewModel(main) { Fix = fix, Sew = sew, Canonical = canonical, MergeFaces = merge };

        Assert.Equal(expected, dialog.SelectedSteps);
    }

    [Fact]
    public void RepairCommand_WithoutSteps_CannotExecute()
    {
        using var main = new MainViewModel(new FakeReader());
        var dialog = new RepairViewModel(main) { Fix = false, Sew = false, Canonical = false, MergeFaces = false };

        Assert.False(dialog.RepairCommand.CanExecute(null));
    }

    [Fact]
    public async Task Dialog_CheckThenRepair_ShowsBothReports()
    {
        var reader = new FakeReader();
        using var main = await LoadedAsync(reader);
        var dialog = new RepairViewModel(main) { MergeFaces = false, Tolerance = 0.2m };

        await dialog.CheckCommand.ExecuteAsync(null);
        Assert.Single(dialog.CheckRows);
        Assert.False(dialog.ShowsRepairReport);
        Assert.Equal(0.2, reader.Documents[0].CheckedWith);

        await dialog.RepairCommand.ExecuteAsync(null);
        Assert.Single(dialog.RepairRows);
        Assert.True(dialog.ShowsRepairReport);
        Assert.Equal(new RepairOptions(RepairSteps.Fix | RepairSteps.Sew | RepairSteps.Canonical, 0.2), reader.Documents[0].RepairedWith);
        Assert.Equal(main.Status, dialog.Summary);
    }

    [Fact]
    public async Task Check_SummarisesResultsAndSelectsTheMostSevereFirst()
    {
        var reader = new FakeReader();
        using var main = await LoadedAsync(reader);
        reader.Documents[0].CheckResult =
        [
            new PartCheck("Clean", 1, 6, 0, 0, 0, 0, 0, 0),
            new PartCheck("Gaps", 0, 6, 24, 0, 0, 0, 0, 0),
            new PartCheck("Broken", 1, 9, 0, 0, 0, 0, 0, 0, FailedChecks: 1),
            new PartCheck("Also clean", 1, 6, 0, 0, 0, 0, 0, 0),
        ];
        var dialog = new RepairViewModel(main);
        Assert.False(dialog.HasCheckResults);

        await dialog.CheckCommand.ExecuteAsync(null);

        Assert.True(dialog.HasCheckResults);
        Assert.Equal((4, 2, 1, 1), (dialog.PartsChecked, dialog.PartsOk, dialog.PartsWithProblems, dialog.PartsWithCheckErrors));
        Assert.Equal("Broken", dialog.SelectedCheckRow!.Part.Name);
        Assert.False(dialog.HasRepairReport); // no repair statistics before a repair
    }

    [Fact]
    public async Task Repair_SummarisesOutcomesAndSwitchesToTheReport()
    {
        var reader = new FakeReader();
        using var main = await LoadedAsync(reader);
        reader.Documents[0].RepairResult =
        [
            new PartRepair("Fine", false, false, 6, 6, 1, 1, 0, 0, 0, 0, 0),
            new PartRepair("Sewn", true, false, 6, 6, 0, 1, 24, 0, 0, 0, 0),
            new PartRepair("Merged", true, false, 6, 3, 1, 1, 0, 0, 0, 0, 1),
            new PartRepair("Hopeless", false, true, 9, 9, 1, 1, 0, 0, 3, 3, 0),
        ];
        var dialog = new RepairViewModel(main);

        await dialog.RepairCommand.ExecuteAsync(null);

        Assert.Equal(RepairViewModel.RepairReport, dialog.SelectedReportIndex);
        Assert.True(dialog.ShowsRepairReport);
        Assert.Equal((1, 1, 1, 1), (dialog.PartsRepaired, dialog.PartsUnchanged, dialog.PartsNeedingAttention, dialog.PartsFailed));
        Assert.Equal("Hopeless", dialog.SelectedRepairRow!.Part.Name);

        dialog.SelectedReportIndex = RepairViewModel.CheckReport; // the user switches back to the check tab
        Assert.False(dialog.ShowsRepairReport);
    }

    [Fact]
    public async Task RepairWindow_ShowsCheckRowsInTheGrid()
    {
        var rows = await HeadlessTestApp.Session.Dispatch(
            async () =>
            {
                using var main = await LoadedAsync(new FakeReader());
                var dialog = new RepairViewModel(main);
                var window = new RepairWindow { DataContext = dialog };
                window.Show();
                await dialog.CheckCommand.ExecuteAsync(null);

                var grid = window.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "CheckGrid");
                var count = grid.ItemsSource!.Cast<object>().Count();
                window.Close();
                return count;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, rows);
    }

    private static async Task<MainViewModel> LoadedAsync(FakeReader reader)
    {
        var viewModel = new MainViewModel(reader);
        await viewModel.LoadAsync("fake.stp");
        return viewModel;
    }
}
