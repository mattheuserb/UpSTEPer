using System.Reflection;
using System.Runtime.InteropServices;
using UpSTEPer.App.Logging;
using UpSTEPer.Occt;

namespace UpSTEPer.App.ViewModels;

/// <summary>What Help → About shows, and copies for bug reports.</summary>
internal sealed class AboutInfo
{
    /// <summary>Initializes a new instance of the <see cref="AboutInfo"/> class.</summary>
    /// <param name="appVersion">The app version.</param>
    /// <param name="geometryVersion">The native bridge and OCCT versions, or why they are unavailable.</param>
    /// <param name="logDirectory">Where the log files are.</param>
    public AboutInfo(string appVersion, string geometryVersion, string logDirectory)
    {
        AppVersion = appVersion;
        GeometryVersion = geometryVersion;
        LogDirectory = logDirectory;
    }

    /// <summary>Gets the app version, e.g. "0.1.0-alpha".</summary>
    public string AppVersion { get; }

    /// <summary>Gets the versions of the native bridge and of Open CASCADE Technology.</summary>
    public string GeometryVersion { get; }

    /// <summary>Gets the .NET runtime.</summary>
    public string Runtime { get; } = RuntimeInformation.FrameworkDescription;

    /// <summary>Gets the operating system and processor architecture.</summary>
    public string OperatingSystem { get; } = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

    /// <summary>Gets the directory of the log files.</summary>
    public string LogDirectory { get; }

    /// <summary>Gets the third-party notices shipped next to the app.</summary>
    public static string NoticesPath => Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md");

    /// <summary>Gets the information of the running app.</summary>
    /// <returns>The information.</returns>
    public static AboutInfo ForThisApp() =>
        new(VersionOf(typeof(AboutInfo).Assembly), GeometryVersionOrReason(), FileLoggerProvider.DefaultDirectory);

    /// <summary>Gets the information as plain text, one item per line (for "Copy info").</summary>
    /// <returns>The text.</returns>
    public string ToText() =>
        string.Join(
            Environment.NewLine,
            $"UpSTEPer {AppVersion}",
            $"Geometry: {GeometryVersion}",
            $"Runtime: {Runtime}",
            $"OS: {OperatingSystem}",
            $"Logs: {LogDirectory}");

    /// <summary>Gets the informational version without the commit hash the SDK appends, e.g. "0.1.0-alpha".</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The version, or "unknown".</returns>
    internal static string VersionOf(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrEmpty(version) ? "unknown" : version.Split('+')[0];
    }

    private static string GeometryVersionOrReason()
    {
        try
        {
            return $"UpSTEPer native {OcctRuntime.Version}, Open CASCADE Technology {OcctRuntime.OcctVersion}";
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return "native library not available";
        }
    }
}
