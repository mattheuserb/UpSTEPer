using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace UpSTEPer.App.Converters;

/// <summary>One-way converter from <see cref="Color"/> to a solid brush (colour previews).</summary>
internal sealed class ColorToBrushConverter : IValueConverter
{
    /// <summary>Gets the shared instance.</summary>
    public static ColorToBrushConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Color color ? new SolidColorBrush(color) : null;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}
