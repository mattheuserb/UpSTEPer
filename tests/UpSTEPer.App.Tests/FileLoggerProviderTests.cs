using Microsoft.Extensions.Logging;
using UpSTEPer.App.Logging;

namespace UpSTEPer.App.Tests;

public sealed class FileLoggerProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "upsteper-log-tests", Guid.NewGuid().ToString("N"));
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 10, 9, 8, 30, 0, TimeSpan.Zero));

    [Fact]
    public void Log_WritesLevelCategoryMessageAndException()
    {
        using (var provider = new FileLoggerProvider(_directory, _clock))
        {
            var logger = provider.CreateLogger("UpSTEPer.Test");
            logger.LogWarning("Opening {Path} failed", "a.stp");
            logger.LogError(new InvalidOperationException("boom"), "Saving failed");
        }

        var lines = File.ReadAllLines(Path.Combine(_directory, "upsteper-20261009.log"));
        Assert.Equal("2026-10-09 08:30:00.000 WARN  UpSTEPer.Test: Opening a.stp failed", lines[0]);
        Assert.Equal("2026-10-09 08:30:00.000 ERROR UpSTEPer.Test: Saving failed", lines[1]);
        Assert.StartsWith("System.InvalidOperationException: boom", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Log_NextDay_StartsNewFile()
    {
        using (var provider = new FileLoggerProvider(_directory, _clock))
        {
            var logger = provider.CreateLogger("Test");
            logger.LogInformation("first");
            _clock.Now = _clock.Now.AddDays(1);
            logger.LogInformation("second");
        }

        Assert.Contains("first", File.ReadAllText(Path.Combine(_directory, "upsteper-20261009.log")), StringComparison.Ordinal);
        Assert.Contains("second", File.ReadAllText(Path.Combine(_directory, "upsteper-20261010.log")), StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DeletesFilesOlderThanRetention()
    {
        Directory.CreateDirectory(_directory);
        var old = Path.Combine(_directory, "upsteper-20260901.log");
        var recent = Path.Combine(_directory, "upsteper-20261008.log");
        var other = Path.Combine(_directory, "notes.txt");
        foreach (var file in new[] { old, recent, other })
        {
            File.WriteAllText(file, "x");
            File.SetLastWriteTimeUtc(file, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        File.SetLastWriteTimeUtc(recent, new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc));

        using var provider = new FileLoggerProvider(_directory, _clock, TimeSpan.FromDays(14));

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void Log_UnwritableDirectory_DoesNotThrow()
    {
        // A file where the directory should be: creating the log file fails.
        Directory.CreateDirectory(Path.GetDirectoryName(_directory)!);
        File.WriteAllText(_directory, "not a directory");
        try
        {
            using var provider = new FileLoggerProvider(_directory, _clock);
            var logger = provider.CreateLogger("Test");

            logger.LogError("first");
            logger.LogError("second");
        }
        finally
        {
            File.Delete(_directory);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
