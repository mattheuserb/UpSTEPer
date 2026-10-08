using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>A colour change: target plus the assigned colour before and after.</summary>
/// <param name="Target">The face or body.</param>
/// <param name="Before">Assigned colour before the change (<see langword="null"/> = none).</param>
/// <param name="After">Assigned colour after the change (<see langword="null"/> = none).</param>
internal sealed record ColorEdit(ColorTarget Target, ColorRgb? Before, ColorRgb? After);

/// <summary>Undo/redo stacks for colour edits.</summary>
/// <remarks>
/// The history only records; the caller applies <see cref="ColorEdit.Before"/> after
/// <see cref="Undo"/> and <see cref="ColorEdit.After"/> after <see cref="Redo"/>.
/// </remarks>
internal sealed class EditHistory
{
    private readonly Stack<ColorEdit> _undo = new();
    private readonly Stack<ColorEdit> _redo = new();

    /// <summary>Gets a value indicating whether there is an edit to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets a value indicating whether there is an undone edit to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Records a new edit; clears the redo stack.</summary>
    /// <param name="edit">The edit that was applied.</param>
    public void Record(ColorEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        _undo.Push(edit);
        _redo.Clear();
    }

    /// <summary>Moves the latest edit to the redo stack.</summary>
    /// <returns>The edit to revert (apply its <see cref="ColorEdit.Before"/>).</returns>
    /// <exception cref="InvalidOperationException">Nothing to undo.</exception>
    public ColorEdit Undo()
    {
        var edit = _undo.Count > 0 ? _undo.Pop() : throw new InvalidOperationException("Nothing to undo.");
        _redo.Push(edit);
        return edit;
    }

    /// <summary>Moves the latest undone edit back to the undo stack.</summary>
    /// <returns>The edit to re-apply (apply its <see cref="ColorEdit.After"/>).</returns>
    /// <exception cref="InvalidOperationException">Nothing to redo.</exception>
    public ColorEdit Redo()
    {
        var edit = _redo.Count > 0 ? _redo.Pop() : throw new InvalidOperationException("Nothing to redo.");
        _undo.Push(edit);
        return edit;
    }

    /// <summary>Forgets all edits (e.g. after loading another file).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
