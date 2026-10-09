using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using UpSTEPer.App.Controls;

namespace UpSTEPer.App.Tests;

public class ModelViewInputTests
{
    [Fact]
    public async Task MouseDown_InsideView_ReachesModelView()
    {
        var pressed = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                var view = new ModelView();
                var window = new Window { Width = 200, Height = 100, Content = view };
                var count = 0;
                view.PointerPressed += (_, _) => count++;
                window.Show();

                window.MouseDown(new Point(100, 50), MouseButton.Left);
                window.MouseUp(new Point(100, 50), MouseButton.Left);
                window.Close();
                return count;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, pressed);
    }
}
