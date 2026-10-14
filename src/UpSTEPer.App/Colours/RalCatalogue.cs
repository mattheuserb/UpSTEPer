using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using UpSTEPer.Core;

namespace UpSTEPer.App.Colours;

/// <summary>A RAL Classic colour with its German and English name and an approximate sRGB value.</summary>
/// <param name="Code">The four-digit number, e.g. "7030".</param>
/// <param name="German">The German name, e.g. "Steingrau".</param>
/// <param name="English">The English name, e.g. "Stone grey".</param>
/// <param name="Hex">The approximate colour as <c>#RRGGBB</c>.</param>
internal sealed record RalColour(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("de")] string German,
    [property: JsonPropertyName("en")] string English,
    [property: JsonPropertyName("hex")] string Hex)
{
    /// <summary>Gets the colour.</summary>
    [JsonIgnore]
    public ColorRgb Color => ColorRgb.FromHex(Hex);

    /// <summary>Gets the name in a language: German for German, English otherwise.</summary>
    /// <param name="culture">The UI language.</param>
    /// <returns>The name.</returns>
    public string Name(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.TwoLetterISOLanguageName == "de" ? German : English;
    }

    /// <summary>Gets the label shown in lists, e.g. "RAL 7030 Steingrau".</summary>
    /// <param name="culture">The UI language.</param>
    /// <returns>The label.</returns>
    public string Label(CultureInfo culture) => $"RAL {Code} {Name(culture)}";
}

/// <summary>
/// The RAL Classic colours shipped with the app (<c>Colours/ral-classic.json</c>, generated from
/// Wikidata by <c>scripts/update-ral.py</c>; CC0). The values are approximations: RAL publishes no
/// official sRGB values.
/// </summary>
internal static class RalCatalogue
{
    private static readonly Lazy<IReadOnlyList<RalColour>> _all = new(Load);

    /// <summary>Gets all colours, ordered by number.</summary>
    public static IReadOnlyList<RalColour> All => _all.Value;

    /// <summary>Gets a colour by its number.</summary>
    /// <param name="code">The four-digit number.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="KeyNotFoundException">The catalogue has no such colour.</exception>
    public static RalColour Get(string code) =>
        All.FirstOrDefault(c => c.Code == code) ?? throw new KeyNotFoundException($"RAL {code} is not in the catalogue.");

    /// <summary>
    /// Finds colours by number or name in either language, ignoring case, spaces and a leading
    /// "RAL" ("7030", "ral70", "stein", "stone grey"); an empty search finds all.
    /// </summary>
    /// <param name="text">The search text.</param>
    /// <returns>The matching colours, ordered by number.</returns>
    public static IReadOnlyList<RalColour> Search(string? text)
    {
        var search = Normalise(text);
        if (search.StartsWith("RAL", StringComparison.Ordinal))
        {
            search = search[3..];
        }

        return search.Length == 0
            ? All
            : [.. All.Where(c => c.Code.StartsWith(search, StringComparison.Ordinal)
                || Normalise(c.German).Contains(search, StringComparison.Ordinal)
                || Normalise(c.English).Contains(search, StringComparison.Ordinal))];
    }

    private static string Normalise(string? text) =>
        string.Concat((text ?? string.Empty).Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    private static IReadOnlyList<RalColour> Load()
    {
        using var stream = typeof(RalCatalogue).Assembly.GetManifestResourceStream("UpSTEPer.App.Colours.ral-classic.json")
            ?? throw new InvalidOperationException("The RAL catalogue resource is missing.");
        var document = JsonSerializer.Deserialize(stream, RalJsonContext.Default.RalDocument)
            ?? throw new InvalidOperationException("The RAL catalogue resource is empty.");
        return [.. document.Colours.OrderBy(c => c.Code, StringComparer.Ordinal)];
    }
}

/// <summary>The catalogue file: its colours (the other members document the source).</summary>
/// <param name="Colours">The colours.</param>
internal sealed record RalDocument([property: JsonPropertyName("colours")] IReadOnlyList<RalColour> Colours);

/// <summary>Source-generated JSON metadata for the RAL catalogue.</summary>
[JsonSerializable(typeof(RalDocument))]
internal sealed partial class RalJsonContext : JsonSerializerContext;
