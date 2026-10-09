using Microsoft.Extensions.Logging;
using UpSTEPer.Occt.Native;

namespace UpSTEPer.Occt.Tests;

// OCCT has one process-wide message channel. Only this class routes it; xUnit runs the tests of
// one class sequentially, so they cannot interfere with each other.
public class OcctMessageRouterTests
{
    [Fact]
    public void Load_WithDebugLogger_ReceivesOcctMessages()
    {
        TestFiles.RequireNativeLibrary();
        var logger = new RecordingLogger(LogLevel.Debug);

        using (new OcctMessageRouter(logger))
        {
            using var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(logger.Entries);
        Assert.All(logger.Entries, entry => Assert.DoesNotMatch(@"^\s|\s$|[\r\n]", entry.Message));
    }

    [Fact]
    public void Dispose_StopsRouting()
    {
        TestFiles.RequireNativeLibrary();
        var logger = new RecordingLogger(LogLevel.Trace);

        // Other test classes may load files while the router is registered; only what arrives
        // after Dispose matters.
        new OcctMessageRouter(logger).Dispose();
        var beforeLoad = logger.Entries.Count;
        using var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(beforeLoad, logger.Entries.Count);
    }

    [Fact]
    public void NewRouter_ReplacesPreviousOne()
    {
        TestFiles.RequireNativeLibrary();
        var first = new RecordingLogger(LogLevel.Trace);
        var second = new RecordingLogger(LogLevel.Trace);

        // Concurrent loads of other test classes may reach "first" before it is replaced; after
        // that, nothing may.
        using var outdated = new OcctMessageRouter(first);
        int firstBeforeLoad;
        using (new OcctMessageRouter(second))
        {
            firstBeforeLoad = first.Entries.Count;
            outdated.Dispose(); // no effect: no longer the active router
            using var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.Equal(firstBeforeLoad, first.Entries.Count);
        Assert.NotEmpty(second.Entries);
    }

    [Fact]
    public void Load_WithWarningLogger_ReceivesNoTranslatorStatistics()
    {
        TestFiles.RequireNativeLibrary();
        var logger = new RecordingLogger(LogLevel.Warning);

        using (new OcctMessageRouter(logger))
        {
            using var document = new OcctStepReader().Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.All(logger.Entries, entry => Assert.True(entry.Level >= LogLevel.Warning, entry.Message));
    }

    [Theory]
    [InlineData(0, LogLevel.Debug)]
    [InlineData(1, LogLevel.Debug)]
    [InlineData(2, LogLevel.Warning)]
    [InlineData(3, LogLevel.Error)]
    [InlineData(4, LogLevel.Error)]
    public void ToLogLevel_MapsOcctGravity(int level, LogLevel expected)
    {
        Assert.Equal(expected, OcctMessageRouter.ToLogLevel((NativeMessageLevel)level));
    }

    [Theory]
    [InlineData("  one line  ", "one line")]
    [InlineData("first\r\n  second \n\n", "first | second")]
    [InlineData(null, "")]
    public void ToSingleLine_JoinsTrimmedLines(string? text, string expected)
    {
        Assert.Equal(expected, OcctMessageRouter.ToSingleLine(text));
    }

    [Fact]
    public void Reader_WithLogger_LogsLoadTime()
    {
        TestFiles.RequireNativeLibrary();
        var logger = new RecordingLogger<OcctStepReader>(LogLevel.Information);

        using var document = new OcctStepReader(logger).Load(TestFiles.CableGland, cancellationToken: TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains($"{document.Statistics.FaceCount} faces", entry.Message, StringComparison.Ordinal);
    }
}
