using Avalonia.Controls;
using UpSTEPer.App.ViewModels;
using UpSTEPer.App.Views;

namespace UpSTEPer.App.Tests;

public class MainWindowLayoutTests
{
    [Fact]
    public async Task HidingModelPanel_FreesItsColumn_AndShowingRestoresTheWidth()
    {
        var widths = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                using var viewModel = new MainViewModel(new FakeReader());
                var window = new MainWindow { DataContext = viewModel };
                window.Show();
                var column = window.FindControl<Grid>("ContentGrid")!.ColumnDefinitions[0];
                column.Width = new GridLength(320); // as after dragging the splitter

                viewModel.IsModelPanelVisible = false;
                var hidden = column.Width.Value;
                viewModel.IsModelPanelVisible = true;
                var shown = column.Width.Value;
                window.Close();
                return (hidden, shown);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(0, widths.hidden);
        Assert.Equal(320, widths.shown);
    }
}
