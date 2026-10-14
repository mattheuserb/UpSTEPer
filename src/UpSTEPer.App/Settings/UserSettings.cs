using System.Text.Json.Serialization;

namespace UpSTEPer.App.Settings;

/// <summary>What the app remembers between runs (stored as JSON by <see cref="SettingsStore"/>).</summary>
/// <remarks>Add new members with defaults: older files without them must still load.</remarks>
internal sealed record UserSettings
{
    /// <summary>Gets the most recently opened STEP files, newest first (full paths).</summary>
    public IReadOnlyList<string> RecentFiles { get; init; } = [];

    /// <summary>Gets the UI language chosen by the user ("en", "de"); <see langword="null"/> follows the system.</summary>
    public string? Language { get; init; }
}

/// <summary>Source-generated JSON metadata for <see cref="UserSettings"/> (no reflection, trimming-safe).</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UserSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
