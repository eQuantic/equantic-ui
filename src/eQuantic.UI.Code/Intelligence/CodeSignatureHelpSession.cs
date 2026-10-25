namespace eQuantic.UI.Code;

/// <summary>
/// The signature help of ONE editor: the panel that explains the call being written, while one shows, and
/// the requests that fill it. It is a session of the plan's fifth section, as the completion and the hover
/// are: a key typed, a move and an answer arriving are methods a test drives with no screen, and the editor
/// only draws the panel it holds.
/// <para>
/// The providers are the authority (<see cref="ICodeSignatureProvider"/>): they say which signature applies
/// and which argument the caret is on, and they close the panel by answering none once the caret has left
/// the call. The panel opens on a provider's trigger character or ⌘⇧Space, and while it shows every edit
/// and every move asks again after <see cref="Delay"/>, as VS Code asks, a retrigger character at once.
/// </para>
/// <para>
/// Latency decides the shape (the plan's sixth section). One request is out at a time, and an answer that
/// arrives after a newer request is dropped by its generation. Every provider is asked at once and each
/// answer applies as it lands: the panel shows the answer of the first provider, in the order of
/// <see cref="Providers"/>, that holds a signature, so a slow provider holds back no other.
/// </para>
/// </summary>
public sealed class CodeSignatureHelpSession
{
    private readonly CodeEditorController _editor;
    private int _generation;
    private CancellationTokenSource? _request;
    private CodeSignatureHelp? _help;
    private int _signature;
    // Whether the person picked the signature shown with the arrows, which a later answer of as many
    // signatures keeps.
    private bool _chosen;
    // The answers of the newest request, one slot per provider asked, and how many are still owed.
    private List<CodeSignatureHelp?> _answers = [];
    private int _owed;

    internal CodeSignatureHelpSession(CodeEditorController editor)
    {
        _editor = editor;
        // Every edit and every move ends in a selection change: while the panel shows, the providers are
        // asked again about where the caret is now.
        editor.SelectionChanged += _ => Moved();
        editor.Changed += Edited;
    }

    /// <summary>
    /// What explains the calls: a language service. None to begin with, and with none the panel never opens
    /// and its keys are the host's.
    /// </summary>
    public IList<ICodeSignatureProvider> Providers { get; } = [];

    /// <summary>How long an edit or a move waits, while the panel shows, before the providers are asked
    /// again: VS Code's <c>ParameterHintsModel.DEFAULT_DELAY</c>.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(120);

    /// <summary>Whether the panel shows.</summary>
    public bool IsOpen => _help is not null;

    /// <summary>What the panel shows: the answer of the first provider that holds a signature.</summary>
    public CodeSignatureHelp? Help => _help;

    /// <summary>The signature shown, an index into the answer's signatures: the provider's, or the one the
    /// person chose with the arrows.</summary>
    public int ActiveSignature => _signature;

    /// <summary>The signature shown, or null when the panel shows none.</summary>
    public CodeSignature? Signature =>
        _help is { } help && _signature >= 0 && _signature < help.Signatures.Count ? help.Signatures[_signature] : null;

    /// <summary>The argument the caret is on, an index into the signature's parameters: the signature's
    /// own, or the answer's, or -1 with no panel.</summary>
    public int ActiveParameter => Signature is { } signature
        ? signature.ActiveParameter ?? _help!.ActiveParameter
        : -1;

    /// <summary>Raised whenever the panel shows, closes, or shows something else.</summary>
    public event Action? Changed;

    /// <summary>Raised when a provider throws, with what it threw. The others' answers still show, so a
    /// failing provider is otherwise silent.</summary>
    public event Action<Exception>? Failed;

    /// <summary>Asks every provider about the caret, at once: ⌘⇧Space. False when there is none to ask.</summary>
    public bool Invoke()
    {
        if (Providers.Count == 0) return false;
        _ = Ask(new CodeSignatureContext(CodeSignatureTrigger.Invoked) { IsRetrigger = IsOpen, Active = Shown() },
            wait: false);
        return true;
    }

