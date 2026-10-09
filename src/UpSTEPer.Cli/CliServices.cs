using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpSTEPer.Core;
using UpSTEPer.Occt;

namespace UpSTEPer.Cli;

/// <summary>Dependency injection setup of the command line.</summary>
internal static class CliServices
{
    /// <summary>Name of the option that enables diagnostic output; read before parsing (see <see cref="IsVerbose"/>).</summary>
    public const string VerboseOption = "--verbose";

    /// <summary>
    /// Gets a value indicating whether <paramref name="args"/> ask for diagnostic output. Read before
    /// System.CommandLine parses, because the commands are built with their loggers.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns><see langword="true"/> when <c>--verbose</c> is present.</returns>
    public static bool IsVerbose(IReadOnlyList<string> args) => args.Contains(VerboseOption, StringComparer.Ordinal);

    /// <summary>
    /// Builds the services: the STEP reader and logging to stderr (warnings and errors, or
    /// everything down to Debug with <paramref name="verbose"/>, including OCCT's messages).
    /// </summary>
    /// <param name="verbose">Whether to log diagnostic details.</param>
    /// <returns>The provider; dispose it before exit so queued log lines are written.</returns>
    public static ServiceProvider Build(bool verbose) =>
        new ServiceCollection()
            .AddLogging(logging => logging
                .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning)
                // stdout is reserved for command output, which scripts parse.
                .AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace)
                .AddSimpleConsole(options => options.SingleLine = true))
            .AddSingleton<IStepReader, OcctStepReader>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
