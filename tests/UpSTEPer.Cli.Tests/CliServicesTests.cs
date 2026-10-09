using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpSTEPer.Core;
using UpSTEPer.Occt;

namespace UpSTEPer.Cli.Tests;

public class CliServicesTests
{
    [Theory]
    [InlineData(new[] { "info", "a.stp", "--verbose" }, true)]
    [InlineData(new[] { "--verbose", "info", "a.stp" }, true)]
    [InlineData(new[] { "info", "a.stp" }, false)]
    [InlineData(new[] { "info", "--verbose-not" }, false)]
    public void IsVerbose_DetectsOption(string[] args, bool expected)
    {
        Assert.Equal(expected, CliServices.IsVerbose(args));
    }

    [Theory]
    [InlineData(false, LogLevel.Warning)]
    [InlineData(true, LogLevel.Debug)]
    public void Build_ResolvesReaderWithMinimumLevel(bool verbose, LogLevel minimum)
    {
        using var services = CliServices.Build(verbose);

        Assert.IsType<OcctStepReader>(services.GetRequiredService<IStepReader>());
        var logger = services.GetRequiredService<ILogger<CliServicesTests>>();
        Assert.True(logger.IsEnabled(minimum));
        Assert.False(logger.IsEnabled(minimum - 1));
    }
}
