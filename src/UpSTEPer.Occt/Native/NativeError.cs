using System.Runtime.InteropServices;
using UpSTEPer.Core;

namespace UpSTEPer.Occt.Native;

/// <summary>Translates native status codes into .NET exceptions.</summary>
internal static class NativeError
{
    /// <summary>Throws when <paramref name="status"/> is not <see cref="NativeStatus.Ok"/>.</summary>
    /// <param name="status">The status returned by the native call (must be called on the same thread).</param>
    /// <param name="operation">Human-readable name of the operation, used in the message.</param>
    public static void ThrowIfFailed(NativeStatus status, string operation)
    {
        if (status == NativeStatus.Ok)
        {
            return;
        }

        var detail = ReadUtf8(NativeMethods.LastErrorMessage());
        var message = string.IsNullOrEmpty(detail) ? $"{operation} failed ({status})." : $"{operation} failed: {detail}";

        throw status switch
        {
            NativeStatus.InvalidArgument => new ArgumentException(message),
            NativeStatus.FileRead or NativeStatus.Transfer => new StepImportException(message),
            _ => new GeometryOperationException(message),
        };
    }

    /// <summary>Reads a library-owned UTF-8 string.</summary>
    /// <param name="pointer">Pointer returned by the native library.</param>
    /// <returns>The string, or an empty string for null.</returns>
    public static string ReadUtf8(nint pointer) => Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
}
