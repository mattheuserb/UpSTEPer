using System.Numerics;
using Avalonia.Headless;
using Avalonia.Input;
using UpSTEPer.App.ViewModels;
using UpSTEPer.App.Views;

namespace UpSTEPer.App.Tests;

public class MainWindowShortcutTests
{
    [Theory]
    [InlineData(Key.NumPad1, RawInputModifiers.None, 0, 1, 0)]
    [InlineData(Key.NumPad1, RawInputModifiers.Control, 0, -1, 0)]
    [InlineData(Key.NumPad3, RawInputModifiers.None, -1, 0, 0)]
    [InlineData(Key.NumPad3, RawInputModifiers.Control, 1, 0, 0)]
    [InlineData(Key.NumPad7, RawInputModifiers.None, 0, 0, -1)]
    [InlineData(Key.NumPad7, RawInputModifiers.Control, 0, 0, 1)]
    public async Task NumPadKey_ShowsStandardView(Key key, RawInputModifiers modifiers, float x, float y, float z)
    {
        var forward = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                using var viewModel = new MainViewModel(new FakeReader());
                var window = new MainWindow { DataContext = viewModel };
                window.Show();
                window.ModelView.Camera.Orbit(40, 25);

                window.KeyPress(key, modifiers, PhysicalKey.None, keySymbol: null);
                window.Close();
                return window.ModelView.Camera.Forward;
            },
            TestContext.Current.CancellationToken);

        Assert.True(Vector3.Distance(new Vector3(x, y, z), forward) < 1e-5f, $"Forward was {forward}.");
    }

    [Fact]
    public async Task NumPad5_TogglesOrthographicProjection()
    {
        var states = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                using var viewModel = new MainViewModel(new FakeReader());
                var window = new MainWindow { DataContext = viewModel };
                window.Show();

                window.KeyPress(Key.NumPad5, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
                var afterFirst = (viewModel.IsOrthographic, window.ModelView.Camera.IsOrthographic);
                window.KeyPress(Key.NumPad5, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
                var afterSecond = (viewModel.IsOrthographic, window.ModelView.Camera.IsOrthographic);
                window.Close();
                return (afterFirst, afterSecond);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal((true, true), states.afterFirst);
        Assert.Equal((false, false), states.afterSecond);
    }
}
