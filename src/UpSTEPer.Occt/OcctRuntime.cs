using UpSTEPer.Occt.Native;

namespace UpSTEPer.Occt;

/// <summary>Information about the native geometry library.</summary>
public static class OcctRuntime
{
    /// <summary>Gets a value indicating whether the native library can be loaded on this machine.</summary>
    /// <remarks>False typically means the native build has not been run; see docs/development.md.</remarks>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                _ = NativeMethods.Version();
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }

    /// <summary>Gets the version of the upsteper_native library.</summary>
    public static string Version => NativeError.ReadUtf8(NativeMethods.Version());

    /// <summary>Gets the version of the linked Open CASCADE Technology.</summary>
    public static string OcctVersion => NativeError.ReadUtf8(NativeMethods.OcctVersion());
}
