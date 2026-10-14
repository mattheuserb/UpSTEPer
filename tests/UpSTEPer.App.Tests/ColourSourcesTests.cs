using System.Globalization;
using System.Text.RegularExpressions;
using UpSTEPer.App.Colours;
using UpSTEPer.App.Settings;
using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

public sealed partial class ColourSourcesTests : IDisposable
{
    private readonly string _settingsPath = Path.Combine(Path.GetTempPath(), $"upsteper-colours-{Guid.NewGuid():N}.json");

    [Fact]
    public void Catalogue_HasEveryColourOnceWithNamesAndValue()
    {
        var all = RalCatalogue.All;

        Assert.InRange(all.Count, 200, 220);
        Assert.Equal(all.Count, all.Select(c => c.Code).Distinct().Count());
        Assert.Equal(all.Select(c => c.Code).Order(StringComparer.Ordinal), all.Select(c => c.Code));
        Assert.All(all, c =>
        {
            Assert.Matches(CodePattern(), c.Code);
            Assert.Matches(HexPattern(), c.Hex);
            Assert.False(string.IsNullOrWhiteSpace(c.German), c.Code);
            Assert.False(string.IsNullOrWhiteSpace(c.English), c.Code);
        });
    }

    [Fact]
    public void Catalogue_Get_KnowsStoneGreyInBothLanguages()
    {
        var stoneGrey = RalCatalogue.Get("7030");

        Assert.Equal(("Steingrau", "Stone grey", "#8B8C7A"), (stoneGrey.German, stoneGrey.English, stoneGrey.Hex));
        Assert.Equal("RAL 7030 Steingrau", stoneGrey.Label(CultureInfo.GetCultureInfo("de-CH")));
        Assert.Equal("RAL 7030 Stone grey", stoneGrey.Label(CultureInfo.GetCultureInfo("en")));
        Assert.Throws<KeyNotFoundException>(() => RalCatalogue.Get("0000"));
    }

    [Theory]
    [InlineData("7030", "7030")]
    [InlineData("RAL 7030", "7030")]
    [InlineData("ral70", "7030")]
    [InlineData("stein", "7030")]   // German name
    [InlineData("Stone Grey", "7030")] // English name, other case
    [InlineData("verkehrsrot", "3020")]
    public void Catalogue_Search_FindsByNumberOrName(string text, string expected)
    {
        Assert.Contains(RalCatalogue.Search(text), c => c.Code == expected);
    }

    [Fact]
    public void Catalogue_Search_EmptyFindsAll_NonsenseFindsNothing()
    {
        Assert.Equal(RalCatalogue.All.Count, RalCatalogue.Search("  ").Count);
        Assert.Empty(RalCatalogue.Search("xyz"));
        Assert.All(RalCatalogue.Search("ral 70"), c => Assert.StartsWith("70", c.Code, StringComparison.Ordinal));
    }

    [Fact]
    public void Palette_DefaultsAreRalColoursNamedInTheUiLanguage()
    {
        Assert.Equal(10, PaletteEntry.Default.Count);
        Assert.All(PaletteEntry.Default, e => Assert.NotNull(e.Ral));
        Assert.Equal("RAL 3020 Traffic red", PaletteEntry.Default[0].Name);
        Assert.Equal("RAL 3020 Traffic red (#C1121C)", PaletteEntry.Default[0].Description);
    }

    [Fact]
    public void RalList_PickingAColour_MakesItActiveAndKeepsItInThePalette()
    {
        using var viewModel = new MainViewModel(new FakeReader());
        viewModel.RalSearch = "7030";
        var stoneGrey = Assert.Single(viewModel.RalColours);

        viewModel.RalSelection = stoneGrey;

        Assert.Equal("RAL 7030 Stone grey", viewModel.SelectedPaletteEntry.Name);
        Assert.Same(viewModel.SelectedPaletteEntry, viewModel.Palette[^1]);
        Assert.Same(viewModel.SelectedPaletteEntry, viewModel.PaletteSelection);

        viewModel.RalSearch = string.Empty;
        viewModel.RalSelection = viewModel.RalColours.Single(e => e.Ral!.Code == "7030"); // again: no duplicate
        Assert.Equal(PaletteEntry.Default.Count + 1, viewModel.Palette.Count);
    }

