using System.Reflection;
using System.Runtime.InteropServices;

namespace UpSTEPer.Occt.Native;

/// <summary>
/// Loads the native bridge by full path from the application directory.
/// </summary>
/// <remarks>
/// <see cref="DefaultDllImportSearchPathsAttribute"/> alone is not enough: on Linux and macOS the
/// runtime only honours its <c>AssemblyDirectory</c> bit, which CA5393 rates unsafe. Loading by
/// absolute path is explicit and identical on all platforms. Dependencies (OCCT) are found next
/// to the library: on Windows by the loader's DLL-directory rule, elsewhere via RPATH
/// <c>$ORIGIN</c>/<c>@loader_path</c> (native/CMakeLists.txt).
/// </remarks>
internal static class NativeLibraryResolver
{
    /// <summary>Registers the resolver for <paramref name="assembly"/>. Call once per assembly.</summary>
    /// <param name="assembly">The assembly whose P/Invokes reference <paramref name="libraryName"/>.</param>
    /// <param name="libraryName">The library name used in <see cref="LibraryImportAttribute"/>.</param>
    public static void Register(Assembly assembly, string libraryName)
    {
        NativeLibrary.SetDllImportResolver(assembly, (name, _, _) =>
            name == libraryName && NativeLibrary.TryLoad(GetPath(libraryName), out var handle) ? handle : IntPtr.Zero);
    }

    /// <summary>Gets the platform-specific absolute path of a native library in the application directory.</summary>
    /// <param name="libraryName">The library name without prefix or extension.</param>
    /// <returns>The absolute path, e.g. <c>/app/libupsteper_native.so</c>.</returns>
    public static string GetPath(string libraryName)
    {
        var fileName = OperatingSystem.IsWindows() ? $"{libraryName}.dll"
            : OperatingSystem.IsMacOS() ? $"lib{libraryName}.dylib"
            : $"lib{libraryName}.so";
        return Path.Combine(AppContext.BaseDirectory, fileName);
    }
}
