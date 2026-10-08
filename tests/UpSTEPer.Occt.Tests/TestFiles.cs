namespace UpSTEPer.Occt.Tests;

/// <summary>Locates test data in the repository and skips tests that need the native library.</summary>
internal static class TestFiles
{
    private static readonly Lazy<string> RepoRoot = new(FindRepoRoot);

    /// <summary>CADENAS cable gland ESKV 12: one solid, 109 analytic faces, one body colour (RAL 7035).</summary>
    public static string CableGland => Path.Combine(RepoRoot.Value, "examples", "ESKV 12 RAL 7035 10066410.stp");

    /// <summary>
    /// Skips the current test when upsteper_native has not been built. When the environment
    /// variable <c>UPS_REQUIRE_NATIVE=1</c> is set (CI), a missing library fails the test instead.
    /// </summary>
    public static void RequireNativeLibrary()
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
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "UpSTEPer.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (UpSTEPer.slnx) not found.");
    }
}
