using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UpSTEPer.Core;
using UpSTEPer.Occt.Native;

namespace UpSTEPer.Occt;

/// <summary>Reads STEP files with Open CASCADE (XDE), preserving colours and assembly structure.</summary>
public sealed partial class OcctStepReader : IStepReader
{
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="OcctStepReader"/> class.</summary>
    /// <param name="logger">Logs load times and cancellations; <see langword="null"/> for none.</param>
    public OcctStepReader(ILogger<OcctStepReader>? logger = null) => _logger = logger ?? NullLogger<OcctStepReader>.Instance;

    /// <inheritdoc />
    public IStepDocument Load(string path, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("STEP file not found.", fullPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var watch = Stopwatch.StartNew();
        using var nativeProgress = new NativeProgress(progress, cancellationToken);
        DocumentSafeHandle handle;
        NativeStatus status;
        unsafe
        {
            status = NativeMethods.DocumentLoadStepWithProgress(fullPath, NativeProgress.Callback, nativeProgress.UserData, out handle);
        }

        try
        {
            // Also throws when the progress handler failed although loading itself completed.
            nativeProgress.ThrowIfFailed(status, $"Loading '{fullPath}'");
        }
        catch (OperationCanceledException)
        {
            handle.Dispose();
            LogLoadCancelled(_logger, fullPath, watch.ElapsedMilliseconds);
            throw;
        }
        catch
        {
            handle.Dispose();
            throw;
        }

        var document = new OcctStepDocument(handle, fullPath);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogLoaded(_logger, fullPath, watch.ElapsedMilliseconds, document.Statistics.SolidCount, document.Statistics.FaceCount);
        }

        return document;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Loaded {Path} in {ElapsedMs} ms: {SolidCount} solids, {FaceCount} faces")]
    private static partial void LogLoaded(ILogger logger, string path, long elapsedMs, int solidCount, int faceCount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Loading {Path} cancelled after {ElapsedMs} ms")]
    private static partial void LogLoadCancelled(ILogger logger, string path, long elapsedMs);
}
