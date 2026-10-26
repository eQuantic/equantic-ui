using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Sheet;

/// <summary>
/// What a KEY means to a sheet: Excel's keyboard, in the one place both targets agree. A host hands
/// the key to the controller (<see cref="SheetController.HandleKey"/>), the surface's model, and this
/// answers for it, so ⌘→ cannot drift between a window and a browser.
/// <para>
/// At rest, Excel's grid chords are one keyboard in both traditions once <see cref="KeyModifiers.Command"/>
/// stands for ⌘ or Ctrl. The convention matters in two places: inside an entry, where a word and a line's
/// ends are reached by each platform's own chords (the code engine's keymap, which the entry's keys fall
/// through to), and in Excel's date and time chords, which a Mac moves to ⌃; and ⌘;. Typed CHARACTERS do
/// not come through here: what a keystroke produces is the platform's business, and it arrives as text
/// (<see cref="SheetController.HandleText"/>).
/// </para>
/// </summary>
public static class SheetKeymap
{
    /// <summary>
    /// Runs the command <paramref name="key"/> means, and answers whether the sheet CLAIMED it. The
    /// copy, cut and paste chords are claimed only with a <paramref name="clipboard"/> to serve them: a
    /// host whose clipboard arrives as events of its own, a browser's copy, cut and paste, hands none, and
    /// the chords are left for those events. At rest they carry cells; while editing, the entry's text.
    /// </summary>
    public static bool Handle(SheetController sheet, string key, KeyModifiers modifiers,
        KeyboardConvention convention, ITextClipboard? clipboard = null)
    {
        // ---- the Tab trap, the code editor's: Escape at rest releases it so the next Tab leaves the grid,
        // and any other key that is not a modifier takes it back before anything else is decided, so
        // Escape, → then Tab moves the active cell. A modifier alone is no other key, or Shift+Tab could
        // never leave backwards.
        if (IsModifierKey(key)) return false;
        if (key != "Tab" && key != "Escape") sheet.TabReleased = false;

        var command = (modifiers & KeyModifiers.Command) != 0;
        var shift = (modifiers & KeyModifiers.Shift) != 0;

        if (command && clipboard is not null && key.Length == 1 && (modifiers & KeyModifiers.Alt) == 0)
        {
            switch (char.ToLowerInvariant(key[0]))
            {
                case 'c':
                    // An entry with nothing selected copies nothing, as Excel's editor does.
                    if (sheet.CopyText() is { Length: > 0 } copied) clipboard.Write(copied);
                    return true;
                case 'x':
                    // The copy reaches the clipboard before anything is emptied: a clipboard that
                    // refuses the text leaves every cell, and the entry, as it was.
                    if (sheet.CopyText() is not { Length: > 0 } cut) return true;
                    clipboard.Write(cut);
                    sheet.Cut();
                    return true;
                case 'v':
                    if (clipboard.Read() is { Length: > 0 } pasted) sheet.Paste(pasted);
                    return true;
            }
        }

        if (IsDateOrTime(key, modifiers, convention, out var time))
        {
            sheet.TypeNow(time);
            return true;
        }

        return sheet.Editing
            ? HandleEntry(sheet, key, modifiers, convention)
            : HandleAtRest(sheet, key, modifiers, convention, command, shift);
    }

    /// <summary>
    /// A key while a cell is edited: Enter and Tab commit and step, Escape cancels, F2 switches the mode,
    /// an arrow in Enter mode commits and moves, Alt+Enter breaks the line and Command+Enter fills the
    /// selection. Everything else is the entry's: the code engine's keymap moves the caret, selects and
    /// deletes in the keyboard's own tradition, and undoes the entry's own edits, never the sheet's.
    /// </summary>
    private static bool HandleEntry(SheetController sheet, string key, KeyModifiers modifiers,
        KeyboardConvention convention)
    {
        var shift = (modifiers & KeyModifiers.Shift) != 0;
        var plain = (modifiers & ~KeyModifiers.Shift) == KeyModifiers.None;
        switch (key)
        {
            case "Enter" when modifiers == KeyModifiers.Alt:
                sheet.BreakLine();
                return true;
            case "Enter" when modifiers == KeyModifiers.Command:
                sheet.FillSelectionWithEntry();
                return true;
            case "Enter" when plain:
                sheet.CommitEdit();
                sheet.Step(shift ? -1 : 1, 0);
                return true;
            case "Tab" when plain:
                sheet.CommitEdit();
                sheet.Step(0, shift ? -1 : 1);
                return true;
            case "Escape":
                sheet.CancelEdit();
                return true;
            case "F2" when modifiers == KeyModifiers.None:
                sheet.ToggleEditMode();
                return true;
        }

        var (dRow, dCol) = Arrow(key);
        if (sheet.EditMode == SheetEditMode.Enter && plain && (dRow != 0 || dCol != 0))
        {
            sheet.CommitEdit();
            sheet.Move(dRow, dCol, SheetMotion.Cell, shift);
            return true;
        }

        var claimed = sheet.EntryKey(key, modifiers, convention);
        // Undo and redo while editing are the entry's, and with nothing left to undo they are still
        // claimed: they never reach the sheet's history while a cell is open.
        return claimed || IsUndoChord(key, modifiers);
    }

