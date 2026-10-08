namespace UpSTEPer.Core;

/// <summary>A STEP file could not be read or converted to geometry.</summary>
public sealed class StepImportException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="StepImportException"/> class.</summary>
    public StepImportException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StepImportException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public StepImportException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StepImportException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public StepImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
