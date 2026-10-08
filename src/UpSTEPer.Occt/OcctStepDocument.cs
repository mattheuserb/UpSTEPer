using UpSTEPer.Core;
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
        ArgumentOutOfRangeException.ThrowIfNegative(faceId.Value, nameof(faceId));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(faceId.Value, Statistics.FaceCount, nameof(faceId));

        var status = NativeMethods.DocumentGetFaceColor(_handle, faceId.Value, out var color, out var hasColor);
        NativeError.ThrowIfFailed(status, "Reading face colour");

        return hasColor != 0 ? new ColorRgb(Clamp(color.R), Clamp(color.G), Clamp(color.B)) : null;
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

    // Guards against sub-ulp overshoot from colour space conversion in OCCT.
    private static float Clamp(float channel) => Math.Clamp(channel, 0f, 1f);

    private ModelStatistics ReadStatistics()
    {
        NativeError.ThrowIfFailed(NativeMethods.DocumentGetStats(_handle, out var stats), "Reading statistics");
        return new ModelStatistics(stats.SolidCount, stats.ShellCount, stats.FaceCount, stats.EdgeCount, stats.VertexCount);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
}
