using Microsoft.Win32.SafeHandles;

namespace UpSTEPer.Occt.Native;

/// <summary>Owns an <c>ups_document*</c>; released with <c>ups_document_free</c>.</summary>
internal sealed class DocumentSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public DocumentSafeHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.DocumentFree(handle);
        return true;
    }
}

/// <summary>Owns an <c>ups_drawing*</c>; released with <c>ups_drawing_free</c>.</summary>
internal sealed class DrawingSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public DrawingSafeHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.DrawingFree(handle);
        return true;
    }
}

/// <summary>Owns an <c>ups_tree*</c>; released with <c>ups_tree_free</c>.</summary>
internal sealed class TreeSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public TreeSafeHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.TreeFree(handle);
        return true;
    }
}

/// <summary>Owns an <c>ups_repair*</c>; released with <c>ups_repair_free</c>.</summary>
internal sealed class RepairSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public RepairSafeHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.RepairFree(handle);
        return true;
    }
}

/// <summary>Owns an <c>ups_check*</c>; released with <c>ups_check_free</c>.</summary>
internal sealed class CheckSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public CheckSafeHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.CheckFree(handle);
        return true;
    }
}

/// <summary>Owns an <c>ups_lines*</c>; released with <c>ups_lines_free</c>.</summary>
internal sealed class LinesSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public LinesSafeHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.LinesFree(handle);
        return true;
    }
}

/// <summary>Owns an <c>ups_mesh*</c>; released with <c>ups_mesh_free</c>.</summary>
internal sealed class MeshSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public MeshSafeHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.MeshFree(handle);
        return true;
    }
}
