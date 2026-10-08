using UpSTEPer.Core.Drawing;

namespace UpSTEPer.Core.Tests.Drawing;

public class DrawingBuilderTests
{
    [Fact]
    public void Build_ProjectsCleansAndArrangesRequestedViews()
    {
        using var document = new ProjectingDocument();

        var views = DrawingBuilder.Build(document, [ViewDirection.Front, ViewDirection.Top], ProjectionOptions.Default, gap: 5);

        Assert.Equal([ViewDirection.Front, ViewDirection.Top], views.Select(v => v.Direction));
        Assert.Equal([ViewDirection.Front, ViewDirection.Top], document.Projected);
        Assert.DoesNotContain(views[0].Curves, c => c.Kind == LineKind.Hidden); // covered hidden line removed
        Assert.Equal(-5, views[1].GetBounds().MaxY, 9);                          // top view below front
    }

    [Fact]
    public void Build_DefaultGap_Is20PercentOfLargestViewButAtLeast10()
    {
        using var document = new ProjectingDocument();

        var views = DrawingBuilder.Build(document, [ViewDirection.Front, ViewDirection.Top], ProjectionOptions.Default);

        Assert.Equal(-10, views[1].GetBounds().MaxY, 9);
    }

    [Fact]
    public void Build_NoViews_Throws()
    {
        using var document = new ProjectingDocument();

        Assert.Throws<ArgumentException>(() => DrawingBuilder.Build(document, [], ProjectionOptions.Default));
    }

    // Every view is a 10 x 10 square with a hidden duplicate of its bottom edge.
    private sealed class ProjectingDocument : IStepDocument
    {
        public List<ViewDirection> Projected { get; } = [];

        public string SourcePath => "fake.stp";

        public ModelStatistics Statistics { get; } = new(1, 1, 1, 4, 4);

        public ProjectedView Project(ViewDirection direction, ProjectionOptions options)
        {
            Projected.Add(direction);
            return new ProjectedView(direction,
            [
                new LineSegment(new Point2(0, 0), new Point2(10, 10), LineKind.Visible),
                new LineSegment(new Point2(0, 0), new Point2(10, 10), LineKind.Hidden),
            ]);
        }

        public ColorRgb? GetFaceColor(FaceId faceId) => null;

        public BodyId? GetBodyOfFace(FaceId faceId) => null;

        public ColorRgb? GetAssignedColor(ColorTarget target) => null;

        public void SetColor(ColorTarget target, ColorRgb? color) => throw new NotSupportedException();

        public void SaveStep(string path) => throw new NotSupportedException();

        public EdgeData TessellateEdges(TessellationOptions options) => EdgeData.Empty;

        public MeshData Tessellate(TessellationOptions options) => MeshData.Empty;

        public void Dispose()
        {
        }
    }
}
