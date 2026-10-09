namespace UpSTEPer.Occt.Tests;

/// <summary>Locates test data in the repository and skips tests that need the native library.</summary>
internal static class TestFiles
{
    private static readonly Lazy<string> _repoRoot = new(FindRepoRoot);

    /// <summary>CADENAS cable gland ESKV 12: one solid, 109 analytic faces, one body colour (RAL 7035).</summary>
    public static string CableGland => Path.Combine(_repoRoot.Value, "examples", "ESKV 12 RAL 7035 10066410.stp");

    /// <summary>
    /// Generated assembly (native/tools/make_test_assembly.cpp): Top → Base plate, Pin assembly (left),
    /// Pin assembly (right); each pin assembly → Pin (1 body), Washer (2 bodies). 7 bodies in total.
    /// </summary>
    public static string TestAssembly => Path.Combine(_repoRoot.Value, "examples", "generated", "test-assembly.stp");

    /// <summary>
    /// Generated parts with one defect each (native/tools/make_damaged_parts.cpp): Healthy box,
    /// Unsewn box (loose faces, gap), Split cylinder (side in 4 faces), Spline cylinder (B-splines).
    /// </summary>
    public static string DamagedParts => Path.Combine(_repoRoot.Value, "examples", "generated", "damaged-parts.stp");

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
