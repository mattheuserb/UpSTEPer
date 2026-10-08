using UpSTEPer.Core;

namespace UpSTEPer.Occt.Tests;

public class ModelTreeTests
{
    private readonly OcctStepReader _reader = new();

    [Fact]
    public void SinglePart_IsOnePartWithOneNamedBody()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.CableGland);

        var root = Assert.Single(document.GetModelTree());

        Assert.Equal(ModelNodeKind.Part, root.Kind);
        Assert.Equal("ESKV 12 RAL 7035 10066410", root.Name);
        var body = Assert.Single(root.Children);
        Assert.Equal(ModelNodeKind.Body, body.Kind);
        Assert.Equal(new BodyId(0), body.Body);
    }

    [Fact]
    public void Assembly_HasExpectedStructureAndNames()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly);

        var top = Assert.Single(document.GetModelTree());

        Assert.Equal(("Top", ModelNodeKind.Assembly), (top.Name, top.Kind));
        Assert.Equal(
            ["Base plate", "Pin assembly (left)", "Pin assembly (right)"],
            top.Children.Select(c => c.Name));
        Assert.Equal(ModelNodeKind.Part, top.Children[0].Kind);

        foreach (var pinAssembly in top.Children.Skip(1))
        {
            Assert.Equal(ModelNodeKind.Assembly, pinAssembly.Kind);
            Assert.Equal(["Pin", "Washer"], pinAssembly.Children.Select(c => c.Name));
            Assert.Single(pinAssembly.Children[0].Children);
            Assert.Equal(2, pinAssembly.Children[1].Children.Count);
        }
    }

    [Fact]
    public void Assembly_EveryBodyIsMappedToAUniqueBodyId()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly);

        var bodies = document.GetModelTree().SelectMany(n => n.GetBodies()).ToList();

        // Both instances of the pin assembly must map to their own (translated) solids.
        Assert.Equal(7, document.Statistics.SolidCount);
        Assert.Equal(Enumerable.Range(0, 7), bodies.Select(b => b.Value).Order());
    }

    [Fact]
    public void Assembly_FacesBelongToTheBodiesListedInTheTree()
    {
        TestFiles.RequireNativeLibrary();
        using var document = _reader.Load(TestFiles.TestAssembly);

        var treeBodies = document.GetModelTree().SelectMany(n => n.GetBodies()).ToHashSet();
        var faceBodies = Enumerable.Range(0, document.Statistics.FaceCount)
            .Select(i => document.GetBodyOfFace(new FaceId(i)))
            .ToHashSet();

        Assert.True(faceBodies.All(b => b is { } body && treeBodies.Contains(body)));
    }
}
