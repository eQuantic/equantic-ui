namespace eQuantic.UI.Code;

/// <summary>
/// The hover of ONE editor: the card that explains the code under the pointer or the caret, while one
/// shows, and the wait before it does. It is a session of the plan's fifth section, as the completion
/// is: a pointer resting, a key and an answer arriving are methods a test drives with no screen, and
/// the editor only draws the card it holds.
/// <para>
/// A card holds the problems reported where it was asked (<see cref="CodeDiagnostics.At"/>), the worst
/// first, and then what the providers answered, in the order of <see cref="Providers"/>
/// (<see cref="ICodeHoverProvider"/>). It shows as soon as anything is there to show and grows as later
/// answers arrive; with nothing to show, none shows.
/// </para>
/// <para>
/// Latency decides the shape (the plan's sixth section). A pointer has to REST for <see cref="Delay"/>
/// before anything is asked, so a pointer passing over the code asks nothing, as a tooltip waits its
/// <c>waitDuration</c> in Flutter and a hover its <c>editor.hover.delay</c> in VS Code. An answer that
/// arrives after the pointer moved on, the caret moved or the text changed is dropped by its generation.
/// </para>
/// </summary>
public sealed class CodeHoverSession
{
    private readonly CodeEditorController _editor;
    private int _generation;
    private CancellationTokenSource? _request;
    // What a request under way is for, from the wait until the card closes or another replaces it: the
    // pointer resting anywhere in it asks nothing again, a provider still answering included.
    private CodeRange? _pending;
    private bool _open;
    private bool _byKeyboard;
    // Whether the keyboard made the newest request, the one waited for or answered: the pointer leaving
    // cancels a request it made itself, and never one the keyboard made.
    private bool _requestByKeyboard;
    private CodeRange _range;
    // Where the card that shows, or whose providers are asked, was asked for: its problems are the ones
    // there, read again whenever the problems change. Null with no such card.
    private CodePosition? _cardAt;
    private List<CodeDiagnostic> _problems = [];
    // One slot per provider asked, in the order of Providers, filled as each answers.
    private List<CodeHover?> _answers = [];
    // What the card shows of them, in the same order: built when an answer lands, read every frame.
    private List<CodeHover> _shown = [];

    internal CodeHoverSession(CodeEditorController editor)
    {
        _editor = editor;
        // A card explains the code as it stood where it was asked: a caret that moves (a click
        // included) or text that changes closes it, and drops whatever was being waited for.
        editor.SelectionChanged += _ => Dismiss();
        editor.Changed += _ => Dismiss();
        // One thing at a time over the caret: the list showing closes the card, which does not come
        // back when the list goes.
        editor.Completion.Changed += CompletionChanged;
        // A card holds the problems where it was asked as they are now: one reported after the card
        // showed (a provider answering after the pause) joins it, and one taken away leaves it.
        editor.Diagnostics.Changed += DiagnosticsChanged;
    }

    private void CompletionChanged()
    {
        if (_editor.Completion.IsOpen) Dismiss();
    }

