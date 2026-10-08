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
