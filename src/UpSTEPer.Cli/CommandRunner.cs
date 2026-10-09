using UpSTEPer.Core;

namespace UpSTEPer.Cli;

/// <summary>Turns expected failures into a one-line message and a non-zero exit code.</summary>
internal static class CommandRunner
{
    /// <summary>Exit code for invalid input (missing or unreadable file, bad geometry, bad arguments).</summary>
    public const int InputError = 2;

    /// <summary>Exit code after the user cancelled (Ctrl+C), as shells report for SIGINT.</summary>
    public const int Cancelled = 130;

    public static int Run(TextWriter error, Func<int> body)
    {
        try
        {
            return body();
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("cancelled");
            return Cancelled;
        }
        // Expected failures caused by user input: missing/unwritable files, invalid STEP, ids out of
        // range, malformed values. Everything else is a bug and must surface with a stack trace.
        catch (Exception exception) when (exception is IOException or StepImportException or GeometryOperationException
            or ArgumentException or FormatException)
        {
            error.WriteLine($"error: {UserMessage(exception)}");
            return InputError;
        }
    }

    // ArgumentException.Message appends " (Parameter 'x')" and, for out-of-range values, a second
    // line "Actual value was ...". Both are for developers; the CLI shows only the first part.
    internal static string UserMessage(Exception exception)
    {
        var message = exception.Message;
        if (exception is ArgumentException { ParamName: { } paramName })
        {
            var suffix = message.IndexOf($" (Parameter '{paramName}')", StringComparison.Ordinal);
            if (suffix >= 0)
            {
                message = message[..suffix];
            }
        }

        return message;
    }
}
