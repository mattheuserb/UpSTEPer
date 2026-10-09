using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace UpSTEPer.App.Logging;

/// <summary>
/// Writes log entries to one text file per day (<c>upsteper-yyyyMMdd.log</c>) and deletes files
/// older than the retention period on start-up.
/// </summary>
/// <remarks>
/// Logging must never take the app down: when the directory or file cannot be written, file
/// logging switches itself off. Entries are flushed immediately, so a crash loses nothing.
/// </remarks>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private StreamWriter? _writer;
    private DateOnly _writerDate;
    private bool _failed;

    /// <summary>Initializes a new instance of the <see cref="FileLoggerProvider"/> class.</summary>
    /// <param name="directory">The log directory; created when missing.</param>
    /// <param name="time">The clock (local time decides the file name); <see langword="null"/> for the system clock.</param>
    /// <param name="retention">How long log files are kept; default 14 days.</param>
    public FileLoggerProvider(string directory, TimeProvider? time = null, TimeSpan? retention = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _time = time ?? TimeProvider.System;
        DeleteOldFiles(retention ?? TimeSpan.FromDays(14));
    }

    /// <summary>Gets the default log directory: <c>UpSTEPer/logs</c> in the user's local application data.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UpSTEPer", "logs");

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
            _failed = true;
        }
    }

    /// <summary>Gets the path of the log file for <paramref name="date"/>.</summary>
    /// <param name="date">The local date.</param>
    /// <returns>The file path.</returns>
    internal string GetFilePath(DateOnly date) =>
        Path.Combine(_directory, string.Create(CultureInfo.InvariantCulture, $"upsteper-{date:yyyyMMdd}.log"));

    private void Write(LogLevel level, string category, string message, Exception? exception)
    {
        var now = _time.GetLocalNow();
        var line = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{now:yyyy-MM-dd HH:mm:ss.fff} {LevelName(level)} {category}: {message}");
        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        lock (_lock)
        {
            if (_failed)
            {
                return;
            }

            try
            {
                var date = DateOnly.FromDateTime(now.DateTime);
                if (_writer is null || date != _writerDate)
                {
                    _writer?.Dispose();
                    Directory.CreateDirectory(_directory);
                    _writer = new StreamWriter(GetFilePath(date), append: true, new UTF8Encoding(false)) { AutoFlush = true };
                    _writerDate = date;
                }

                _writer.WriteLine(line.ToString());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _failed = true;
                _writer?.Dispose();
                _writer = null;
            }
        }
    }

    private void DeleteOldFiles(TimeSpan retention)
    {
        var cutoff = _time.GetUtcNow() - retention;
        try
        {
            if (!Directory.Exists(_directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(_directory, "upsteper-*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff.UtcDateTime)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Old files are harmless; try again next start.
        }
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        _ => "CRIT ",
    };

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}
