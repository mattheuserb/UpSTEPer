using UpSTEPer.Core;
using UpSTEPer.Occt.Native;

namespace UpSTEPer.Occt;

/// <summary>Reads STEP files with Open CASCADE (XDE), preserving colours and assembly structure.</summary>
public sealed class OcctStepReader : IStepReader
{
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
        catch
        {
            handle.Dispose();
            throw;
        }

        return new OcctStepDocument(handle, fullPath);
    }
}
