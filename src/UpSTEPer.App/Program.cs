using Avalonia;
using Avalonia.Logging;

namespace UpSTEPer.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var options = AppOptions.Parse(args);
        return BuildAvaloniaApp(options).StartWithClassicDesktopLifetime(args);
    }

    // Used by the Avalonia designer.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(AppOptions.Default);

    private static AppBuilder BuildAvaloniaApp(AppOptions options) =>
        AppBuilder.Configure(() => new DesktopApplication(options))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTextWriter(Console.Error, LogEventLevel.Warning);
}
