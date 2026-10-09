using System.Globalization;
using UpSTEPer.Core;
using UpSTEPer.Core.Drawing;

namespace UpSTEPer.App;

/// <summary>Command-line options of the desktop app.</summary>
/// <param name="FilePath">A STEP file to open on start-up.</param>
/// <param name="ScreenshotPath">
/// When set, the app renders <paramref name="FilePath"/>, saves the first frame as PNG to this path
/// and exits (exit code 0 on success). Used to verify rendering without looking at the window.
/// </param>
/// <param name="SelectFace">A face to select after loading (verifies the selection highlight).</param>
/// <param name="View">A standard view to show after loading.</param>
/// <param name="Orthographic">Whether to start with the orthographic projection.</param>
/// <param name="Quality">The display quality to load the model with.</param>
internal sealed record AppOptions(
    string? FilePath,
    string? ScreenshotPath,
    int? SelectFace,
    ViewDirection? View = null,
    bool Orthographic = false,
    DisplayQuality Quality = DisplayQuality.Normal)
{
    /// <summary>Gets options for a plain start without arguments.</summary>
    public static AppOptions Default { get; } = new(null, null, null);

    /// <summary>
    /// Parses <c>[file] [--screenshot &lt;png&gt;] [--select-face &lt;id&gt;] [--view &lt;front|back|left|right|top|bottom|iso&gt;] [--ortho] [--quality &lt;coarse|normal|fine&gt;]</c>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException">The arguments are malformed.</exception>
    public static AppOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? file = null;
        string? screenshot = null;
        int? selectFace = null;
        ViewDirection? view = null;
        var orthographic = false;
        var quality = DisplayQuality.Normal;
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
                case "--view":
                    view = ParseName<ViewDirection>(Value(args, ++i, "--view requires a view name."))
                        ?? throw new ArgumentException("--view must be front, back, left, right, top, bottom or iso.", nameof(args));
                    break;
                case "--quality":
                    quality = ParseName<DisplayQuality>(Value(args, ++i, "--quality requires a quality name."))
                        ?? throw new ArgumentException("--quality must be coarse, normal or fine.", nameof(args));
                    break;
                case "--ortho":
                    orthographic = true;
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

        return new AppOptions(file, screenshot, selectFace, view, orthographic, quality);
    }

    // Names only: Enum.TryParse would also accept numbers such as "1".
    private static T? ParseName<T>(string name)
        where T : struct, Enum =>
        name.Length > 0 && name.All(char.IsAsciiLetter) && Enum.TryParse<T>(name, ignoreCase: true, out var value)
            ? value
            : null;

    private static string Value(IReadOnlyList<string> args, int index, string error) =>
        index < args.Count ? args[index] : throw new ArgumentException(error, nameof(args));
}
