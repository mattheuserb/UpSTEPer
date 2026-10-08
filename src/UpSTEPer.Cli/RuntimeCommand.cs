using System.CommandLine;
using UpSTEPer.Occt;

namespace UpSTEPer.Cli;

/// <summary><c>upsteper runtime</c>: reports whether and which native geometry kernel is loaded.</summary>
internal static class RuntimeCommand
{
    public static Command Create(TextWriter output)
    {
        var command = new Command("runtime", "Show versions of the native library and Open CASCADE.");
        command.SetAction(_ =>
        {
            if (!OcctRuntime.IsAvailable)
            {
                output.WriteLine("Native library 'upsteper_native' not found. Build it first (docs/development.md).");
                return 1;
            }

            output.WriteLine($"upsteper_native {OcctRuntime.Version}");
            output.WriteLine($"Open CASCADE    {OcctRuntime.OcctVersion}");
            return 0;
        });
        return command;
    }
}
