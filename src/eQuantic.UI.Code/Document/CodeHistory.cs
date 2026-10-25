namespace eQuantic.UI.Code;

/// <summary>
/// UNDO, as a person expects it: one press takes back the last THING, not the last character.
/// <para>
/// Runs of typing coalesce until something breaks the run — a newline, a deletion, a caret that
/// moved elsewhere, or a pause. Anything else (a paste, a formatting pass, a refactor) is its own
/// step because it arrived as one.
/// </para>
/// <para>
/// A step is the edits ONE command made, in the order it made them, each in the coordinates of the
/// document it was made on: a line moved is the line it passed written on its other side and deleted
/// where it was, two edits and one step (#1031). Undo takes them back last first, and redo makes them
/// again first first.
/// </para>
/// </summary>
public sealed class CodeHistory
{
    private readonly List<IReadOnlyList<CodeEdit>> _past = [];
    private readonly List<IReadOnlyList<CodeEdit>> _future = [];
    private CodePosition _runEnd = new(-1, -1);

    /// <summary>How many steps deep the history goes before the oldest is dropped.</summary>
    public int Limit { get; init; } = 500;

    public bool CanUndo => _past.Count > 0;
    public bool CanRedo => _future.Count > 0;

    /// <summary>Records the edits of one step, in the order they were made. A new step always kills the
    /// redo branch — the future you did not take stops existing the moment you type something else,
    /// which is what every editor does. A step of no edits records nothing.</summary>
    public void Record(IReadOnlyList<CodeEdit> edits)
    {
        if (edits.Count == 0) return;
        _future.Clear();

        // Coalesce: a character TYPED right where the last typed one landed continues that run, and
        // a run may begin by typing over a selection, the replacement being its first step. What
        // was not typed (a paste, a cut, an indent) never joins one and never starts one, and neither
        // does a step of several edits.
        var edit = edits[0];
        if (edits.Count == 1 && edit.Typed && edit.IsSimpleInsert && _past.Count > 0 && edit.Range.Start == _runEnd)
        {
            var step = _past[^1];
            if (step.Count == 1 && step[0] is { Typed: true, BreaksLine: false } previous)
            {
                _past[^1] =
                [
                    previous with
                    {
                        InsertedText = previous.InsertedText + edit.InsertedText,
                        SelectionAfter = edit.SelectionAfter,
                    },
                ];
                _runEnd = edit.InsertedRange.End;
                return;
            }
        }

        _past.Add(edits);
        if (_past.Count > Limit) _past.RemoveAt(0);
        _runEnd = edits.Count == 1 && edit.Typed && !edit.BreaksLine
            ? edit.InsertedRange.End
            : new CodePosition(-1, -1);
    }

    /// <summary>Ends the current typing run, so the NEXT character starts a new undo step. Called
    /// when the caret moves somewhere else, when the editor loses focus, when a file is saved.</summary>
    public void Break() => _runEnd = new CodePosition(-1, -1);

    /// <summary>
    /// Takes the last step back, applying it to <paramref name="document"/>: its edits last first, each
    /// in the document the later ones left once they were taken back. <paramref name="made"/> is every
    /// replacement that made, in the order made, each in the coordinates of the document it was made on
    /// and holding what it removed and what it wrote, which is what anything kept per line (the colours,
    /// the widths) and anything carrying a range through edits is brought up to date by, as it is for an
    /// edit. <paramref name="selection"/> is the selection the step found.
    /// </summary>
    public CodeDocument? Undo(CodeDocument document, out CodeRange selection, out IReadOnlyList<CodeEdit> made)
    {
        selection = default;
        made = [];
        if (_past.Count == 0) return null;

        var step = _past[^1];
        _past.RemoveAt(_past.Count - 1);
        _future.Add(step);
        Break();

        var next = document;
        var undone = new List<CodeEdit>();
        for (var i = step.Count - 1; i >= 0; i--)
        {
            var edit = step[i];
            var replaced = new CodeRange(next.Clamp(edit.InsertedRange.Start), next.Clamp(edit.InsertedRange.End));
            var removed = next.TextIn(replaced);
            next = next.Replace(replaced, edit.RemovedText, out var end);
            undone.Add(new CodeEdit(replaced, removed, next.TextIn(new CodeRange(replaced.Start, end)),
                edit.SelectionAfter, edit.SelectionBefore, false));
        }
        made = undone;
        selection = step[0].SelectionBefore;
        return next;
    }

    /// <summary>Puts back what <see cref="Undo"/> took, its edits first first, and says what it made and
    /// the selection the step left as <see cref="Undo"/> does.</summary>
    public CodeDocument? Redo(CodeDocument document, out CodeRange selection, out IReadOnlyList<CodeEdit> made)
    {
        selection = default;
        made = [];
        if (_future.Count == 0) return null;

        var step = _future[^1];
        _future.RemoveAt(_future.Count - 1);
        _past.Add(step);
        Break();

        var next = document;
        var redone = new List<CodeEdit>();
        foreach (var edit in step)
        {
            var replaced = new CodeRange(next.Clamp(edit.Range.Start), next.Clamp(edit.Range.End));
            var removed = next.TextIn(replaced);
            next = next.Replace(replaced, edit.InsertedText, out var end);
            redone.Add(new CodeEdit(replaced, removed, next.TextIn(new CodeRange(replaced.Start, end)),
                edit.SelectionBefore, edit.SelectionAfter, false));
        }
        made = redone;
        selection = step[^1].SelectionAfter;
        return next;
    }

    public void Clear()
    {
        _past.Clear();
        _future.Clear();
        Break();
    }
}
