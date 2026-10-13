using System.Text;
using UpSTEPer.Core;
using UpSTEPer.Core.Drawing;
using UpSTEPer.Occt.Native;

namespace UpSTEPer.Occt;

/// <summary>An <see cref="IStepDocument"/> backed by a native OCCT XDE document.</summary>
internal sealed class OcctStepDocument : IStepDocument
{
    private readonly DocumentSafeHandle _handle;
    // Re-created after a repair, which changes the topology.
    private Lazy<ModelStatistics> _statistics;
    private Lazy<ModelBounds?> _bounds;

    public OcctStepDocument(DocumentSafeHandle handle, string sourcePath)
    {
        _handle = handle;
        SourcePath = sourcePath;
        _statistics = new Lazy<ModelStatistics>(ReadStatistics);
        _bounds = new Lazy<ModelBounds?>(ReadBounds);
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

    public ModelBounds? Bounds
    {
        get
        {
            ThrowIfDisposed();
            return _bounds.Value;
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

    public IReadOnlyList<PartRepair> Repair(RepairOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfDisposed();
        if (!double.IsFinite(options.Tolerance) || options.Tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Tolerance, "Tolerance must be a positive, finite number.");
        }

        if ((options.Steps & ~RepairSteps.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Steps, "Unknown repair step.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var parameters = new NativeRepairParams { Steps = (int)options.Steps, Tolerance = options.Tolerance };
        RepairSafeHandle repair;
        using (var nativeProgress = new NativeProgress(progress, cancellationToken))
        {
            NativeStatus status;
            unsafe
            {
                status = NativeMethods.DocumentRepair(_handle, parameters, NativeProgress.Callback, nativeProgress.UserData, out repair);
            }

            try
            {
                nativeProgress.ThrowIfFailed(status, "Repair");
            }
            catch
            {
                repair.Dispose();
                throw;
            }
        }

        // Topology changed: statistics and bounds are read again on demand.
        _statistics = new Lazy<ModelStatistics>(ReadStatistics);
        _bounds = new Lazy<ModelBounds?>(ReadBounds);

        using (repair)
        {
            NativeError.ThrowIfFailed(NativeMethods.RepairGetInfo(repair, out var partCount, out var namesLength), "Reading repair info");
            var parts = new NativePartRepair[partCount];
            var names = new byte[namesLength];
            unsafe
            {
                fixed (NativePartRepair* partsPtr = parts)
                fixed (byte* namesPtr = names)
                {
                    NativeError.ThrowIfFailed(NativeMethods.RepairCopy(repair, partsPtr, parts.Length, namesPtr, names.Length), "Copying repair results");
                }
            }

            return [.. parts.Select(p => new PartRepair(
                Encoding.UTF8.GetString(names, p.NameOffset, p.NameLength),
                p.Changed != 0,
                p.Failed != 0,
                p.FacesBefore,
                p.FacesAfter,
                p.SolidsBefore,
                p.SolidsAfter,
                p.FreeEdgesBefore,
                p.FreeEdgesAfter,
                p.InvalidBefore,
                p.InvalidAfter,
                p.ColourConflicts,
                p.Reverted != 0,
                p.CanonicalFaces,
                p.MaxDeviation,
                p.CanonicalEdges))];
        }
    }

    public IReadOnlyList<PartCheck> Check(double tolerance = 0.01)
    {
        ThrowIfDisposed();
        if (!double.IsFinite(tolerance) || tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be a positive, finite number.");
        }

        NativeError.ThrowIfFailed(NativeMethods.DocumentCheck(_handle, tolerance, out var check), "Checking the model");
        using (check)
        {
            NativeError.ThrowIfFailed(NativeMethods.CheckGetInfo(check, out var partCount, out var namesLength), "Reading check info");
            var parts = new NativePartCheck[partCount];
            var names = new byte[namesLength];
            unsafe
            {
                fixed (NativePartCheck* partsPtr = parts)
                fixed (byte* namesPtr = names)
                {
                    NativeError.ThrowIfFailed(NativeMethods.CheckCopy(check, partsPtr, parts.Length, namesPtr, names.Length), "Copying check results");
                }
            }

            return [.. parts.Select(p => new PartCheck(
                Encoding.UTF8.GetString(names, p.NameOffset, p.NameLength),
                p.SolidCount,
                p.FaceCount,
                p.FreeEdges,
                p.InvalidShapes,
                p.SplineFaces,
                p.CanonicalFaces,
                p.MergeableFaces,
                p.SmallEdges,
                p.FailedChecks,
                p.SplineEdges,
                p.CanonicalEdges))];
        }
    }

    public IReadOnlyList<ModelNode> GetModelTree()
    {
        ThrowIfDisposed();

        NativeError.ThrowIfFailed(NativeMethods.DocumentGetTree(_handle, out var tree), "Reading the product structure");
        using (tree)
        {
            NativeError.ThrowIfFailed(NativeMethods.TreeGetInfo(tree, out var nodeCount, out var namesLength), "Reading tree info");
            var nodes = new NativeTreeNode[nodeCount];
            var names = new byte[namesLength];
            unsafe
            {
                fixed (NativeTreeNode* nodesPtr = nodes)
                fixed (byte* namesPtr = names)
                {
                    NativeError.ThrowIfFailed(NativeMethods.TreeCopy(tree, nodesPtr, nodes.Length, namesPtr, names.Length), "Copying tree");
                }
            }

            return BuildTree(nodes, names);
        }
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

    public void SaveStep(string path, StepSchema schema = StepSchema.Ap214)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ThrowIfDisposed();
        if (!Enum.IsDefined(schema))
        {
            throw new ArgumentOutOfRangeException(nameof(schema), schema, "Unknown STEP schema.");
        }

        var fullPath = Path.GetFullPath(path);
        NativeError.ThrowIfFailed(NativeMethods.DocumentSaveStepAs(_handle, fullPath, (NativeStepSchema)schema), $"Saving '{fullPath}'");
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

    public MeshData Tessellate(TessellationOptions options, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var parameters = new NativeTessellationParams
        {
            LinearDeflection = options.LinearDeflection,
            AngularDeflection = options.AngularDeflectionRadians,
            MinSize = options.MinSize ?? 0,
            Relative = options.IsRelative ? 1 : 0,
        };

        MeshSafeHandle meshHandle;
        using (var nativeProgress = new NativeProgress(progress, cancellationToken))
        {
            NativeStatus status;
            unsafe
            {
                status = NativeMethods.DocumentTessellateWithProgress(
                    _handle, parameters, NativeProgress.Callback, nativeProgress.UserData, out meshHandle);
            }

            try
            {
                nativeProgress.ThrowIfFailed(status, "Tessellation");
            }
            catch
            {
                meshHandle.Dispose();
                throw;
            }
        }

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

    // Native nodes come depth-first with parents before children; rebuild the hierarchy bottom-up.
    private static IReadOnlyList<ModelNode> BuildTree(NativeTreeNode[] nodes, byte[] names)
    {
        var children = new List<int>[nodes.Length];
        var roots = new List<int>();
        for (var i = 0; i < nodes.Length; i++)
        {
            children[i] = [];
            if (nodes[i].Parent < 0)
            {
                roots.Add(i);
            }
            else
            {
                children[nodes[i].Parent].Add(i);
            }
        }

        ModelNode Create(int index)
        {
            var node = nodes[index];
            var name = Encoding.UTF8.GetString(names, node.NameOffset, node.NameLength);
            var body = node.Kind == (int)ModelNodeKind.Body && node.BodyId >= 0 ? new BodyId(node.BodyId) : (BodyId?)null;
            return new ModelNode(name, (ModelNodeKind)node.Kind, body, [.. children[index].Select(Create)]);
        }

        return [.. roots.Select(Create)];
    }

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

    private ModelBounds? ReadBounds()
    {
        NativeError.ThrowIfFailed(NativeMethods.DocumentGetBounds(_handle, out var bounds), "Reading bounds");
        return bounds.IsVoid != 0 ? null : new ModelBounds(bounds.MinX, bounds.MinY, bounds.MinZ, bounds.MaxX, bounds.MaxY, bounds.MaxZ);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
}
