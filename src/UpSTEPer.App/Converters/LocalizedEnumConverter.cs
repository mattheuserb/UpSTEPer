using System.Globalization;
using Avalonia.Data.Converters;
using UpSTEPer.App.Localization;

namespace UpSTEPer.App.Converters;

/// <summary>
/// Multi-value converter: an enum value (first value) as the text <c>{EnumType}_{Value}</c>, e.g.
/// <c>DisplayQuality_Fine</c>. Bind <see cref="Localizer.Culture"/> as the second value, so the text
/// is converted again when the language changes.
/// </summary>
internal sealed class LocalizedEnumConverter : IMultiValueConverter
{
    /// <summary>Gets the shared instance.</summary>
    public static LocalizedEnumConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Count > 0 && values[0] is Enum value ? Text(value) : null;
    }

    /// <summary>Gets the text of an enum value in the current language.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    public static string Text(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Localizer.Instance[$"{value.GetType().Name}_{value}"];
    }
}