    private static bool HandleAtRest(SheetController sheet, string key, KeyModifiers modifiers,
        KeyboardConvention convention, bool command, bool shift)
    {
        // Excel's Go To: F5 and Ctrl+G (⌃G on a Mac) take the keyboard to the name box, when the app drew
        // one; with none, the keys stay the host's.
        if (IsGoTo(key, modifiers, convention))
        {
            if (!sheet.NameBox.IsPlaced) return false;
            sheet.NameBox.RequestFocus();
            return true;
        }

        // The chords whose modifier a Mac moves from ⌘ to ⌃ (Excel for Mac's own): the columns of the
        // selection, showing the active cell, and the selection's next corner.
        var control = convention == KeyboardConvention.Apple ? KeyModifiers.Control : KeyModifiers.Command;
        if (modifiers == control)
        {
            switch (key)
            {
                case " " or "Space": sheet.SelectActiveColumns(); return true;
                case "Backspace": sheet.ShowActiveCell(); return true;
                case ".": sheet.NextCorner(); return true;
            }
        }

        var motion = command ? SheetMotion.DataEdge : SheetMotion.Cell;
        switch (key)
        {
            case " " or "Space" when modifiers == KeyModifiers.Shift:
                sheet.SelectActiveRows();
                return true;
            case "PageUp" when (modifiers & KeyModifiers.Alt) != 0 && !command:
                return sheet.MovePageAcross(-1, shift);
            case "PageDown" when (modifiers & KeyModifiers.Alt) != 0 && !command:
                return sheet.MovePageAcross(1, shift);
            case "F2": sheet.BeginEdit(sheet.Document.GetCell(sheet.ActiveCell), SheetEditMode.Edit); return true;
            case "ArrowUp": sheet.Move(-1, 0, motion, shift); return true;
            case "ArrowDown": sheet.Move(1, 0, motion, shift); return true;
            case "ArrowLeft": sheet.Move(0, -1, motion, shift); return true;
            case "ArrowRight": sheet.Move(0, 1, motion, shift); return true;
            case "Tab":
                // Released by Escape: the Tab is the host's, which moves the keyboard on.
                if (sheet.TabReleased) return false;
                sheet.Step(0, shift ? -1 : 1);
                return true;
            case "Enter": sheet.Step(shift ? -1 : 1, 0); return true;
            case "Home": sheet.Move(0, -1, command ? SheetMotion.SheetBoundary : SheetMotion.RowBoundary, shift); return true;
            case "End": sheet.Move(0, 1, command ? SheetMotion.SheetBoundary : SheetMotion.RowBoundary, shift); return true;
            case "PageUp" when !command: return sheet.MovePage(-1, shift);
            case "PageDown" when !command: return sheet.MovePage(1, shift);
            // Excel's Backspace clears the active cell and begins editing it: the value comes back with
            // Escape and goes with a commit. Delete clears the whole selection.
            case "Backspace" when modifiers == KeyModifiers.None:
                sheet.BeginEdit("", SheetEditMode.Enter);
                return true;
            case "Delete": sheet.ClearSelection(); return true;
            case "Escape":
                // Add mode ends first, as Excel's does; with nothing to cancel, Escape releases Tab and is left
                // to whatever is around the sheet.
                if (sheet.AddingRanges)
                {
                    sheet.AddingRanges = false;
                    return true;
                }
                sheet.TabReleased = true;
                return false;
            case "F8" when modifiers == KeyModifiers.Shift:
                sheet.AddingRanges = !sheet.AddingRanges;
                return true;
        }

        if (command && key.Length == 1)
        {
            switch (key)
            {
                case "a": case "A": sheet.SelectRegionThenSheet(); return true;
                case "z": case "Z": if (shift) sheet.Redo(); else sheet.Undo(); return true;
                case "y": case "Y": sheet.Redo(); return true;
                case "d": case "D": sheet.FillDown(); return true;
                case "r": case "R": sheet.FillRight(); return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Excel's date and time chords: Ctrl+; for today's date and Ctrl+Shift+; for the time; on a Mac ⌃;
    /// and ⌘;, Excel for Mac's own. The key is <c>;</c>, or <c>:</c> on a layout where Shift turns one into
    /// the other.
    /// </summary>
    private static bool IsDateOrTime(string key, KeyModifiers modifiers, KeyboardConvention convention, out bool time)
    {
        time = false;
        if (key is not (";" or ":")) return false;
        if (convention == KeyboardConvention.Apple)
        {
            if (modifiers == KeyModifiers.Control) return true;
            time = true;
            return modifiers == KeyModifiers.Command;
        }
        if (modifiers == KeyModifiers.Command) return true;
        time = true;
        return modifiers == (KeyModifiers.Command | KeyModifiers.Shift);
    }

    private static bool IsGoTo(string key, KeyModifiers modifiers, KeyboardConvention convention) =>
        (key == "F5" && modifiers == KeyModifiers.None)
        || (key is "g" or "G"
            && modifiers == (convention == KeyboardConvention.Apple ? KeyModifiers.Control : KeyModifiers.Command));

    private static bool IsUndoChord(string key, KeyModifiers modifiers) =>
        (modifiers & KeyModifiers.Command) != 0 && key is "z" or "Z" or "y" or "Y";

    /// <summary>The step an arrow key names, or none.</summary>
    private static (int Row, int Col) Arrow(string key) => key switch
    {
        "ArrowUp" => (-1, 0),
        "ArrowDown" => (1, 0),
        "ArrowLeft" => (0, -1),
        "ArrowRight" => (0, 1),
        _ => (0, 0),
    };

    /// <summary>Whether a key name is a modifier pressed alone: never a command, and never the end of Tab's
    /// release.</summary>
    internal static bool IsModifierKey(string key) =>
        key is "Shift" or "Control" or "Alt" or "AltGraph" or "Meta" or "OS" or "CapsLock" or "Fn" or "Hyper" or "Super";
}
