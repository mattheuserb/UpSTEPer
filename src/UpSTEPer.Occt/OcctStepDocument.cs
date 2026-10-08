using UpSTEPer.Core;
using UpSTEPer.Core.Drawing;
using UpSTEPer.Occt.Native;

namespace UpSTEPer.Occt;

/// <summary>An <see cref="IStepDocument"/> backed by a native OCCT XDE document.</summary>
internal sealed class OcctStepDocument : IStepDocument
{
    private readonly DocumentSafeHandle _handle;
    private readonly Lazy<ModelStatistics> _statistics;

    public OcctStepDocument(DocumentSafeHandle handle, string sourcePath)
    {
        _handle = handle;
        SourcePath = sourcePath;
        _statistics = new Lazy<ModelStatistics>(ReadStatistics);
    }

    public string SourcePath { get; }

    public ModelStatistics Statistics
    {
        get
        {
            ThrowIfDisposed();
            return _statistics.Value;
        }
    }

    public ColorRgb? GetFaceColor(FaceId faceId)
    {
        ThrowIfDisposed();
        ValidateTarget(ColorTarget.Of(faceId));

        var status = NativeMethods.DocumentGetFaceColor(_handle, faceId.Value, out var color, out var hasColor);
        NativeError.ThrowIfFailed(status, "Reading face colour");

        return hasColor != 0 ? ToColor(color) : null;
    }

    public BodyId? GetBodyOfFace(FaceId faceId)
    {
        ThrowIfDisposed();
        ValidateTarget(ColorTarget.Of(faceId));

        NativeError.ThrowIfFailed(NativeMethods.DocumentGetFaceBody(_handle, faceId.Value, out var bodyId), "Reading face body");
        return bodyId >= 0 ? new BodyId(bodyId) : null;
    }

    public ColorRgb? GetAssignedColor(ColorTarget target)
    {
        ThrowIfDisposed();
        ValidateTarget(target);

        var status = NativeMethods.DocumentGetAssignedColor(_handle, ToNative(target.Kind), target.Id, out var color, out var hasColor);
        NativeError.ThrowIfFailed(status, $"Reading colour of {target}");
        return hasColor != 0 ? ToColor(color) : null;
    }

    public unsafe void SetColor(ColorTarget target, ColorRgb? color)
    {
        ThrowIfDisposed();
        ValidateTarget(target);

        NativeStatus status;
        if (color is { } value)
        {
            var native = new NativeColor { R = value.R, G = value.G, B = value.B };
            status = NativeMethods.DocumentSetColor(_handle, ToNative(target.Kind), target.Id, &native);
        }
        else
        {
            status = NativeMethods.DocumentSetColor(_handle, ToNative(target.Kind), target.Id, null);
        }

        NativeError.ThrowIfFailed(status, $"Colouring {target}");
    }

    public void SaveStep(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ThrowIfDisposed();

        var fullPath = Path.GetFullPath(path);
        NativeError.ThrowIfFailed(NativeMethods.DocumentSaveStep(_handle, fullPath), $"Saving '{fullPath}'");
    }

    public ProjectedView Project(ViewDirection direction, ProjectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfDisposed();

        var parameters = new NativeProjectionParams
        {
            View = (int)direction,
            Deflection = options.Deflection,
            IncludeHidden = options.IncludeHidden ? 1 : 0,
            IncludeTangent = options.IncludeTangent ? 1 : 0,
        };

        NativeError.ThrowIfFailed(NativeMethods.DocumentProject(_handle, parameters, out var drawing), $"Projecting {direction} view");
        using (drawing)
        {
            NativeError.ThrowIfFailed(NativeMethods.DrawingGetInfo(drawing, out var curveCount, out var pointCount), "Reading drawing info");
            var curves = new NativeDrawingCurve[curveCount];
            var points = new double[pointCount * 2];
            unsafe
            {
                fixed (NativeDrawingCurve* curvesPtr = curves)
                fixed (double* pointsPtr = points)
                {
                    NativeError.ThrowIfFailed(
                        NativeMethods.DrawingCopy(drawing, curvesPtr, curves.Length, pointsPtr, points.Length), "Copying drawing");
                }
            }

            var result = new DrawingCurve[curveCount];
            for (var i = 0; i < curveCount; i++)
            {
                result[i] = DrawingConverter.ToCurve(curves[i], points);
            }

            return new ProjectedView(direction, result);
        }
    }

