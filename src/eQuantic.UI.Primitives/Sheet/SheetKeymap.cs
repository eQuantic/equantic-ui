namespace eQuantic.UI.Primitives;

/// <summary>
/// ONE keymap for both targets (the code editor's pattern): everything a key MEANS on a sheet is
/// here, on the controller — the native host and the web surface both call it, so ⌘→ can never
/// drift between them. Clipboard stays with the platforms (they own it); Escape-leaves-the-sheet
/// stays with the hosts (focus is theirs).
/// </summary>
public static class SheetKeymap
{
    /// <summary>Handles one key. Answers false for keys the sheet does not claim.</summary>
    public static bool Handle(SheetController sheet, string key, bool command, bool shift)
    {
        var motion = command ? SheetMotion.DataEdge : SheetMotion.Cell;

        if (sheet.Editing)
        {
            switch (key)
            {
                case "Enter": sheet.CommitEdit(); sheet.Step(shift ? -1 : 1, 0); return true;
                case "Tab": sheet.CommitEdit(); sheet.Step(0, shift ? -1 : 1); return true;
                case "Escape": sheet.CancelEdit(); return true;
                case "Backspace": sheet.EraseFromDraft(); return true;
                case "ArrowUp": sheet.CommitEdit(); sheet.Move(-1, 0); return true;
                case "ArrowDown": sheet.CommitEdit(); sheet.Move(1, 0); return true;
                case "ArrowLeft": sheet.CommitEdit(); sheet.Move(0, -1); return true;
                case "ArrowRight": sheet.CommitEdit(); sheet.Move(0, 1); return true;
            }
            return false;
        }

        switch (key)
        {
            case "F2": sheet.BeginEdit(sheet.Document.GetCell(sheet.ActiveCell)); return true;
            case "ArrowUp": sheet.Move(-1, 0, motion, shift); return true;
            case "ArrowDown": sheet.Move(1, 0, motion, shift); return true;
            case "ArrowLeft": sheet.Move(0, -1, motion, shift); return true;
            case "ArrowRight": sheet.Move(0, 1, motion, shift); return true;
            case "Tab": sheet.Step(0, shift ? -1 : 1); return true;
            case "Enter": sheet.Step(shift ? -1 : 1, 0); return true;
            case "Home": sheet.Move(0, -1, command ? SheetMotion.SheetBoundary : SheetMotion.RowBoundary, shift); return true;
            case "End": sheet.Move(0, 1, command ? SheetMotion.SheetBoundary : SheetMotion.RowBoundary, shift); return true;
            case "Backspace":
            case "Delete": sheet.ClearSelection(); return true;
        }

        if (command && key.Length == 1)
        {
            switch (key)
            {
                case "a": case "A": sheet.SelectAll(); return true;
                case "z": if (shift) sheet.Redo(); else sheet.Undo(); return true;
            }
        }
        return false;
    }

    /// <summary>Typed text, Excel's way: the first character replaces (a fresh draft), the rest
    /// append. Both targets' text pipelines call this.</summary>
    public static void Type(SheetController sheet, string text)
    {
        if (sheet.Editing) sheet.TypeIntoDraft(text);
        else sheet.BeginEdit(text);
    }
}
