using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// The completion of ONE editor: the providers it asks, and the list while one is open (the word
/// it completes, what that word matches, which entry is selected). It is the session of the plan's
/// fifth section: the keymap hands ↑, ↓, Enter, Tab and Escape here while a list shows, and every
/// behaviour is a method that a test drives with keystrokes and no screen.
/// <para>
/// Latency decides the shape (the plan's sixth section). A list asks its providers ONCE, when a word
/// starts: a trigger character, a word started by typing, ⌃Space. Every keystroke after that filters
/// and ranks what they answered, here and at once (<see cref="CodeFuzzyMatch"/>), and only a provider
/// that called its answer incomplete is asked again. An answer that arrives after its word was left,
/// or after a newer request, is dropped by its generation rather than trusted to have been cancelled.
/// </para>
/// <para>
/// An editor with no language service still completes, from the language's own words
/// (<see cref="CodeKeywordCompletionProvider"/>) and the document's
/// (<see cref="CodeWordCompletionProvider"/>). <see cref="Providers"/> starts EMPTY, so an editor
/// that nothing draws a list for never routes a key to one: whoever shows the list says what it
/// offers.
/// </para>
/// </summary>
public sealed class CodeCompletion
{
    private readonly CodeEditorController _editor;
    private readonly List<CodeCompletionAnswer> _answers = [];
    // Every offer of every answer, in the order the providers answered, and how many groups of
    // offers that are one entry they make (see Gather).
    private List<CodeCompletionOffer> _candidates = [];
    private int _groups;
    private List<CodeCompletionMatch> _items = [];
    private List<CodeCompletionOffer> _shown = [];
    private int _selected = -1;
    private bool _active;
    private CodePosition _start;
    private bool _openedEmpty;
    private string _askedWord = "";
    private int _generation;
    // Two lifetimes: a REQUEST is cancelled by the next one (a word's incomplete answer asked again),
    // and the LIST by closing, which is what a resolve of one of its items is waiting on.
    private CancellationTokenSource? _request;
    private CancellationTokenSource? _list;

    internal CodeCompletion(CodeEditorController editor)
    {
        _editor = editor;
        // Every edit and every move ends in a selection change, typed or not: the word is read
        // again, and the list follows it or closes.
        editor.SelectionChanged += _ => Follow();
    }

    /// <summary>
    /// The providers this editor asks, in the order their answers are listed when they rank the same.
    /// None to begin with, and with none no list opens.
    /// </summary>
    public IList<ICodeCompletionProvider> Providers { get; } = [];

    /// <summary>Whether starting a word opens the list, as a code editor's does. Off, only a
    /// trigger character and ⌃Space open it.</summary>
    public bool OpensAsYouType { get; set; } = true;

    /// <summary>How many entries PageUp and PageDown step: the view sets it to the rows it shows.</summary>
    public int PageSize { get; set; } = 12;

    /// <summary>Whether a list is showing: a word is being completed and something matches it.</summary>
    public bool IsOpen => _active && _items.Count > 0;

    /// <summary>Whether a word is being completed and no provider has answered for it yet.</summary>
    public bool IsLoading => _active && _answers.Count == 0;

    /// <summary>What matches the word, best first.</summary>
    public IReadOnlyList<CodeCompletionMatch> Items => _items;

    /// <summary>The entry Enter accepts, as an index into <see cref="Items"/>, or -1.</summary>
    public int Selected => _selected;

    /// <summary>Where the word being completed starts: where a list lines its labels up.</summary>
    public CodePosition Start => _start;

    /// <summary>Raised whenever what the list shows changes: it opened or closed, an answer arrived,
    /// the word filtered it, the selection moved, or an item was resolved.</summary>
    public event Action? Changed;

    /// <summary>
    /// Raised when a provider throws, with what it threw. The list still shows what the others
    /// offered, so a failing provider is otherwise silent. A callback a provider registered on its
    /// cancellation throws when the list cancels it, and arrives as the <c>AggregateException</c>
    /// cancelling gathers it in.
    /// </summary>
    public event Action<Exception>? Failed;

    // ---- opening ----------------------------------------------------------------------------

    /// <summary>
    /// Asks every provider for the word before the caret, or for the caret itself when no word is
    /// before it: ⌃Space. False when the editor reads only or has no provider.
    /// </summary>
    public bool Invoke()
    {
        if (_editor.ReadOnly || Providers.Count == 0) return false;
        Open(WordStart(_editor.Caret));
        _ = Ask(CodeCompletionTrigger.Invoked, null, Providers.ToList());
        return true;
    }

