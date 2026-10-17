namespace eQuantic.UI.Sheet;

/// <summary>
/// Undo/redo for the sheet — the code editor's shape: two stacks, redo cleared by any new edit.
/// Edits are atomic (a cell commit, a structural change), so there is no typing coalescing here;
/// the in-cell editor owns its own keystrokes and commits ONE edit.
/// </summary>
public sealed class SheetHistory
{
    private readonly List<SheetEdit> _undo = new();
    private readonly List<SheetEdit> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Push(SheetEdit edit)
    {
        _undo.Add(edit);
        _redo.Clear();
    }

    public SheetEdit? PopUndo()
    {
        if (_undo.Count == 0) return null;
        var edit = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(edit);
        return edit;
    }

    public SheetEdit? PopRedo()
    {
        if (_redo.Count == 0) return null;
        var edit = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(edit);
        return edit;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
