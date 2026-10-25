using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Sheet;

/// <summary>
/// The formula bar's model: the second view of the sheet's ONE entry, which the formula bar component
/// draws as a code surface. It holds no text of its own. At rest the bar shows the active cell's value; a
/// press in it, or text typed into it, begins editing the active cell in Edit mode from there, and while a
/// cell is edited it shows the entry and, when it holds the caret, the entry's caret and selection. Enter,
/// Tab and Escape do what they do in the cell, and then the grid takes the keyboard back, as Excel's does.
/// <para>
/// The bar's geometry is its own, handed over by the component that draws it on every build
/// (<see cref="Grid"/>); the sheet puts the entry on it whenever the bar holds the caret, so a caret, a band
/// and a press here are read on the bar's face and the cell's editor keeps its own.
/// </para>
/// </summary>
public sealed class SheetFormulaBar : ICodeSurfaceModel
{
    private readonly SheetController _sheet;
    private bool _pressed;
    private int _focusVersion;

    internal SheetFormulaBar(SheetController sheet) => _sheet = sheet;

    /// <summary>Where the bar draws the entry, in its own surface's coordinates, measured in its own type.</summary>
    public CodeGrid Grid { get; set; } = CodeGrid.Default;

    /// <summary>Whether the bar holds the entry's caret: a cell is edited, from the bar.</summary>
    public bool HoldsTheCaret => _sheet.Editing && _sheet.EntryView == SheetEntryView.FormulaBar;

    /// <inheritdoc />
    public IReadOnlyList<Rect> Carets
    {
        get
        {
            if (!HoldsTheCaret) return [];
            _sheet.UseView();
            return _sheet.Entry.Carets;
        }
    }

    /// <inheritdoc />
    public int RevealVersion => _sheet.Entry.RevealVersion;

    /// <inheritdoc />
    public int FocusVersion => _focusVersion;

    /// <summary>Asks for the keyboard: the bar's surface takes it on its next frame.</summary>
    public void RequestFocus() => _focusVersion++;

    /// <summary>Makes the bar the view that holds the entry's caret, beginning an entry in Edit mode from the
    /// active cell's value when none is open, as a press in Excel's formula bar does.</summary>
    private void Take()
    {
        if (!_sheet.Editing) _sheet.BeginEdit(_sheet.Document.GetCell(_sheet.ActiveCell), SheetEditMode.Edit);
        // The bar is always in Edit mode: its arrows move the caret, as Excel's do.
        _sheet.EnterEditMode();
        _sheet.HoldEntryIn(SheetEntryView.FormulaBar);
    }

    /// <inheritdoc />
    public bool HandleKey(string key, KeyModifiers modifiers, KeyboardConvention convention, ITextClipboard? clipboard)
    {
        // At rest the bar's Tab and Escape are the host's: nothing is being edited from here.
        if (!_sheet.Editing && key is "Tab" or "Escape") return false;
        if (key is "Shift" or "Control" or "Alt" or "AltGraph" or "Meta" or "OS" or "CapsLock") return false;
        Take();
        var claimed = SheetKeymap.Handle(_sheet, key, modifiers, convention, clipboard);
        // The edit ended here (Enter, Tab, Escape, a fill of the selection): the grid takes the keyboard.
        if (!_sheet.Editing) _sheet.RequestFocus();
        return claimed;
    }

    /// <inheritdoc />
    public bool HandleText(string text)
    {
        if (text.Length == 0) return false;
        Take();
        return _sheet.Entry.HandleText(text);
    }

    /// <inheritdoc />
    public bool SetComposition(string text)
    {
        if (!_sheet.Editing && text.Length == 0) return false;
        Take();
        return _sheet.Entry.SetComposition(text);
    }

    /// <inheritdoc />
    public string CopyText() => _sheet.Editing ? _sheet.CopyText() : "";

    /// <inheritdoc />
    public string Cut() => _sheet.Editing ? _sheet.Cut() : "";

    /// <inheritdoc />
    public bool Paste(string text)
    {
        if (text.Length == 0) return false;
        Take();
        return _sheet.Paste(text);
    }

    /// <inheritdoc />
    public bool HandlePointer(PointerPhase phase, Point position, KeyModifiers modifiers, int clicks)
    {
        switch (phase)
        {
            case PointerPhase.Down:
                Take();
                _pressed = true;
                _sheet.Entry.HandlePointer(phase, position, modifiers, clicks);
                return true;
            case PointerPhase.Move or PointerPhase.Up or PointerPhase.Cancel when _pressed:
                if (phase != PointerPhase.Move) _pressed = false;
                if (!HoldsTheCaret) return false;
                _sheet.UseView();
                return _sheet.Entry.HandlePointer(phase, position, modifiers, clicks);
            default:
                return false;
        }
    }

    /// <inheritdoc />
    public void FocusChanged(bool focused)
    {
        // Leaving the bar cancels a composition and keeps the entry open, as leaving the grid does.
        if (!focused && HoldsTheCaret) _sheet.Entry.FocusChanged(false);
    }
}