    /// <summary>
    /// What typing <paramref name="typed"/> opens, the editor calls once the character is in the
    /// document: a provider's trigger character asks that provider alone, after the character; a
    /// word started outside a comment or a string asks every provider, from the word's start.
    /// </summary>
    internal void Typed(char typed)
    {
        if (_editor.ReadOnly || _active || Providers.Count == 0 || !_editor.Selection.IsEmpty) return;

        var triggered = new List<ICodeCompletionProvider>();
        foreach (var provider in Providers)
        {
            if (provider.TriggerCharacters.Contains(typed)) triggered.Add(provider);
        }
        if (triggered.Count > 0)
        {
            Open(_editor.Caret);
            _ = Ask(CodeCompletionTrigger.Character, typed, triggered);
            return;
        }

        if (!OpensAsYouType || !CodeDocument.IsWordChar(typed)) return;
        var caret = _editor.Caret;
        var start = WordStart(caret);
        var first = _editor.Document.Line(caret.Line)[start.Column];
        // A number is not a word anybody completes.
        if (!char.IsLetter(first) && first != '_') return;
        if (InCommentOrString(caret)) return;
        Open(start);
        _ = Ask(CodeCompletionTrigger.Typing, typed, Providers.ToList());
    }

    /// <summary>Starts completing the word that begins at <paramref name="start"/>, dropping any
    /// list before it.</summary>
    private void Open(CodePosition start)
    {
        Close(raise: false);
        _active = true;
        _start = start;
        _openedEmpty = start == _editor.Caret;
        _list = new CancellationTokenSource();
        Changed?.Invoke();
    }

