using Avalonia.Controls;
using UpSTEPer.App.ViewModels;
using UpSTEPer.App.Views;

namespace UpSTEPer.App.Tests;

public class WindowIconTests
{
    public static TheoryData<string> Windows => ["MainWindow", "AboutWindow", "ShortcutsWindow", "RepairWindow"];

    [Theory]
    [MemberData(nameof(Windows))]
    public async Task Window_ShowsTheAppIcon(string name)
    {
        var hasIcon = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                using var viewModel = new MainViewModel(new FakeReader());
                Window window = name switch
                {
                    "MainWindow" => new MainWindow { DataContext = viewModel },
                    "AboutWindow" => new AboutWindow(),
                    "ShortcutsWindow" => new ShortcutsWindow(),
                    _ => new RepairWindow(),
                };
                window.Show();
                var icon = window.Icon;
                window.Close();
                return icon is not null;
            },
            TestContext.Current.CancellationToken);

        Assert.True(hasIcon);
    }
}
