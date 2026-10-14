using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace UpSTEPer.App.Localization;

/// <summary>
/// The UI language. Texts come from <c>Strings.resx</c> (English) and <c>Strings.de.resx</c>
/// (German); C# reads them as <see cref="Strings"/> properties, XAML through <see cref="TrExtension"/>,
/// which binds to the indexer so that switching the language updates every visible text.
/// Number formats keep following the system's regional settings; only the texts change.
/// </summary>
internal sealed class Localizer : INotifyPropertyChanged
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    private Localizer()
    {
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the shared instance.</summary>
    public static Localizer Instance { get; } = new();

    /// <summary>Gets the languages the UI offers.</summary>
    public static IReadOnlyList<CultureInfo> Languages { get; } = [_english, _german];

    /// <summary>Gets the current UI language.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Bound from XAML on the instance, which raises the change notification.")]
    public CultureInfo Culture => Strings.Culture ?? _english;

    /// <summary>Gets the text for <paramref name="key"/> in the current language ("[key]" if it does not exist).</summary>
    /// <param name="key">The resource name.</param>
    public string this[string key] => Strings.ResourceManager.GetString(key, Culture) ?? $"[{key}]";

    /// <summary>Fills a text's placeholders ({0}, {1:N0}, ...) with numbers in the regional format of the system.</summary>
    /// <param name="format">A text from <see cref="Strings"/>.</param>
    /// <param name="args">The values.</param>
    /// <returns>The text.</returns>
    public static string Format(string format, params object?[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    /// <summary>Picks the language from the settings (a language name), else from the system (German or English).</summary>
    /// <param name="setting">The stored choice, e.g. "de"; <see langword="null"/> for none.</param>
    /// <param name="system">The system's UI culture.</param>
    /// <returns>One of <see cref="Languages"/>.</returns>
    public static CultureInfo Resolve(string? setting, CultureInfo system)
    {
        ArgumentNullException.ThrowIfNull(system);
        var name = string.IsNullOrWhiteSpace(setting) ? system.TwoLetterISOLanguageName : setting;
        return Languages.FirstOrDefault(language => string.Equals(language.Name, name, StringComparison.OrdinalIgnoreCase)) ?? _english;
    }

    /// <summary>Switches the UI language; every text bound through <see cref="TrExtension"/> updates.</summary>
    /// <param name="culture">One of <see cref="Languages"/>.</param>
    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        Strings.Culture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        // Indexer bindings listen for "Item" (Avalonia) or "Item[]" (WPF convention).
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    // Without this, texts would follow the system's UI language until the app sets one, also in
    // tests: start in English, the neutral language.
    [ModuleInitializer]
    internal static void StartInEnglish() => Strings.Culture = _english;
}
