namespace UpSTEPer.App;

/// <summary>Command-line options of the desktop app.</summary>
/// <param name="FilePath">A STEP file to open on start-up.</param>
/// <param name="ScreenshotPath">
/// When set, the app renders <paramref name="FilePath"/>, saves the first frame as PNG to this path
/// and exits (exit code 0 on success). Used to verify rendering without looking at the window.
/// </param>
internal sealed record AppOptions(string? FilePath, string? ScreenshotPath)
{
    /// <summary>Gets options for a plain start without arguments.</summary>
    public static AppOptions Default { get; } = new(null, null);

    /// <summary>Parses <c>[file] [--screenshot &lt;png&gt;]</c>.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException">The arguments are malformed.</exception>
    public static AppOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? file = null;
        string? screenshot = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--screenshot")
            {
                screenshot = i + 1 < args.Count ? args[++i] : throw new ArgumentException("--screenshot requires a file path.", nameof(args));
            }
            else
            {
                file = args[i];
            }
        }

        if (screenshot is not null && file is null)
        {
            throw new ArgumentException("--screenshot requires a STEP file to render.", nameof(args));
        }

        return new AppOptions(file, screenshot);
    }
}
