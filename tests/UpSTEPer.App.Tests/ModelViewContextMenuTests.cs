using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using UpSTEPer.App.Controls;
using UpSTEPer.Core;
using UpSTEPer.Rendering;

namespace UpSTEPer.App.Tests;

public class ModelViewContextMenuTests
{
    // One large square (face 0) filling the middle of the view, as in ModelViewHoverTests.
    private static readonly MeshData _square = new(
        positions: [-10, -10, 0, 10, -10, 0, 10, 10, 0, -10, -10, 0, 10, 10, 0, -10, 10, 0],
        normals: new float[18],
        indices: [0, 1, 2, 3, 4, 5],
        triangleFaceIds: [0, 0]);

    [Fact]
    public async Task RightClick_OnFace_SelectsItAndOpensFaceMenu()
    {
        var state = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                var (window, view, menu) = Show();
                window.MouseDown(new Point(100, 50), MouseButton.Right);
                window.MouseUp(new Point(100, 50), MouseButton.Right);
                var result = (view.SelectedFace, menu.IsOpen);
                menu.Hide();
                window.Close();
                return result;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(new FaceId(0), state.SelectedFace);
        Assert.True(state.IsOpen);
    }

    [Fact]
    public async Task RightDrag_PansWithoutMenuOrSelection()
    {
        var state = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                var (window, view, menu) = Show();
                var target = view.Camera.Target;
                window.MouseDown(new Point(100, 50), MouseButton.Right);
                window.MouseMove(new Point(140, 60));
                window.MouseUp(new Point(140, 60), MouseButton.Right);
                var result = (view.SelectedFace, menu.IsOpen, Panned: view.Camera.Target != target);
                window.Close();
                return result;
            },
            TestContext.Current.CancellationToken);

        Assert.Null(state.SelectedFace);
        Assert.False(state.IsOpen);
        Assert.True(state.Panned);
    }

    [Fact]
    public async Task LeftClick_SelectsFaceWithoutMenu()
    {
        var state = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                var (window, view, menu) = Show();
                window.MouseDown(new Point(100, 50), MouseButton.Left);
                window.MouseUp(new Point(100, 50), MouseButton.Left);
                var result = (view.SelectedFace, menu.IsOpen);
                window.Close();
                return result;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(new FaceId(0), state.SelectedFace);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public async Task FitCommand_AfterPanning_CentresModelAgain()
    {
        var targets = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                var (window, view, _) = Show();
                var fitted = view.Camera.Target;
                window.MouseDown(new Point(100, 50), MouseButton.Middle);
                window.MouseMove(new Point(160, 70));
                window.MouseUp(new Point(160, 70), MouseButton.Middle);
                var panned = view.Camera.Target;
                view.FitCommand.Execute(null);
                var result = (fitted, panned, refitted: view.Camera.Target);
                window.Close();
                return result;
            },
            TestContext.Current.CancellationToken);

        Assert.NotEqual(targets.fitted, targets.panned);
        Assert.Equal(targets.fitted, targets.refitted);
    }

    private static (Window Window, ModelView View, MenuFlyout Menu) Show()
    {
        var menu = new MenuFlyout();
        menu.Items.Add(new MenuItem { Header = "Fit model" });
        var view = new ModelView { Scene = SceneData.Create(_square, _ => null), FaceMenu = menu };
        var window = new Window { Width = 200, Height = 100, Content = view };
        window.Show();
        return (window, view, menu);
    }
}
