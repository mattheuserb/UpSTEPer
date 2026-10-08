using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using UpSTEPer.App.Controls;
using UpSTEPer.App.ViewModels;

namespace UpSTEPer.App.Views;

/// <summary>The main window: toolbar, 3D view and status line.</summary>
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
}
