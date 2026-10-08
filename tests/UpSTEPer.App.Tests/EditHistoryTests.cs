using UpSTEPer.App.ViewModels;
using UpSTEPer.Core;

namespace UpSTEPer.App.Tests;

public class EditHistoryTests
{
    private static readonly ColorEdit _first = new(new ColorTarget(ColorTargetKind.Face, 1), null, ColorRgb.FromHex("#FF0000"));
    private static readonly ColorEdit _second = new(new ColorTarget(ColorTargetKind.Body, 0), null, ColorRgb.FromHex("#00FF00"));

    [Fact]
    public void Empty_CannotUndoOrRedo()
    {
        var history = new EditHistory();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Throws<InvalidOperationException>(history.Undo);
        Assert.Throws<InvalidOperationException>(history.Redo);
    }

    [Fact]
    public void UndoRedo_AreLastInFirstOut()
    {
        var history = new EditHistory();
        history.Record(_first);
        history.Record(_second);

        Assert.Same(_second, history.Undo());
        Assert.Same(_first, history.Undo());
        Assert.False(history.CanUndo);
        Assert.Same(_first, history.Redo());
        Assert.Same(_second, history.Redo());
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Record_ClearsRedo()
    {
        var history = new EditHistory();
        history.Record(_first);
        history.Undo();

        history.Record(_second);

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Clear_ForgetsEverything()
    {
        var history = new EditHistory();
        history.Record(_first);
        history.Record(_second);
        history.Undo();

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }
}
