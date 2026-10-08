using System.Globalization;

namespace UpSTEPer.Core;

/// <summary>
/// An opaque sRGB colour with channels in the range [0, 1], as stored in STEP files.
/// </summary>
public readonly record struct ColorRgb
{
    /// <summary>Initializes a new instance of the <see cref="ColorRgb"/> struct.</summary>
    /// <param name="r">Red channel, 0..1.</param>
    /// <param name="g">Green channel, 0..1.</param>
    /// <param name="b">Blue channel, 0..1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A channel is outside [0, 1] or not a number.</exception>
    public ColorRgb(float r, float g, float b)
    {
        R = ValidateChannel(r, nameof(r));
        G = ValidateChannel(g, nameof(g));
        B = ValidateChannel(b, nameof(b));
    }

    /// <summary>Gets the red channel (0..1).</summary>
    public float R { get; }

    /// <summary>Gets the green channel (0..1).</summary>
    public float G { get; }

    /// <summary>Gets the blue channel (0..1).</summary>
    public float B { get; }

    /// <summary>Creates a colour from 8-bit channel values.</summary>
    /// <param name="r">Red channel, 0..255.</param>
    /// <param name="g">Green channel, 0..255.</param>
    /// <param name="b">Blue channel, 0..255.</param>
    /// <returns>The colour.</returns>
    public static ColorRgb FromBytes(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f);

    /// <summary>Parses a hex colour in the form <c>#RRGGBB</c> or <c>RRGGBB</c>.</summary>
    /// <param name="hex">The hex string.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="FormatException"><paramref name="hex"/> is not a valid colour.</exception>
    public static ColorRgb FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var span = hex.AsSpan().TrimStart('#');
        if (span.Length != 6 || !uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"'{hex}' is not a colour in the form #RRGGBB.");
        }

        return FromBytes((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>Formats the colour as <c>#RRGGBB</c>.</summary>
    /// <returns>The hex representation.</returns>
    public string ToHex() =>
        string.Create(CultureInfo.InvariantCulture, $"#{ToByte(R):X2}{ToByte(G):X2}{ToByte(B):X2}");

    /// <inheritdoc />
    public override string ToString() => ToHex();

    private static byte ToByte(float channel) => (byte)MathF.Round(channel * 255f);

    private static float ValidateChannel(float value, string paramName)
    {
        if (float.IsNaN(value) || value < 0f || value > 1f)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Colour channels must be within [0, 1].");
        }

        return value;
    }
}
