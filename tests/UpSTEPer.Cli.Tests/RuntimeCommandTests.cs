using System.CommandLine;
using UpSTEPer.Occt;

namespace UpSTEPer.Cli.Tests;

public class RuntimeCommandTests
{
    [Fact]
    public void Runtime_ReportsNativeVersionsOrMissingLibrary()
    {
        using var output = new StringWriter();
        var root = new RootCommand { RuntimeCommand.Create(output) };

        var exitCode = root.Parse(["runtime"]).Invoke();

        // The CLI test output contains the native library whenever it has been built (always in CI).
        if (OcctRuntime.IsAvailable)
        {
            Assert.Equal(0, exitCode);
            Assert.Contains($"upsteper_native {OcctRuntime.Version}", output.ToString(), StringComparison.Ordinal);
            Assert.Contains($"Open CASCADE    {OcctRuntime.OcctVersion}", output.ToString(), StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(1, exitCode);
            Assert.Contains("not found", output.ToString(), StringComparison.Ordinal);
        }
    }
}
