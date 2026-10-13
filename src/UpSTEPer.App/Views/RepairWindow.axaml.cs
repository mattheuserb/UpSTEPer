using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UpSTEPer.App.Views;

/// <summary>Dialog to check the model for defects and repair it (see <see cref="ViewModels.RepairViewModel"/>).</summary>
[ExcludeFromCodeCoverage(Justification = "UI wiring; the logic lives in RepairViewModel and MainViewModel and is tested there.")]
internal sealed partial class RepairWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="RepairWindow"/> class.</summary>
    public RepairWindow() => InitializeComponent();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
