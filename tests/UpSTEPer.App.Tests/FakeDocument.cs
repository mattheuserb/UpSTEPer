using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

/// <summary>
/// In-memory document with two faces. The file colours face 0 red; bodies have no colour.
/// Default: one part with one body owning both faces. With <c>twoBodies</c>: an assembly with
/// "Part A" (body 0, face 0) and "Part B" (body 1, face 1).
/// Colour resolution mirrors the native layer: face colour, else body colour, else none.
/// </summary>
internal sealed class FakeDocument(bool twoBodies = false) : IStepDocument
{
    public static readonly ColorRgb FileRed = ColorRgb.FromHex("#FF0000");

    private readonly Dictionary<ColorTarget, ColorRgb> _assigned = new()
    {
        [new ColorTarget(ColorTargetKind.Face, 0)] = FileRed,
    };

    public string SourcePath => "fake.stp";

    public ModelStatistics Statistics { get; } = new(twoBodies ? 2 : 1, twoBodies ? 2 : 1, 2, 6, 4);

    public string? SavedPath { get; private set; }

    public Exception? SaveFailure { get; set; }

    public bool IsDisposed { get; private set; }

    public ColorRgb? GetFaceColor(FaceId faceId) =>
        _assigned.TryGetValue(ColorTarget.Of(faceId), out var face) ? face
        : _assigned.TryGetValue(ColorTarget.Of(GetBodyOfFace(faceId)!.Value), out var body) ? body
        : null;

    public IReadOnlyList<ModelNode> GetModelTree() => twoBodies
        ?
        [
            new ModelNode("Assembly", ModelNodeKind.Assembly, null,
            [
                new ModelNode("Part A", ModelNodeKind.Part, null, [new ModelNode("Part A", ModelNodeKind.Body, new BodyId(0), [])]),
                new ModelNode("Part B", ModelNodeKind.Part, null, [new ModelNode("Part B", ModelNodeKind.Body, new BodyId(1), [])]),
            ]),
        ]
        : [new ModelNode("Fake part", ModelNodeKind.Part, null, [new ModelNode("Fake part", ModelNodeKind.Body, new BodyId(0), [])])];

    public BodyId? GetBodyOfFace(FaceId faceId) => new BodyId(twoBodies ? faceId.Value : 0);

    public ColorRgb? GetAssignedColor(ColorTarget target) => _assigned.TryGetValue(target, out var color) ? color : null;

    public void SetColor(ColorTarget target, ColorRgb? color)
    {
        if (color is { } value)
        {
            _assigned[target] = value;
        }
        else
        {
            _assigned.Remove(target);
        }
    }

    public void SaveStep(string path)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        SavedPath = path;
    }

    public UpSTEPer.Core.Drawing.ProjectedView Project(UpSTEPer.Core.Drawing.ViewDirection direction, UpSTEPer.Core.Drawing.ProjectionOptions options) =>
        new(direction, []);

    public EdgeData TessellateEdges(TessellationOptions options) => EdgeData.Empty;

    public MeshData Tessellate(TessellationOptions options) =>
        new(new float[18], new float[18], [0, 1, 2, 3, 4, 5], [0, 1]);

    public void Dispose() => IsDisposed = true;
}

/// <summary>Reader handing out <see cref="FakeDocument"/>s (or throwing) and remembering them.</summary>
internal sealed class FakeReader(Func<string, IStepDocument>? load = null, bool twoBodies = false) : IStepReader
{
    public List<FakeDocument> Documents { get; } = [];

    public IStepDocument Load(string path)
    {
        if (load is not null)
        {
            return load(path);
        }

        var document = new FakeDocument(twoBodies);
        Documents.Add(document);
        return document;
    }
}
