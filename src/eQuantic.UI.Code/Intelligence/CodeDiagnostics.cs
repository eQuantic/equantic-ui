namespace eQuantic.UI.Code;

/// <summary>
/// The problems of ONE editor: what its app reported, what its providers answered, and where each
/// stands now. It is a session of the plan's fifth section, as the completion is: every behaviour is a
/// method or an edit a test drives with no screen, and the editor only draws what it holds.
/// <para>
/// A problem is DATA. The app reports its own (<see cref="Report"/>, a build's output), each provider's
/// latest answer is a set of its own, and <see cref="Items"/> is their union in document order. A new
/// report from the app replaces only what the app reported before, and an answer from a provider only
/// what that provider answered before.
/// </para>
/// <para>
/// Every problem travels with the edits made after it was reported (<see cref="CodeEdit.Carry"/>),
/// undo and redo included, until whoever reported it reports again: a squiggle stays under the word
/// it is about while the line around it is edited. A document replaced whole drops them all, since no
/// range in it means anything in the new one.
/// </para>
/// <para>
/// Latency decides how the providers are asked (the plan's sixth section). An edit restarts a wait of
/// <see cref="Delay"/>; when it ends, every provider is asked about the document as it stands. One
/// request is out at a time: the next edit cancels its token, and an answer that arrives after a newer
/// edit is dropped by its generation, whether its provider honoured the token or not.
/// </para>
/// </summary>
public sealed class CodeDiagnostics
{
    private readonly CodeEditorController _editor;
    private List<CodeDiagnostic> _reported = [];
    // Each provider's latest answer, by the provider, carried through every edit since it came.
    private readonly List<(ICodeDiagnosticProvider Provider, List<CodeDiagnostic> Problems)> _answered = [];
    private List<CodeDiagnostic> _items = [];
    private int _generation;
    private CancellationTokenSource? _request;
    private int _waiting;

    internal CodeDiagnostics(CodeEditorController editor)
    {
        _editor = editor;
        editor.Changed += Edited;
    }

    /// <summary>
    /// The sources this editor asks as its text changes, in no order that matters: their answers are
    /// shown together, in document order. None to begin with, and with none nothing is asked.
    /// </summary>
    public IList<ICodeDiagnosticProvider> Providers { get; } = [];

    /// <summary>How long typing has to pause before the providers are asked. An edit inside it starts
    /// it again, so a burst of keystrokes asks once, about the text they left.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Every problem the editor holds, in document order: by where it starts, the worst first
    /// among those that start together.</summary>
    public IReadOnlyList<CodeDiagnostic> Items => _items;

    /// <summary>Whether the providers are being asked, or waited for: a request is out, or typing has
    /// not paused long enough yet.</summary>
    public bool IsChecking => _waiting > 0;

    /// <summary>Raised whenever <see cref="Items"/> changes: a report, an answer, an edit carrying them.
    /// Problems equal to those held already raise nothing.</summary>
    public event Action? Changed;

    /// <summary>Raised when a provider throws, with what it threw. The other providers' answers still
    /// show, so a failing provider is otherwise silent.</summary>
    public event Action<Exception>? Failed;

    /// <summary>
    /// The problems the APP knows about: a build's output, an analyzer run, a test. They replace what
    /// the app reported before, and travel with every edit after, as an answer does.
    /// </summary>
    public void Report(IEnumerable<CodeDiagnostic> problems)
    {
        _reported = Placed(problems);
        Gather();
    }

    /// <summary>Asks every provider about the document as it stands, now: when a file opens, when a
    /// provider is added, or when something only the app knows about makes the answers stale.</summary>
    public void Check() => _ = Ask(wait: false);

    /// <summary>The problems at <paramref name="position"/>: those whose range holds it, its ends
    /// included, the worst first.</summary>
    public IReadOnlyList<CodeDiagnostic> At(CodePosition position) =>
        _items.Where(problem => problem.Range.Start <= position && position <= problem.Range.End)
            .OrderByDescending(problem => (int)problem.Severity)
            .ThenBy(problem => problem.Range.Start.Line)
            .ThenBy(problem => problem.Range.Start.Column)
            .ToList();

    /// <summary>
    /// Puts the caret at the start of the next problem after it, or the last one before it when
    /// <paramref name="backward"/>, wrapping at either end of the document, and shows that problem's
    /// card: F8 and Shift+F8. Answers the problem, or null when the editor holds none.
    /// </summary>
    public CodeDiagnostic? Next(bool backward = false)
    {
        if (_items.Count == 0) return null;
        var caret = _editor.Caret;
        var next = backward
            ? _items.LastOrDefault(problem => problem.Range.Start < caret) ?? _items[_items.Count - 1]
            : _items.FirstOrDefault(problem => problem.Range.Start > caret) ?? _items[0];
        _editor.Selection = new CodeRange(next.Range.Start);
        _editor.Hover.ShowProblems(next.Range.Start);
        return next;
    }

