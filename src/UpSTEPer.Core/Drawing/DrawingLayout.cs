namespace UpSTEPer.Core.Drawing;

/// <summary>
/// Places views on one sheet using first-angle projection (ISO 128 method E, standard in Europe):
/// the top view is below the front view, the view from the left is to the right of it, the view from
/// the right to the left, the bottom view above, the back view right of the left view. The isometric
/// view goes to the top right. Aligned views share the front view's coordinates.
/// </summary>
public static class DrawingLayout
{
    /// <summary>Arranges views; each view in the result is translated to its sheet position.</summary>
    /// <param name="views">Views in their own coordinates; at most one per direction.</param>
    /// <param name="gap">Space between views (mm).</param>
    /// <returns>The translated views.</returns>
    /// <exception cref="ArgumentException">A direction occurs twice.</exception>
    public static IReadOnlyList<ProjectedView> Arrange(IReadOnlyList<ProjectedView> views, double gap)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentOutOfRangeException.ThrowIfNegative(gap);
        if (views.Select(v => v.Direction).Distinct().Count() != views.Count)
        {
            throw new ArgumentException("Each view direction may occur only once.", nameof(views));
        }

        var byDirection = views.ToDictionary(v => v.Direction);
        var front = byDirection.TryGetValue(ViewDirection.Front, out var f) ? f.GetBounds() : default;
        var placed = new Dictionary<ViewDirection, (ProjectedView View, Bounds2 Bounds)>();

        void Place(ViewDirection direction, Func<Bounds2, Point2> offset)
        {
            if (byDirection.TryGetValue(direction, out var view))
            {
                var bounds = view.GetBounds();
                var shift = offset(bounds);
                placed[direction] = (view.Translate(shift), Shift(bounds, shift));
            }
        }

        Place(ViewDirection.Front, _ => default);
        Place(ViewDirection.Top, b => new Point2(0, front.MinY - gap - b.MaxY));
        Place(ViewDirection.Bottom, b => new Point2(0, front.MaxY + gap - b.MinY));
        Place(ViewDirection.Left, b => new Point2(front.MaxX + gap - b.MinX, 0));
        Place(ViewDirection.Right, b => new Point2(front.MinX - gap - b.MaxX, 0));

        var rightEdge = placed.Values.Select(p => p.Bounds.MaxX).DefaultIfEmpty(front.MaxX).Max();
        Place(ViewDirection.Back, b => new Point2(rightEdge + gap - b.MinX, 0));

        rightEdge = placed.Values.Select(p => p.Bounds.MaxX).DefaultIfEmpty(front.MaxX).Max();
        var topEdge = placed.Values.Select(p => p.Bounds.MaxY).DefaultIfEmpty(front.MaxY).Max();
        Place(ViewDirection.Iso, b => new Point2(rightEdge + gap - b.MinX, topEdge - b.MaxY));

        return [.. views.Select(v => placed[v.Direction].View)];
    }

    private static Bounds2 Shift(Bounds2 bounds, Point2 offset) =>
        new(bounds.MinX + offset.X, bounds.MinY + offset.Y, bounds.MaxX + offset.X, bounds.MaxY + offset.Y);
}
