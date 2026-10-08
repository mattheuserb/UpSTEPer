namespace UpSTEPer.Core;

/// <summary>A geometric operation (tessellation, repair, export) failed in the geometry kernel.</summary>
public sealed class GeometryOperationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GeometryOperationException"/> class.</summary>
    public GeometryOperationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GeometryOperationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public GeometryOperationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GeometryOperationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public GeometryOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
