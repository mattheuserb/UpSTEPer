using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace UpSTEPer.App.Controls;

/// <summary>
/// A line icon: <see cref="Data"/> on the 24×24 Lucide grid (resources <c>Icon.*</c> in
/// <c>Themes/Icons.axaml</c>), stroked in <see cref="TemplatedControl.Foreground"/> and scaled to the
/// control size, so it follows the enabled/disabled colour of the button it sits in.
/// </summary>
internal sealed class Icon : TemplatedControl
{
    /// <summary>Defines the <see cref="Data"/> property.</summary>
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

    /// <summary>Gets or sets the icon geometry (24×24 grid).</summary>
    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
