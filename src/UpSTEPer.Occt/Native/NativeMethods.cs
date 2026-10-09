using System.Runtime.InteropServices;

// Native libraries must never come from the current directory or PATH, so a planted DLL cannot
// be picked up (CA5392). The primary mechanism is NativeLibraryResolver, which loads
// upsteper_native by full path from the application directory on every platform. This
// attribute is the fallback for anything the resolver does not handle; SafeDirectories rather
// than AssemblyDirectory, which CA5393 rates unsafe.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

namespace UpSTEPer.Occt.Native;

/// <summary>
/// P/Invoke declarations mirroring <c>native/include/upsteper/upsteper.h</c>.
/// Keep both files in sync; every change to the header needs a matching change here.
/// </summary>
internal static partial class NativeMethods
{
    internal const string LibraryName = "upsteper_native";

    // An explicit static constructor runs before the first P/Invoke in this class.
    static NativeMethods() => NativeLibraryResolver.Register(typeof(NativeMethods).Assembly, LibraryName);

    [LibraryImport(LibraryName, EntryPoint = "ups_last_error_message")]
    internal static partial nint LastErrorMessage();

    [LibraryImport(LibraryName, EntryPoint = "ups_version")]
    internal static partial nint Version();

    [LibraryImport(LibraryName, EntryPoint = "ups_occt_version")]
    internal static partial nint OcctVersion();

    [LibraryImport(LibraryName, EntryPoint = "ups_document_load_step_with_progress", StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial NativeStatus DocumentLoadStepWithProgress(
        string utf8Path, delegate* unmanaged[Cdecl]<nint, double, byte*, int> callback, nint userData, out DocumentSafeHandle document);

    [LibraryImport(LibraryName, EntryPoint = "ups_set_message_handler")]
    internal static unsafe partial NativeStatus SetMessageHandler(
        delegate* unmanaged[Cdecl]<nint, NativeMessageLevel, byte*, void> callback, nint userData, NativeMessageLevel minLevel);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_free")]
    internal static partial void DocumentFree(nint document);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_get_stats")]
    internal static partial NativeStatus DocumentGetStats(DocumentSafeHandle document, out NativeDocumentStats stats);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_get_bounds")]
    internal static partial NativeStatus DocumentGetBounds(DocumentSafeHandle document, out NativeBounds bounds);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_get_face_color")]
    internal static partial NativeStatus DocumentGetFaceColor(
        DocumentSafeHandle document, int faceId, out NativeColor color, out int hasColor);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_get_face_body")]
    internal static partial NativeStatus DocumentGetFaceBody(DocumentSafeHandle document, int faceId, out int bodyId);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_get_assigned_color")]
    internal static partial NativeStatus DocumentGetAssignedColor(
        DocumentSafeHandle document, NativeTargetKind kind, int id, out NativeColor color, out int hasColor);

    // color: pointer to the colour, or null to remove the assigned colour.
    [LibraryImport(LibraryName, EntryPoint = "ups_document_set_color")]
    internal static unsafe partial NativeStatus DocumentSetColor(
        DocumentSafeHandle document, NativeTargetKind kind, int id, NativeColor* color);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_save_step", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeStatus DocumentSaveStep(DocumentSafeHandle document, string utf8Path);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_project")]
    internal static partial NativeStatus DocumentProject(
        DocumentSafeHandle document, in NativeProjectionParams parameters, out DrawingSafeHandle drawing);

    [LibraryImport(LibraryName, EntryPoint = "ups_drawing_free")]
    internal static partial void DrawingFree(nint drawing);

    [LibraryImport(LibraryName, EntryPoint = "ups_drawing_get_info")]
    internal static partial NativeStatus DrawingGetInfo(DrawingSafeHandle drawing, out int curveCount, out int pointCount);

