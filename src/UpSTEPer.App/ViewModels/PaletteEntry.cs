using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>A named colour in the toolbar palette.</summary>
/// <param name="Name">Display name, e.g. "RAL 3020 Traffic red".</param>
/// <param name="Color">The colour.</param>
internal sealed record PaletteEntry(string Name, ColorRgb Color)
{
    /// <summary>Gets common RAL colours (approximate sRGB values).</summary>
    public static IReadOnlyList<PaletteEntry> Default { get; } =
    [
        new("RAL 3020 Traffic red", ColorRgb.FromHex("#C1121C")),
        new("RAL 2004 Pure orange", ColorRgb.FromHex("#F44611")),
        new("RAL 1023 Traffic yellow", ColorRgb.FromHex("#F7B500")),
        new("RAL 6018 Yellow green", ColorRgb.FromHex("#57A639")),
        new("RAL 5015 Sky blue", ColorRgb.FromHex("#2271B3")),
        new("RAL 5002 Ultramarine blue", ColorRgb.FromHex("#20214F")),
        new("RAL 7035 Light grey", ColorRgb.FromHex("#C5C7C4")),
        new("RAL 7016 Anthracite grey", ColorRgb.FromHex("#383E42")),
        new("RAL 9010 Pure white", ColorRgb.FromHex("#F1ECE1")),
        new("RAL 9005 Jet black", ColorRgb.FromHex("#0E0E10")),
    ];

    /// <summary>Gets the colour as <c>#RRGGBB</c> (bindable as a brush in XAML).</summary>
    public string Hex => Color.ToHex();
}
