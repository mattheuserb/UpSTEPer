using System.Runtime.InteropServices;

namespace UpSTEPer.Occt.Native;

/// <summary>
/// P/Invoke declarations mirroring <c>native/include/upsteper/upsteper.h</c>.
/// Keep both files in sync; every change to the header needs a matching change here.
/// </summary>
internal static partial class NativeMethods
{
    internal const string LibraryName = "upsteper_native";

    [LibraryImport(LibraryName, EntryPoint = "ups_last_error_message")]
    internal static partial nint LastErrorMessage();

    [LibraryImport(LibraryName, EntryPoint = "ups_version")]
    internal static partial nint Version();

    [LibraryImport(LibraryName, EntryPoint = "ups_occt_version")]
    internal static partial nint OcctVersion();

    [LibraryImport(LibraryName, EntryPoint = "ups_document_load_step", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeStatus DocumentLoadStep(string utf8Path, out DocumentSafeHandle document);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_free")]
    internal static partial void DocumentFree(nint document);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_get_stats")]
    internal static partial NativeStatus DocumentGetStats(DocumentSafeHandle document, out NativeDocumentStats stats);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_get_face_color")]
    internal static partial NativeStatus DocumentGetFaceColor(
        DocumentSafeHandle document, int faceId, out NativeColor color, out int hasColor);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_tessellate")]
    internal static partial NativeStatus DocumentTessellate(
        DocumentSafeHandle document, in NativeTessellationParams parameters, out MeshSafeHandle mesh);

    [LibraryImport(LibraryName, EntryPoint = "ups_mesh_free")]
    internal static partial void MeshFree(nint mesh);

    [LibraryImport(LibraryName, EntryPoint = "ups_mesh_get_info")]
    internal static partial NativeStatus MeshGetInfo(MeshSafeHandle mesh, out NativeMeshInfo info);

    [LibraryImport(LibraryName, EntryPoint = "ups_mesh_copy")]
    internal static unsafe partial NativeStatus MeshCopy(
        MeshSafeHandle mesh,
        float* positions,
        long positionsCapacity,
        float* normals,
        long normalsCapacity,
        uint* indices,
        long indicesCapacity,
        int* triangleFaces,
        long triangleFacesCapacity);
}

/// <summary>Mirrors <c>ups_status</c>.</summary>
internal enum NativeStatus
{
    Ok = 0,
    InvalidArgument = 1,
    FileRead = 2,
    Transfer = 3,
    Operation = 4,
    BufferTooSmall = 5,
    Internal = 99,
}

/// <summary>Mirrors <c>ups_document_stats</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeDocumentStats
{
    public int SolidCount;
    public int ShellCount;
    public int FaceCount;
    public int EdgeCount;
    public int VertexCount;
}

/// <summary>Mirrors <c>ups_color</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeColor
{
    public float R;
    public float G;
    public float B;
}

/// <summary>Mirrors <c>ups_tessellation_params</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeTessellationParams
{
    public double LinearDeflection;
    public double AngularDeflection;
    public int Relative;
}

/// <summary>Mirrors <c>ups_mesh_info</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeMeshInfo
{
    public int VertexCount;
    public int TriangleCount;
}