    private async Task Ask(CodeCompletionTrigger trigger, char? character, List<ICodeCompletionProvider> providers)
    {
        var generation = ++_generation;
        Cancel(_request);
        var cancellation = new CancellationTokenSource();
        _request = cancellation;
        var document = _editor.Document;
        var position = _editor.Caret;
        var context = new CodeCompletionContext(trigger, _editor.Highlighter.Language) { Character = character };
        _askedWord = Word();

        // Every provider is asked before any is waited for, so they work at the same time.
        var asked = new List<Task<CodeCompletionList>?>();
        var errors = new List<Exception>();
        foreach (var provider in providers)
        {
            try
            {
                asked.Add(provider.CompleteAsync(document, position, context, cancellation.Token));
            }
            catch (Exception error)
            {
                asked.Add(null);
                errors.Add(error);
            }
        }

        var answers = new List<CodeCompletionAnswer>();
        for (var i = 0; i < providers.Count; i++)
        {
            var list = CodeCompletionList.Empty;
            if (asked[i] is { } answer)
            {
                try
                {
                    list = await answer;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    // Cancelled because the answer was no longer wanted: the generation drops it. One
                    // the provider threw on its own, with this request still wanted, is its failure.
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            answers.Add(new CodeCompletionAnswer(providers[i], document.Line(position.Line).Length, list));
        }

        OnUiThread(() =>
        {
            foreach (var error in errors) Failed?.Invoke(error);
            Deliver(generation, answers);
        });
    }

    /// <summary>Takes the answers of the request numbered <paramref name="generation"/>, unless a
    /// newer one was made or the list closed while they were on their way.</summary>
    private void Deliver(int generation, List<CodeCompletionAnswer> answers)
    {
        if (generation != _generation || !_active) return;

        foreach (var answer in answers)
        {
            var at = _answers.FindIndex(known => ReferenceEquals(known.Provider, answer.Provider));
            if (at >= 0) _answers[at] = answer;
            else _answers.Add(answer);
        }

        // Nothing offered and nothing more to ask for: no list will show for this word.
        if (_answers.All(answer => answer.Offers.Count == 0 && !answer.IsIncomplete))
        {
            Close(raise: true);
            return;
        }

        Gather();
        Filter();
        AskAgainIfIncomplete();
        ResolveSelected();
        Changed?.Invoke();
    }

    // ---- following the word -----------------------------------------------------------------

    /// <summary>
    /// The word as it stands after an edit or a move: the list filters by it, or closes once the
    /// caret has left it (another line, before its start, a selection, or a character that ends a
    /// word between its start and the caret). A word typed away to nothing closes it too, unless the
    /// list opened with nothing typed (after a dot, or ⌃Space between two words), where an empty
    /// word is where it began. An answer the provider called incomplete is asked for again whenever
    /// the word changed.
    /// </summary>
    private void Follow()
    {
        if (!_active) return;
        var caret = _editor.Caret;
        if (!_editor.Selection.IsEmpty || caret.Line != _start.Line || caret.Column < _start.Column
            || (caret.Column == _start.Column && !_openedEmpty))
        {
            Close(raise: true);
            return;
        }
        var word = Word();
        foreach (var c in word)
        {
            if (CodeDocument.IsWordChar(c)) continue;
            Close(raise: true);
            return;
        }
        if (_answers.Count == 0) return;

        Filter();
        AskAgainIfIncomplete();
        ResolveSelected();
        Changed?.Invoke();
    }

    /// <summary>Asks again every provider whose answer was incomplete, once the word is no longer the
    /// one it answered for. The rest keep their answers.</summary>
    private void AskAgainIfIncomplete()
    {
        if (Word() == _askedWord) return;
        var incomplete = new List<ICodeCompletionProvider>();
        foreach (var answer in _answers)
        {
            if (answer.IsIncomplete) incomplete.Add(answer.Provider);
        }
        if (incomplete.Count > 0) _ = Ask(CodeCompletionTrigger.Incomplete, null, incomplete);
    }

    /// <summary>
    /// Lays out what the answers offer for <see cref="Filter"/>, once per answer rather than once per
    /// keystroke: every offer in the order the providers answered, its sort key, case aside, and its
    /// group, the offers that are one entry (one label and one inserted text) whichever provider
    /// offered them.
    /// </summary>
    private void Gather()
    {
        _candidates = [];
        var groups = new Dictionary<string, int>();
        foreach (var answer in _answers)
        {
            foreach (var offer in answer.Offers)
            {
                var item = offer.Item;
                // The label's length leads, so that no label and inserted text run together into
                // another pair's: "ab" inserting "c" and "a" inserting "bc" are two entries.
                var key = item.Label.Length + ":" + item.Label + (item.InsertText ?? item.Label);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = groups.Count;
                    groups[key] = group;
                }
                offer.Group = group;
                offer.SortKey = (item.SortText ?? item.Label).ToLowerInvariant();
                _candidates.Add(offer);
            }
        }
        _groups = groups.Count;
    }

    /// <summary>
    /// Matches every offer against the word typed over its range, keeps one of each group (the first
    /// provider's copy THAT MATCHES: a copy that does not must not hide one that does), and ranks
    /// them: the better match first, then the sort text (or the label) case aside, then the label,
    /// then the order the providers answered in. Selects the first entry, or the first one a provider
    /// preselected among those that match best.
    /// </summary>
    private void Filter()
    {
        var line = _editor.Document.Line(_start.Line);
        var caret = _editor.Caret;
        var word = Word();

        var matches = new List<CodeCompletionMatch>();
        var offers = new List<CodeCompletionOffer>();
        var listed = new bool[_groups];
        foreach (var offer in _candidates)
        {
            if (listed[offer.Group]) continue;
            var item = offer.Item;

            var typed = word;
            if (item.Replacing is { } replacing && replacing.Start.Line == caret.Line
                && replacing.Start.Column != _start.Column && replacing.Start.Column <= caret.Column)
                typed = line.Substring(replacing.Start.Column, caret.Column - replacing.Start.Column);

            var filter = item.FilterText ?? item.Label;
            if (CodeFuzzyMatch.Of(typed, filter) is not { } match) continue;
            listed[offer.Group] = true;
            var highlights = item.FilterText is null || item.FilterText == item.Label
                ? match.Positions
                : CodeFuzzyMatch.Of(typed, item.Label, anywhere: true)?.Positions ?? [];

            matches.Add(new CodeCompletionMatch(item, match.Score, highlights));
            offers.Add(offer);
        }

        var order = new List<int>();
        for (var i = 0; i < matches.Count; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            if (matches[a].Score != matches[b].Score) return matches[b].Score - matches[a].Score;
            var byKey = string.CompareOrdinal(offers[a].SortKey, offers[b].SortKey);
            if (byKey != 0) return byKey;
            var byLabel = string.CompareOrdinal(matches[a].Item.Label, matches[b].Item.Label);
            return byLabel != 0 ? byLabel : a - b;
        });

        _items = [];
        _shown = [];
        foreach (var i in order)
        {
            _items.Add(matches[i]);
            _shown.Add(offers[i]);
        }

        _selected = _items.Count > 0 ? 0 : -1;
        for (var i = 0; i < _items.Count && _items[i].Score == _items[0].Score; i++)
        {
            if (!_items[i].Item.Preselect) continue;
            _selected = i;
            break;
        }
    }