    [Fact]
    public async Task Favourite_AddedWithOwnName_IsRememberedAndNamesTheColouring()
    {
        using (var viewModel = new MainViewModel(new FakeReader(), settings: new SettingsStore(_settingsPath)))
        {
            viewModel.SelectedPaletteEntry = viewModel.Palette[4];
            Assert.False(viewModel.HasFavourites);

            viewModel.AddFavouriteCommand.Execute(null);
            Assert.True(viewModel.IsEditingFavourite);
            Assert.Equal(viewModel.Palette[4].Name, viewModel.FavouriteDraftName); // suggestion: the colour's name

            viewModel.FavouriteDraftName = "  Housing cover ";
            viewModel.SaveFavouriteCommand.Execute(null);

            Assert.False(viewModel.IsEditingFavourite);
            Assert.Equal("Favourite added: Housing cover", viewModel.Status);
        }

        using var reopened = new MainViewModel(new FakeReader(), settings: new SettingsStore(_settingsPath));
        var favourite = Assert.Single(reopened.Favourites);
        Assert.Equal(("Housing cover", PaletteEntry.Default[4].Hex), (favourite.Name, favourite.Hex));

        await reopened.LoadAsync("part.stp");
        reopened.FavouriteSelection = favourite;
        reopened.SelectedFace = new FaceId(1);
        await reopened.ColorFaceCommand.ExecuteAsync(null);

        Assert.Same(favourite, reopened.SelectedPaletteEntry);
        Assert.Null(reopened.PaletteSelection); // a favourite need not be in the palette
        Assert.Equal("Face colour applied: Housing cover", reopened.Status);
    }

    [Fact]
    public void Favourite_EmptyNameKeepsTheColourName_CancelAddsNothing()
    {
        using var viewModel = new MainViewModel(new FakeReader(), settings: new SettingsStore(_settingsPath));

        viewModel.AddFavouriteCommand.Execute(null);
        viewModel.CancelFavouriteCommand.Execute(null);
        Assert.Empty(viewModel.Favourites);

        viewModel.AddFavouriteCommand.Execute(null);
        viewModel.FavouriteDraftName = " ";
        viewModel.SaveFavouriteCommand.Execute(null);
        Assert.Equal("RAL 3020 Traffic red", Assert.Single(viewModel.Favourites).Name);
    }

    [Fact]
    public void Favourites_RenameMoveAndRemove_ArePersisted()
    {
        using var viewModel = new MainViewModel(new FakeReader(), settings: new SettingsStore(_settingsPath));
        foreach (var name in new[] { "A", "B", "C" })
        {
            viewModel.AddFavouriteCommand.Execute(null);
            viewModel.FavouriteDraftName = name;
            viewModel.SaveFavouriteCommand.Execute(null);
        }

        viewModel.FavouriteSelection = viewModel.Favourites[1];
        viewModel.RenameFavouriteCommand.Execute(viewModel.Favourites[1]);
        viewModel.FavouriteDraftName = "Bee";
        viewModel.SaveFavouriteCommand.Execute(null);
        Assert.Equal("Bee", viewModel.SelectedPaletteEntry.Name); // the active colour follows the rename

        viewModel.MoveFavouriteUpCommand.Execute(viewModel.Favourites[1]);
        viewModel.MoveFavouriteUpCommand.Execute(viewModel.Favourites[0]); // already first: nothing
        viewModel.MoveFavouriteDownCommand.Execute(viewModel.Favourites[2]); // already last: nothing
        viewModel.RemoveFavouriteCommand.Execute(viewModel.Favourites[2]);

        Assert.Equal(["Bee", "A"], viewModel.Favourites.Select(f => f.Name));
        Assert.Equal(["Bee", "A"], new SettingsStore(_settingsPath).Current.Favourites.Select(f => f.Name));
    }

    [Fact]
    public void Favourites_BrokenValueInSettings_IsSkipped()
    {
        File.WriteAllText(_settingsPath, """{ "favourites": [ { "name": "Good", "hex": "#123456" }, { "name": "Bad", "hex": "no colour" } ] }""");

        using var viewModel = new MainViewModel(new FakeReader(), settings: new SettingsStore(_settingsPath));

        Assert.Equal("Good", Assert.Single(viewModel.Favourites).Name);
    }

    [Fact]
    public void PaletteSelection_FollowsTheActiveColour()
    {
        using var viewModel = new MainViewModel(new FakeReader());

        viewModel.PaletteSelection = viewModel.Palette[2];
        Assert.Same(viewModel.Palette[2], viewModel.SelectedPaletteEntry);

        viewModel.SelectedPaletteEntry = new PaletteEntry("Elsewhere", ColorRgb.FromHex("#010203"));
        Assert.Null(viewModel.PaletteSelection);

        viewModel.PaletteSelection = null; // a list clearing its selection does not clear the active colour
        Assert.Equal("Elsewhere", viewModel.SelectedPaletteEntry.Name);
    }

    public void Dispose() => File.Delete(_settingsPath);

    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^#[0-9A-F]{6}$")]
    private static partial Regex HexPattern();
}
