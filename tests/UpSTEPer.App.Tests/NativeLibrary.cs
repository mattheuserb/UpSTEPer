using UpSTEPer.Occt;

namespace UpSTEPer.App.Tests;

/// <summary>Skips tests that need upsteper_native when it is missing, and fails them in CI (UPS_REQUIRE_NATIVE=1).</summary>
internal static class NativeLibrary
{
    public static void Require()
    {
        const string Reason = "upsteper_native not built; see docs/development.md.";
        if (Environment.GetEnvironmentVariable("UPS_REQUIRE_NATIVE") == "1")
        {
            Assert.True(OcctRuntime.IsAvailable, Reason);
            return;
        }

        Assert.SkipUnless(OcctRuntime.IsAvailable, Reason);
    }
}
