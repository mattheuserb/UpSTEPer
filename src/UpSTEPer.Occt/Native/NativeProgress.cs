using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using UpSTEPer.Core;

namespace UpSTEPer.Occt.Native;

/// <summary>
/// Connects a native <c>ups_progress_callback</c> to <see cref="IProgress{T}"/> and a
/// <see cref="CancellationToken"/> for the duration of one native call.
/// </summary>
/// <remarks>
/// The native side may invoke the callback from OCCT worker threads (calls are serialised), so
/// this class only touches thread-safe state. The instance is reachable through a
/// <see cref="GCHandle"/> passed as <c>user_data</c>; a static <see cref="UnmanagedCallersOnlyAttribute"/>
/// function needs no delegate that could be collected while native code still holds it.
/// </remarks>
internal sealed unsafe class NativeProgress : IDisposable
{
    private readonly IProgress<OperationProgress>? _progress;
    private readonly CancellationToken _cancellationToken;
    private GCHandle _handle;
    private Exception? _callbackFailure;

    /// <summary>Initializes a new instance of the <see cref="NativeProgress"/> class.</summary>
    /// <param name="progress">Receives reports; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the native operation.</param>
    public NativeProgress(IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        _progress = progress;
        _cancellationToken = cancellationToken;
        _handle = GCHandle.Alloc(this);
    }

    /// <summary>Gets the native callback.</summary>
    public static delegate* unmanaged[Cdecl]<nint, double, byte*, int> Callback => &OnProgress;

    /// <summary>Gets the <c>user_data</c> to pass with <see cref="Callback"/>.</summary>
    public nint UserData => GCHandle.ToIntPtr(_handle);

    /// <summary>
    /// Throws for the outcome of the native call: an exception thrown by the progress handler,
    /// <see cref="OperationCanceledException"/> on cancellation, otherwise as <see cref="NativeError.ThrowIfFailed"/>.
    /// </summary>
    /// <param name="status">The status returned by the native call (same thread).</param>
    /// <param name="operation">Human-readable name of the operation.</param>
    public void ThrowIfFailed(NativeStatus status, string operation)
    {
        if (_callbackFailure is not null)
        {
            ExceptionDispatchInfo.Throw(_callbackFailure);
        }

        if (status == NativeStatus.Cancelled)
        {
            throw new OperationCanceledException($"{operation} was cancelled.", _cancellationToken);
        }

        NativeError.ThrowIfFailed(status, operation);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "An exception must not unwind into native code (that terminates the process); it is rethrown after the native call returns.")]
    private static int OnProgress(nint userData, double fraction, byte* phase)
    {
        var self = (NativeProgress)GCHandle.FromIntPtr(userData).Target!;
        if (self._callbackFailure is not null)
        {
            return 1;
        }

        try
        {
            self._progress?.Report(new OperationProgress(fraction, Marshal.PtrToStringUTF8((nint)phase) ?? string.Empty));
            return self._cancellationToken.IsCancellationRequested ? 1 : 0;
        }
        catch (Exception exception)
        {
            self._callbackFailure = exception;
            return 1;
        }
    }
}
