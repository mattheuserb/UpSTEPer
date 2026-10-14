using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace UpSTEPer.App.Converters;

/// <summary>
/// One-way converter: <see langword="true"/> when the bound value equals the converter parameter,
/// e.g. to tick the current enum value in a menu.
/// </summary>
internal sealed class EqualsConverter : IValueConverter
{
    /// <summary>Gets the shared instance.</summary>
    public static EqualsConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Equals(value, parameter);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}