    [LibraryImport(LibraryName, EntryPoint = "ups_drawing_copy")]
    internal static unsafe partial NativeStatus DrawingCopy(
        DrawingSafeHandle drawing, NativeDrawingCurve* curves, long curvesCapacity, double* points, long pointsCapacity);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_get_tree")]
    internal static partial NativeStatus DocumentGetTree(DocumentSafeHandle document, out TreeSafeHandle tree);

    [LibraryImport(LibraryName, EntryPoint = "ups_tree_free")]
    internal static partial void TreeFree(nint tree);

    [LibraryImport(LibraryName, EntryPoint = "ups_tree_get_info")]
    internal static partial NativeStatus TreeGetInfo(TreeSafeHandle tree, out int nodeCount, out int namesLength);

    [LibraryImport(LibraryName, EntryPoint = "ups_tree_copy")]
    internal static unsafe partial NativeStatus TreeCopy(
        TreeSafeHandle tree, NativeTreeNode* nodes, long nodesCapacity, byte* names, long namesCapacity);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_tessellate_edges")]
    internal static partial NativeStatus DocumentTessellateEdges(DocumentSafeHandle document, double deflection, out LinesSafeHandle lines);

    [LibraryImport(LibraryName, EntryPoint = "ups_lines_free")]
    internal static partial void LinesFree(nint lines);

    [LibraryImport(LibraryName, EntryPoint = "ups_lines_get_info")]
    internal static partial NativeStatus LinesGetInfo(LinesSafeHandle lines, out int vertexCount, out int segmentCount);

    [LibraryImport(LibraryName, EntryPoint = "ups_lines_copy")]
    internal static unsafe partial NativeStatus LinesCopy(
        LinesSafeHandle lines,
        float* positions,
        long positionsCapacity,
        uint* indices,
        long indicesCapacity,
        int* segmentEdges,
        long segmentEdgesCapacity);

    [LibraryImport(LibraryName, EntryPoint = "ups_document_tessellate_with_progress")]
    internal static unsafe partial NativeStatus DocumentTessellateWithProgress(
        DocumentSafeHandle document,
        in NativeTessellationParams parameters,
        delegate* unmanaged[Cdecl]<nint, double, byte*, int> callback,
        nint userData,
        out MeshSafeHandle mesh);

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
    FileWrite = 6,
    Cancelled = 7,
    Internal = 99,
}

/// <summary>Mirrors <c>ups_message_level</c>.</summary>
internal enum NativeMessageLevel
{
    Trace = 0,
    Info = 1,
    Warning = 2,
    Alarm = 3,
    Fail = 4,
}

/// <summary>Mirrors <c>ups_target_kind</c>.</summary>
internal enum NativeTargetKind
{
    Face = 0,
    Body = 1,
}

/// <summary>Mirrors <c>ups_tree_node</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeTreeNode
{
    public int Parent;
    public int Kind;
    public int BodyId;
    public int NameOffset;
    public int NameLength;
}

/// <summary>Mirrors <c>ups_curve_type</c>.</summary>
internal enum NativeCurveType
{
    Line = 0,
    Arc = 1,
    Circle = 2,
    Ellipse = 3,
    Polyline = 4,
}

/// <summary>Mirrors <c>ups_drawing_curve</c> (72 bytes: 2 ints, 7 doubles, 2 ints).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeDrawingCurve
{
    public NativeCurveType Type;
    public int Kind;
    public double D0;
    public double D1;
    public double D2;
    public double D3;
    public double D4;
    public double D5;
    public double D6;
    public int FirstPoint;
    public int PointCount;
}

/// <summary>Mirrors <c>ups_projection_params</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeProjectionParams
{
    public int View;
    public double Deflection;
    public int IncludeHidden;
    public int IncludeTangent;
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

/// <summary>Mirrors <c>ups_bounds</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeBounds
{
    public double MinX;
    public double MinY;
    public double MinZ;
    public double MaxX;
    public double MaxY;
    public double MaxZ;
    public int IsVoid;
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
