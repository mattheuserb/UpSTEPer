using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using UpSTEPer.App.ViewModels;

namespace UpSTEPer.App.Views;

/// <summary>Help → About: versions, system and where the logs are (content: <see cref="AboutInfo"/>).</summary>
[ExcludeFromCodeCoverage(Justification = "UI wiring: clipboard and launcher; the content is AboutInfo, tested there.")]
internal sealed partial class AboutWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="AboutWindow"/> class.</summary>
    public AboutWindow() => InitializeComponent();

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AboutInfo info && Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(info.ToText()).ConfigureAwait(true);
        }
    }

    private async void OnOpenLogsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AboutInfo info)
        {
            Directory.CreateDirectory(info.LogDirectory);
            await Launcher.LaunchUriAsync(new Uri(info.LogDirectory)).ConfigureAwait(true);
        }
    }

    private async void OnOpenNoticesClick(object? sender, RoutedEventArgs e)
    {
        if (File.Exists(AboutInfo.NoticesPath))
        {
            await Launcher.LaunchUriAsync(new Uri(AboutInfo.NoticesPath)).ConfigureAwait(true);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
