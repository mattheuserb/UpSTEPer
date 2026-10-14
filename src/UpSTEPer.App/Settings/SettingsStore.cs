using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UpSTEPer.App.Settings;

/// <summary>
/// Loads and saves <see cref="UserSettings"/> as a JSON file. A missing or unreadable file gives the
/// defaults (an unreadable one is logged, never fatal); saving writes a temporary file first and
/// then replaces the old one, so an interrupted save cannot leave half a file. UI thread only.
/// </summary>
internal sealed partial class SettingsStore
{
    private readonly string _path;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="SettingsStore"/> class and loads the settings.</summary>
    /// <param name="path">The settings file.</param>
    /// <param name="logger">Logs unreadable or unwritable files; <see langword="null"/> for none.</param>
    public SettingsStore(string path, ILogger<SettingsStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _logger = logger ?? NullLogger<SettingsStore>.Instance;
        Current = Load();
    }

    /// <summary>Gets the default settings file: <c>UpSTEPer/settings.json</c> in the user's local application data.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UpSTEPer", "settings.json");

    /// <summary>Gets the current settings.</summary>
    public UserSettings Current { get; private set; }

    /// <summary>Changes the settings and saves them; a failed save is logged and the change kept in memory.</summary>
    /// <param name="change">Returns the new settings from the current ones.</param>
    public void Update(Func<UserSettings, UserSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Current = change(Current);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(Current, SettingsJsonContext.Default.UserSettings));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, _path, exception);
        }
    }

    private UserSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new UserSettings();
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(_path), SettingsJsonContext.Default.UserSettings) ?? new UserSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogLoadFailed(_logger, _path, exception);
            return new UserSettings();
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Settings {Path} could not be read; using defaults")]
    private static partial void LogLoadFailed(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Settings {Path} could not be saved")]
    private static partial void LogSaveFailed(ILogger logger, string path, Exception exception);
}
