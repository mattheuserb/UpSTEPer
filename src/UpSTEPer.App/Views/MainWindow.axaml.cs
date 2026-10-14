using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using UpSTEPer.App.Controls;
using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;

namespace UpSTEPer.App.Views;

/// <summary>
/// The main window: menu, toolbars, model panel, 3D view and status bar. The commands here need
/// the window (file pickers, dialogs); menu entries, toolbar buttons and shortcuts share them.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "UI wiring; exercised by the viewer screenshot smoke test in CI.")]
internal sealed partial class MainWindow : Window
{
    private static readonly FilePickerFileType _stepFileType = new("STEP files")
    {
        Patterns = ["*.stp", "*.step", "*.STP", "*.STEP"],
    };

    private static readonly FilePickerFileType _stepAp214FileType = new("STEP AP214 (widest support)")
    {
        Patterns = ["*.stp", "*.step"],
    };

    private static readonly FilePickerFileType _stepAp242FileType = new("STEP AP242")
    {
        Patterns = ["*.stp", "*.step"],
    };

    private MainViewModel? _viewModel;

    // Width of the model panel before it was hidden, restored when it is shown again.
    private GridLength _modelPanelWidth = new(260);

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        OpenCommand = new AsyncRelayCommand(OpenAsync);
        SaveAsCommand = new AsyncRelayCommand(SaveAsAsync);
        RepairCommand = new AsyncRelayCommand(RepairAsync);
        ExportDxfCommand = new AsyncRelayCommand(ExportDxfAsync);
        ShowShortcutsCommand = new AsyncRelayCommand(() => new ShortcutsWindow().ShowDialog(this));
        OpenCustomColourCommand = new RelayCommand(OpenCustomColour);
        ExitCommand = new RelayCommand(Close);
        InitializeComponent();
    }

    /// <summary>Gets the 3D view.</summary>
    public ModelView ModelView => ModelViewControl;

    /// <summary>Gets the command that opens a STEP file (Ctrl+O).</summary>
    public ICommand OpenCommand { get; }

    /// <summary>Gets the command that saves the model as STEP AP214 or AP242 (Ctrl+S).</summary>
    public ICommand SaveAsCommand { get; }

    /// <summary>Gets the command that opens the check and repair dialog.</summary>
    public ICommand RepairCommand { get; }

    /// <summary>Gets the command that exports the standard views as DXF.</summary>
    public ICommand ExportDxfCommand { get; }

    /// <summary>Gets the command that lists all mouse and keyboard shortcuts (F1).</summary>
    public ICommand ShowShortcutsCommand { get; }

    /// <summary>Gets the command that opens the colour panel with the custom colour picker expanded.</summary>
    public ICommand OpenCustomColourCommand { get; }

    /// <summary>Gets the command that closes the window.</summary>
    public ICommand ExitCommand { get; }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);
    }

    // The splitter sets an absolute width on the panel column; hiding the panel must free it.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsModelPanelVisible) || _viewModel is null)
        {
            return;
        }

        var column = ContentGrid.ColumnDefinitions[0];
        if (_viewModel.IsModelPanelVisible)
        {
            column.Width = _modelPanelWidth;
        }
        else
        {
            _modelPanelWidth = column.Width;
            column.Width = new GridLength(0);
        }
    }

    private async Task OpenAsync()
    {
        if (_viewModel is not { IsBusy: false } viewModel)
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

    private async Task SaveAsAsync()
    {
        if (_viewModel is not { HasDocument: true, IsBusy: false } viewModel)
        {
            return;
        }

        // Both protocols use .stp, so the chosen file type selects the schema.
        var result = await StorageProvider.SaveFilePickerWithResultAsync(new FilePickerSaveOptions
        {
            Title = "Save STEP file",
            DefaultExtension = "stp",
            SuggestedFileName = "model.stp",
            FileTypeChoices = [_stepAp214FileType, _stepAp242FileType],
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);

        if (result.File?.TryGetLocalPath() is { } path)
        {
            var schema = result.SelectedFileType == _stepAp242FileType ? StepSchema.Ap242 : StepSchema.Ap214;
            await viewModel.SaveAsync(path, schema).ConfigureAwait(true);
        }
    }

    private async Task RepairAsync()
    {
        if (_viewModel is { HasDocument: true, IsBusy: false } viewModel)
        {
            await new RepairWindow { DataContext = new RepairViewModel(viewModel) }.ShowDialog(this).ConfigureAwait(true);
        }
    }

    private async Task ExportDxfAsync()
    {
        if (_viewModel is not { HasDocument: true, IsBusy: false } viewModel)
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

    private void OpenCustomColour()
    {
        CustomColourExpander.IsExpanded = true;
        ColourButton.Flyout?.ShowAt(ColourButton);
    }

    // Starts at the active colour, so it can be varied rather than picked from scratch.
    private void OnCustomColourExpanding(object? sender, CancelRoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            CustomSpectrum.Color = Color.Parse(_viewModel.SelectedPaletteEntry.Hex);
            CustomHexBox.Text = _viewModel.SelectedPaletteEntry.Hex;
        }
    }

    private void OnUseCustomColorClick(object? sender, RoutedEventArgs e)
    {
        ApplyCustomHex();
        if (_viewModel is not null)
        {
            var color = CustomSpectrum.Color;
            _viewModel.UseCustomColor(ColorRgb.FromBytes(color.R, color.G, color.B));
        }

        CustomColourExpander.IsExpanded = false;
    }

    private void OnCustomSpectrumColorChanged(object? sender, ColorChangedEventArgs e) =>
        CustomHexBox.Text = $"#{e.NewColor.R:X2}{e.NewColor.G:X2}{e.NewColor.B:X2}";

    private void OnCustomHexKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyCustomHex();
            e.Handled = true;
        }
    }

    private void OnCustomHexLostFocus(object? sender, RoutedEventArgs e) => ApplyCustomHex();

    // A valid typed value moves the spectrum there; anything else is replaced by the current colour.
    private void ApplyCustomHex()
    {
        var text = CustomHexBox.Text?.Trim() ?? string.Empty;
        if (Color.TryParse(text.StartsWith('#') ? text : "#" + text, out var color) && text.TrimStart('#').Length == 6)
        {
            CustomSpectrum.Color = color;
        }

        var current = CustomSpectrum.Color;
        CustomHexBox.Text = $"#{current.R:X2}{current.G:X2}{current.B:X2}";
    }
}
