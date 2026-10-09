using System.Numerics;
using UpSTEPer.Core;

namespace UpSTEPer.Rendering.Tests;

public class SceneDataTests
{
    // Two triangles on two faces, vertices not shared (as produced by the native tessellator).
    private static readonly MeshData _mesh = new(
        positions: [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 5, 1, 0, 5, 0, 1, 5],
        normals: new float[18],
        indices: [0, 1, 2, 3, 4, 5],
        triangleFaceIds: [0, 1]);

    [Fact]
    public void Create_ColoursVerticesByFace_AndUsesDefaultForUncoloured()
    {
        var red = ColorRgb.FromHex("#FF0000");

        var scene = SceneData.Create(_mesh, face => face.Value == 0 ? red : null);

        var colors = scene.GetVertexColors(new HashSet<FaceId>()).ToArray();
        Assert.Equal(18, colors.Length);
        Assert.Equal([1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f], colors[..9]);
        var grey = VertexColors.Default;
        Assert.Equal([grey.R, grey.G, grey.B], colors[9..12]);
    }

    [Fact]
    public void WithGeometry_KeepsFaceColoursAndModelIdentity()
    {
        var red = ColorRgb.FromHex("#FF0000");
        var scene = SceneData.Create(_mesh, face => face.Value == 0 ? red : null);
        var finer = new MeshData(
            positions: [0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 5, 1, 0, 5, 0, 1, 5],
            normals: new float[18],
            indices: [0, 1, 2, 3, 4, 5],
            triangleFaceIds: [0, 1]);

        var remeshed = scene.WithGeometry(finer, EdgeData.Empty);

        Assert.Same(finer, remeshed.Mesh);
        Assert.Equal(red, remeshed.GetFaceColor(new FaceId(0)));
        Assert.Null(remeshed.GetFaceColor(new FaceId(1)));
        Assert.Equal(new Vector3(2, 2, 5), remeshed.Bounds.Max);
        Assert.True(remeshed.IsSameModel(scene));
        Assert.True(scene.WithFaceColors(_ => null).IsSameModel(remeshed));
    }

    [Fact]
    public void IsSameModel_SeparatelyCreatedScenes_IsFalse()
    {
        var first = SceneData.Create(_mesh, _ => null);
        var second = SceneData.Create(_mesh, _ => null);

        Assert.False(first.IsSameModel(second));
        Assert.False(first.IsSameModel(null));
    }

    [Fact]
    public void GetDisplayColor_HighlightBeatsHover_HoverTintsModelColour()
    {
        var red = ColorRgb.FromHex("#FF0000");
        var scene = SceneData.Create(_mesh, face => face.Value == 0 ? red : null);
        var face0 = new FaceId(0);
        var face1 = new FaceId(1);

        Assert.Equal(red, scene.GetDisplayColor(face0, new HashSet<FaceId>(), hovered: face1));
        Assert.Equal(SceneData.HighlightColor, scene.GetDisplayColor(face0, new HashSet<FaceId> { face0 }, hovered: face0));

        var tinted = scene.GetDisplayColor(face0, new HashSet<FaceId>(), hovered: face0);
        var tint = SceneData.HoverTint;
        var amount = SceneData.HoverTintAmount;
        Assert.Equal(red.R + ((tint.R - red.R) * amount), tinted.R, 5);
        Assert.Equal(red.B + ((tint.B - red.B) * amount), tinted.B, 5);

        var grey = VertexColors.Default;
        Assert.Equal(grey.G + ((tint.G - grey.G) * amount), scene.GetDisplayColor(face1, new HashSet<FaceId>(), face1).G, 5);
    }

    [Fact]
    public void GetVertexColors_WithHover_TintsOnlyThatFace()
    {
        var scene = SceneData.Create(_mesh, _ => null);
        var hovered = new FaceId(1);

        var colors = scene.GetVertexColors(new HashSet<FaceId>(), hovered).ToArray();

        var tinted = scene.GetDisplayColor(hovered, new HashSet<FaceId>(), hovered);
        var grey = VertexColors.Default;
        Assert.Equal([grey.R, grey.G, grey.B], colors[..3]);
        Assert.Equal([tinted.R, tinted.G, tinted.B], colors[9..12]);
    }

    [Fact]
    public void Pick_HitsTheClosestFace_AlsoAfterColourChanges()
    {
        var scene = SceneData.Create(_mesh, _ => null);
        var downwards = new Ray(new Vector3(0.2f, 0.2f, 10f), -Vector3.UnitZ);

        Assert.Equal(new FaceId(1), scene.Pick(downwards)?.Face); // face 1 lies at z = 5, above face 0
        Assert.Equal(new FaceId(1), scene.WithFaceColors(_ => null).Pick(downwards)?.Face);
        Assert.Null(scene.Pick(new Ray(new Vector3(5f, 5f, 10f), -Vector3.UnitZ)));
    }

    [Fact]
    public void FaceVertexRanges_DescribeTheMesh()
    {
        var scene = SceneData.Create(_mesh, _ => null);

        Assert.True(scene.FaceVertexRanges.TryGetRange(new FaceId(1), out var first, out var count));
        Assert.Equal((3, 3), (first, count));
    }

    [Fact]
    public void Create_ComputesBounds()
    {
        var scene = SceneData.Create(_mesh, _ => null);

        Assert.Equal(new Vector3(0, 0, 0), scene.Bounds.Min);
        Assert.Equal(new Vector3(1, 1, 5), scene.Bounds.Max);
        Assert.Same(_mesh, scene.Mesh);
    }

    [Fact]
    public void GetVertexColors_HighlightsOnlySelectedFace()
    {
        var scene = SceneData.Create(_mesh, _ => ColorRgb.FromHex("#0000FF"));
        var highlight = SceneData.HighlightColor;

        var colors = scene.GetVertexColors(new HashSet<FaceId> { new FaceId(1) }).ToArray();

        Assert.Equal([0f, 0f, 1f], colors[..3]);
        Assert.Equal([highlight.R, highlight.G, highlight.B], colors[9..12]);
    }

    [Fact]
    public void GetFaceColor_ReturnsFileColourOrNull()
    {
        var red = ColorRgb.FromHex("#FF0000");
        var scene = SceneData.Create(_mesh, face => face.Value == 0 ? red : null);

        Assert.Equal(red, scene.GetFaceColor(new FaceId(0)));
        Assert.Null(scene.GetFaceColor(new FaceId(1)));
        Assert.Null(scene.GetFaceColor(new FaceId(99)));
        Assert.Null(scene.GetFaceColor(new FaceId(-1)));
    }

    [Fact]
    public void Edges_DefaultToEmpty_AndSurviveColourChanges()
    {
        var edges = new EdgeData(new float[6], [0, 1], [3]);

        var withoutEdges = SceneData.Create(_mesh, _ => null);
        var withEdges = SceneData.Create(_mesh, _ => null, edges);

        Assert.Same(EdgeData.Empty, withoutEdges.Edges);
        Assert.Same(edges, withEdges.WithFaceColors(_ => ColorRgb.FromHex("#00FF00")).Edges);
    }

    [Fact]
    public void Build_QueriesEachFaceOnlyOnce()
    {
        var calls = 0;
        var mesh = new MeshData(new float[18], new float[18], [0, 1, 2, 3, 4, 5], [7, 7]);

        VertexColors.Build(mesh, _ =>
        {
            calls++;
            return null;
        });

        Assert.Equal(1, calls);
    }
}
