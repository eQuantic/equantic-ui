using eQuantic.UI.Primitives;

namespace eQuantic.UI.Sheet;

/// <summary>
/// What a KEY means to a sheet: Excel's keyboard, in the one place both targets agree. A host hands
/// the key to the controller (<see cref="SheetController.HandleKey"/>), the surface's model, and this
/// answers for it, so ⌘→ cannot drift between a window and a browser.
/// <para>
/// One keyboard serves both traditions. Excel's chords are the same on a Mac and elsewhere once
/// <see cref="KeyModifiers.Command"/> stands for ⌘ or Ctrl, which is what it already says, so no
/// keyboard convention is read here. Typed CHARACTERS do not come through here: what a keystroke
/// produces is the platform's business, and it arrives as text (<see cref="SheetController.HandleText"/>).
/// Focus is the host's, so Escape outside an edit is left to it.
/// </para>
/// </summary>
public static class SheetKeymap
{
    /// <summary>
    /// Runs the command <paramref name="key"/> means, and answers whether the sheet CLAIMED it. The
    /// copy, cut and paste chords are claimed only with a <paramref name="clipboard"/> to serve them,
    /// whether or not a cell is being edited (the draft has no selection of its own, so a copy takes
    /// the selected cells): a host whose clipboard arrives as events of its own, a browser's copy,
    /// cut and paste, hands none, and the chords are left for those events.
    /// </summary>
    public static bool Handle(SheetController sheet, string key, KeyModifiers modifiers,
        ITextClipboard? clipboard = null)
    {
        var command = (modifiers & KeyModifiers.Command) != 0;
        var shift = (modifiers & KeyModifiers.Shift) != 0;

        if (command && clipboard is not null && key.Length == 1)
        {
            switch (char.ToLowerInvariant(key[0]))
            {
                case 'c':
                    clipboard.Write(sheet.CopyText());
                    return true;
                case 'x':
                    clipboard.Write(sheet.Cut());
                    return true;
                case 'v':
                    if (clipboard.Read() is { Length: > 0 } pasted) sheet.Paste(pasted);
                    return true;
            }
        }

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
                case "d": case "D": sheet.FillDown(); return true;
                case "r": case "R": sheet.FillRight(); return true;
            }
        }
        return false;
    }

    /// <summary>Typed text, Excel's way: the first character replaces (a fresh draft), the rest
    /// append.</summary>
    public static void Type(SheetController sheet, string text)
    {
        if (sheet.Editing) sheet.TypeIntoDraft(text);
        else sheet.BeginEdit(text);
    }
}
