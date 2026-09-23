namespace eQuantic.UI.Code;

/// <summary>
/// UNDO, as a person expects it: one press takes back the last THING, not the last character.
/// <para>
/// Runs of typing coalesce until something breaks the run — a newline, a deletion, a caret that
/// moved elsewhere, or a pause. Anything else (a paste, a formatting pass, a refactor) is its own
/// step because it arrived as one.
/// </para>
/// </summary>
public sealed class CodeHistory
{
    private readonly List<CodeEdit> _past = [];
    private readonly List<CodeEdit> _future = [];
    private CodePosition _runEnd = new(-1, -1);

    /// <summary>How many steps deep the history goes before the oldest is dropped.</summary>
    public int Limit { get; init; } = 500;

    public bool CanUndo => _past.Count > 0;
    public bool CanRedo => _future.Count > 0;

    /// <summary>Records an edit. A new edit always kills the redo branch — the future you did not
    /// take stops existing the moment you type something else, which is what every editor does.</summary>
    public void Record(CodeEdit edit)
    {
        _future.Clear();

        // Coalesce: a character typed right where the last one landed continues that run.
        if (edit.IsSimpleInsert && _past.Count > 0 && edit.Range.Start == _runEnd)
        {
            var previous = _past[^1];
            if (previous.IsSimpleInsert)
            {
                _past[^1] = previous with
                {
                    InsertedText = previous.InsertedText + edit.InsertedText,
                    SelectionAfter = edit.SelectionAfter,
                };
                _runEnd = edit.InsertedRange.End;
                return;
            }
        }

        _past.Add(edit);
        if (_past.Count > Limit) _past.RemoveAt(0);
        _runEnd = edit.IsSimpleInsert ? edit.InsertedRange.End : new CodePosition(-1, -1);
    }

    /// <summary>Ends the current typing run, so the NEXT character starts a new undo step. Called
    /// when the caret moves somewhere else, when the editor loses focus, when a file is saved.</summary>
    public void Break() => _runEnd = new CodePosition(-1, -1);

    /// <summary>Takes the last step back, applying it to <paramref name="document"/>.</summary>
    public CodeDocument? Undo(CodeDocument document, out CodeRange selection)
    {
        selection = default;
        if (_past.Count == 0) return null;

        var edit = _past[^1];
        _past.RemoveAt(_past.Count - 1);
        _future.Add(edit);
        Break();

        var next = document.Replace(edit.InsertedRange, edit.RemovedText, out _);
        selection = edit.SelectionBefore;
        return next;
    }

    /// <summary>Puts back what <see cref="Undo"/> took.</summary>
    public CodeDocument? Redo(CodeDocument document, out CodeRange selection)
    {
        selection = default;
        if (_future.Count == 0) return null;

        var edit = _future[^1];
        _future.RemoveAt(_future.Count - 1);
        _past.Add(edit);
        Break();

        var next = document.Replace(edit.Range, edit.InsertedText, out _);
        selection = edit.SelectionAfter;
        return next;
    }

    public void Clear()
    {
        _past.Clear();
        _future.Clear();
        Break();
    }
}
