using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using UpSTEPer.App.ViewModels;
using UpSTEPer.App.Views;
using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

public class CustomColorTests
{
    [Fact]
    public void UseCustomColor_AddsEntryAfterRalColoursAndSelectsIt()
    {
        using var viewModel = new MainViewModel(new FakeReader());
        var color = ColorRgb.FromHex("#123456");

        viewModel.UseCustomColor(color);

        Assert.Equal(PaletteEntry.Default.Count + 1, viewModel.Palette.Count);
        Assert.Equal(PaletteEntry.Custom(color), viewModel.Palette[^1]);
        Assert.Same(viewModel.Palette[^1], viewModel.SelectedPaletteEntry);
        Assert.Equal("Custom #123456", viewModel.SelectedPaletteEntry.Name);
    }

    [Fact]
    public void UseCustomColor_KnownColour_SelectsExistingEntryWithoutDuplicate()
    {
        using var viewModel = new MainViewModel(new FakeReader());
        var ral = PaletteEntry.Default[3];

        viewModel.UseCustomColor(ral.Color);
        viewModel.UseCustomColor(ColorRgb.FromHex("#123456"));
        viewModel.UseCustomColor(ColorRgb.FromHex("#123456"));

        Assert.Equal(PaletteEntry.Default.Count + 1, viewModel.Palette.Count);
        Assert.Equal("#123456", viewModel.SelectedPaletteEntry.Hex);
    }

    [Fact]
    public void UseCustomColor_BeyondLimit_DropsOldestPickedColour()
    {
        using var viewModel = new MainViewModel(new FakeReader());

        for (var i = 1; i <= MainViewModel.MaxCustomColors + 2; i++)
        {
            viewModel.UseCustomColor(ColorRgb.FromBytes((byte)i, 0, 0));
        }

        var custom = viewModel.Palette.Skip(PaletteEntry.Default.Count).Select(e => e.Hex).ToArray();
        Assert.Equal(MainViewModel.MaxCustomColors, custom.Length);
        Assert.Equal("#030000", custom[0]); // #010000 and #020000 dropped out
        Assert.Equal("#080000", custom[^1]);
        Assert.Equal(PaletteEntry.Default, viewModel.Palette.Take(PaletteEntry.Default.Count)); // RAL colours stay
    }

    [Fact]
    public async Task ColorFace_WithCustomColour_AppliesIt()
    {
        var reader = new FakeReader();
        using var viewModel = new MainViewModel(reader);
        await viewModel.LoadAsync("part.stp");
        viewModel.SelectedFace = new FaceId(1);

        viewModel.UseCustomColor(ColorRgb.FromHex("#ABCDEF"));
        await viewModel.ColorFaceCommand.ExecuteAsync(null);

        Assert.Equal(ColorRgb.FromHex("#ABCDEF"), reader.Documents[0].GetFaceColor(new FaceId(1)));
    }

    [Fact]
    public async Task UseColourButton_InFlyout_AddsPickedColourAndClosesFlyout()
    {
        var result = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                using var viewModel = new MainViewModel(new FakeReader());
                var window = new MainWindow { DataContext = viewModel };
                window.Show();
                var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CustomColorButton");
                var flyout = (Flyout)button.Flyout!;
                flyout.ShowAt(button);

                var content = (Panel)flyout.Content!;
                content.Children.OfType<ColorView>().Single().Color = Color.FromRgb(0x12, 0xAB, 0x34);
                content.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                var state = (viewModel.SelectedPaletteEntry.Hex, flyout.IsOpen);
                window.Close();
                return state;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("#12AB34", result.Hex);
        Assert.False(result.IsOpen);
    }
}
