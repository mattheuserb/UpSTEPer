using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Core.Tests.Drawing;

public class DrawingLayoutTests
{
    private const double Gap = 10;

    [Fact]
    public void Arrange_FirstAngle_PlacesTopBelowAndLeftViewRightOfFront()
    {
        var front = Box(ViewDirection.Front, 0, 0, 40, 20);
        var top = Box(ViewDirection.Top, 0, -15, 40, 15);
        var left = Box(ViewDirection.Left, -15, 0, 15, 20);

        var arranged = DrawingLayout.Arrange([front, top, left], Gap).ToDictionary(v => v.Direction, v => v.GetBounds());

        Assert.Equal(new Bounds2(0, 0, 40, 20), arranged[ViewDirection.Front]);
        Assert.Equal(new Bounds2(0, -40, 40, -10), arranged[ViewDirection.Top]);   // below, same x
        Assert.Equal(new Bounds2(50, 0, 80, 20), arranged[ViewDirection.Left]);   // right, same y
    }

    [Fact]
    public void Arrange_PlacesRightBottomBackAndIso()
    {
        var views = new[]
        {
            Box(ViewDirection.Front, 0, 0, 40, 20),
            Box(ViewDirection.Right, 0, 0, 30, 20),
            Box(ViewDirection.Bottom, 0, 0, 40, 30),
            Box(ViewDirection.Left, 0, 0, 30, 20),
            Box(ViewDirection.Back, 0, 0, 40, 20),
            Box(ViewDirection.Iso, 0, 0, 25, 25),
        };

        var arranged = DrawingLayout.Arrange(views, Gap).ToDictionary(v => v.Direction, v => v.GetBounds());

        Assert.Equal(-40, arranged[ViewDirection.Right].MinX, 9);  // left of front
        Assert.Equal(30, arranged[ViewDirection.Bottom].MinY, 9);  // above front
        Assert.Equal(90, arranged[ViewDirection.Back].MinX, 9);    // right of the left view
        Assert.Equal(140, arranged[ViewDirection.Iso].MinX, 9);    // right of everything
        Assert.Equal(60, arranged[ViewDirection.Iso].MaxY, 9);     // top-aligned with the highest view
    }

    [Fact]
    public void Arrange_KeepsInputOrder()
    {
        var arranged = DrawingLayout.Arrange([Box(ViewDirection.Iso, 0, 0, 1, 1), Box(ViewDirection.Front, 0, 0, 1, 1)], Gap);

        Assert.Equal([ViewDirection.Iso, ViewDirection.Front], arranged.Select(v => v.Direction));
    }

    [Fact]
    public void Arrange_DuplicateDirection_Throws()
    {
        Assert.Throws<ArgumentException>(() => DrawingLayout.Arrange([Box(ViewDirection.Top, 0, 0, 1, 1), Box(ViewDirection.Top, 0, 0, 1, 1)], Gap));
    }

    [Fact]
    public void Arrange_WithoutFrontView_StillPlacesViews()
    {
        var arranged = DrawingLayout.Arrange([Box(ViewDirection.Top, 0, 0, 10, 10), Box(ViewDirection.Iso, 0, 0, 10, 10)], Gap);

        Assert.True(arranged[1].GetBounds().MinX >= arranged[0].GetBounds().MaxX);
    }

    internal static ProjectedView Box(ViewDirection direction, double minX, double minY, double maxX, double maxY) =>
        new(direction, [new LineSegment(new Point2(minX, minY), new Point2(maxX, maxY), LineKind.Visible)]);
}
