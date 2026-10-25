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
/// again first first. A command made at several carets is one step too, with every caret on each side
/// of it (<see cref="CodeStep"/>), and typing at several carets is one run, as typing at one is (#1012).
/// </para>
/// </summary>
public sealed class CodeHistory
{
    private readonly List<CodeStep> _past = [];
    private readonly List<CodeStep> _future = [];
    private bool _running;

    /// <summary>How many steps deep the history goes before the oldest is dropped.</summary>
    public int Limit { get; init; } = 500;

    public bool CanUndo => _past.Count > 0;
    public bool CanRedo => _future.Count > 0;

    /// <summary>Records one step. A new step always kills the redo branch — the future you did not take
    /// stops existing the moment you type something else, which is what every editor does. A step of no
    /// edits records nothing.</summary>
    public void Record(CodeStep step)
    {
        if (step.Edits.Count == 0) return;
        _future.Clear();

        // Coalesce: characters TYPED at the carets, right where the last ones typed left them, continue that
        // run, and a run may begin by typing over a selection, the replacement being its first step. What was
        // not typed (a paste, a cut, an indent) never joins one and never starts one, and neither does a
        // step that is not one typed edit at each caret.
        if (_running && _past.Count > 0 && Continues(_past[^1], step))
        {
            _past[^1] = Joined(_past[^1], step);
            return;
        }

        _past.Add(step);
        if (_past.Count > Limit) _past.RemoveAt(0);
        _running = Runs(step);
    }

    /// <summary>Whether <paramref name="step"/> may begin a run: one typed edit at each caret, none of them
    /// breaking a line.</summary>
    private static bool Runs(CodeStep step)
    {
        if (step.Edits.Count != step.Before.Count) return false;
        foreach (var edit in step.Edits)
            if (!edit.Typed || edit.BreaksLine) return false;
        return true;
    }

    /// <summary>
    /// Whether <paramref name="step"/> goes on with the run <paramref name="previous"/> is: as many carets,
    /// each where the run left it, and at each one character typed there. The edits of a step at several
    /// carets are made from the last caret to the first, so the one made k-th is at the caret k-th from
    /// the end, in coordinates no edit before it moved.
    /// </summary>
    private static bool Continues(CodeStep previous, CodeStep step)
    {
        var count = step.Edits.Count;
        if (count != previous.Edits.Count || count != step.Before.Count || count != previous.After.Count)
            return false;
        for (var i = 0; i < count; i++)
            if (step.Before[i] != previous.After[i] || !step.Before[i].IsEmpty) return false;
        var carets = new List<CodePosition>();
        foreach (var caret in step.Before) carets.Add(caret.Focus);
        carets.Sort();
        for (var k = 0; k < count; k++)
        {
            var edit = step.Edits[k];
            if (!edit.Typed || !edit.IsSimpleInsert || edit.Range.Start != carets[count - 1 - k]) return false;
        }
        return true;
    }

    /// <summary><paramref name="previous"/> and <paramref name="step"/> as one step: each caret's text typed
    /// on after its own, from the carets the run found to the ones it leaves.</summary>
    private static CodeStep Joined(CodeStep previous, CodeStep step)
    {
        var edits = new List<CodeEdit>();
        for (var k = 0; k < step.Edits.Count; k++)
        {
            var before = previous.Edits[k];
            var edit = step.Edits[k];
            edits.Add(before with
            {
                InsertedText = before.InsertedText + edit.InsertedText,
                SelectionAfter = edit.SelectionAfter,
            });
        }
        return new CodeStep(edits, previous.Before, step.After);
    }

    /// <summary>Ends the current typing run, so the NEXT character starts a new undo step. Called
    /// when the caret moves somewhere else, when the editor loses focus, when a file is saved.</summary>
    public void Break() => _running = false;

    /// <summary>
    /// Takes the last step back, applying it to <paramref name="document"/>: its edits last first, each
    /// in the document the later ones left once they were taken back. <paramref name="made"/> is every
    /// replacement that made, in the order made, each in the coordinates of the document it was made on
    /// and holding what it removed and what it wrote, which is what anything kept per line (the colours,
    /// the widths) and anything carrying a range through edits is brought up to date by, as it is for an
    /// edit. <paramref name="selections"/> are the selections the step found, the primary first.
    /// </summary>
    public CodeDocument? Undo(CodeDocument document, out IReadOnlyList<CodeRange> selections,
        out IReadOnlyList<CodeEdit> made)
    {
        selections = [];
        made = [];
        if (_past.Count == 0) return null;

        var step = _past[^1];
        _past.RemoveAt(_past.Count - 1);
        _future.Add(step);
        Break();

        var next = document;
        var undone = new List<CodeEdit>();
        for (var i = step.Edits.Count - 1; i >= 0; i--)
        {
            var edit = step.Edits[i];
            var replaced = new CodeRange(next.Clamp(edit.InsertedRange.Start), next.Clamp(edit.InsertedRange.End));
            var removed = next.TextIn(replaced);
            next = next.Replace(replaced, edit.RemovedText, out var end);
            undone.Add(new CodeEdit(replaced, removed, next.TextIn(new CodeRange(replaced.Start, end)),
                edit.SelectionAfter, edit.SelectionBefore, false));
        }
        made = undone;
        selections = step.Before;
        return next;
    }

    /// <summary>Puts back what <see cref="Undo"/> took, its edits first first, and says what it made and
    /// the selections the step left as <see cref="Undo"/> does.</summary>
    public CodeDocument? Redo(CodeDocument document, out IReadOnlyList<CodeRange> selections,
        out IReadOnlyList<CodeEdit> made)
    {
        selections = [];
        made = [];
        if (_future.Count == 0) return null;

        var step = _future[^1];
        _future.RemoveAt(_future.Count - 1);
        _past.Add(step);
        Break();

        var next = document;
        var redone = new List<CodeEdit>();
        foreach (var edit in step.Edits)
        {
            var replaced = new CodeRange(next.Clamp(edit.Range.Start), next.Clamp(edit.Range.End));
            var removed = next.TextIn(replaced);
            next = next.Replace(replaced, edit.InsertedText, out var end);
            redone.Add(new CodeEdit(replaced, removed, next.TextIn(new CodeRange(replaced.Start, end)),
                edit.SelectionBefore, edit.SelectionAfter, false));
        }
        made = redone;
        selections = step.After;
        return next;
    }

    public void Clear()
    {
        _past.Clear();
        _future.Clear();
        Break();
    }
}
