namespace UpSTEPer.Core;

/// <summary>
/// Controls how finely B-Rep geometry is triangulated for display.
/// </summary>
/// <remarks>
/// Tessellation only affects the on-screen approximation, never the model. Circles
/// that look polygonal in a viewer are usually a coarse tessellation, not a defect
/// of the STEP file; lower <see cref="AngularDeflectionRadians"/> to make them rounder.
/// </remarks>
public sealed record TessellationOptions
{
    // Smallest chord tolerance ForModel uses (mm), so tiny or empty models stay cheap to mesh.
    private const double MinimumModelDeflection = 1e-3;

    /// <summary>Initializes a new instance of the <see cref="TessellationOptions"/> class.</summary>
    /// <param name="linearDeflection">Maximum chord deviation in model units (mm), or a ratio when <paramref name="isRelative"/>.</param>
    /// <param name="angularDeflectionRadians">Maximum angle between adjacent segments, in radians.</param>
    /// <param name="isRelative">Whether <paramref name="linearDeflection"/> is relative to edge size.</param>
    /// <exception cref="ArgumentOutOfRangeException">A deflection is not a positive, finite number.</exception>
    public TessellationOptions(double linearDeflection, double angularDeflectionRadians, bool isRelative = false)
    {
        LinearDeflection = ValidatePositive(linearDeflection, nameof(linearDeflection));
        AngularDeflectionRadians = ValidatePositive(angularDeflectionRadians, nameof(angularDeflectionRadians));
        IsRelative = isRelative;
    }

    /// <summary>Gets a fast preview quality (~29° per segment).</summary>
    public static TessellationOptions Coarse { get; } = new(0.2, 0.5);

    /// <summary>Gets the default display quality (~11° per segment).</summary>
    public static TessellationOptions Default { get; } = new(0.05, 0.2);

    /// <summary>Gets a high display quality (~3° per segment).</summary>
    public static TessellationOptions Fine { get; } = new(0.01, 0.05);

    /// <summary>
    /// Gets display options for a model: chord tolerance and shortest triangle edge scale with the
    /// model size, so a quality level looks alike on a 3 mm part and a 3 m assembly.
    /// </summary>
    /// <remarks>
    /// The minimum size keeps the angular deflection from giving every small screw and hole of a
    /// large assembly the full number of segments: on a 2.5 m engine with 72,000 faces it cuts
    /// Normal from 12.1 to 2.7 million triangles, while a single 40 mm part hardly changes
    /// (measurements in docs/test-data.md).
    /// </remarks>
    /// <param name="quality">The quality level.</param>
    /// <param name="bounds">The model bounds, or <see langword="null"/> when unknown (uses the fixed presets).</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quality"/> is not defined.</exception>
    public static TessellationOptions ForModel(DisplayQuality quality, ModelBounds? bounds)
    {
        // Factors of the bounding box diagonal; angles in radians.
        var (fixedPreset, linearFactor, angle, minSizeFactor) = quality switch
        {
            DisplayQuality.Coarse => (Coarse, 2e-3, 0.5, 2e-3),
            DisplayQuality.Normal => (Default, 5e-4, 0.2, 1e-3),
            DisplayQuality.Fine => (Fine, 1e-4, 0.1, 5e-4),
            _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, "Unknown display quality."),
        };

        if (bounds is null)
        {
            return fixedPreset;
        }

        var diagonal = bounds.Diagonal;
        return new TessellationOptions(Math.Max(diagonal * linearFactor, MinimumModelDeflection), angle)
        {
            MinSize = Math.Max(diagonal * minSizeFactor, MinimumModelDeflection),
        };
    }

    /// <summary>Gets the maximum chord deviation.</summary>
    public double LinearDeflection { get; }

    /// <summary>Gets the maximum angular deviation in radians.</summary>
    public double AngularDeflectionRadians { get; }

    /// <summary>Gets a value indicating whether <see cref="LinearDeflection"/> is relative.</summary>
    public bool IsRelative { get; }

    /// <summary>
    /// Gets the shortest triangle edge in model units (mm), or <see langword="null"/> for the
    /// kernel default (a tenth of <see cref="LinearDeflection"/>). Limits the number of segments on
    /// small features such as screws and holes, where the angular deflection would otherwise rule.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a positive, finite number.</exception>
    public double? MinSize
    {
        get;
        init => field = value is { } size ? ValidatePositive(size, nameof(MinSize)) : null;
    }

    private static double ValidatePositive(double value, string paramName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be a positive, finite number.");
        }

        return value;
    }
}
