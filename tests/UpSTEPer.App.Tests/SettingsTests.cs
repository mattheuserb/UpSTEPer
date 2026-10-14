using Microsoft.Extensions.Logging;
using UpSTEPer.App.Settings;
using UpSTEPer.App.ViewModels;

namespace UpSTEPer.App.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "upsteper-settings-tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Store_WithoutFile_StartsWithDefaults_AndSavesOnUpdate()
    {
        var store = new SettingsStore(SettingsPath);
        Assert.Empty(store.Current.RecentFiles);
        Assert.False(File.Exists(SettingsPath)); // nothing written until something changes

        store.Update(s => s with { RecentFiles = ["a.stp"] });

        Assert.Equal(["a.stp"], new SettingsStore(SettingsPath).Current.RecentFiles);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1, 2, 3]")]
    public void Store_UnreadableFile_UsesDefaultsAndLogs(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, content);
        var logger = new RecordingLogger<SettingsStore>();

        var store = new SettingsStore(SettingsPath, logger);

        Assert.Empty(store.Current.RecentFiles);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("could not be read", StringComparison.Ordinal));
    }

    [Fact]
    public void Store_UnknownAndMissingMembers_AreTolerated()
    {
        // A file from a newer version (extra member) or an older one (member missing) still loads.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "recentFiles": ["x.stp"], "somethingNew": 42 }""");

        Assert.Equal(["x.stp"], new SettingsStore(SettingsPath).Current.RecentFiles);
    }

    [Fact]
    public void Store_UnwritableLocation_KeepsTheChangeInMemoryAndLogs()
    {
        Directory.CreateDirectory(SettingsPath); // a directory where the file should be: saving fails
        var logger = new RecordingLogger<SettingsStore>();
        var store = new SettingsStore(SettingsPath, logger);

        store.Update(s => s with { RecentFiles = ["a.stp"] });

        Assert.Equal(["a.stp"], store.Current.RecentFiles);
        Assert.Contains(logger.Entries, e => e.Message.Contains("could not be saved", StringComparison.Ordinal));
    }

    [Fact]
    public void RecentFileList_PutsNewestFirst_WithoutDuplicates_AtMostFive()
    {
        IReadOnlyList<string> files = [];
        foreach (var name in new[] { "1.stp", "2.stp", "3.stp", "4.stp", "5.stp", "6.stp", "3.stp" })
        {
            files = RecentFileList.Add(files, Path.Combine(_directory, name));
        }

        Assert.Equal(["3.stp", "6.stp", "5.stp", "4.stp", "2.stp"], files.Select(Path.GetFileName));
        Assert.All(files, f => Assert.True(Path.IsPathFullyQualified(f)));
        Assert.Equal(4, RecentFileList.Remove(files, files[1]).Count);
    }

    [Fact]
    public void RecentFileEntry_Header_NumbersAndEscapesUnderscores()
    {
        var entry = new RecentFileEntry(2, Path.Combine(_directory, "my_part.stp"), new CommunityToolkit.Mvvm.Input.RelayCommand(() => { }));

        Assert.Equal("_2 my__part.stp", entry.Header);
    }

    [Fact]
    public async Task MainViewModel_RemembersOpenedFilesAcrossRuns()
    {
        var file = CreateFile("bracket.stp");
        using (var first = new MainViewModel(new FakeReader(), settings: new SettingsStore(SettingsPath)))
        {
            Assert.False(first.HasRecentFiles);
            await first.LoadAsync(file);
            Assert.Equal(file, Assert.Single(first.RecentFiles).FullPath);
        }

        using var second = new MainViewModel(new FakeReader(), settings: new SettingsStore(SettingsPath));
        Assert.Equal(file, Assert.Single(second.RecentFiles).FullPath);
        Assert.True(second.HasRecentFiles);
    }

    [Fact]
    public async Task OpenRecent_LoadsExistingFile_AndRemovesMissingOne()
    {
        var existing = CreateFile("a.stp");
        var missing = CreateFile("b.stp");
        var reader = new FakeReader();
        using var viewModel = new MainViewModel(reader, settings: new SettingsStore(SettingsPath));
        await viewModel.LoadAsync(existing);
        await viewModel.LoadAsync(missing);
        File.Delete(missing);

        await viewModel.OpenRecentCommand.ExecuteAsync(missing);
        Assert.Equal("b.stp no longer exists; it was removed from the recent files.", viewModel.Status);
        Assert.Equal([existing], viewModel.RecentFiles.Select(r => r.FullPath));

        await viewModel.OpenRecentCommand.ExecuteAsync(existing);
        Assert.Equal(3, reader.Documents.Count);
        Assert.Equal("a.stp - UpSTEPer", viewModel.Title);
    }

    [Fact]
    public async Task ClearRecentFiles_EmptiesTheListAndTheFile()
    {
        using var viewModel = new MainViewModel(new FakeReader(), settings: new SettingsStore(SettingsPath));
        await viewModel.LoadAsync(CreateFile("a.stp"));

        viewModel.ClearRecentFilesCommand.Execute(null);

        Assert.False(viewModel.HasRecentFiles);
        Assert.Empty(new SettingsStore(SettingsPath).Current.RecentFiles);
    }

    [Fact]
    public async Task UndoRepair_DoesNotAddTheSnapshotToRecentFiles()
    {
        var file = CreateFile("a.stp");
        using var viewModel = new MainViewModel(new FakeReader(), settings: new SettingsStore(SettingsPath));
        await viewModel.LoadAsync(file);
        await viewModel.RepairModelAsync(UpSTEPer.Core.RepairOptions.Default);

        await viewModel.UndoRepairCommand.ExecuteAsync(null);

        Assert.Equal([file], viewModel.RecentFiles.Select(r => r.FullPath));
    }

    [Fact]
    public void AboutInfo_TextAndVersion()
    {
        var info = new AboutInfo("1.2.3-beta", "native 1, OCCT 8.0.1", "/logs");

        Assert.Equal(
            ["UpSTEPer 1.2.3-beta", "Geometry: native 1, OCCT 8.0.1", $"Runtime: {info.Runtime}", $"OS: {info.OperatingSystem}", "Logs: /logs"],
            info.ToText().Split(Environment.NewLine));
        Assert.DoesNotContain('+', AboutInfo.VersionOf(typeof(AboutInfo).Assembly)); // no commit hash
    }

    [Fact]
    public void AboutInfo_ForThisApp_NamesTheGeometryKernel()
    {
        NativeLibrary.Require();

        Assert.Contains("Open CASCADE Technology", AboutInfo.ForThisApp().GeometryVersion, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string CreateFile(string name)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, "ISO-10303-21;");
        return path;
    }
}
