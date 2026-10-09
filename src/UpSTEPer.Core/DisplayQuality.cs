namespace UpSTEPer.Core;

/// <summary>How finely a model is triangulated for display; see <see cref="TessellationOptions.ForModel"/>.</summary>
public enum DisplayQuality
{
    /// <summary>Fast, visibly faceted (~29° per segment).</summary>
    Coarse = 0,

    /// <summary>The default (~11° per segment).</summary>
    Normal = 1,

    /// <summary>Smooth curves, more triangles (~3° per segment).</summary>
    Fine = 2,
}
