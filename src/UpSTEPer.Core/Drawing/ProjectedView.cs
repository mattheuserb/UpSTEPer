namespace UpSTEPer.Core.Drawing;

/// <summary>Standard viewing directions. Z is up; <see cref="Front"/> looks along +Y.</summary>
public enum ViewDirection
{
    /// <summary>From −Y.</summary>
    Front = 0,

    /// <summary>From +Z.</summary>
    Top = 1,

    /// <summary>From −X.</summary>
    Left = 2,

    /// <summary>From +X.</summary>
    Right = 3,

    /// <summary>From +Y.</summary>
    Back = 4,

    /// <summary>From −Z.</summary>
    Bottom = 5,

    /// <summary>Isometric from front-right-top.</summary>
    Iso = 6,
}

/// <summary>Options for <see cref="IStepDocument.Project"/>.</summary>
/// <param name="IncludeHidden">Whether to compute hidden lines.</param>
/// <param name="IncludeTangent">Whether to include smooth edges between faces.</param>
/// <param name="Deflection">Chord tolerance (mm) for curves that are exported as polylines.</param>
public sealed record ProjectionOptions(bool IncludeHidden = true, bool IncludeTangent = false, double Deflection = 0.01)
{
    /// <summary>Gets the default options: hidden lines, no tangent edges, 0.01 mm deflection.</summary>
    public static ProjectionOptions Default { get; } = new();
}

/// <summary>One 2D view of the model in its own view-plane coordinates (mm).</summary>
/// <param name="Direction">The viewing direction.</param>
/// <param name="Curves">The projected edges.</param>
public sealed record ProjectedView(ViewDirection Direction, IReadOnlyList<DrawingCurve> Curves)
{
    /// <summary>Gets the bounds of all curves.</summary>
    /// <returns>The bounds.</returns>
    public Bounds2 GetBounds() => Bounds2.Of(Curves.SelectMany(c => c.Sample()));

    /// <summary>Returns the view moved by <paramref name="offset"/>.</summary>
    /// <param name="offset">The translation.</param>
    /// <returns>The moved view.</returns>
    public ProjectedView Translate(Point2 offset) => this with { Curves = [.. Curves.Select(c => c.Translate(offset))] };
}
