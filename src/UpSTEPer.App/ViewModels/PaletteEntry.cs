using CommunityToolkit.Mvvm.ComponentModel;
using UpSTEPer.App.Colours;
using UpSTEPer.App.Localization;
using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>
/// A colour the user can pick: a RAL colour (named in the UI language), a colour picked by the user
/// or a favourite with its own name.
/// </summary>
internal sealed class PaletteEntry : ObservableObject
{
    private static readonly string[] _defaultCodes = ["3020", "2004", "1023", "6018", "5015", "5002", "7035", "7016", "9010", "9005"];

    private readonly string? _name;
    private readonly bool _isCustom;

    /// <summary>Initializes a new instance of the <see cref="PaletteEntry"/> class with a fixed name.</summary>
    /// <param name="name">The name, e.g. of a favourite.</param>
    /// <param name="color">The colour.</param>
    public PaletteEntry(string name, ColorRgb color)
    {
        _name = name;
        Color = color;
    }

    private PaletteEntry(string name, ColorRgb color, bool isCustom)
        : this(name, color) => _isCustom = isCustom;

    private PaletteEntry(RalColour ral)
    {
        Ral = ral;
        Color = ral.Color;
    }

    /// <summary>Gets the RAL colours of the toolbar palette.</summary>
    public static IReadOnlyList<PaletteEntry> Default { get; } = [.. _defaultCodes.Select(code => FromRal(RalCatalogue.Get(code)))];

    /// <summary>Gets the RAL colour, or <see langword="null"/> for other colours.</summary>
    public RalColour? Ral { get; }

    /// <summary>Gets the colour.</summary>
    public ColorRgb Color { get; }

    /// <summary>Gets the name: "RAL 3020 Verkehrsrot" in the UI language for RAL colours, else the fixed name.</summary>
    public string Name =>
        Ral?.Label(Localizer.Instance.Culture) ?? (_isCustom ? Localizer.Format(Strings.Colour_CustomName, Hex) : _name!);

    /// <summary>Gets the colour as <c>#RRGGBB</c> (bindable as a brush in XAML).</summary>
    public string Hex => Color.ToHex();

    /// <summary>Gets the name with the colour value, e.g. "RAL 3020 Traffic red (#C1121C)" (tooltips).</summary>
    public string Description => Name.EndsWith(Hex, StringComparison.Ordinal) ? Name : $"{Name} ({Hex})";

    /// <summary>Creates an entry for a RAL colour.</summary>
    /// <param name="ral">The RAL colour.</param>
    /// <returns>The entry.</returns>
    public static PaletteEntry FromRal(RalColour ral)
    {
        ArgumentNullException.ThrowIfNull(ral);
        return new PaletteEntry(ral);
    }

    /// <summary>Creates an entry for a colour picked by the user.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The entry, named after its hex value.</returns>
    public static PaletteEntry Custom(ColorRgb color) => new(string.Empty, color, isCustom: true);

    /// <summary>Raises change notifications for the texts after the UI language changed.</summary>
    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
    }
}
