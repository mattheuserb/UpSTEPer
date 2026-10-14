using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using UpSTEPer.App.Converters;
using UpSTEPer.App.Localization;
using UpSTEPer.App.Settings;
using UpSTEPer.App.ViewModels;
using UpSTEPer.App.Views;
using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

/// <summary>Switching the language changes global state: these tests run alone, never in parallel with others.</summary>
[CollectionDefinition(nameof(LanguageSwitching), DisableParallelization = true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires collection definitions to be public (xUnit1027).")]
public sealed class LanguageSwitching;

public static partial class LocalizationTests
{
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    // Texts that are the same in every language.
    private static readonly HashSet<string> _notTranslated = ["UpSTEPer", "English", "Deutsch", " "];

    public static TheoryData<string> Views => ["MainWindow", "RepairWindow", "ShortcutsWindow", "AboutWindow"];

    [Fact]
    public static void Resources_EveryKeyExistsInBothLanguagesWithTheSamePlaceholders()
    {
        var english = Read(CultureInfo.InvariantCulture);
        var german = Read(_german);

        Assert.Equal(english.Keys.Order(), german.Keys.Order());
        Assert.All(english, pair =>
        {
            Assert.False(string.IsNullOrWhiteSpace(german[pair.Key]), pair.Key);
            Assert.Equal(Placeholders(pair.Value), Placeholders(german[pair.Key]));
        });
    }

    [Fact]
    public static void Resources_EnumValuesShownInTheUiHaveTexts()
    {
        var english = Read(CultureInfo.InvariantCulture);
        IEnumerable<Enum> values = [.. Enum.GetValues<DisplayQuality>().Cast<Enum>(), .. Enum.GetValues<ModelNodeKind>().Cast<Enum>()];

        Assert.All(values, value => Assert.Contains($"{value.GetType().Name}_{value}", english.Keys));
    }

    [Theory]
    [MemberData(nameof(Views))]
    public static void Views_UseOnlyExistingKeysAndNoLiteralTexts(string view)
    {
        var xaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "UpSTEPer.App", "Views", view + ".axaml"));
        var keys = Read(CultureInfo.InvariantCulture).Keys.ToHashSet();

        Assert.All(TrKey().Matches(xaml).Select(m => m.Groups[1].Value), key => Assert.Contains(key, keys));
        Assert.All(LiteralText().Matches(xaml).Select(m => m.Groups[1].Value), text => Assert.Contains(text, _notTranslated));
    }

    [Theory]
    [InlineData(null, "de-DE", "de")]
    [InlineData(null, "de-AT", "de")]
    [InlineData(null, "en-GB", "en")]
    [InlineData(null, "fr-FR", "en")] // unsupported system language: English
    [InlineData("en", "de-DE", "en")] // the stored choice wins
    [InlineData("de", "en-US", "de")]
    [InlineData("xx", "de-DE", "en")] // unknown stored value
    public static void Resolve_PrefersTheStoredChoiceThenTheSystem(string? setting, string system, string expected)
    {
        Assert.Equal(expected, Localizer.Resolve(setting, CultureInfo.GetCultureInfo(system)).Name);
    }

    [Fact]
    public static void Indexer_UnknownKey_ShowsTheKey()
    {
        Assert.Equal("[No_Such_Key]", Localizer.Instance["No_Such_Key"]);
    }

    private static Dictionary<string, string> Read(CultureInfo culture)
    {
        var set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!;
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
    }

    private static string[] Placeholders(string text) =>
        [.. PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value).Distinct().Order()];

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UpSTEPer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [GeneratedRegex(@"\{(\d+)[^}]*\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\{loc:Tr (\w+)\}")]
    private static partial Regex TrKey();

    // Header="...", Text="...", ... whose value is not a markup extension or binding.
    [GeneratedRegex(@"(?<=\s)(?:Header|Text|ToolTip\.Tip|Content|Title|Watermark)=""([^""{#][^""]*)""")]
    private static partial Regex LiteralText();
}

[Collection(nameof(LanguageSwitching))]
public sealed class LanguageSwitchTests : IDisposable
{
    private readonly string _settingsPath = Path.Combine(Path.GetTempPath(), $"upsteper-language-{Guid.NewGuid():N}.json");

    [Fact]
    public void SetLanguageCommand_SwitchesTextsAndRemembersTheChoice()
    {
        using var viewModel = new MainViewModel(new FakeReader(), settings: new SettingsStore(_settingsPath));
        var changed = new List<string?>();
        Localizer.Instance.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.SetLanguageCommand.Execute("de");

        Assert.Equal("de", Localizer.Instance.Culture.Name);
        Assert.Equal("Prüfen", Localizer.Instance["Repair_Check"]);
        Assert.Equal("Fein", LocalizedEnumConverter.Text(DisplayQuality.Fine));
        Assert.Contains("Item[]", changed);
        Assert.Equal("de", new SettingsStore(_settingsPath).Current.Language);

        // RAL colours in the palette are renamed at once.
        Assert.Equal("RAL 3020 Verkehrsrot", viewModel.Palette[0].Name);
        Assert.Contains(viewModel.RalColours, e => e.Name == "RAL 7030 Steingrau");

        // New messages come in the new language, with the system's number format.
        viewModel.SelectedNode = null;
        Assert.Equal("Keine Probleme", new CheckRow(new PartCheck("P", 1, 1, 0, 0, 0, 0, 0, 0)).Findings);
    }

    [Fact]
    public async Task SwitchingLanguage_UpdatesTheOpenWindow()
    {
        var headers = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                using var viewModel = new MainViewModel(new FakeReader());
                var window = new MainWindow { DataContext = viewModel };
                window.Show();
                var menu = window.GetLogicalDescendants().OfType<Menu>().Single();
                var before = ((MenuItem)menu.Items[0]!).Header as string;

                viewModel.SetLanguageCommand.Execute("de");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var after = ((MenuItem)menu.Items[0]!).Header as string;
                window.Close();
                return (before, after);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(("_File", "_Datei"), headers);
    }

    public void Dispose()
    {
        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("en"));
        File.Delete(_settingsPath);
    }
}
