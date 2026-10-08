using UpSTEPer.Occt.Native;

namespace UpSTEPer.Occt.Tests;

public class NativeLibraryResolverTests
{
    [Fact]
    public void GetPath_IsAbsoluteAndInsideApplicationDirectory()
    {
        var path = NativeLibraryResolver.GetPath(NativeMethods.LibraryName);

        Assert.True(Path.IsPathFullyQualified(path));
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),
            Path.GetDirectoryName(path));
    }

    [Fact]
    public void GetPath_UsesPlatformFileName()
    {
        var fileName = Path.GetFileName(NativeLibraryResolver.GetPath(NativeMethods.LibraryName));

        var expected = OperatingSystem.IsWindows() ? "upsteper_native.dll"
            : OperatingSystem.IsMacOS() ? "libupsteper_native.dylib"
            : "libupsteper_native.so";
        Assert.Equal(expected, fileName);
    }

    [Fact]
    public void NativeLibrary_LoadsFromApplicationDirectory()
    {
        TestFiles.RequireNativeLibrary();

        Assert.True(File.Exists(NativeLibraryResolver.GetPath(NativeMethods.LibraryName)));
        Assert.False(string.IsNullOrEmpty(OcctRuntime.Version));
    }
}
