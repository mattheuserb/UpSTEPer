using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;
using UpSTEPer.Occt;

namespace UpSTEPer.App.Tests;

// End to end with the real kernel: repair in the view model, then undo through the STEP snapshot.
public class RepairWithOcctTests
{
    private static readonly string _damagedParts = Path.Combine(FindRepoRoot(), "examples", "generated", "damaged-parts.stp");

    [Fact]
    public async Task RepairThenUndo_RestoresTheOriginalModel()
    {
        RequireNativeLibrary();
        using var main = new MainViewModel(new OcctStepReader());
        Assert.True(await main.LoadAsync(_damagedParts));
        Assert.Equal(21, CountFaces(main));
        Assert.Equal(3, main.ModelTree.Single().GetBodies().Count());

        var report = await main.RepairModelAsync(RepairOptions.Default);

        Assert.NotNull(report);
        Assert.Equal(4, main.ModelTree.Single().GetBodies().Count()); // the sewn box became a body
        Assert.Equal(18, CountFaces(main));
        Assert.True(main.CanUndoRepair);

        await main.UndoRepairCommand.ExecuteAsync(null);

        Assert.Equal(3, main.ModelTree.Single().GetBodies().Count());
        Assert.Equal(21, CountFaces(main));
        Assert.False(main.CanUndoRepair);
    }

    // Faces of the scene: the highest face id in the mesh + 1.
    private static int CountFaces(MainViewModel main) => main.Scene!.Mesh.TriangleFaceIds.ToArray().Max() + 1;

    private static void RequireNativeLibrary()
    {
        const string Reason = "upsteper_native not built; see docs/development.md.";
        if (Environment.GetEnvironmentVariable("UPS_REQUIRE_NATIVE") == "1")
        {
            Assert.True(OcctRuntime.IsAvailable, Reason);
            return;
        }

        Assert.SkipUnless(OcctRuntime.IsAvailable, Reason);
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UpSTEPer.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
