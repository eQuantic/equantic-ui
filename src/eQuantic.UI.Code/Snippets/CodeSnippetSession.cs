namespace eQuantic.UI.Code;

/// <summary>
/// The snippet being filled in (#1013): its stops, where each is written, and which one the carets stand at,
/// as VS Code's snippet session holds them. <see cref="CodeEditorController.InsertSnippet"/> starts one at the
/// snippet's first stop; Tab (<see cref="Next"/>) and ⇧Tab (<see cref="Previous"/>) walk the stops, and every
/// place a stop is written is selected with it, the others as carets, so a mirror is typed with its stop.
/// Reaching the final stop ends it, and so do Escape (<see cref="Leave"/>) and a selection that stands
/// outside every stop.
/// <para>
/// The stop the carets stand at takes in what is typed at its start or end, and every other stop is carried
/// as a mark, which never grows at its edges, as VS Code switches a placeholder's stickiness when it becomes
/// active: typing at the end of <c>${1:a}</c> beside <c>$2</c> lengthens the first and leaves the second
/// after the typing. When the carets leave a stop whose text is not what it was when they reached it, the
/// stops nested in its placeholder are gone.
/// </para>
/// </summary>
public sealed class CodeSnippetSession
{
    private readonly CodeEditorController _editor;
    private List<CodeSnippetStop>? _stops;
    private int _current = -1;
    private string _reached = "";
    // While the session places the carets itself, the selection it hears is its own.
    private bool _placing;

    internal CodeSnippetSession(CodeEditorController editor)
    {
        _editor = editor;
        // Heard before the selection the edit leaves, so the stops already stand where the edit carried them
        // when the selection is asked whether it is still in one.
        editor.Changed += Carry;
        editor.SelectionChanged += _ => Followed();
    }

    /// <summary>Whether a snippet is being filled in.</summary>
    public bool IsActive => _stops is not null;

    /// <summary>Whether ⇧Tab has a stop to go back to: the carets stand past the snippet's first stop.</summary>
    public bool HasPrevious => _stops is not null && PreviousOf(_current) >= 0;

    /// <summary>Raised when a session starts, moves to another stop, or ends: what a view draws changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// Tab: the carets go to the next stop, every place it is written selected, the primary's first. Reaching
    /// the final stop ends the session. False when no snippet is being filled in.
    /// </summary>
    public bool Next()
    {
        if (_stops is null) return false;
        // The stops nested in a placeholder typed over go first, so the walk does not stop at one.
        Leaving();
        var next = _current + 1;
        while (next < _stops.Count - 1 && Skipped(_stops[next])) next++;
        Reach(next);
        return true;
    }

    /// <summary>⇧Tab: the carets go back to the stop before. False at the first stop, so ⇧Tab outdents there,
    /// and when no snippet is being filled in.</summary>
    public bool Previous()
    {
        if (_stops is null || PreviousOf(_current) < 0) return false;
        Leaving();
        // The stop before may have been nested in the placeholder just typed over; the carets stay then.
        var previous = PreviousOf(_current);
        if (previous >= 0) Reach(previous);
        return true;
    }

    /// <summary>Escape: the session ends, the carets where they are. False when no snippet is being filled
    /// in, so Escape goes on to what it means next.</summary>
    public bool Leave()
    {
        if (_stops is null) return false;
        End();
        return true;
    }

    /// <summary>
    /// Every placeholder of the snippet with text, on the lines from <paramref name="first"/> to
    /// <paramref name="last"/>: what a view washes while the session lasts. The final stop and an empty stop
    /// are not among them; the caret that stands in one shows it.
    /// </summary>
    public IReadOnlyList<CodeRange> PlaceholdersIn(int first, int last)
    {
        var placeholders = new List<CodeRange>();
        if (_stops is null) return placeholders;
        foreach (var stop in _stops)
        {
            if (stop.Final || stop.Dropped) continue;
            foreach (var range in stop.Ranges)
            {
                if (range.IsEmpty || range.End.Line < first || range.Start.Line > last) continue;
                placeholders.Add(range);
            }
        }
        return placeholders;
    }

    /// <summary>Starts a session over <paramref name="stops"/>, in the order Tab walks them, the final stop
    /// last; the carets already stand at the first.</summary>
    internal void Start(List<CodeSnippetStop> stops)
    {
        End();
        _stops = stops;
        _current = 0;
        _reached = TextOf(stops[0]);
        Changed?.Invoke();
        OfferChoices(stops[0]);
    }

    /// <summary>Writes <paramref name="text"/> over every place the stop the carets stand at is written, as one
    /// undo step: a choice taken from its list.</summary>
    internal bool Choose(string text)
    {
        if (_stops is null || _editor.ReadOnly) return false;
        _placing = true;
        try
        {
            // Taken even when it writes nothing: an empty option over an empty placeholder is a choice made, and
            // answered false, the Enter that took it went on to break the line (Copilot on #1079).
            _editor.ReplaceEvery(new List<CodeRange>(_stops[_current].Ranges), text);
            return true;
        }
        finally
        {
            _placing = false;
        }
    }

    /// <summary>Ends the session, if there is one, and drops a choice's list.</summary>
    internal void End()
    {
        if (_stops is null) return;
        _stops = null;
        _current = -1;
        _editor.Completion.DropChoices();
        Changed?.Invoke();
    }

    /// <summary>The stop before <paramref name="index"/> that has not been dropped, or -1.</summary>
    private int PreviousOf(int index)
    {
        if (_stops is null) return -1;
        for (var i = index - 1; i >= 0; i--)
            if (!Skipped(_stops[i])) return i;
        return -1;
    }

    private static bool Skipped(CodeSnippetStop stop) => stop.Dropped || stop.Ranges.Count == 0;

