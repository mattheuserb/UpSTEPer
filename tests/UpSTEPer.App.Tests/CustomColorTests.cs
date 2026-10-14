using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
        Assert.Equal(("#123456", null), (viewModel.Palette[^1].Hex, viewModel.Palette[^1].Ral));
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
    public async Task CustomColour_InColourPanel_StartsAtActiveColourAndAddsPickedColour()
    {
        var result = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                using var viewModel = new MainViewModel(new FakeReader());
                var window = new MainWindow { DataContext = viewModel };
                window.Show();
                var button = window.FindControl<DropDownButton>("ColourButton")!;
                var flyout = (Flyout)button.Flyout!;
                flyout.ShowAt(button);

                var expander = window.FindControl<Expander>("CustomColourExpander")!;
                expander.IsExpanded = true;
                var spectrum = window.FindControl<ColorSpectrum>("CustomSpectrum")!;
                var startHex = window.FindControl<TextBox>("CustomHexBox")!.Text;
                spectrum.Color = Color.FromRgb(0x12, 0xAB, 0x34);
                window.FindControl<Button>("UseCustomColourButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                var state = (startHex, viewModel.SelectedPaletteEntry.Hex, flyout.IsOpen, expander.IsExpanded);
                window.Close();
                return state;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(PaletteEntry.Default[0].Hex, result.startHex);
        Assert.Equal("#12AB34", result.Hex);
        Assert.True(result.IsOpen); // stays open for "Apply to"
        Assert.False(result.IsExpanded);
    }

    [Theory]
    [InlineData("#12AB34", "#12AB34")]
    [InlineData("12ab34", "#12AB34")]
    [InlineData("nonsense", "#C1121C")] // invalid: back to the current colour
    [InlineData("#FFF", "#C1121C")] // short forms are not accepted
    public async Task CustomColour_HexInput_MovesPickerOnEnter(string typed, string expected)
    {
        var hex = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                using var viewModel = new MainViewModel(new FakeReader());
                var window = new MainWindow { DataContext = viewModel };
                window.Show();
                var button = window.FindControl<DropDownButton>("ColourButton")!;
                ((Flyout)button.Flyout!).ShowAt(button);
                window.FindControl<Expander>("CustomColourExpander")!.IsExpanded = true;

                var box = window.FindControl<TextBox>("CustomHexBox")!;
                box.Text = typed;
                box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                var color = window.FindControl<ColorSpectrum>("CustomSpectrum")!.Color;
                window.Close();
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, hex);
    }
}
