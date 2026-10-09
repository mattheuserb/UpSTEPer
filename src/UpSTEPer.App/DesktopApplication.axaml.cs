using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using UpSTEPer.App.ViewModels;
using UpSTEPer.App.Views;
using UpSTEPer.Core;
using UpSTEPer.Occt;

namespace UpSTEPer.App;

/// <summary>The Avalonia application. Composition root of the desktop app.</summary>
[ExcludeFromCodeCoverage(Justification = "UI wiring; exercised by the viewer screenshot smoke test in CI.")]
internal sealed partial class DesktopApplication : Application
{
    private const int ScreenshotTimeoutExitCode = 2;
    private const int RenderingFailedExitCode = 3;
    private static readonly TimeSpan _screenshotTimeout = TimeSpan.FromSeconds(60);

    private readonly AppOptions _options;

    /// <summary>Initializes a new instance of the <see cref="DesktopApplication"/> class (designer).</summary>
    public DesktopApplication()
        : this(AppOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DesktopApplication"/> class.</summary>
    /// <param name="options">Command-line options.</param>
    public DesktopApplication(AppOptions options) => _options = options;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainViewModel(new OcctStepReader());
            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => viewModel.Dispose();

            if (_options.ScreenshotPath is not null)
            {
                // Automation mode: make GL diagnostics visible on stderr. The screenshot itself is
                // requested after loading and selection (below), so the frame reflects both.
                Trace.Listeners.Add(new ConsoleTraceListener(useErrorStream: true));
                window.ModelView.ScreenshotSaved += (_, _) => desktop.Shutdown(0);
                window.ModelView.RenderingFailed += (_, _) => desktop.Shutdown(RenderingFailedExitCode);

                // Never hang in automation (CI): give up when no frame was rendered in time.
                DispatcherTimer.RunOnce(() => desktop.Shutdown(ScreenshotTimeoutExitCode), _screenshotTimeout);
            }

            if (_options.FilePath is { } filePath)
            {
                window.Opened += async (_, _) =>
                {
                    var loaded = await viewModel.LoadAsync(filePath).ConfigureAwait(true);
                    if (!loaded && _options.ScreenshotPath is not null)
                    {
                        desktop.Shutdown(1);
                        return;
                    }

                    if (loaded && _options.SelectFace is { } face)
                    {
                        viewModel.SelectedFace = new FaceId(face);
                    }

                    viewModel.IsOrthographic |= _options.Orthographic;
                    if (loaded && _options.View is { } view)
                    {
                        window.ModelView.ShowView(view);
                    }

                    window.ModelView.ScreenshotPath = _options.ScreenshotPath;
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