    // ---- what the editor did ----------------------------------------------------------------------

    /// <summary>An edit carries every problem through it and asks again once typing pauses; a document
    /// replaced whole drops them and asks at once.</summary>
    private void Edited(CodeEdit? edit)
    {
        if (edit is null)
        {
            _reported = [];
            _answered.Clear();
            Gather();
            _ = Ask(wait: false);
            return;
        }
        _reported = _reported.Select(problem => problem with { Range = edit.Carry(problem.Range) }).ToList();
        for (var i = 0; i < _answered.Count; i++)
        {
            var (provider, problems) = _answered[i];
            _answered[i] = (provider, problems.Select(problem => problem with { Range = edit.Carry(problem.Range) }).ToList());
        }
        Gather();
        _ = Ask(wait: true);
    }

    // ---- asking -----------------------------------------------------------------------------------

    /// <summary>
    /// Asks every provider, after the pause when <paramref name="wait"/> says so. Whatever was waiting
    /// or out is cancelled first, so at most one request is out; an answer is applied only while its
    /// generation is the latest.
    /// </summary>
    private async Task Ask(bool wait)
    {
        var generation = ++_generation;
        _request?.Cancel();
        _request = null;
        if (Providers.Count == 0) return;
        var request = new CancellationTokenSource();
        _request = request;
        var document = _editor.Document;
        if (wait && Delay > TimeSpan.Zero)
        {
            _waiting++;
            try
            {
                await Task.Delay(Delay, request.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                _waiting--;
            }
            if (generation != _generation) return;
            document = _editor.Document;
        }

        // Every provider is asked before any is waited for, so they work at the same time, and each
        // answer is applied as it comes: their union is in document order whatever order they land in.
        foreach (var provider in Providers.ToList()) _ = AskOne(provider, document, generation, request.Token);
    }

    private async Task AskOne(ICodeDiagnosticProvider provider, CodeDocument document, int generation,
        CancellationToken cancellation)
    {
        _waiting++;
        IReadOnlyList<CodeDiagnostic> answer;
        try
        {
            answer = await provider.DiagnoseAsync(document, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Cancelled because the answer was no longer wanted: a newer edit asks again.
            _waiting--;
            return;
        }
        catch (Exception error)
        {
            _waiting--;
            UiThread.Run(() => Failed?.Invoke(error));
            return;
        }
        _waiting--;
        UiThread.Run(() =>
        {
            if (generation != _generation) return;
            var problems = Placed(answer);
            var at = -1;
            for (var i = 0; i < _answered.Count; i++)
                if (ReferenceEquals(_answered[i].Provider, provider)) at = i;
            if (at >= 0) _answered[at] = (provider, problems);
            else _answered.Add((provider, problems));
            Gather();
        });
    }

    // ---- the union --------------------------------------------------------------------------------

    /// <summary>The problems as the document can hold them: each range ordered and inside the
    /// document, whatever a provider sent.</summary>
    private List<CodeDiagnostic> Placed(IEnumerable<CodeDiagnostic> problems)
    {
        var document = _editor.Document;
        return problems
            .Select(problem => problem with
            {
                Range = new CodeRange(document.Clamp(problem.Range.Start), document.Clamp(problem.Range.End)),
            })
            .ToList();
    }

    /// <summary>The app's problems and the answers of the providers the editor still asks, in document
    /// order, the worst first among those that start together.</summary>
    private void Gather()
    {
        // The answer of a provider taken out goes with it: put back, it shows nothing until it answers
        // again, and one made anew for every build of a component leaves no answer behind per build.
        for (var i = _answered.Count - 1; i >= 0; i--)
        {
            var provider = _answered[i].Provider;
            if (!Providers.Any(held => ReferenceEquals(held, provider))) _answered.RemoveAt(i);
        }
        var items = _reported.Concat(_answered.SelectMany(entry => entry.Problems))
            .OrderBy(problem => problem.Range.Start.Line)
            .ThenBy(problem => problem.Range.Start.Column)
            .ThenByDescending(problem => (int)problem.Severity)
            .ThenBy(problem => problem.Range.End.Line)
            .ThenBy(problem => problem.Range.End.Column)
            .ToList();
        // The same problems as before say nothing: an answer that found what the last one did, a report
        // of what was reported, a keystroke that moved none of them. A component builds again on this
        // event, so one raised for nothing costs a build per key, and a provider handed anew on every
        // build would be asked again by the build its own answer caused, without end.
        if (Same(items, _items)) return;
        _items = items;
        Changed?.Invoke();
    }

    /// <summary>Whether two lists hold equal problems in the same order.</summary>
    private static bool Same(List<CodeDiagnostic> one, List<CodeDiagnostic> other)
    {
        if (one.Count != other.Count) return false;
        for (var i = 0; i < one.Count; i++)
            if (!one[i].Equals(other[i])) return false;
        return true;
    }
}
