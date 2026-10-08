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
            // macOS: Avalonia 12 composes with Metal by default, which gives OpenGlControlBase no GL
            // context. The 3D view needs the OpenGL compositor (see ADR 0004).
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software],
            })
            // Linux: Avalonia refuses the Mesa software renderer (llvmpipe) and VMware's SVGA3D driver
            // and then renders without OpenGL, which leaves the 3D view empty. A slow 3D view in a VM or
            // without GPU drivers is better than none.
            .With(new X11PlatformOptions { GlxRendererBlacklist = [] })
            .WithInterFont()
            .LogToTextWriter(Console.Error, LogEventLevel.Warning);
}
