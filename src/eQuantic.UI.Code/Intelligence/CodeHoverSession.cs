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
    // What a wait under way is for: the pointer resting anywhere in it goes on waiting.
    private CodeRange? _pending;
    private bool _open;
    private bool _byKeyboard;
    private CodeRange _range;
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
    }

    private void CompletionChanged()
    {
        if (_editor.Completion.IsOpen) Dismiss();
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
        var at = _editor.Document.Clamp(position);
        var explained = Explained(at);
        if (explained is null && Providers.Count == 0) return false;
        _ = Rest(explained ?? new CodeRange(at), at, wait: false, keyboard: true, ask: true);
        return true;
    }

    /// <summary>Closes the card, and drops what was being waited for. Answers whether a card was
    /// showing, which is what Escape claims.</summary>
    public bool Dismiss()
    {
        _generation++;
        _request?.Cancel();
        _request = null;
        _pending = null;
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
        if (_open && !_byKeyboard && Holds(_range, position)) return;
        if (_pending is { } pending && Holds(pending, position)) return;
        if (Explained(position) is not { } explained)
        {
            PointerLeft();
            return;
        }
        if (_open && !_byKeyboard) Dismiss();
        _pending = explained;
        _ = Rest(explained, position, wait: true, keyboard: false, ask: true);
    }

    /// <summary>The pointer left the surface, or rests on nothing a card explains: a wait ends, and a
    /// card the pointer asked for closes. One the keyboard asked for stays.</summary>
    internal void PointerLeft()
    {
        if (_open && _byKeyboard)
        {
            _pending = null;
            return;
        }
        Dismiss();
    }

    /// <summary>The card of the problems at <paramref name="position"/>, at once and with no provider
    /// asked: what F8 shows after it put the caret on a problem.</summary>
    internal void ShowProblems(CodePosition position)
    {
        var explained = Explained(position) ?? new CodeRange(position);
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
        var generation = ++_generation;
        _request?.Cancel();
        var request = new CancellationTokenSource();
        _request = request;
        if (wait && Delay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(Delay, request.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (generation != _generation) return;
        }

        _pending = null;
        var document = _editor.Document;
        var providers = ask ? Providers.ToList() : new List<ICodeHoverProvider>();
        _range = explained;
        _problems = _editor.Diagnostics.At(position).ToList();
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
            _ = AskOne(slot, providers[slot], document, position, generation, request.Token);
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
    /// the character there, with the range of every problem that holds it, or null when the character is
    /// white space and no problem holds it.
    /// </summary>
    private CodeRange? Explained(CodePosition position)
    {
        var document = _editor.Document;
        var line = document.Line(position.Line);
        CodeRange? explained = position.Column < line.Length && !char.IsWhiteSpace(line[position.Column])
            ? document.WordAt(position)
            : null;
        foreach (var problem in _editor.Diagnostics.At(position))
            explained = explained is { } before ? Union(before, problem.Range) : problem.Range;
        return explained;
    }

    private static bool Holds(CodeRange range, CodePosition position) =>
        range.Start <= position && position <= range.End;

    private static CodeRange Union(CodeRange a, CodeRange b) =>
        new(a.Start <= b.Start ? a.Start : b.Start, a.End >= b.End ? a.End : b.End);
}
