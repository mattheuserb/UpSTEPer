namespace UpSTEPer.Core;

/// <summary>What a colour is assigned to.</summary>
public enum ColorTargetKind
{
    /// <summary>A single face.</summary>
    Face = 0,

    /// <summary>A body (solid); applies to all its faces without their own colour.</summary>
    Body = 1,
}

/// <summary>A face or body that can carry a colour.</summary>
/// <param name="Kind">Face or body.</param>
/// <param name="Id">The 0-based face or body id.</param>
public readonly record struct ColorTarget(ColorTargetKind Kind, int Id)
{
    /// <summary>Creates a face target.</summary>
    /// <param name="face">The face.</param>
    /// <returns>The target.</returns>
    public static ColorTarget Of(FaceId face) => new(ColorTargetKind.Face, face.Value);

    /// <summary>Creates a body target.</summary>
    /// <param name="body">The body.</param>
    /// <returns>The target.</returns>
    public static ColorTarget Of(BodyId body) => new(ColorTargetKind.Body, body.Value);

    /// <inheritdoc />
    public override string ToString() => $"{Kind} {Id}";
}