    /// <summary>
    /// The problems changed while a card shows or its providers are asked: the card holds the problems
    /// where it was asked as they are now, and explains their ranges, without asking its providers again,
    /// so a problem that arrives before any answer opens it. A card left with nothing to show closes, as
    /// if dismissed, so an answer still owed does not bring it back unasked. A wait that has not ended
    /// asks nothing here: it reads the problems when it ends.
    /// </summary>
    private void DiagnosticsChanged()
    {
        if (_cardAt is not { } position) return;
        var problems = _byKeyboard ? _editor.Diagnostics.At(position).ToList() : ProblemsUnder(position);
        if (CodeDiagnostics.Same(problems, _problems)) return;
        if (problems.Count == 0 && _shown.Count == 0)
        {
            Dismiss();
            return;
        }
        _problems = problems;
        var document = _editor.Document;
        var range = Explained(position, character: !_byKeyboard) ?? new CodeRange(position);
        foreach (var answer in _shown)
            range = Union(range, new CodeRange(document.Clamp(answer.Range.Start), document.Clamp(answer.Range.End)));
        _range = range;
        _open = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// What explains the code: a language service, a documentation index. None to begin with, and with
    /// none a card holds only the problems reported where it was asked.
    /// </summary>
    public IList<ICodeHoverProvider> Providers { get; } = [];

    /// <summary>How long a pointer rests before the card is asked for. VS Code's default.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Whether a card shows.</summary>
    public bool IsOpen => _open;

    /// <summary>Whether the keyboard asked for the card that shows (F8, ⌘K ⌘I): such a card is
    /// announced, and a pointer passing over the code does not close it.</summary>
    public bool ByKeyboard => _byKeyboard;

    /// <summary>What the card explains: the word it was asked about, with the ranges of the problems and
    /// the answers it holds. The card stands over its first line, and a pointer anywhere in it keeps the
    /// card open.</summary>
    public CodeRange Range => _range;

    /// <summary>The problems the card holds, the worst first.</summary>
    public IReadOnlyList<CodeDiagnostic> Problems => _problems;

    /// <summary>What the providers answered, in the order of <see cref="Providers"/>, as they arrive.</summary>
    public IReadOnlyList<CodeHover> Answers => _shown;

    /// <summary>Raised whenever the card shows, closes or grows.</summary>
    public event Action? Changed;

    /// <summary>Raised when a provider throws, with what it threw. The card still shows what the
    /// others answered, so a failing provider is otherwise silent.</summary>
    public event Action<Exception>? Failed;

    /// <summary>
    /// Shows the card for <paramref name="position"/> at once, as the keyboard asks for it (⌘K ⌘I):
    /// the problems there and what the providers answer. False when there is nothing that could be
    /// shown: no problem there and no provider to ask.
    /// </summary>
    public bool ShowAt(CodePosition position)
    {
        if (!CanShowAt(position)) return false;
        var at = _editor.Document.Clamp(position);
        var explained = Explained(at, character: false);
        _ = Rest(explained ?? new CodeRange(at), at, wait: false, keyboard: true, ask: true);
        return true;
    }

    /// <summary>
    /// Whether <see cref="ShowAt"/> would show a card for <paramref name="position"/>: no completion list
    /// showing, and a provider to ask or a problem there. What the keymap reads before it waits for the
    /// second half of ⌘K ⌘I, so the chord waits only while its card could show. A word alone explains
    /// nothing: ShowAt answered true over one with no provider and no problem, and showed no card.
    /// </summary>
    internal bool CanShowAt(CodePosition position)
    {
        // One thing at a time over the caret: while the list shows, the card waits for it to go.
        if (_editor.Completion.IsOpen) return false;
        return Providers.Count > 0 || _editor.Diagnostics.At(_editor.Document.Clamp(position)).Count > 0;
    }

    /// <summary>Closes the card, and drops what was being waited for. Answers whether a card was
    /// showing, which is what Escape claims.</summary>
    public bool Dismiss()
    {
        _generation++;
        ProviderRequest.Cancel(_request, Failed);
        _request = null;
        _pending = null;
        _requestByKeyboard = false;
        _cardAt = null;
        if (!_open) return false;
        _open = false;
        _byKeyboard = false;
        _problems = [];
        _answers = [];
        _shown = [];
        Changed?.Invoke();
        return true;
    }

    // ---- the pointer and the problems -------------------------------------------------------------

    /// <summary>
    /// The pointer moved over the surface with nothing pressed, over the character at
    /// <paramref name="character"/>, or over no character (null): past the end of a line, in the
    /// gutter, under the last line. Resting on what a card or a wait already explains changes nothing;
    /// resting anywhere else closes a card the pointer asked for and waits again.
    /// </summary>
    internal void PointerAt(CodePosition? character)
    {
        if (character is not { } position)
        {
            PointerLeft();
            return;
        }
        // While the completion list shows, the pointer resting on the code asks nothing: a card it showed
        // would wait behind the list and appear when the list went, unasked.
        if (_editor.Completion.IsOpen) return;
        if (_open && !_byKeyboard && Covers(_range, position)) return;
        if (_pending is { } pending && Covers(pending, position)) return;
        if (Explained(position, character: true) is not { } explained)
        {
            PointerLeft();
            return;
        }
        if (_open && !_byKeyboard) Dismiss();
        _pending = explained;
        _ = Rest(explained, position, wait: true, keyboard: false, ask: true);
    }

    /// <summary>
    /// The pointer left the surface, or rests on nothing a card explains: a request it made itself ends,
    /// and a card it asked for closes. What the keyboard asked for stays, its card and a request of its
    /// still on its way, and so does a card the keyboard showed while the pointer's own request ends.
    /// </summary>
    internal void PointerLeft()
    {
        if (_requestByKeyboard) return;
        if (_open && _byKeyboard)
        {
            _generation++;
            ProviderRequest.Cancel(_request, Failed);
            _request = null;
            _pending = null;
            return;
        }
        Dismiss();
    }

    /// <summary>The card of the problems at <paramref name="position"/>, at once and with no provider
    /// asked: what F8 shows after it put the caret on a problem.</summary>
    internal void ShowProblems(CodePosition position)
    {
        var explained = Explained(position, character: false) ?? new CodeRange(position);
        _ = Rest(explained, position, wait: false, keyboard: true, ask: false);
    }

    // ---- the card ---------------------------------------------------------------------------------

    /// <summary>
    /// Waits for the pointer to rest when <paramref name="wait"/> says so, then shows the problems at
    /// <paramref name="position"/> and asks the providers. Whatever was waiting or asked is cancelled
    /// first; an answer is applied only while its generation is the latest.
    /// </summary>
    private async Task Rest(CodeRange explained, CodePosition position, bool wait, bool keyboard, bool ask)
    {
        if (_editor.Completion.IsOpen) return;
        var generation = ++_generation;
        ProviderRequest.Cancel(_request, Failed);
        var request = new CancellationTokenSource();
        _request = request;
        _pending = explained;
        _requestByKeyboard = keyboard;
        if (wait && Delay > TimeSpan.Zero)
        {
            var waited = true;
            try
            {
                await Task.Delay(Delay, request.Token);
            }
            catch (OperationCanceledException)
            {
                waited = false;
            }
            // Back where the editor lives: a timer ends the wait on a thread of its own, and the generation
            // is read on the UI thread, so a card closed while the wait ended is never opened again (a
            // Photon host has no synchronization context to bring an await back to it).
            UiThread.Run(() =>
            {
                if (waited && generation == _generation) Show(explained, position, keyboard, ask, generation, request.Token);
            });
            return;
        }
        Show(explained, position, keyboard, ask, generation, request.Token);
    }

    /// <summary>Shows the problems at <paramref name="position"/> at once and asks the providers, on the UI
    /// thread, for the request numbered <paramref name="generation"/>.</summary>
    private void Show(CodeRange explained, CodePosition position, bool keyboard, bool ask, int generation,
        CancellationToken cancellation)
    {
        var document = _editor.Document;
        var providers = ask ? Providers.ToList() : new List<ICodeHoverProvider>();
        _range = explained;
        _cardAt = position;
        // The pointer is on a character, the caret between two: a problem ending where the pointer rests
        // is not under it, and is beside the caret.
        _problems = keyboard ? _editor.Diagnostics.At(position).ToList() : ProblemsUnder(position);
        _answers = [];
        _shown = [];
        foreach (var _ in providers) _answers.Add(null);
        _byKeyboard = keyboard;
        // A card asked for again over one that shows says so either way: it shows what is here now,
        // or nothing until an answer comes.
        var showed = _open;
        _open = _problems.Count > 0;
        if (_open || showed) Changed?.Invoke();
        for (var slot = 0; slot < providers.Count; slot++)
            _ = AskOne(slot, providers[slot], document, position, generation, cancellation);
    }

    private async Task AskOne(int slot, ICodeHoverProvider provider, CodeDocument document,
        CodePosition position, int generation, CancellationToken cancellation)
    {
        CodeHover? answer;
        try
        {
            answer = await provider.HoverAsync(document, position, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Cancelled because the card was no longer wanted: its generation is gone.
            return;
        }
        catch (Exception error)
        {
            UiThread.Run(() => Failed?.Invoke(error));
            return;
        }
        UiThread.Run(() =>
        {
            if (generation != _generation || answer is null) return;
            _answers[slot] = answer;
            var shown = new List<CodeHover>();
            foreach (var held in _answers)
                if (held is not null) shown.Add(held);
            _shown = shown;
            // What the provider says it explains is part of what the card explains: the pointer
            // resting anywhere in it keeps the card.
            var range = new CodeRange(document.Clamp(answer.Range.Start), document.Clamp(answer.Range.End));
            _range = Union(_range, range);
            _open = true;
            Changed?.Invoke();
        });
    }

    /// <summary>
    /// What a card asked about <paramref name="position"/> explains: the word or the run of symbols on
    /// the character there, with the range of every problem at it, or null when the character is white
    /// space and no problem is at it. A problem is at the CHARACTER under the pointer when its range
    /// covers it (<paramref name="character"/>), and at the caret when its range holds the caret, its
    /// ends included.
    /// </summary>
    private CodeRange? Explained(CodePosition position, bool character)
    {
        var document = _editor.Document;
        var line = document.Line(position.Line);
        CodeRange? explained = position.Column < line.Length && !char.IsWhiteSpace(line[position.Column])
            ? document.WordAt(position)
            : null;
        foreach (var problem in character ? ProblemsUnder(position) : _editor.Diagnostics.At(position))
            explained = explained is { } before ? Union(before, problem.Range) : problem.Range;
        return explained;
    }

    /// <summary>The problems on the character at <paramref name="position"/>, the worst first: those whose
    /// range covers it.</summary>
    private List<CodeDiagnostic> ProblemsUnder(CodePosition position)
    {
        var under = new List<CodeDiagnostic>();
        foreach (var problem in _editor.Diagnostics.At(position))
            if (Covers(problem.Range, position)) under.Add(problem);
        return under;
    }

    /// <summary>Whether the character at <paramref name="position"/> is in <paramref name="range"/>: a
    /// range ends before the character at its end, as <c>WordAt</c> answers one, and a range of no width
    /// covers the character where it stands.</summary>
    private static bool Covers(CodeRange range, CodePosition position) =>
        range.IsEmpty ? position == range.Start : range.Start <= position && position < range.End;

    private static CodeRange Union(CodeRange a, CodeRange b) =>
        new(a.Start <= b.Start ? a.Start : b.Start, a.End >= b.End ? a.End : b.End);
}
