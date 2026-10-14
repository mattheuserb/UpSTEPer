using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpSTEPer.App.Colours;
using UpSTEPer.App.Localization;
using UpSTEPer.App.Settings;
using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>
/// Where the active colour comes from: the toolbar palette, the RAL Classic list and the user's
/// named favourites. Each list has its own selection; picking in a list makes that colour active,
/// and the active colour is marked in every list that contains it.
/// </summary>
internal sealed partial class MainViewModel
{
    // The favourite being renamed, or null when a new one is being added.
    private PaletteEntry? _renamingFavourite;

    /// <summary>Gets the user's favourite colours with their own names.</summary>
    public ObservableCollection<PaletteEntry> Favourites { get; } = [];

    /// <summary>Gets a value indicating whether there are favourites.</summary>
    public bool HasFavourites => Favourites.Count > 0;

    /// <summary>Gets or sets the selection of the palette lists (follows the active colour; null if it is not in the palette).</summary>
    [ObservableProperty]
    public partial PaletteEntry? PaletteSelection { get; set; }

    /// <summary>Gets or sets the selection of the favourites list.</summary>
    [ObservableProperty]
    public partial PaletteEntry? FavouriteSelection { get; set; }

    /// <summary>Gets or sets the search text of the RAL list (number or name).</summary>
    [ObservableProperty]
    public partial string RalSearch { get; set; } = string.Empty;

    /// <summary>Gets or sets the RAL colours matching <see cref="RalSearch"/>.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PaletteEntry> RalColours { get; set; } = [];

    /// <summary>Gets or sets the selection of the RAL list.</summary>
    [ObservableProperty]
    public partial PaletteEntry? RalSelection { get; set; }

    /// <summary>Gets or sets a value indicating whether the name of a new or renamed favourite is being edited.</summary>
    [ObservableProperty]
    public partial bool IsEditingFavourite { get; set; }

    /// <summary>Gets or sets the name typed for the new or renamed favourite.</summary>
    [ObservableProperty]
    public partial string FavouriteDraftName { get; set; } = string.Empty;

    /// <summary>
    /// Makes a colour active and keeps it in the palette: one with the same value is reused,
    /// otherwise it is added after the RAL colours (the oldest added one drops out beyond
    /// <see cref="MaxCustomColors"/>).
    /// </summary>
    /// <param name="entry">The colour.</param>
    public void UseColour(PaletteEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Same hex = same entry: picking a colour twice must not fill the palette with duplicates.
        var existing = Palette.FirstOrDefault(e => e.Hex == entry.Hex);
        if (existing is not null)
        {
            SelectedPaletteEntry = existing;
            return;
        }

        Palette.Add(entry);
        if (Palette.Count - PaletteEntry.Default.Count > MaxCustomColors)
        {
            Palette.RemoveAt(PaletteEntry.Default.Count);
        }

        SelectedPaletteEntry = entry;
    }

    /// <summary>Starts adding the active colour as a favourite; the name starts as the colour's name.</summary>
    [RelayCommand]
    private void AddFavourite()
    {
        _renamingFavourite = null;
        FavouriteDraftName = SelectedPaletteEntry.Name;
        IsEditingFavourite = true;
    }

    /// <summary>Starts renaming a favourite.</summary>
    /// <param name="favourite">The favourite.</param>
    [RelayCommand]
    private void RenameFavourite(PaletteEntry favourite)
    {
        _renamingFavourite = favourite;
        FavouriteDraftName = favourite.Name;
        IsEditingFavourite = true;
    }

