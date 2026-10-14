using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpSTEPer.App.Controls;
using UpSTEPer.App.Localization;
using UpSTEPer.App.Logging;
using UpSTEPer.App.Settings;
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
    private const double WindowScreenshotWidth = 1280;
    private const double WindowScreenshotHeight = 800;
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

    // The 3D frame of a whole-window screenshot, before it is combined with the rest of the window.
    private string ViewScreenshotPath => _options.ScreenshotPath + ".view.png";

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Automation mode (screenshot) also logs to stderr, so CI shows GL and OCCT diagnostics.
            // Screenshot runs (automation, CI) must not add their file to the user's recent files.
            var settingsPath = _options.ScreenshotPath is null ? null : Path.Combine(Path.GetTempPath(), $"upsteper-settings-{Guid.NewGuid():N}.json");
            var services = AppServices.Build(FileLoggerProvider.DefaultDirectory, logToConsole: _options.ScreenshotPath is not null, settingsPath);
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var occtMessages = OcctMessageRouter.TryStart(loggerFactory.CreateLogger("OCCT"));
            LogUnhandledExceptions(loggerFactory.CreateLogger<DesktopApplication>());

            // The stored language, else the system's (German or English); before any window or text exists.
            var settings = services.GetRequiredService<SettingsStore>();
            Localizer.Instance.SetCulture(Localizer.Resolve(_options.Language ?? settings.Current.Language, CultureInfo.CurrentUICulture));

            var viewModel = services.GetRequiredService<MainViewModel>();
            viewModel.DisplayQuality = _options.Quality;
            var window = new MainWindow { DataContext = viewModel };
            window.ModelView.Logger = loggerFactory.CreateLogger<ModelView>();
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                viewModel.Dispose();
                occtMessages?.Dispose();
                services.Dispose();
            };

            if (_options.ScreenshotPath is not null)
            {
                // The screenshot itself is requested after loading and selection (below), so the
                // frame reflects both.
                window.ModelView.ScreenshotSaved += (_, _) =>
                {
                    if (_options.WindowScreenshot)
                    {
                        WindowCapture.Save(window, window.ModelView, ViewScreenshotPath, _options.ScreenshotPath);
                        File.Delete(ViewScreenshotPath);
                    }

                    desktop.Shutdown(0);
                };
                if (_options.WindowScreenshot)
                {
                    // Fixed size, so pictures in the documentation look the same on every machine.
                    window.Width = WindowScreenshotWidth;
                    window.Height = WindowScreenshotHeight;
                }

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

                    if (loaded && _options.HoverFace is { } hovered)
                    {
                        viewModel.HoveredFace = new FaceId(hovered);
                    }

                    viewModel.IsOrthographic |= _options.Orthographic;
                    if (loaded && _options.View is { } view)
                    {
                        window.ModelView.ShowView(view);
                    }

                    window.ModelView.ScreenshotPath = _options.WindowScreenshot ? ViewScreenshotPath : _options.ScreenshotPath;
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Exceptions that escape event handlers or tasks would otherwise vanish or end the process
    // without a trace in the log.
    private static void LogUnhandledExceptions(ILogger logger)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogUnhandled(logger, e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => LogUnobservedTask(logger, e.Exception);
        Dispatcher.UIThread.UnhandledException += (_, e) => LogUnhandled(logger, e.Exception);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception? exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Unobserved task exception")]
    private static partial void LogUnobservedTask(ILogger logger, Exception exception);
}
