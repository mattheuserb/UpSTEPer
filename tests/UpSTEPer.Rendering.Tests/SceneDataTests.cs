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

        var colors = scene.VertexColors.ToArray();
        Assert.Equal(18, colors.Length);
        Assert.Equal([1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f], colors[..9]);
        var grey = VertexColors.Default;
        Assert.Equal([grey.R, grey.G, grey.B], colors[9..12]);
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
