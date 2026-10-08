using System.Globalization;
using UpSTEPer.Core;

namespace UpSTEPer.Cli;

/// <summary>A colour change from the command line: <c>&lt;id&gt;=&lt;#RRGGBB|none&gt;</c>.</summary>
/// <param name="Target">The face or body.</param>
/// <param name="Color">The new colour, or <see langword="null"/> to remove the assigned colour.</param>
internal sealed record ColorAssignment(ColorTarget Target, ColorRgb? Color)
{
    /// <summary>Parses <c>80=#FF0000</c> or <c>80=none</c>.</summary>
    /// <param name="text">The option value.</param>
    /// <param name="kind">Whether the id is a face or a body id.</param>
    /// <returns>The assignment.</returns>
    /// <exception cref="FormatException"><paramref name="text"/> is malformed.</exception>
    public static ColorAssignment Parse(string text, ColorTargetKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parts = text.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            throw new FormatException($"'{text}' is not of the form <id>=<#RRGGBB|none>.");
        }

        var color = string.Equals(parts[1], "none", StringComparison.OrdinalIgnoreCase) ? (ColorRgb?)null : ColorRgb.FromHex(parts[1]);
        return new ColorAssignment(new ColorTarget(kind, id), color);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Target} = {Color?.ToHex() ?? "none"}";
}
