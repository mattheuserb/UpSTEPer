namespace UpSTEPer.Core.Drawing;

/// <summary>Projects a model into several standard views and arranges them on one sheet.</summary>
public static class DrawingBuilder
{
    /// <summary>
    /// Gets the default view set: front, top and left (first-angle). The isometric view is opt-in
    /// (decision 2026-10-13, ADR 0006).
    /// </summary>
    public static IReadOnlyList<ViewDirection> StandardViews { get; } =
        [ViewDirection.Front, ViewDirection.Top, ViewDirection.Left];

    /// <summary>Projects and arranges the views.</summary>
    /// <param name="document">The model.</param>
    /// <param name="directions">The views to create, each at most once.</param>
    /// <param name="options">Projection options.</param>
    /// <param name="gap">Space between views (mm); defaults to 20 % of the largest view size, at least 10 mm.</param>
    /// <returns>The arranged views in sheet coordinates.</returns>
    public static IReadOnlyList<ProjectedView> Build(
        IStepDocument document, IReadOnlyList<ViewDirection> directions, ProjectionOptions options, double? gap = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(directions);
        ArgumentNullException.ThrowIfNull(options);
        if (directions.Count == 0)
        {
            throw new ArgumentException("At least one view is required.", nameof(directions));
        }

        var views = directions
            .Select(d => HiddenLineCleanup.RemoveCoveredHidden(document.Project(d, options), polylineDeflection: options.Deflection))
            .ToList();
        var size = views.Select(v => v.GetBounds()).Select(b => Math.Max(b.Width, b.Height)).Max();
        return DrawingLayout.Arrange(views, gap ?? Math.Max(10, 0.2 * size));
    }
}
