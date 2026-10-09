using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using UpSTEPer.App.Controls;
using UpSTEPer.Core;
using UpSTEPer.Rendering;

namespace UpSTEPer.App.Tests;

public class ModelViewHoverTests
{
    // One large square (two triangles of face 0) in the XY plane: the default iso camera fitted to
    // it looks at its centre, so the middle of the view is always on the face.
    private static readonly MeshData _square = new(
        positions: [-10, -10, 0, 10, -10, 0, 10, 10, 0, -10, -10, 0, 10, 10, 0, -10, 10, 0],
        normals: new float[18],
        indices: [0, 1, 2, 3, 4, 5],
        triangleFaceIds: [0, 0]);

    [Fact]
    public async Task PointerMove_OverFace_SetsHoveredFace_AndLeavingClearsIt()
    {
        var states = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                var (window, view) = Show();
                window.MouseMove(new Point(100, 50));
                var over = view.HoveredFace;
                window.MouseMove(new Point(250, 50)); // onto the neighbouring panel
                var left = view.HoveredFace;
                window.Close();
                return (over, left);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(new FaceId(0), states.over);
        Assert.Null(states.left);
    }

    [Fact]
    public async Task PointerMove_OverBackground_ClearsHoveredFace()
    {
        var hovered = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                var (window, view) = Show();
                window.MouseMove(new Point(100, 50));
                view.ShowView(UpSTEPer.Core.Drawing.ViewDirection.Front); // edge-on: the square disappears
                window.MouseMove(new Point(100, 20));
                var result = view.HoveredFace;
                window.Close();
                return result;
            },
            TestContext.Current.CancellationToken);

        Assert.Null(hovered);
    }

    [Fact]
    public async Task Press_ClearsHover_AndPlainMoveAfterReleaseHoversAgain()
    {
        var states = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                var (window, view) = Show();
                window.MouseMove(new Point(100, 50));
                window.MouseDown(new Point(100, 50), MouseButton.Left);
                var whilePressed = view.HoveredFace;
                window.MouseUp(new Point(100, 50), MouseButton.Left);
                window.MouseMove(new Point(105, 50));
                var afterRelease = view.HoveredFace;
                window.Close();
                return (whilePressed, afterRelease);
            },
            TestContext.Current.CancellationToken);

        Assert.Null(states.whilePressed);
        Assert.Equal(new FaceId(0), states.afterRelease);
    }

    [Fact]
    public async Task CaptureLost_DuringDrag_EndsRotation()
    {
        var yawUnchanged = await HeadlessTestApp.Session.Dispatch(
            () =>
            {
                var (window, view) = Show();
                window.MouseDown(new Point(100, 50), MouseButton.Left);

                // E.g. Alt+Tab while dragging: the view loses the pointer without a release.
                view.RaiseEvent(new PointerCaptureLostEventArgs(view, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true)));
                var yaw = view.Camera.Yaw;
                window.MouseMove(new Point(150, 50));
                var result = view.Camera.Yaw == yaw;
                window.Close();
                return result;
            },
            TestContext.Current.CancellationToken);

        Assert.True(yawUnchanged);
    }

    private static (Window Window, ModelView View) Show()
    {
        var view = new ModelView { Scene = SceneData.Create(_square, _ => null) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("200,100") };
        grid.Children.Add(view);
        var neighbour = new Border { Background = Brushes.White };
        Grid.SetColumn(neighbour, 1);
        grid.Children.Add(neighbour);
        var window = new Window { Width = 300, Height = 100, Content = grid };
        window.Show();
        return (window, view);
    }
}
