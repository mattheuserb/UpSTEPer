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

    /// <summary>Gets the maximum chord deviation.</summary>
    public double LinearDeflection { get; }

    /// <summary>Gets the maximum angular deviation in radians.</summary>
    public double AngularDeflectionRadians { get; }

    /// <summary>Gets a value indicating whether <see cref="LinearDeflection"/> is relative.</summary>
    public bool IsRelative { get; }

    private static double ValidatePositive(double value, string paramName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be a positive, finite number.");
        }

        return value;
    }
}