    // ---- the list's keys --------------------------------------------------------------------

    /// <summary>
    /// Moves the selection by <paramref name="delta"/> entries, round from the last to the first and
    /// back: ↓ and ↑. False with fewer than two entries, so an arrow with one item showing moves the
    /// caret instead, as it does in VS Code.
    /// </summary>
    public bool Move(int delta)
    {
        if (!IsOpen || _items.Count < 2) return false;
        var count = _items.Count;
        _selected = ((_selected + delta) % count + count) % count;
        ResolveSelected();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Moves the selection by <paramref name="pages"/> of <see cref="PageSize"/> entries,
    /// stopping at either end: PageDown and PageUp.</summary>
    public bool MovePage(int pages)
    {
        if (!IsOpen || _items.Count < 2) return false;
        _selected = Math.Max(0, Math.Min(_items.Count - 1, _selected + pages * PageSize));
        ResolveSelected();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Selects the entry at <paramref name="index"/>: a pointer over the list.</summary>
    public bool Select(int index)
    {
        if (!IsOpen || index < 0 || index >= _items.Count) return false;
        _selected = index;
        ResolveSelected();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Puts the selected entry in the document, over the word typed (or the provider's own range,
    /// see <see cref="CodeCompletionItem.Replacing"/>), and closes the list with the caret after
    /// it. One edit, so one undo gives back the word as it was typed: Enter and Tab.
    /// </summary>
    public bool Accept()
    {
        if (!IsOpen || _selected < 0) return false;
        var offer = _shown[_selected];
        var range = RangeOf(offer);
        var text = offer.Item.InsertText ?? offer.Item.Label;
        Close(raise: true);

        // Accepting what is already there is a step that changes nothing, and nothing to undo.
        if (_editor.Document.TextIn(range) == text)
        {
            _editor.Selection = new CodeRange(range.End);
            return true;
        }
        _editor.Apply(range, text);
        return true;
    }

    /// <summary>
    /// Whether accepting the selected entry would change the text, which is when Enter accepts it.
    /// A word typed out in full and a list still showing it is a line to end, not a completion to
    /// take, so Enter ends the line there, which VS Code does only when asked to (its "smart").
    /// </summary>
    public bool AcceptChangesText
    {
        get
        {
            if (!IsOpen || _selected < 0) return false;
            var offer = _shown[_selected];
            return _editor.Document.TextIn(RangeOf(offer)) != (offer.Item.InsertText ?? offer.Item.Label);
        }
    }

    /// <summary>Whether typing <paramref name="typed"/> accepts the selected entry first (its
    /// <see cref="CodeCompletionItem.CommitCharacters"/>).</summary>
    internal bool AcceptsOn(char typed) =>
        IsOpen && _selected >= 0 && _shown[_selected].Item.CommitCharacters is { } characters
        && characters.Contains(typed);

    /// <summary>Closes the list, and drops whatever is still on its way: Escape. False when nothing
    /// was being completed.</summary>
    public bool Dismiss()
    {
        if (!_active) return false;
        Close(raise: true);
        return true;
    }

    private void Close(bool raise)
    {
        // A request still out is no longer wanted: its answer, and every resolve, is dropped.
        _generation++;
        var request = _request;
        var list = _list;
        _request = null;
        _list = null;
        var was = _active;
        _active = false;
        _answers.Clear();
        _candidates = [];
        _groups = 0;
        _items = [];
        _shown = [];
        _selected = -1;
        // Once the list is closed, so that what cancelling runs at once finds it closed.
        Cancel(request);
        Cancel(list);
        if (raise && was) Changed?.Invoke();
    }

    /// <summary>
    /// Cancels <paramref name="source"/>, which runs at once, on this thread, every callback a
    /// provider registered on its token. One that throws is that provider's failure, reported like
    /// any other (<see cref="Failed"/>), and never the keystroke's: cancelling gathers what the
    /// callbacks threw into one <c>AggregateException</c> after running them all, and it escaped into
    /// the edit that had closed the list.
    /// </summary>
    private void Cancel(CancellationTokenSource? source)
    {
        if (source is null) return;
        try
        {
            source.Cancel();
        }
        catch (Exception error)
        {
            Failed?.Invoke(error);
        }
    }

    // ---- resolving ------------------------------------------------------------------------------

    /// <summary>Asks the selected entry's provider, once, for what it left out of it.</summary>
    private void ResolveSelected()
    {
        if (_selected < 0 || _list is null) return;
        var offer = _shown[_selected];
        if (offer.Resolving) return;
        offer.Resolving = true;
        _ = Resolve(offer, _list.Token);
    }

    private async Task Resolve(CodeCompletionOffer offer, CancellationToken cancellation)
    {
        CodeCompletionItem resolved;
        try
        {
            resolved = await offer.Answer.Provider.ResolveAsync(offer.Item, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The list it was resolving for closed. Thrown with the list still open, it is the
            // provider's failure, and goes on to the clause below.
            return;
        }
        catch (Exception error)
        {
            OnUiThread(() => Failed?.Invoke(error));
            return;
        }

        OnUiThread(() =>
        {
            // The list it was resolved for may be gone, and the provider may have had nothing to add.
            if (!_active || ReferenceEquals(resolved, offer.Item)) return;
            offer.Item = resolved;
            var at = _shown.IndexOf(offer);
            if (at < 0) return;
            _items[at] = _items[at] with { Item = resolved };
            Changed?.Invoke();
        });
    }

    // ---- what the word is -------------------------------------------------------------------------

    /// <summary>The word typed so far: from where it starts to the caret.</summary>
    private string Word()
    {
        var caret = _editor.Caret;
        if (caret.Line != _start.Line || caret.Column < _start.Column) return "";
        return _editor.Document.Line(caret.Line).Substring(_start.Column, caret.Column - _start.Column);
    }

    /// <summary>Where the word that ends at <paramref name="caret"/> starts: the caret itself when
    /// the character before it ends no word.</summary>
    private CodePosition WordStart(CodePosition caret)
    {
        var line = _editor.Document.Line(caret.Line);
        var column = caret.Column;
        while (column > 0 && CodeDocument.IsWordChar(line[column - 1])) column--;
        return new CodePosition(caret.Line, column);
    }

    /// <summary>
    /// The range accepting <paramref name="offer"/> replaces: the provider's own, or the word typed so
    /// far. The provider's range ends as far from the END of its line as it did when the provider was
    /// asked: what was typed or deleted since happened at the caret, before that end, and a move of
    /// the caret alone moves nothing. Measured from the caret, an arrow moved the end with it, and
    /// accepting after one left the rest of the word behind.
    /// </summary>
    private CodeRange RangeOf(CodeCompletionOffer offer)
    {
        var caret = _editor.Caret;
        var line = _editor.Document.Line(caret.Line);
        if (offer.Item.Replacing is { } replacing && replacing.Start.Line == caret.Line
            && replacing.End.Line == caret.Line && replacing.Start.Column <= caret.Column)
        {
            var fromLineEnd = Math.Max(0, offer.Answer.AskedLineLength - replacing.End.Column);
            return new CodeRange(replacing.Start, new CodePosition(caret.Line, Math.Max(caret.Column, line.Length - fromLineEnd)));
        }
        return new CodeRange(_start, caret);
    }

    /// <summary>Whether the character before <paramref name="caret"/> is in a comment or a string,
    /// where a word started is prose rather than code, and no list opens unasked.</summary>
    private bool InCommentOrString(CodePosition caret)
    {
        if (caret.Column == 0) return false;
        foreach (var token in _editor.Highlighter.TokensFor(_editor.Document, caret.Line))
        {
            if (token.Start <= caret.Column - 1 && caret.Column - 1 < token.End)
                return token.Kind is CodeTokenKind.Comment or CodeTokenKind.String;
        }
        return false;
    }

    /// <summary>
    /// Runs <paramref name="work"/> where the editor lives. An answer that a provider completed on
    /// another thread is POSTED to the UI thread, as <c>SetState</c> posts a mutation, because the list
    /// it changes is the one the render thread is reading; where there is one thread (the browser) or
    /// the answer came back on the UI thread, it runs at once.
    /// </summary>
    private static void OnUiThread(Action work)
    {
        if (UiDispatcher.Current is { IsOnUiThread: false } dispatcher) dispatcher.Post(work);
        else work();
    }
}
