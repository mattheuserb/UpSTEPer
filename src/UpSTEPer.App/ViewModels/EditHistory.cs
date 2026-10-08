using UpSTEPer.Core;

namespace UpSTEPer.App.ViewModels;

/// <summary>A colour change: target plus the assigned colour before and after.</summary>
/// <param name="Target">The face or body.</param>
/// <param name="Before">Assigned colour before the change (<see langword="null"/> = none).</param>
/// <param name="After">Assigned colour after the change (<see langword="null"/> = none).</param>
internal sealed record ColorEdit(ColorTarget Target, ColorRgb? Before, ColorRgb? After);

/// <summary>Undo/redo stacks of edit steps; one step may change several targets (e.g. all bodies of a part).</summary>
/// <remarks>
/// The history only records; the caller applies every <see cref="ColorEdit.Before"/> (in reverse
/// order) after <see cref="Undo"/> and every <see cref="ColorEdit.After"/> after <see cref="Redo"/>.
/// </remarks>
internal sealed class EditHistory
{
    private readonly Stack<IReadOnlyList<ColorEdit>> _undo = new();
    private readonly Stack<IReadOnlyList<ColorEdit>> _redo = new();

    /// <summary>Gets a value indicating whether there is a step to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Gets a value indicating whether there is an undone step to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Records a new step; clears the redo stack. Empty steps are ignored.</summary>
    /// <param name="step">The edits that were applied together.</param>
    public void Record(IReadOnlyList<ColorEdit> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (step.Count == 0)
        {
            return;
        }

        _undo.Push(step);
        _redo.Clear();
    }

    /// <summary>Moves the latest step to the redo stack.</summary>
    /// <returns>The step to revert.</returns>
    /// <exception cref="InvalidOperationException">Nothing to undo.</exception>
    public IReadOnlyList<ColorEdit> Undo()
    {
        var step = _undo.Count > 0 ? _undo.Pop() : throw new InvalidOperationException("Nothing to undo.");
        _redo.Push(step);
        return step;
    }

    /// <summary>Moves the latest undone step back to the undo stack.</summary>
    /// <returns>The step to re-apply.</returns>
    /// <exception cref="InvalidOperationException">Nothing to redo.</exception>
    public IReadOnlyList<ColorEdit> Redo()
    {
        var step = _redo.Count > 0 ? _redo.Pop() : throw new InvalidOperationException("Nothing to redo.");
        _undo.Push(step);
        return step;
    }

    /// <summary>Forgets all steps (e.g. after loading another file).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
