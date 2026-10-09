using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using UpSTEPer.Occt.Native;

namespace UpSTEPer.Occt;

/// <summary>
/// Routes Open CASCADE's diagnostic messages (warnings about skipped STEP entities, translator
/// statistics, ...) to an <see cref="ILogger"/> while the instance is alive.
/// </summary>
/// <remarks>
/// OCCT has one process-wide message channel, so only one router can be active: creating a new one
/// replaces the previous one, and disposing an outdated router has no effect. OCCT levels map to
/// <see cref="LogLevel"/> as Trace and Info → Debug, Warning → Warning, Alarm and Fail → Error.
/// The STEP translators report even real file problems ("Incorrect record ...") at OCCT's Trace
/// level, mixed with statistics, so Debug is the level to enable when diagnosing a file.
/// Messages may be logged from any thread.
/// </remarks>
public sealed unsafe partial class OcctMessageRouter : IDisposable
{
    private static readonly Lock _gate = new();
    private static OcctMessageRouter? _active;

    private readonly ILogger _logger;
    private GCHandle _handle;

    /// <summary>Initializes a new instance of the <see cref="OcctMessageRouter"/> class and starts routing.</summary>
    /// <param name="logger">Receives the messages; only levels it has enabled are requested from OCCT.</param>
    /// <exception cref="DllNotFoundException">The native library is missing.</exception>
    public OcctMessageRouter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handle = GCHandle.Alloc(this);

        lock (_gate)
        {
            NativeError.ThrowIfFailed(
                NativeMethods.SetMessageHandler(&OnMessage, GCHandle.ToIntPtr(_handle), MinimumLevel(logger)),
                "Routing OCCT messages");
            _active?.ReleaseHandle();
            _active = this;
        }
    }

    /// <summary>
    /// Starts routing like the constructor, but returns <see langword="null"/> instead of throwing
    /// when the native library is missing, so an app can still start and report that later.
    /// </summary>
    /// <param name="logger">Receives the messages.</param>
    /// <returns>The router to dispose at exit, or <see langword="null"/>.</returns>
    public static OcctMessageRouter? TryStart(ILogger logger)
    {
        try
        {
            return new OcctMessageRouter(logger);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>Stops routing (if this is still the active router).</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (ReferenceEquals(_active, this))
            {
                // Afterwards the native side never calls the old handler again, so the handle can go.
                NativeError.ThrowIfFailed(NativeMethods.SetMessageHandler(null, 0, NativeMessageLevel.Fail), "Stopping OCCT message routing");
                _active = null;
            }

            ReleaseHandle();
        }
    }

    internal static LogLevel ToLogLevel(NativeMessageLevel level) => level switch
    {
        NativeMessageLevel.Trace or NativeMessageLevel.Info => LogLevel.Debug,
        NativeMessageLevel.Warning => LogLevel.Warning,
        _ => LogLevel.Error,
    };

    private static NativeMessageLevel MinimumLevel(ILogger logger) =>
        logger.IsEnabled(LogLevel.Debug) ? NativeMessageLevel.Trace
        : logger.IsEnabled(LogLevel.Warning) ? NativeMessageLevel.Warning
        : NativeMessageLevel.Alarm;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "An exception must not unwind into native code (that terminates the process); a failing logger loses the message.")]
    private static void OnMessage(nint userData, NativeMessageLevel level, byte* text)
    {
        try
        {
            var logLevel = ToLogLevel(level);
            if (GCHandle.FromIntPtr(userData).Target is OcctMessageRouter router && router._logger.IsEnabled(logLevel))
            {
                var message = ToSingleLine(Marshal.PtrToStringUTF8((nint)text));
                LogOcctMessage(router._logger, logLevel, message);
            }
        }
        catch (Exception)
        {
            // Nothing sensible to do: the logger itself failed.
        }
    }

    // OCCT messages often span several lines; one line per log entry keeps log files greppable.
    internal static string ToSingleLine(string? text) =>
        string.Join(" | ", (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    [LoggerMessage(EventId = 1000, Message = "{Text}")]
    private static partial void LogOcctMessage(ILogger logger, LogLevel level, string text);

    private void ReleaseHandle()
    {
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }
    }
}
