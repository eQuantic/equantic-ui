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

        // Coalesce: a character TYPED right where the last typed one landed continues that run, and
        // a run may begin by typing over a selection, the replacement being its first step. What
        // was not typed (a paste, a cut, an indent) never joins one and never starts one.
        if (edit.Typed && edit.IsSimpleInsert && _past.Count > 0 && edit.Range.Start == _runEnd)
        {
            var previous = _past[^1];
            if (previous.Typed && !previous.InsertedText.Contains('\n'))
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
        _runEnd = edit.Typed && !edit.InsertedText.Contains('\n')
            ? edit.InsertedRange.End
            : new CodePosition(-1, -1);
    }

    /// <summary>Ends the current typing run, so the NEXT character starts a new undo step. Called
    /// when the caret moves somewhere else, when the editor loses focus, when a file is saved.</summary>
    public void Break() => _runEnd = new CodePosition(-1, -1);

    /// <summary>
    /// Takes the last step back, applying it to <paramref name="document"/>. A step is one
    /// replacement: <paramref name="replaced"/> is the range of <paramref name="document"/> it wrote
    /// over, and <paramref name="written"/> the range of the result that holds what it wrote, which is
    /// what anything kept per line (the colours, the widths) is brought up to date by, as it is for
    /// an edit.
    /// </summary>
    public CodeDocument? Undo(CodeDocument document, out CodeRange selection, out CodeRange replaced,
        out CodeRange written)
    {
        selection = default;
        replaced = default;
        written = default;
        if (_past.Count == 0) return null;

        var edit = _past[^1];
        _past.RemoveAt(_past.Count - 1);
        _future.Add(edit);
        Break();

        replaced = new CodeRange(document.Clamp(edit.InsertedRange.Start), document.Clamp(edit.InsertedRange.End));
        var next = document.Replace(replaced, edit.RemovedText, out var end);
        written = new CodeRange(replaced.Start, end);
        selection = edit.SelectionBefore;
        return next;
    }

    /// <summary>Puts back what <see cref="Undo"/> took, and says what it replaced as
    /// <see cref="Undo"/> does.</summary>
    public CodeDocument? Redo(CodeDocument document, out CodeRange selection, out CodeRange replaced,
        out CodeRange written)
    {
        selection = default;
        replaced = default;
        written = default;
        if (_future.Count == 0) return null;

        var edit = _future[^1];
        _future.RemoveAt(_future.Count - 1);
        _past.Add(edit);
        Break();

        replaced = new CodeRange(document.Clamp(edit.Range.Start), document.Clamp(edit.Range.End));
        var next = document.Replace(replaced, edit.InsertedText, out var end);
        written = new CodeRange(replaced.Start, end);
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
