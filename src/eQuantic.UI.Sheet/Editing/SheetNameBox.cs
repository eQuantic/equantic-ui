using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Sheet;

/// <summary>
/// The name box's model: where the selection is, and a way to go somewhere else. At rest the component
/// that draws it shows the active cell's address, or the size of the range a drag is drawing
/// (<see cref="SheetController.Selecting"/>). Given the keyboard, it holds a one-line entry of its own
/// with the address selected, so typing replaces it; Enter goes to what was typed
/// (<see cref="SheetController.GoTo"/>) and hands the keyboard back to the grid, or, when the text names
/// nothing in the grid, keeps it and says so (<see cref="Invalid"/>); Escape puts the address back and
/// hands the keyboard back.
/// </summary>
public sealed class SheetNameBox : ICodeSurfaceModel
{
    private readonly SheetController _sheet;
    private readonly CodeEditorController _box = new("", new SheetEntryLanguage()) { SingleLine = true };
    private int _focusVersion;
    private int _placed;

    /// <summary>The press that gave the box the keyboard: it leaves the address selected, as a click on
    /// Excel's name box does, rather than placing a caret in it.</summary>
    private bool _focusingPress;
    private bool _justFocused;

    internal SheetNameBox(SheetController sheet) => _sheet = sheet;

    /// <summary>Where the box draws its text, in its own surface's coordinates, measured in its own type.</summary>
    public CodeGrid Grid
    {
        get => _box.Grid;
        set => _box.Grid = value;
    }

    /// <summary>The box's own entry: what is being typed into it while it holds the keyboard.</summary>
    public CodeEditorController Entry => _box;

    /// <summary>Whether the box holds the keyboard, and so shows what is typed rather than the address.</summary>
    public bool Typing { get; private set; }

    /// <summary>Whether the last text Enter went to named nothing in the grid: the box keeps it, and says so.</summary>
    public bool Invalid { get; private set; }

    /// <summary>Whether a name box is drawn for this sheet: the keys that go to it are left unclaimed
    /// otherwise. The component counts itself in as it mounts and out as it unmounts.</summary>
    public bool IsPlaced => _placed > 0;

    /// <summary>A name box for this sheet was mounted.</summary>
    public void Placed() => _placed++;

    /// <summary>A name box for this sheet was unmounted.</summary>
    public void Removed() => _placed = Math.Max(0, _placed - 1);

    /// <inheritdoc />
    public IReadOnlyList<Rect> Carets => Typing ? _box.Carets : [];

    /// <inheritdoc />
    public int RevealVersion => _box.RevealVersion;

    /// <inheritdoc />
    public int FocusVersion => _focusVersion;

    /// <summary>Asks for the keyboard: F5 and the Go To chord, through the sheet's keymap.</summary>
    public void RequestFocus() => _focusVersion++;

    /// <inheritdoc />
    public bool HandleKey(string key, KeyModifiers modifiers, KeyboardConvention convention, ITextClipboard? clipboard)
    {
        if (!Typing) return false;
        _justFocused = false;
        switch (key)
        {
            // Enter with any modifier goes there too: the box is one line, and a break in it names nothing.
            case "Enter":
                if (_sheet.GoTo(_box.Document.Text))
                {
                    Invalid = false;
                    Typing = false;
                }
                else Invalid = true;
                return true;
            case "Escape":
                Invalid = false;
                Typing = false;
                _sheet.RequestFocus();
                return true;
            case "Tab":
                return false;
        }
        var claimed = _box.HandleKey(key, modifiers, convention, clipboard);
        if (claimed) Invalid = false;
        return claimed;
    }

    /// <inheritdoc />
    public bool HandleText(string text)
    {
        if (!Typing || text.Length == 0) return false;
        _justFocused = false;
        Invalid = false;
        return _box.HandleText(text);
    }

    /// <inheritdoc />
    public bool SetComposition(string text) => Typing && _box.SetComposition(text);

    /// <inheritdoc />
    public string CopyText() => Typing ? _box.Document.TextIn(_box.Selection) : "";

    /// <inheritdoc />
    public string Cut() => Typing && !_box.Selection.IsEmpty ? _box.Cut() : "";

    /// <inheritdoc />
    public bool Paste(string text) => Typing && _box.Paste(text);

    /// <inheritdoc />
    public bool HandlePointer(PointerPhase phase, Point position, KeyModifiers modifiers, int clicks)
    {
        if (!Typing) return false;
        if (phase == PointerPhase.Down && _justFocused)
        {
            _justFocused = false;
            _focusingPress = true;
            return true;
        }
        if (_focusingPress)
        {
            if (phase is PointerPhase.Up or PointerPhase.Cancel) _focusingPress = false;
            return phase != PointerPhase.Hover;
        }
        return _box.HandlePointer(phase, position, modifiers, clicks);
    }

    /// <inheritdoc />
    public void FocusChanged(bool focused)
    {
        _box.FocusChanged(focused);
        if (focused == Typing) return;
        Typing = focused;
        // Leaving puts the address back, and a refused name with it.
        Invalid = false;
        if (!focused) return;
        // Taking the keyboard selects the address, so what is typed replaces it.
        _box.Document = CodeDocument.FromText(_sheet.ActiveCell.ToA1());
        _box.Selection = new CodeRange(CodePosition.Start, _box.Document.End);
        Invalid = false;
        _justFocused = true;
    }

    /// <inheritdoc />
    /// <remarks>The text cursor: the box follows no link (#1084).</remarks>
    public PointerCursor CursorAt(Point position) => PointerCursor.Text;
}
