using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

/// <summary>
/// In-memory document: two faces on one body. The file colours face 0 red; the body has no colour.
/// Colour resolution mirrors the native layer: face colour, else body colour, else none.
/// </summary>
internal sealed class FakeDocument : IStepDocument
{
    public static readonly ColorRgb FileRed = ColorRgb.FromHex("#FF0000");

    private readonly Dictionary<ColorTarget, ColorRgb> _assigned = new()
    {
        [new ColorTarget(ColorTargetKind.Face, 0)] = FileRed,
    };

    public string SourcePath => "fake.stp";

    public ModelStatistics Statistics { get; } = new(1, 1, 2, 6, 4);

    public string? SavedPath { get; private set; }

    public Exception? SaveFailure { get; set; }

    public bool IsDisposed { get; private set; }

    public ColorRgb? GetFaceColor(FaceId faceId) =>
        _assigned.TryGetValue(ColorTarget.Of(faceId), out var face) ? face
        : _assigned.TryGetValue(new ColorTarget(ColorTargetKind.Body, 0), out var body) ? body
        : null;

    public BodyId? GetBodyOfFace(FaceId faceId) => new BodyId(0);

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

    public MeshData Tessellate(TessellationOptions options) =>
        new(new float[18], new float[18], [0, 1, 2, 3, 4, 5], [0, 1]);

    public void Dispose() => IsDisposed = true;
}

/// <summary>Reader handing out <see cref="FakeDocument"/>s (or throwing) and remembering them.</summary>
internal sealed class FakeReader(Func<string, IStepDocument>? load = null) : IStepReader
{
    public List<FakeDocument> Documents { get; } = [];

    public IStepDocument Load(string path)
    {
        if (load is not null)
        {
            return load(path);
        }

        var document = new FakeDocument();
        Documents.Add(document);
        return document;
    }
}
