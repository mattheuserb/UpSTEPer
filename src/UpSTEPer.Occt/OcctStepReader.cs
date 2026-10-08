using UpSTEPer.Core;
using UpSTEPer.Occt.Native;

namespace UpSTEPer.Occt;

/// <summary>Reads STEP files with Open CASCADE (XDE), preserving colours and assembly structure.</summary>
public sealed class OcctStepReader : IStepReader
{
    /// <inheritdoc />
    public IStepDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("STEP file not found.", fullPath);
        }

        var status = NativeMethods.DocumentLoadStep(fullPath, out var handle);
        if (status != NativeStatus.Ok)
        {
            handle.Dispose();
            NativeError.ThrowIfFailed(status, $"Loading '{fullPath}'");
        }

        return new OcctStepDocument(handle, fullPath);
    }
}
