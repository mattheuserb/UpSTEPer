using Avalonia;
using Avalonia.Headless;
using UpSTEPer.App.Tests;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace UpSTEPer.App.Tests;

/// <summary>Avalonia application for UI tests without a display or GPU.</summary>
internal sealed class HeadlessTestApp : Application
{
    /// <summary>The session shared by all UI tests; it lives as long as the test process.</summary>
    public static HeadlessUnitTestSession Session => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessTestApp).Assembly);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
