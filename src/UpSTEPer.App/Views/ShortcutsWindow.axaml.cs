using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UpSTEPer.App.Views;

/// <summary>Lists all mouse and keyboard shortcuts (Help → Keyboard shortcuts, F1).</summary>
[ExcludeFromCodeCoverage(Justification = "Static content; opened from the main window.")]
internal sealed partial class ShortcutsWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="ShortcutsWindow"/> class.</summary>
    public ShortcutsWindow() => InitializeComponent();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
