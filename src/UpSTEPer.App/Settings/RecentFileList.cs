namespace UpSTEPer.App.Settings;

/// <summary>Rules of the recent files list: newest first, no duplicates, at most <see cref="MaxCount"/> entries.</summary>
internal static class RecentFileList
{
    /// <summary>Gets the number of files remembered.</summary>
    public const int MaxCount = 5;

    // Paths are case-insensitive on Windows and macOS (default file systems), case-sensitive on Linux.
    private static readonly StringComparer _comparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>Puts <paramref name="path"/> first, removing an older entry of the same file and the oldest beyond the limit.</summary>
    /// <param name="files">The current list.</param>
    /// <param name="path">The opened file.</param>
    /// <returns>The new list.</returns>
    public static IReadOnlyList<string> Add(IReadOnlyList<string> files, string path)
    {
        var full = Path.GetFullPath(path);
        return [full, .. files.Where(f => !_comparer.Equals(f, full)).Take(MaxCount - 1)];
    }

    /// <summary>Removes <paramref name="path"/> (e.g. a file that no longer exists).</summary>
    /// <param name="files">The current list.</param>
    /// <param name="path">The file to remove.</param>
    /// <returns>The new list.</returns>
    public static IReadOnlyList<string> Remove(IReadOnlyList<string> files, string path) =>
        [.. files.Where(f => !_comparer.Equals(f, path))];
}
