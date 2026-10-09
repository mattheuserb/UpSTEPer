using UpSTEPer.Core;

namespace UpSTEPer.Cli.Tests;

/// <summary>In-memory <see cref="IStepReader"/> so CLI commands can be tested without the native kernel.</summary>
internal sealed class FakeStepReader : IStepReader
{
    private readonly Func<string, IStepDocument> _load;

    public FakeStepReader(Func<string, IStepDocument> load) => _load = load;

    public List<string> LoadedPaths { get; } = [];

    public IStepDocument Load(string path)
    {
        LoadedPaths.Add(path);
        return _load(path);
    }
}

/// <summary>A document with three faces: two red, one without colour; one triangle per face.</summary>
internal sealed class FakeStepDocument : IStepDocument
{
    private static readonly ColorRgb _red = ColorRgb.FromHex("#FF0000");

    public FakeStepDocument(string sourcePath) => SourcePath = sourcePath;

    public string SourcePath { get; }

    public ModelStatistics Statistics { get; } = new(SolidCount: 1, ShellCount: 1, FaceCount: 3, EdgeCount: 9, VertexCount: 6);

    public ModelBounds? Bounds { get; } = new(0, 0, 0, 1, 1, 1);

    public TessellationOptions? LastOptions { get; private set; }

    public bool IsDisposed { get; private set; }

    public Dictionary<ColorTarget, ColorRgb?> AssignedColors { get; } = [];

    public string? SavedPath { get; private set; }

    public ColorRgb? GetFaceColor(FaceId faceId) =>
        AssignedColors.TryGetValue(ColorTarget.Of(faceId), out var assigned) ? assigned : faceId.Value < 2 ? _red : null;

    public IReadOnlyList<ModelNode> GetModelTree() => [];

    public BodyId? GetBodyOfFace(FaceId faceId) => new BodyId(0);

    public ColorRgb? GetAssignedColor(ColorTarget target) => AssignedColors.GetValueOrDefault(target);

    public void SetColor(ColorTarget target, ColorRgb? color)
    {
        if (target.Id < 0 || target.Id >= (target.Kind == ColorTargetKind.Face ? Statistics.FaceCount : Statistics.SolidCount))
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        AssignedColors[target] = color;
    }

    public void SaveStep(string path) => SavedPath = path;

    public UpSTEPer.Core.Drawing.ProjectedView Project(UpSTEPer.Core.Drawing.ViewDirection direction, UpSTEPer.Core.Drawing.ProjectionOptions options) =>
        new(direction, []);

    public EdgeData TessellateEdges(TessellationOptions options) => EdgeData.Empty;

    public MeshData Tessellate(TessellationOptions options)
    {
        LastOptions = options;
        return new MeshData(new float[27], new float[27], [0, 1, 2, 3, 4, 5, 6, 7, 8], [0, 1, 2]);
    }

    public void Dispose() => IsDisposed = true;
}
