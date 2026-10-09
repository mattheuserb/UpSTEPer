using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpSTEPer.App.Logging;
using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;
using UpSTEPer.Occt;

namespace UpSTEPer.App;

/// <summary>Dependency injection setup of the desktop app.</summary>
internal static class AppServices
{
    /// <summary>
    /// Builds the services: STEP reader, view model and logging to a daily file in
    /// <paramref name="logDirectory"/> (Debug and above, including OCCT's messages about files).
    /// </summary>
    /// <param name="logDirectory">Where log files are written.</param>
    /// <param name="logToConsole">Also log to stderr (automation, CI).</param>
    /// <returns>The provider; dispose it at exit.</returns>
    public static ServiceProvider Build(string logDirectory, bool logToConsole) =>
        new ServiceCollection()
            .AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                // Created by the container (not AddProvider) so that it is disposed with it.
                logging.Services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(logDirectory));
                if (logToConsole)
                {
                    logging
                        .AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace)
                        .AddSimpleConsole(options => options.SingleLine = true);
                }
            })
            .AddSingleton<IStepReader, OcctStepReader>()
            .AddTransient<MainViewModel>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