    /// <summary>The carets at the stop <paramref name="index"/> of the walk: every place it is written selected,
    /// and the session ended when it is the final stop.</summary>
    private void Reach(int index)
    {
        if (_stops is null) return;
        var stop = _stops[index];
        _current = index;
        _reached = TextOf(stop);
        // The list of the stop the carets leave is that stop's: left showing, a choice taken from it was written
        // over the stop reached (Copilot on #1079).
        _editor.Completion.DropChoices();
        _placing = true;
        try
        {
            _editor.Selections = stop.Ranges;
        }
        finally
        {
            _placing = false;
        }
        if (stop.Final)
        {
            End();
            return;
        }
        Changed?.Invoke();
        OfferChoices(stop);
    }

    private void OfferChoices(CodeSnippetStop stop)
    {
        if (stop.Choices is { Count: > 0 } options) _editor.Completion.OfferChoices(stop.Ranges[0], options);
    }

    /// <summary>Drops the stops nested in the placeholder the carets are leaving, when its text is not what it
    /// was when they reached it, as VS Code drops them.</summary>
    private void Leaving()
    {
        if (_stops is null) return;
        var leaving = _stops[_current];
        if (TextOf(leaving) == _reached) return;
        foreach (var stop in _stops)
            if (!stop.Final && NestedIn(stop, leaving.Index)) stop.Dropped = true;
    }

    /// <summary>Whether <paramref name="stop"/> is nested, at any depth, in the placeholder of stop
    /// <paramref name="index"/>.</summary>
    private bool NestedIn(CodeSnippetStop stop, int index)
    {
        var parent = stop.Parent;
        // A walk up the placeholders, never longer than there are stops.
        for (var depth = 0; parent >= 0 && _stops is not null && depth < _stops.Count; depth++)
        {
            if (parent == index) return true;
            var next = -1;
            foreach (var other in _stops)
                if (other.Index == parent) next = other.Parent;
            parent = next;
        }
        return false;
    }

    private string TextOf(CodeSnippetStop stop) =>
        stop.Ranges.Count == 0 ? "" : _editor.Document.TextIn(stop.Ranges[0]);

    /// <summary>
    /// Carries every stop through <paramref name="change"/>'s edits: the stop the carets stand at, and every
    /// placeholder it is nested in, take in what is typed at their edges, as VS Code makes the placeholders around
    /// the active one grow with it (Copilot on #1079), and every other stop is carried as a mark. Each growing
    /// stop's places are then kept apart, once for the whole change. A document replaced whole ends the session.
    /// </summary>
    private void Carry(CodeChange change)
    {
        if (_stops is null) return;
        if (change.Replaced)
        {
            End();
            return;
        }
        if (change.Edits.Count == 0) return;
        var growing = Growing();
        foreach (var edit in change.Edits)
        {
            for (var s = 0; s < _stops.Count; s++)
            {
                var ranges = _stops[s].Ranges;
                for (var i = 0; i < ranges.Count; i++)
                {
                    var range = ranges[i];
                    ranges[i] = growing[s]
                        ? new CodeRange(edit.CarryAnchor(range.Start), edit.CarryReach(range.End))
                        : edit.Carry(range);
                }
            }
        }
        for (var s = 0; s < _stops.Count; s++)
            if (growing[s]) Separate(_stops[s].Ranges);
    }

    /// <summary>Which stops grow at their edges: the one the carets stand at, and the placeholders it is nested
    /// in.</summary>
    private bool[] Growing()
    {
        var growing = new bool[_stops?.Count ?? 0];
        if (_stops is null || _current < 0) return growing;
        growing[_current] = true;
        var current = _stops[_current];
        for (var s = 0; s < _stops.Count; s++)
            if (!_stops[s].Final && NestedIn(current, _stops[s].Index)) growing[s] = true;
        return growing;
    }

    /// <summary>
    /// Keeps a growing stop's places apart: what is typed where one ends and the next begins is the one's that
    /// ends there, whose caret typed it. Both took it in, and two mirrors written side by side, <c>${1:a}$1</c>,
    /// overlapped and were made one caret (Copilot on #1079). Read in document order, each place against the one
    /// before it, so a snippet at a thousand carets is not compared pair by pair.
    /// </summary>
    private static void Separate(List<CodeRange> ranges)
    {
        if (ranges.Count < 2) return;
        var order = new List<int>();
        for (var i = 0; i < ranges.Count; i++) order.Add(i);
        order.Sort((a, b) => ranges[a].Start.CompareTo(ranges[b].Start));
        for (var k = 1; k < order.Count; k++)
        {
            var before = ranges[order[k - 1]];
            var range = ranges[order[k]];
            if (before.Start < range.Start && range.Start < before.End)
                ranges[order[k]] = new CodeRange(before.End < range.End ? before.End : range.End, range.End);
        }
    }

    /// <summary>Ends the session when a selection heard stands outside every stop: a press elsewhere, an arrow
    /// out of the placeholder.</summary>
    private void Followed()
    {
        if (_stops is null || _placing) return;
        if (!WithinAStop()) End();
    }

    /// <summary>Whether every selection stands inside a place of one stop, as VS Code asks it of a stop's
    /// group: the final stop aside, since a caret that reaches it is done.</summary>
    private bool WithinAStop()
    {
        if (_stops is null) return false;
        var selections = _editor.Selections;
        foreach (var stop in _stops)
        {
            if (stop.Final || stop.Dropped) continue;
            var all = true;
            foreach (var selection in selections)
            {
                var inside = false;
                foreach (var range in stop.Ranges)
                    if (selection.Start >= range.Start && selection.End <= range.End) inside = true;
                if (!inside)
                {
                    all = false;
                    break;
                }
            }
            if (all) return true;
        }
        return false;
    }
}
