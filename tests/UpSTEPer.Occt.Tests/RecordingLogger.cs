using Microsoft.Extensions.Logging;

namespace UpSTEPer.Occt.Tests;

/// <summary>Collects formatted log entries at or above a minimum level (thread-safe).</summary>
internal class RecordingLogger(LogLevel minimumLevel) : ILogger
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
        {
            lock (_entries)
            {
                _entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}

/// <summary>Typed variant for components that take <see cref="ILogger{TCategoryName}"/>.</summary>
internal sealed class RecordingLogger<T>(LogLevel minimumLevel) : RecordingLogger(minimumLevel), ILogger<T>;
