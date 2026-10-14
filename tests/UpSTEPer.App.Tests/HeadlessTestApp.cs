using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using UpSTEPer.App.Tests;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace UpSTEPer.App.Tests;

/// <summary>Avalonia application for UI tests without a display or GPU.</summary>
internal sealed class HeadlessTestApp : Application
{
    /// <summary>The session shared by all UI tests; it lives as long as the test process.</summary>
    public static HeadlessUnitTestSession Session => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessTestApp).Assembly);

    // Popups (flyouts) render into the window's overlay layer, which needs the window template of
    // a theme; the same themes as the app keep control templates realistic.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://UpSTEPer.App.Tests/"))
        {
            Source = new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://UpSTEPer.App.Tests/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        });

        // The app's own resources and styles, as DesktopApplication.axaml includes them. avares://
        // only finds loaded assemblies: load the app explicitly, or a UI test that runs first fails.
        RuntimeHelpers.RunClassConstructor(typeof(UpSTEPer.App.Controls.Icon).TypeHandle);
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://UpSTEPer.App.Tests/"))
        {
            Source = new Uri("avares://UpSTEPer.Desktop/Themes/AppResources.axaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://UpSTEPer.App.Tests/"))
        {
            Source = new Uri("avares://UpSTEPer.Desktop/Themes/Styles.axaml"),
        });
    }
}