    public EdgeData TessellateEdges(TessellationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfDisposed();

        NativeError.ThrowIfFailed(
            NativeMethods.DocumentTessellateEdges(_handle, options.LinearDeflection, out var lines), "Discretising edges");
        using (lines)
        {
            NativeError.ThrowIfFailed(NativeMethods.LinesGetInfo(lines, out var vertexCount, out var segmentCount), "Reading line info");
            var positions = new float[vertexCount * 3];
            var indices = new uint[segmentCount * 2];
            var edges = new int[segmentCount];
            unsafe
            {
                fixed (float* positionsPtr = positions)
                fixed (uint* indicesPtr = indices)
                fixed (int* edgesPtr = edges)
                {
                    NativeError.ThrowIfFailed(
                        NativeMethods.LinesCopy(lines, positionsPtr, positions.Length, indicesPtr, indices.Length, edgesPtr, edges.Length),
                        "Copying lines");
                }
            }

            return new EdgeData(positions, indices, edges);
        }
    }

    public MeshData Tessellate(TessellationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfDisposed();

        var parameters = new NativeTessellationParams
        {
            LinearDeflection = options.LinearDeflection,
            AngularDeflection = options.AngularDeflectionRadians,
            Relative = options.IsRelative ? 1 : 0,
        };

        NativeError.ThrowIfFailed(NativeMethods.DocumentTessellate(_handle, parameters, out var meshHandle), "Tessellation");
        using (meshHandle)
        {
            NativeError.ThrowIfFailed(NativeMethods.MeshGetInfo(meshHandle, out var info), "Reading mesh info");

            var positions = new float[info.VertexCount * 3];
            var normals = new float[info.VertexCount * 3];
            var indices = new uint[info.TriangleCount * 3];
            var triangleFaces = new int[info.TriangleCount];

            unsafe
            {
                fixed (float* positionsPtr = positions)
                fixed (float* normalsPtr = normals)
                fixed (uint* indicesPtr = indices)
                fixed (int* triangleFacesPtr = triangleFaces)
                {
                    var status = NativeMethods.MeshCopy(
                        meshHandle,
                        positionsPtr,
                        positions.Length,
                        normalsPtr,
                        normals.Length,
                        indicesPtr,
                        indices.Length,
                        triangleFacesPtr,
                        triangleFaces.Length);
                    NativeError.ThrowIfFailed(status, "Copying mesh data");
                }
            }

            return new MeshData(positions, normals, indices, triangleFaces);
        }
    }

    public void Dispose() => _handle.Dispose();

    // Clamping guards against sub-ulp overshoot from colour space conversion in OCCT.
    private static ColorRgb ToColor(NativeColor color) =>
        new(Math.Clamp(color.R, 0f, 1f), Math.Clamp(color.G, 0f, 1f), Math.Clamp(color.B, 0f, 1f));

    private static NativeTargetKind ToNative(ColorTargetKind kind) => kind switch
    {
        ColorTargetKind.Face => NativeTargetKind.Face,
        ColorTargetKind.Body => NativeTargetKind.Body,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown target kind."),
    };

    private void ValidateTarget(ColorTarget target)
    {
        var (count, noun) = target.Kind == ColorTargetKind.Face ? (Statistics.FaceCount, "faces") : (Statistics.SolidCount, "bodies");
        if (target.Id < 0 || target.Id >= count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(target), target.Id, $"{target.Kind} {target.Id} does not exist; the model has {count} {noun} (0..{count - 1}).");
        }
    }

    private ModelStatistics ReadStatistics()
    {
        NativeError.ThrowIfFailed(NativeMethods.DocumentGetStats(_handle, out var stats), "Reading statistics");
        return new ModelStatistics(stats.SolidCount, stats.ShellCount, stats.FaceCount, stats.EdgeCount, stats.VertexCount);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
}