    /// <summary>Saves the new or renamed favourite; an empty name keeps the colour's name.</summary>
    [RelayCommand]
    private void SaveFavourite()
    {
        if (!IsEditingFavourite)
        {
            return;
        }

        var name = FavouriteDraftName.Trim();
        if (_renamingFavourite is { } renamed)
        {
            var index = Favourites.IndexOf(renamed);
            if (index >= 0)
            {
                var entry = new PaletteEntry(name.Length == 0 ? renamed.Name : name, renamed.Color);
                Favourites[index] = entry;
                if (ReferenceEquals(SelectedPaletteEntry, renamed))
                {
                    SelectedPaletteEntry = entry;
                }
            }
        }
        else
        {
            var entry = new PaletteEntry(name.Length == 0 ? SelectedPaletteEntry.Name : name, SelectedPaletteEntry.Color);
            Favourites.Add(entry);
            Status = Localizer.Format(Strings.Status_FavouriteAdded, entry.Name);
        }

        CancelFavourite();
        SaveFavourites();
    }

    /// <summary>Stops editing a favourite's name without saving.</summary>
    [RelayCommand]
    private void CancelFavourite()
    {
        IsEditingFavourite = false;
        FavouriteDraftName = string.Empty;
        _renamingFavourite = null;
    }

    /// <summary>Removes a favourite.</summary>
    /// <param name="favourite">The favourite.</param>
    [RelayCommand]
    private void RemoveFavourite(PaletteEntry favourite)
    {
        if (Favourites.Remove(favourite))
        {
            SaveFavourites();
        }
    }

    /// <summary>Moves a favourite one place up.</summary>
    /// <param name="favourite">The favourite.</param>
    [RelayCommand]
    private void MoveFavouriteUp(PaletteEntry favourite) => MoveFavourite(favourite, -1);

    /// <summary>Moves a favourite one place down.</summary>
    /// <param name="favourite">The favourite.</param>
    [RelayCommand]
    private void MoveFavouriteDown(PaletteEntry favourite) => MoveFavourite(favourite, +1);

    private void MoveFavourite(PaletteEntry favourite, int step)
    {
        var index = Favourites.IndexOf(favourite);
        var target = index + step;
        if (index < 0 || target < 0 || target >= Favourites.Count)
        {
            return;
        }

        Favourites.Move(index, target);
        SaveFavourites();
    }

    private void LoadFavourites()
    {
        foreach (var favourite in _settings?.Current.Favourites ?? [])
        {
            try
            {
                Favourites.Add(new PaletteEntry(favourite.Name, ColorRgb.FromHex(favourite.Hex)));
            }
            catch (FormatException)
            {
                // A hand-edited settings file with a broken value: skip that favourite.
            }
        }

        Favourites.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFavourites));
    }

    private void SaveFavourites() =>
        _settings?.Update(settings => settings with { Favourites = [.. Favourites.Select(f => new FavouriteColour(f.Name, f.Hex))] });

    partial void OnPaletteSelectionChanged(PaletteEntry? value)
    {
        if (value is not null)
        {
            SelectedPaletteEntry = value;
        }
    }

    partial void OnFavouriteSelectionChanged(PaletteEntry? value)
    {
        if (value is not null)
        {
            SelectedPaletteEntry = value;
        }
    }

    partial void OnRalSelectionChanged(PaletteEntry? value)
    {
        if (value is not null)
        {
            UseColour(value);
        }
    }

    partial void OnRalSearchChanged(string value) => RalColours = [.. RalCatalogue.Search(value).Select(PaletteEntry.FromRal)];

    // Marks the active colour in every list that contains it (null elsewhere: no stale selection).
    partial void OnSelectedPaletteEntryChanged(PaletteEntry value)
    {
        PaletteSelection = Palette.Contains(value) ? value : null;
        FavouriteSelection = Favourites.Contains(value) ? value : null;
        if (RalSelection is { } ral && ral.Hex != value.Hex)
        {
            RalSelection = null;
        }
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Culture))
        {
            return;
        }

        foreach (var entry in Palette)
        {
            entry.OnLanguageChanged();
        }

        RalColours = [.. RalCatalogue.Search(RalSearch).Select(PaletteEntry.FromRal)];
    }
}
