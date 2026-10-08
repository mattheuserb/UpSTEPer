using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using UpSTEPer.App.Controls;
using UpSTEPer.App.ViewModels;

namespace UpSTEPer.App.Views;

/// <summary>The main window: toolbar, 3D view and status line.</summary>
[ExcludeFromCodeCoverage(Justification = "UI wiring; exercised by the viewer screenshot smoke test in CI.")]
internal sealed partial class MainWindow : Window
{
    private static readonly FilePickerFileType _stepFileType = new("STEP files")
    {
        Patterns = ["*.stp", "*.step", "*.STP", "*.STEP"],
    };

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow() => InitializeComponent();

    /// <summary>Gets the 3D view.</summary>
    public ModelView ModelView => ModelViewControl;

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open STEP file",
            AllowMultiple = false,
            FileTypeFilter = [_stepFileType],
        }).ConfigureAwait(true);

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await viewModel.LoadAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnExportDxfClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { HasDocument: true } viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export DXF drawing",
            DefaultExtension = "dxf",
            SuggestedFileName = "drawing.dxf",
            FileTypeChoices = [new FilePickerFileType("DXF drawing") { Patterns = ["*.dxf"] }],
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);

        if (file?.TryGetLocalPath() is { } path)
        {
            await viewModel.ExportDxfAsync(path).ConfigureAwait(true);
        }
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { HasDocument: true } viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save STEP file",
            DefaultExtension = "stp",
            SuggestedFileName = "model.stp",
            FileTypeChoices = [_stepFileType],
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);

        if (file?.TryGetLocalPath() is { } path)
        {
            await viewModel.SaveAsync(path).ConfigureAwait(true);
        }
    }
}
