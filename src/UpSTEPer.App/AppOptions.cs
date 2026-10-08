using System.Globalization;

namespace UpSTEPer.App;

/// <summary>Command-line options of the desktop app.</summary>
/// <param name="FilePath">A STEP file to open on start-up.</param>
/// <param name="ScreenshotPath">
/// When set, the app renders <paramref name="FilePath"/>, saves the first frame as PNG to this path
/// and exits (exit code 0 on success). Used to verify rendering without looking at the window.
/// </param>
/// <param name="SelectFace">A face to select after loading (verifies the selection highlight).</param>
internal sealed record AppOptions(string? FilePath, string? ScreenshotPath, int? SelectFace)
{
    /// <summary>Gets options for a plain start without arguments.</summary>
    public static AppOptions Default { get; } = new(null, null, null);

    /// <summary>Parses <c>[file] [--screenshot &lt;png&gt;] [--select-face &lt;id&gt;]</c>.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException">The arguments are malformed.</exception>
    public static AppOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? file = null;
        string? screenshot = null;
        int? selectFace = null;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--screenshot":
                    screenshot = Value(args, ++i, "--screenshot requires a file path.");
                    break;
                case "--select-face":
                    selectFace = int.TryParse(Value(args, ++i, "--select-face requires a face id."), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                        ? id
                        : throw new ArgumentException("--select-face requires a non-negative integer.", nameof(args));
                    break;
                default:
                    file = args[i];
                    break;
            }
        }

        if ((screenshot is not null || selectFace is not null) && file is null)
        {
            throw new ArgumentException("--screenshot and --select-face require a STEP file.", nameof(args));
        }

        return new AppOptions(file, screenshot, selectFace);
    }

    private static string Value(IReadOnlyList<string> args, int index, string error) =>
        index < args.Count ? args[index] : throw new ArgumentException(error, nameof(args));
}
