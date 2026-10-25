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
/// that called its answer incomplete is asked again. Each answer shows as it arrives, listed in the
/// order of <see cref="Providers"/> whatever the order the answers land in, so a slow provider holds
/// back no other (#620). An answer that arrives after its word was left, or after its own provider was
/// asked again, is dropped rather than trusted to have been cancelled.
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
    // Every edit made since the list opened, in the order made: a provider's range is in the document it
    // was asked about, and stands where the edits made after it carried it (see CodeCompletionAnswer.Asked).
    private readonly List<CodeEdit> _edits = [];
    // Two lifetimes: a REQUEST, each provider's newest, is cancelled when its provider is asked again (a
    // word's incomplete answer), and the LIST by closing, which is what a resolve of one of its items is
    // waiting on.
    private readonly List<CodeCompletionRequest> _requests = [];
    private CancellationTokenSource? _list;

    internal CodeCompletion(CodeEditorController editor)
    {
        _editor = editor;
        // An edit is heard before the selection it leaves, so the word's start already stands where the
        // edit carried it when the word is read again.
        editor.Changed += Carry;
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

    /// <summary>Whether a word is being completed and a provider asked for it has not answered yet.</summary>
    public bool IsLoading => _active && _requests.Any(request => !request.Answered);

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
        Open(WordStart(_editor.Caret), CodeCompletionTrigger.Invoked, null, Providers.ToList());
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
            Open(_editor.Caret, CodeCompletionTrigger.Character, typed, triggered);
            return;
        }

        if (!OpensAsYouType || !CodeDocument.IsWordChar(typed)) return;
        var caret = _editor.Caret;
        var start = WordStart(caret);
        var first = _editor.Document.Line(caret.Line)[start.Column];
        // A number is not a word anybody completes.
        if (!char.IsLetter(first) && first != '_') return;
        if (InCommentOrString(caret)) return;
        Open(start, CodeCompletionTrigger.Typing, typed, Providers.ToList());
    }

    /// <summary>Starts completing the word that begins at <paramref name="start"/>, dropping any
    /// list before it, and asks <paramref name="providers"/> for it.</summary>
    private void Open(CodePosition start, CodeCompletionTrigger trigger, char? character,
        List<ICodeCompletionProvider> providers)
    {
        Close(raise: false);
        _active = true;
        _edits.Clear();
        _start = start;
        _openedEmpty = start == _editor.Caret;
        _list = new CancellationTokenSource();
        Ask(trigger, character, providers, announce: true);
    }

    /// <summary>
    /// Asks <paramref name="providers"/> for the word, each with a request of its own: one still out for
    /// the same provider is cancelled, and another provider's goes on. Every provider is asked before any
    /// answer is applied, so they work at the same time.
    /// <para>
    /// A list opening (<paramref name="announce"/>) is heard of here, once its requests are out and
    /// before any answer lands, so <see cref="IsLoading"/> already says what is still out: it was heard
    /// of before its requests were made, and a list asked of providers that all answer late never said
    /// it was loading. A list asked again is heard of by whoever asked (a keystroke, an answer).
    /// </para>
    /// </summary>
    private void Ask(CodeCompletionTrigger trigger, char? character, List<ICodeCompletionProvider> providers,
        bool announce)
    {
        var document = _editor.Document;
        var position = _editor.Caret;
        var context = new CodeCompletionContext(trigger, _editor.Highlighter.Language) { Character = character };
        var asked = _edits.Count;
        var word = Word();

        var requests = new List<CodeCompletionRequest>();
        foreach (var provider in providers)
        {
            for (var i = _requests.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_requests[i].Provider, provider)) continue;
                var stale = _requests[i];
                _requests.RemoveAt(i);
                Cancel(stale.Cancellation);
            }
            var request = new CodeCompletionRequest(provider, word);
            _requests.Add(request);
            requests.Add(request);
        }
        if (announce) Changed?.Invoke();
        foreach (var request in requests) _ = AskOne(request, document, position, context, asked);
    }

    private async Task AskOne(CodeCompletionRequest request, CodeDocument document, CodePosition position,
        CodeCompletionContext context, int asked)
    {
        var list = CodeCompletionList.Empty;
        Exception? failure = null;
        try
        {
            list = await request.Provider.CompleteAsync(document, position, context, request.Cancellation.Token);
        }
        catch (OperationCanceledException) when (request.Cancellation.IsCancellationRequested)
        {
            // Cancelled because the answer was no longer wanted: its request is no longer the list's, and
            // the answer is dropped. One the provider threw on its own, with the request still wanted, is
            // its failure.
        }
        catch (Exception error)
        {
            failure = error;
        }

        UiThread.Run(() =>
        {
            if (failure is not null) Failed?.Invoke(failure);
            Deliver(request, new CodeCompletionAnswer(request.Provider, request.Word, asked, list));
        });
    }

    /// <summary>
    /// Takes the answer to <paramref name="request"/>, unless its provider was asked again or the list
    /// closed while it was on its way. It is listed where its provider stands in
    /// <see cref="Providers"/>, and the entry selected stays selected if it is still listed. With nothing
    /// offered by any answer and nothing more to ask for, the list closes once no provider owes one.
    /// </summary>
    private void Deliver(CodeCompletionRequest request, CodeCompletionAnswer answer)
    {
        if (!_active || request.Answered || _requests.IndexOf(request) < 0) return;
        request.Answered = true;

        var known = _answers.FindIndex(held => ReferenceEquals(held.Provider, answer.Provider));
        if (known >= 0) _answers.RemoveAt(known);
        // In the providers' order, whatever the order the answers land in: the ranking's last tie is that
        // order, and it must not depend on which provider was quicker.
        var rank = RankOf(answer.Provider);
        var at = 0;
        while (at < _answers.Count && RankOf(_answers[at].Provider) <= rank) at++;
        _answers.Insert(at, answer);

        // Nothing offered and nothing more to ask for: no list shows for this word. While a provider is
        // still out the list waits for it, and still takes this answer, which may withdraw every entry it
        // showed before.
        if (_answers.All(held => held.Offers.Count == 0 && !held.IsIncomplete) && !IsLoading)
        {
            Close(raise: true);
            return;
        }

        // The entry, not the offer: an answer that lands may bring its own copy of the entry selected,
        // which is the one listed when its provider comes first.
        var selected = _selected >= 0 ? EntryOf(_shown[_selected].Item) : null;
        Gather();
        Filter();
        if (selected is not null)
        {
            for (var i = 0; i < _shown.Count; i++)
            {
                if (EntryOf(_shown[i].Item) != selected) continue;
                _selected = i;
                break;
            }
        }
        AskAgainIfIncomplete();
        ResolveSelected();
        Changed?.Invoke();
    }

    /// <summary>Where <paramref name="provider"/> stands in <see cref="Providers"/>, or after all of
    /// them once it has been taken out.</summary>
    private int RankOf(ICodeCompletionProvider provider)
    {
        for (var i = 0; i < Providers.Count; i++)
            if (ReferenceEquals(Providers[i], provider)) return i;
        return Providers.Count;
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

    /// <summary>
    /// Asks again every provider whose answer was incomplete, once the word is not the one that provider
    /// was asked for LAST: the word of its answer, or of a request of its still out. Each provider keeps
    /// its own word, so a slow provider's answer for a word left behind is asked again, whoever was asked
    /// last. And a request still out for another word is replaced even when the answer in hand is for
    /// the word typed (a letter typed, then backspaced): it would land over that answer, with what it
    /// offers for the other word. The rest keep their answers.
    /// </summary>
    private void AskAgainIfIncomplete()
    {
        var word = Word();
        var incomplete = new List<ICodeCompletionProvider>();
        foreach (var answer in _answers)
        {
            if (answer.IsIncomplete && LastAskedWord(answer.Provider) != word) incomplete.Add(answer.Provider);
        }
        if (incomplete.Count > 0) Ask(CodeCompletionTrigger.Incomplete, null, incomplete, announce: false);
    }

    /// <summary>The word <paramref name="provider"/> was asked for last: that of its newest request,
    /// answered or still out, the one request of it the list holds.</summary>
    private string? LastAskedWord(ICodeCompletionProvider provider)
    {
        foreach (var request in _requests)
        {
            if (ReferenceEquals(request.Provider, provider)) return request.Word;
        }
        return null;
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
                var key = EntryOf(item);
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
            if (item.Replacing is { } asked)
            {
                var replacing = offer.Answer.Carried(asked, _edits);
                if (replacing.Start.Line == caret.Line && replacing.Start.Column != _start.Column
                    && replacing.Start.Column <= caret.Column)
                    typed = line.Substring(replacing.Start.Column, caret.Column - replacing.Start.Column);
            }

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

    /// <summary>What makes two offers one entry: one label and one inserted text, whichever provider
    /// offered them. The label's length leads, so that no label and inserted text run together into
    /// another pair's: "ab" inserting "c" and "a" inserting "bc" are two entries.</summary>
    private static string EntryOf(CodeCompletionItem item) =>
        item.Label.Length + ":" + item.Label + (item.InsertText ?? item.Label);

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

        // With several carets the entry goes in at each, replacing around each what it replaces around the
        // primary, as VS Code applies one suggestion at every cursor (#1012).
        var caret = _editor.Caret;
        if (_editor.HasOtherCarets && range.Start.Line == caret.Line && range.End.Line == caret.Line)
            return _editor.ReplaceAroundEveryCaret(caret.Column - range.Start.Column, range.End.Column - caret.Column, text);

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
        var requests = _requests.ToList();
        _requests.Clear();
        var list = _list;
        _list = null;
        var was = _active;
        _active = false;
        _edits.Clear();
        _answers.Clear();
        _candidates = [];
        _groups = 0;
        _items = [];
        _shown = [];
        _selected = -1;
        // Once the list is closed, so that what cancelling runs at once finds it closed.
        foreach (var request in requests) Cancel(request.Cancellation);
        Cancel(list);
        if (raise && was) Changed?.Invoke();
    }

    /// <summary>Cancels <paramref name="source"/>: a callback that throws is its provider's failure
    /// (<see cref="Failed"/>), and never the keystroke's that closed the list.</summary>
    private void Cancel(CancellationTokenSource? source) => ProviderRequest.Cancel(source, Failed);

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
            UiThread.Run(() => Failed?.Invoke(error));
            return;
        }

        UiThread.Run(() =>
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
    /// far. The provider's range stands where the edits made since it was asked carried it, and reaches
    /// the caret at least: what was typed at its end is in it, and a move of the caret alone moves
    /// nothing. Measured from the caret, an arrow moved the end with it, and accepting after one left the
    /// rest of the word behind; measured from the end of the line, a caret further on the line moved it.
    /// </summary>
    private CodeRange RangeOf(CodeCompletionOffer offer)
    {
        var caret = _editor.Caret;
        if (offer.Item.Replacing is { } asked)
        {
            var replacing = offer.Answer.Carried(asked, _edits);
            if (replacing.Start.Line == caret.Line && replacing.End.Line == caret.Line
                && replacing.Start.Column <= caret.Column)
                return new CodeRange(replacing.Start, replacing.End < caret ? caret : replacing.End);
        }
        return new CodeRange(_start, caret);
    }

    /// <summary>
    /// Carries the word's start through <paramref name="change"/>'s edits, and keeps them, for a provider's
    /// range to be carried through. An edit at another caret moves the word as an edit at the caret does:
    /// with a caret before it on its line, a character typed or deleted there moved the word, and the
    /// start, left where the word had been, took a character of it out of what was replaced (#1012's review).
    /// A document replaced whole is no edit: the editor dismisses the list before it replaces it.
    /// </summary>
    private void Carry(CodeChange change)
    {
        if (!_active) return;
        foreach (var edit in change.Edits)
        {
            _start = edit.CarryAnchor(_start);
            _edits.Add(edit);
        }
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
}
