using System.Text.Json;
using System.Text.RegularExpressions;

namespace UpSTEPer.App.Tests;

public static partial class ThirdPartyNoticesTests
{
    public static TheoryData<string> ShippedProjects => ["UpSTEPer.App", "UpSTEPer.Cli"];

    [Theory]
    [MemberData(nameof(ShippedProjects))]
    public static void Notices_ListEveryPackageTheProjectDependsOn(string project)
    {
        var root = FindRepoRoot();
        var listed = ListedPackages(File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.md")));
        Assert.NotEmpty(listed);

        var missing = RestoredPackages(Path.Combine(root, "src", project, "obj", "project.assets.json"))
            .Where(package => !listed.Any(entry => Covers(entry, package)))
            .ToList();

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("Avalonia.*", "Avalonia.Skia", true)]
    [InlineData("Avalonia.*", "Avalonia", false)]
    [InlineData("Avalonia", "Avalonia", true)]
    [InlineData("SkiaSharp", "SkiaSharp.NativeAssets.Linux", false)]
    public static void Covers_MatchesExactNamesAndPrefixes(string entry, string package, bool expected)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(package);

        Assert.Equal(expected, Covers(entry, package));
    }

    // Package names in the notices are code spans in the "Packages" column, either exact ids or "Prefix.*".
    private static List<string> ListedPackages(string notices) =>
        [.. notices.Split('\n')
            .Where(line => line.StartsWith('|'))
            .Select(line => line.Split('|'))
            .Where(cells => cells.Length > 3)
            .SelectMany(cells => CodeSpan().Matches(cells[2]).Select(m => m.Groups[1].Value))];

    private static IEnumerable<string> RestoredPackages(string assetsFile)
    {
        // Written by the restore that precedes every build; lists direct and transitive packages.
        using var assets = JsonDocument.Parse(File.ReadAllText(assetsFile));
        return [.. assets.RootElement.GetProperty("libraries").EnumerateObject()
            .Where(library => library.Value.GetProperty("type").GetString() == "package")
            .Select(library => library.Name.Split('/')[0])];
    }

    private static bool Covers(string entry, string package) =>
        entry.EndsWith(".*", StringComparison.Ordinal)
            ? package.StartsWith(entry[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(entry, package, StringComparison.OrdinalIgnoreCase);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UpSTEPer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex CodeSpan();
}
