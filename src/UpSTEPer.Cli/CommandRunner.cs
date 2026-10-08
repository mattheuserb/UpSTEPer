using UpSTEPer.Core;

namespace UpSTEPer.Cli;

/// <summary>Turns expected failures into a one-line message and a non-zero exit code.</summary>
internal static class CommandRunner
{
    /// <summary>Exit code for invalid input (missing or unreadable file, bad geometry).</summary>
    public const int InputError = 2;

    public static int Run(TextWriter error, Func<int> body)
    {
        try
        {
            return body();
        }
        catch (Exception exception) when (exception is FileNotFoundException or StepImportException or GeometryOperationException)
        {
            error.WriteLine($"error: {exception.Message}");
            return InputError;
        }
    }
}
