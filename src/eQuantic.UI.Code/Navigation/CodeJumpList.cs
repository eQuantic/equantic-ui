namespace eQuantic.UI.Code;

/// <summary>
/// The places the caret has been in ONE editor, and the walk back and forward through them (#1083):
/// VS Code's navigation history for its plain "Go Back", whose selection stack hears every move.
/// <para>
/// A move whose top line is <see cref="Distance"/> lines or more from the place it left is a new place, and
/// a nearer one replaces the place it left, so typing along a function and stepping through it leaves one
/// place rather than one per keystroke. A JUMP (a line or a symbol gone to, a definition) is always a new
/// place, unless it lands on the line it left. Walking moves the caret and records nothing; a new place
/// after walking back drops the places ahead, as a browser's history does; <see cref="Capacity"/> places are
/// kept, the oldest going first.
/// </para>
/// <para>
/// VS Code keeps each place's line numbers, so a line inserted above one sends the walk to the wrong line.
/// The places here are carried through every edit, as the problems are (<see cref="CodeEdit.Carry"/>), which
/// is what Rider's range markers do.
/// </para>
/// </summary>
public sealed class CodeJumpList
{
    /// <summary>How many lines a move has to go to be a place of its own (VS Code's
    /// <c>TEXT_EDITOR_SELECTION_THRESHOLD</c>).</summary>
    public const int Distance = 10;

    /// <summary>How many places are kept (VS Code's <c>MAX_STACK_SIZE</c>).</summary>
    public const int Capacity = 50;

    private readonly CodeEditorController _editor;
    private readonly List<CodeRange> _places = [];
    private int _index = -1;
    // The selection last heard, which a move is measured from: the place walked to, after a walk.
    private CodeRange _heard;
    private bool _walking;
    private bool _jumping;

    internal CodeJumpList(CodeEditorController editor)
    {
        _editor = editor;
        _heard = editor.Selection;
        _places.Add(_heard);
        _index = 0;
        editor.Changed += Carry;
        editor.SelectionChanged += Heard;
    }

    /// <summary>Every place, the oldest first.</summary>
    public IReadOnlyList<CodeRange> Places => _places;

    /// <summary>Where in <see cref="Places"/> the caret is: the place a walk back leaves.</summary>
    public int Index => _index;

    /// <summary>Whether there is a place to go back to.</summary>
    public bool CanGoBack => _index > 0;

    /// <summary>Whether there is a place ahead, after a walk back.</summary>
    public bool CanGoForward => _index < _places.Count - 1;

    /// <summary>Selects the place before the current one, centred when it is out of view. Answers whether
    /// there was one.</summary>
    public bool Back() => Walk(_index - 1);

    /// <summary>Selects the place after the current one, centred when it is out of view. Answers whether
    /// there was one.</summary>
    public bool Forward() => Walk(_index + 1);

    /// <summary>Runs <paramref name="move"/>, a jump: the move it makes is a place of its own however near it
    /// lands, the place it left kept behind it.</summary>
    internal void Jump(Action move)
    {
        _jumping = true;
        try
        {
            move();
        }
        finally
        {
            _jumping = false;
        }
    }

    /// <summary>A new document: its first place is where the caret stands in it.</summary>
    internal void Reset()
    {
        _places.Clear();
        _heard = _editor.Selection;
        _places.Add(_heard);
        _index = 0;
    }

    private bool Walk(int to)
    {
        if (to < 0 || to >= _places.Count) return false;
        _index = to;
        var place = _places[to];
        _walking = true;
        try
        {
            _editor.Selection = place;
        }
        finally
        {
            _walking = false;
        }
        _heard = _editor.Selection;
        _editor.RevealInCenterIfOutsideView(place.Focus);
        return true;
    }

    private void Heard(CodeRange selection)
    {
        if (_walking) return;
        var lines = Math.Abs(selection.Start.Line - _heard.Start.Line);
        // A jump is a place of its own even nearby; a move that stays on the line, jump or not, replaces it.
        var newPlace = lines >= Distance || (_jumping && lines > 0);
        _heard = selection;
        if (newPlace && _places[_index].Start.Line != selection.Start.Line) Add(selection);
        else _places[_index] = selection;
    }

    private void Add(CodeRange place)
    {
        if (_index < _places.Count - 1) _places.RemoveRange(_index + 1, _places.Count - _index - 1);
        _places.Add(place);
        if (_places.Count > Capacity) _places.RemoveAt(0);
        _index = _places.Count - 1;
    }

    /// <summary>Every place, and the selection a move is measured from, carried through what changed.</summary>
    private void Carry(CodeChange change)
    {
        if (change.Replaced) return;
        foreach (var edit in change.Edits)
        {
            for (var i = 0; i < _places.Count; i++) _places[i] = Carried(edit, _places[i]);
            _heard = Carried(edit, _heard);
        }
    }

    /// <summary><paramref name="place"/> carried through <paramref name="edit"/> in the direction it was selected
    /// in: a selection made upward goes back with its caret at its top, where the edit's carry orders a range's
    /// ends.</summary>
    private static CodeRange Carried(CodeEdit edit, CodeRange place)
    {
        var carried = edit.Carry(place);
        return place.Anchor.CompareTo(place.Focus) > 0 ? new CodeRange(carried.End, carried.Start) : carried;
    }
}
