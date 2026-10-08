namespace UpSTEPer.App.Controls;

/// <summary>Describes why the 3D view cannot render.</summary>
/// <param name="message">The error message, e.g. the shader compiler log.</param>
internal sealed class RenderingFailedEventArgs(string message) : EventArgs
{
    /// <summary>Gets the error message.</summary>
    public string Message { get; } = message;
}