    /// <summary>Closes the panel, and drops whatever was asked for it. Answers whether a panel was showing,
    /// which is what Escape claims.</summary>
    public bool Dismiss()
    {
        _generation++;
        ProviderRequest.Cancel(_request, Failed);
        _request = null;
        _answers = [];
        _owed = 0;
        if (_help is null) return false;
        _help = null;
        _signature = 0;
        _chosen = false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Shows the signature <paramref name="delta"/> after the one shown, round from the last to the
    /// first and back: ↓ and ↑. False with fewer than two, so an arrow then moves the caret.</summary>
    public bool Move(int delta)
    {
        if (_help is not { } help || help.Signatures.Count < 2) return false;
        var count = help.Signatures.Count;
        _signature = ((_signature + delta) % count + count) % count;
        _chosen = true;
        Changed?.Invoke();
        return true;
    }

    // ---- what the editor did ----------------------------------------------------------------------

    /// <summary>
    /// What typing <paramref name="typed"/> asks, the editor calls once the character is in the document: a
    /// provider's trigger character, or a retrigger character while the panel shows, asks every provider at
    /// once, with the character in the context. Every provider, as VS Code's <c>provideSignatureHelp</c>
    /// asks every one it holds: the trigger says WHEN to ask, and each provider reads the call itself. The
    /// move the character made has already asked after the pause, which this request replaces.
    /// </summary>
    internal void Typed(char typed)
    {
        if (Providers.Count == 0) return;
        var triggered = false;
        var retriggered = false;
        foreach (var provider in Providers)
        {
            triggered |= provider.TriggerCharacters.Contains(typed);
            retriggered |= provider.RetriggerCharacters.Contains(typed);
        }
        if (!triggered && !(retriggered && IsOpen)) return;
        _ = Ask(new CodeSignatureContext(CodeSignatureTrigger.Character)
        {
            Character = typed,
            IsRetrigger = IsOpen,
            Active = Shown(),
        }, wait: false);
    }

    /// <summary>While the panel shows, or a request for it is out, a move or an edit asks again after the
    /// pause.</summary>
    private void Moved()
    {
        if (_help is null && _request is null) return;
        _ = Ask(new CodeSignatureContext(CodeSignatureTrigger.ContentChange) { IsRetrigger = IsOpen, Active = Shown() },
            wait: true);
    }

    /// <summary>A document replaced whole closes the panel: no call in the old one is in the new one.</summary>
    private void Edited(CodeChange change)
    {
        if (change.Replaced) Dismiss();
    }

    /// <summary>What the panel shows, with the signature shown as its active one.</summary>
    private CodeSignatureHelp? Shown() => _help is { } help ? help with { ActiveSignature = _signature } : null;

    // ---- asking -----------------------------------------------------------------------------------

    /// <summary>
    /// Asks every provider, after the pause when <paramref name="wait"/> says so. Whatever was waiting or out
    /// is cancelled first, so one request is out; an answer applies only while its generation is the newest.
    /// </summary>
    private async Task Ask(CodeSignatureContext context, bool wait)
    {
        var generation = ++_generation;
        ProviderRequest.Cancel(_request, Failed);
        var request = new CancellationTokenSource();
        _request = request;
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
            // Back where the editor lives: a timer ends the wait on a thread of its own, and the generation,
            // the document and the caret are the UI thread's.
            UiThread.Run(() =>
            {
                if (waited && generation == _generation) AskEvery(context, generation, request.Token);
            });
            return;
        }
        AskEvery(context, generation, request.Token);
    }

    private void AskEvery(CodeSignatureContext context, int generation, CancellationToken cancellation)
    {
        var document = _editor.Document;
        var position = _editor.Caret;
        var providers = Providers.ToList();
        if (providers.Count == 0)
        {
            // Nobody left to ask: the panel closes, as it does when every provider answers none.
            _answers = [];
            _owed = 0;
            Apply();
            return;
        }
        _answers = [];
        foreach (var _ in providers) _answers.Add(null);
        _owed = providers.Count;
        for (var slot = 0; slot < providers.Count; slot++)
            _ = AskOne(slot, providers[slot], document, position, context, generation, cancellation);
    }

    private async Task AskOne(int slot, ICodeSignatureProvider provider, CodeDocument document,
        CodePosition position, CodeSignatureContext context, int generation, CancellationToken cancellation)
    {
        CodeSignatureHelp? answer = null;
        Exception? failure = null;
        try
        {
            answer = await provider.SignatureHelpAsync(document, position, context, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Cancelled because a newer request replaced this one: its generation is gone.
        }
        catch (Exception error)
        {
            failure = error;
        }
        UiThread.Run(() =>
        {
            if (failure is not null) Failed?.Invoke(failure);
            if (generation != _generation) return;
            _answers[slot] = answer is { Signatures.Count: > 0 } ? answer : null;
            _owed--;
            Apply();
        });
    }

    /// <summary>
    /// Shows the answer of the first provider, in the order of <see cref="Providers"/>, that holds a
    /// signature, or closes the panel once every provider has answered and none does.
    /// </summary>
    private void Apply()
    {
        CodeSignatureHelp? first = null;
        foreach (var answer in _answers)
        {
            if (answer is null) continue;
            first = answer;
            break;
        }
        if (first is null)
        {
            if (_owed > 0) return;
            // Every provider answered none: nothing is out any more, and a move asks nothing until the next
            // trigger.
            _request = null;
            if (_help is null) return;
            _help = null;
            _signature = 0;
            _chosen = false;
            Changed?.Invoke();
            return;
        }
        if (_owed == 0) _request = null;
        // The person's choice holds while the call has as many signatures; the provider's otherwise.
        var keep = _chosen && _help is { } shown && shown.Signatures.Count == first.Signatures.Count;
        if (!keep)
        {
            _signature = Math.Max(0, Math.Min(first.Signatures.Count - 1, first.ActiveSignature));
            _chosen = false;
        }
        _help = first;
        Changed?.Invoke();
    }
}
