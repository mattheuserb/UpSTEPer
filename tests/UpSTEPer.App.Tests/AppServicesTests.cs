using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;
using UpSTEPer.Occt;

namespace UpSTEPer.App.Tests;

public sealed class AppServicesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "upsteper-log-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_ResolvesReaderAndViewModel_AndLogsToFile()
    {
        using (var services = AppServices.Build(_directory, logToConsole: false))
        {
            Assert.IsType<OcctStepReader>(services.GetRequiredService<IStepReader>());
            using var viewModel = services.GetRequiredService<MainViewModel>();

            var logger = services.GetRequiredService<ILogger<AppServicesTests>>();
            Assert.True(logger.IsEnabled(LogLevel.Debug));
            logger.LogInformation("hello from the test");
        }

        var file = Assert.Single(Directory.GetFiles(_directory, "upsteper-*.log"));
        Assert.Contains("hello from the test", File.ReadAllText(file), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
