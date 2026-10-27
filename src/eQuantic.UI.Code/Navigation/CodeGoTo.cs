using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// The go-to field of ONE editor (#1083): ⌃G to a line and ⇧⌘O to a symbol, as VS Code's quick access has
/// them. It is a session of the plan's fifth section: what is typed, what it lists, the entry selected and
/// what accepting does are methods a test drives with no screen, and the editor draws the field it holds.
/// <para>
/// The text holds its prefix, as VS Code's quick open does: <c>:</c> reads a line (<see cref="Line"/>), and
/// <c>@</c> filters the document's symbols (<see cref="Entries"/>), so deleting one prefix and typing the
/// other goes from one to the other. While a line or a symbol is read, the code is centred on it and its
/// lines are washed (<see cref="Preview"/>), and the selection does not move. Accepting selects it, centres
/// it and records a jump in the <see cref="CodeJumpList"/>; closing gives back the view exactly as it was
/// when the field opened. Either gives the code the keyboard back.
/// </para>
/// <para>
/// The symbols are asked once each time the field turns to them (<see cref="CodeSymbols"/>) and flattened
/// as VS Code's outline flattens them: in the order their ranges start, a symbol before those it holds, each
/// carrying the name of the one that holds it. What is typed after <c>@</c> filters them by its first word
/// against the name and by the others against the container, with the completion's matcher
/// (<see cref="CodeFuzzyMatch"/>), from anywhere in the name. With nothing typed, the symbol the caret stands
/// in is selected.
/// </para>
/// <para>
/// It also goes where a name is defined and used (#1084), as VS Code's <c>SymbolNavigationAction</c> does:
/// <see cref="Definition"/> and <see cref="References"/> ask the language services at the caret, go to a single
/// answer, list several in the field (<see cref="CodeGoToMode.Definitions"/>, <see cref="CodeGoToMode.References"/>,
/// modes its text does not choose and only filters), and say when there is none (<see cref="NotFound"/>). Going
/// to a place selects its start, records a jump and brings it near the top of the view; a place in another
/// document is handed to the app (<see cref="CodeEditorController.OpenRequested"/>). The places of the last list
/// are kept, carried through every edit, for <see cref="NextPlace"/> and <see cref="PreviousPlace"/> (F4, ⇧F4)
/// to walk once it is closed.
/// </para>
/// </summary>
public sealed class CodeGoTo
{
    private readonly CodeEditorController _editor;
    private bool _open;
    private string _query = "";
    private CodeGoToMode _mode;
    private CodeGoToLine? _line;
    private CodePosition? _lineTarget;
    private List<CodeGoToEntry> _entries = [];
    private int _selected = -1;
    private CodeRange? _preview;
    private CodePosition? _revealed;
    // The view when the field opened, which closing gives back.
    private Rect _view;
    // The symbols of the latest answer, flattened, each with the name of the one that holds it.
    private readonly List<CodeSymbol> _symbols = [];
    private readonly List<string?> _containers = [];
    private bool _asking;
    // Whether the symbol the caret stands in is selected: from the field turning to symbols until the
    // text is first changed, as VS Code selects it only when its list first shows.
    private bool _enclose;
    private int _generation;
    private CancellationTokenSource? _request;
    // The places of the last list, which a walk goes through once it is closed, carried through every edit.
    private readonly List<CodeLocation> _places = [];
    // The place last gone to in _places, or the one nearest the caret when the list opened; -1 with none.
    private int _placeAt = -1;
    // The place nearest the caret when the list opened, which it selects with nothing typed.
    private int _nearest = -1;
    // Which of _places each entry shows, in the order of _entries.
    private readonly List<int> _entryPlaces = [];
    // The place whose row stays selected as the rows are made again, or -1: an edit while the list shows.
    private int _keepPlace = -1;
    private CodeDefinitionKind _kind;
    private CodeGoToNotFound? _notFound;
    // The places asked for and not answered yet: a request of its own, dropped when the caret moves or the text
    // changes, as VS Code drops one (CodeEditorStateFlag.Value | Position).
    private int _placesGeneration;
    private CancellationTokenSource? _placesRequest;

    internal CodeGoTo(CodeEditorController editor)
    {
        _editor = editor;
        editor.Changed += Carry;
        editor.SelectionChanged += _ => Moved();
    }

    /// <summary>Whether the field shows.</summary>
    public bool IsOpen => _open;

    /// <summary>
    /// What the field holds, its prefix included. Setting it reads it again, as typing does: a line is read
    /// and the code follows it, the symbols are filtered, and a new prefix changes what the field goes to.
    /// Setting it while the field is closed does nothing.
    /// </summary>
    public string Query
    {
        get => _query;
        set
        {
            if (!_open || value == _query) return;
            Read(value, opening: false);
        }
    }

    /// <summary>What the text goes to, by its first character.</summary>
    public CodeGoToMode Mode => _mode;

    /// <summary>The line the text reads, in <see cref="CodeGoToMode.Line"/>, or null when it reads none.</summary>
    public CodeGoToLine? Line => _line;

    /// <summary>What the field lists: the symbols the text matches, in <see cref="CodeGoToMode.Symbol"/>, the best
    /// first, or the places the text matches, in <see cref="CodeGoToMode.Definitions"/> and
    /// <see cref="CodeGoToMode.References"/>, in the document's order.</summary>
    public IReadOnlyList<CodeGoToEntry> Entries => _entries;

    /// <summary>What the definitions listed answer, in <see cref="CodeGoToMode.Definitions"/>: where the name is
    /// defined, declared, typed or implemented.</summary>
    public CodeDefinitionKind DefinitionKind => _kind;

    /// <summary>The places of the last list, which <see cref="NextPlace"/> and <see cref="PreviousPlace"/> walk,
    /// carried through every edit. Empty until a list is shown, and again when the document is replaced.</summary>
    public IReadOnlyList<CodeLocation> Places => _places;

    /// <summary>The place last gone to in <see cref="Places"/>, or the one nearest the caret when the list opened;
    /// -1 with none.</summary>
    public int PlaceIndex => _placeAt;

    /// <summary>That nothing was found where the person last asked, for the editor to say at the caret, or null.</summary>
    public CodeGoToNotFound? NotFound => _notFound;

    /// <summary>The index of the selected entry in <see cref="Entries"/>, or -1 with none.</summary>
    public int Selected => _selected;

    /// <summary>Whether the symbols have been asked for and not answered yet.</summary>
    public bool IsAsking => _asking;

    /// <summary>What the code is centred on and washed, whole lines at a time: the line read, or the
    /// selected symbol's range. Null with neither.</summary>
    public CodeRange? Preview => _preview;

    /// <summary>How many entries a page holds, which PageUp and PageDown move by: the rows the list shows,
    /// which the editor that draws it sets.</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>Raised whenever what the field shows changes: it opened or closed, the text was read again,
    /// an answer came, another entry was selected.</summary>
    public event Action? Changed;

    /// <summary>
    /// Opens the field on <paramref name="mode"/>, its text the mode's prefix alone. Opened already, it
    /// starts again from the prefix and keeps the view to give back that it had. Whatever was over the code
    /// (the completion's list, the card, the panel) closes, as the keyboard leaves the code. A list of places is
    /// not opened here but by asking for it (<see cref="Definition"/>, <see cref="References"/>): a mode of one
    /// opens the field with nothing typed.
    /// </summary>
    public void Open(CodeGoToMode mode)
    {
        Begin();
        // The field leaves a list of places for the modes its text chooses.
        if (Lists(_mode)) _mode = CodeGoToMode.None;
        Read(mode switch { CodeGoToMode.Line => ":", CodeGoToMode.Symbol => "@", _ => "" }, opening: true);
    }

    /// <summary>Shows the field, unless it shows already, keeping the view it opens over to give back.</summary>
    private void Begin()
    {
        if (_open) return;
        _open = true;
        _view = _editor.View;
        _editor.Completion.Dismiss();
        _editor.Hover.Dismiss();
        _editor.SignatureHelp.Dismiss();
        Unsay();
        // A composition in flight goes with the keyboard: its text is not the code's yet.
        if (_editor.Composition is not null) _editor.SetComposition("");
    }

    /// <summary>Whether <paramref name="mode"/> lists places, which the text filters and does not choose.</summary>
    private static bool Lists(CodeGoToMode mode) => mode == CodeGoToMode.Definitions || mode == CodeGoToMode.References;

    /// <summary>Selects the entry at <paramref name="index"/>, clamped to the list, and centres the code on
    /// it. Answers whether the selection moved.</summary>
    public bool Select(int index)
    {
        if (!_open || _entries.Count == 0) return false;
        var next = Math.Clamp(index, 0, _entries.Count - 1);
        if (next == _selected) return false;
        _selected = next;
        Previewed();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Selects the next entry, the first after the last, as VS Code's quick pick wraps.</summary>
    public bool SelectNext() => _entries.Count > 1 && Select((_selected + 1) % _entries.Count);

    /// <summary>Selects the entry before, the last before the first.</summary>
    public bool SelectPrevious() =>
        _entries.Count > 1 && Select((_selected - 1 + _entries.Count) % _entries.Count);

    /// <summary>Selects the entry a page down, or the last.</summary>
    public bool SelectNextPage() => Select(_selected + Math.Max(1, PageSize));

    /// <summary>Selects the entry a page up, or the first.</summary>
    public bool SelectPreviousPage() => Select(_selected - Math.Max(1, PageSize));

    /// <summary>
    /// Goes where the field reads: the caret to the line read, or to where the selected symbol's name
    /// starts, centred, recorded as a jump, the field closed and the keyboard given to the code. A selected place
    /// is gone to as a single answer is (<see cref="Definition"/>). With nothing to go to it does nothing, and the
    /// field stays, as VS Code's does.
    /// </summary>
    public bool Accept()
    {
        if (!_open) return false;
        if (Lists(_mode))
        {
            // A place is gone to as a single answer is: its start, near the top of the view, a jump; one in
            // another document is the app's, with the view given back.
            if (_selected < 0) return false;
            var index = _entryPlaces[_selected];
            var view = _view;
            End();
            _placeAt = index;
            GoToPlace(_places[index], view);
            _editor.RequestFocus();
            Changed?.Invoke();
            return true;
        }
        CodePosition? target = _mode == CodeGoToMode.Line ? _lineTarget
            : _mode == CodeGoToMode.Symbol && _selected >= 0 ? _entries[_selected].Target
            : null;
        if (target is not { } to) return false;
        End();
        _editor.JumpList.Jump(() => _editor.Selection = new CodeRange(to));
        _editor.RevealInCenter(to);
        _editor.RequestFocus();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Closes the field, gives back the view it opened over and the keyboard to the code, and leaves
    /// the selection where it was: Escape. Answers whether it was open.</summary>
    public bool Close()
    {
        if (!_open) return false;
        End();
        _editor.ScrollTo(_view);
        _editor.RequestFocus();
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// A key that reaches the CODE while the field shows. On the web the field takes the keyboard on the frame
    /// that draws it, and what is typed before that is the field's, never the code's: ⌃G and a line number
    /// typed at once edited the code. The field's own keys act on it and are claimed, Backspace takes the last
    /// character, and every other key is left to the host, a character among them, which arrives as text
    /// (<see cref="Type"/>). A chord with ⌘, ⌃ or ⌥ is the host's too. F4 and ⇧F4 walk a list of places.
    /// </summary>
    internal bool Key(string key, KeyModifiers modifiers)
    {
        if ((modifiers & (KeyModifiers.Command | KeyModifiers.Control | KeyModifiers.Alt)) != 0) return false;
        switch (key)
        {
            case "F4":
                if (!Lists(_mode)) return false;
                if ((modifiers & KeyModifiers.Shift) != 0) SelectPrevious();
                else SelectNext();
                return true;
            case "Enter":
                Accept();
                return true;
            case "Escape":
                Close();
                return true;
            case "ArrowDown":
                SelectNext();
                return true;
            case "ArrowUp":
                SelectPrevious();
                return true;
            case "PageDown":
                SelectNextPage();
                return true;
            case "PageUp":
                SelectPreviousPage();
                return true;
            case "Backspace":
                if (_query.Length > 0) Query = _query.Substring(0, _query.Length - 1);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Text that reaches the code while the field shows, typed or pasted before the field had the
    /// keyboard: it goes on the field's text, its line breaks left out.</summary>
    internal void Type(string text) => Query = _query + text.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);

    /// <summary>
    /// Closes the field where the view stands, the selection and the keyboard wherever they are: what a press
    /// elsewhere does, the code included, as VS Code's quick input hides when it loses the keyboard. A press in
    /// the code lands where the person pointed, on the code the preview showed. Answers whether it was open.
    /// </summary>
    public bool Dismiss()
    {
        if (!_open) return false;
        End();
        Changed?.Invoke();
        return true;
    }

    /// <summary>A new document: the field closes, with nothing to give back, and the places held, the request
    /// for them and what was said about them go, since they were the old document's.</summary>
    internal void Reset()
    {
        _places.Clear();
        _placeAt = -1;
        StopAsking();
        var said = _notFound is not null;
        _notFound = null;
        if (!_open)
        {
            if (said) Changed?.Invoke();
            return;
        }
        End();
        Changed?.Invoke();
    }

    private void End()
    {
        _open = false;
        StopSymbols();
        _query = "";
        _mode = CodeGoToMode.None;
        _line = null;
        _lineTarget = null;
        _entries = [];
        _entryPlaces.Clear();
        _nearest = -1;
        _selected = -1;
        _preview = null;
        _revealed = null;
    }

    /// <summary>Drops the symbols asked for or answered.</summary>
    private void StopSymbols()
    {
        _generation++;
        ProviderRequest.Cancel(_request, null);
        _request = null;
        _asking = false;
        _symbols.Clear();
        _containers.Clear();
    }

    private void Read(string query, bool opening)
    {
        // A list of places keeps its mode: the text filters it.
        if (Lists(_mode))
        {
            _query = query;
            _enclose = false;
            Refresh();
            return;
        }
        var was = _mode;
        _query = query;
        _mode = query.StartsWith(':') ? CodeGoToMode.Line
            : query.StartsWith('@') ? CodeGoToMode.Symbol
            : CodeGoToMode.None;
        if (_mode == CodeGoToMode.Symbol && (opening || was != CodeGoToMode.Symbol))
        {
            _enclose = true;
            Ask();
        }
        else
        {
            _enclose = false;
            if (_mode != CodeGoToMode.Symbol) StopSymbols();
        }
        Refresh();
    }

    /// <summary>Reads the text again for its mode, selects, centres and raises.</summary>
    private void Refresh()
    {
        _line = null;
        _lineTarget = null;
        _entries = [];
        _entryPlaces.Clear();
        _selected = -1;
        if (_mode == CodeGoToMode.Line) ReadLine();
        else if (_mode == CodeGoToMode.Symbol) Filter();
        else if (Lists(_mode)) FilterPlaces();
        Previewed();
        Changed?.Invoke();
    }

    // ---- a line -----------------------------------------------------------------------------------------

    /// <summary>
    /// The line and the column after the <c>:</c>, as <c>gotoLineQuickAccess</c> reads them: split on
    /// <c>,</c>, <c>:</c> or <c>#</c>, each part read as JavaScript's <c>parseInt</c> reads it, a negative
    /// line from the end, a line past either end the nearest one, and the column a visible column, clamped,
    /// a negative one from the line's end.
    /// </summary>
    private void ReadLine()
    {
        var text = _query.Trim().Substring(1);
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != ',' && text[i] != ':' && text[i] != '#') continue;
            parts.Add(text.Substring(start, i - start));
            start = i + 1;
        }
        parts.Add(text.Substring(start));

        if (ParseInt(parts[0].Trim()) is not { } typed) return;
        var count = _editor.Document.LineCount;
        var line = (int)Math.Min(Math.Max(1, typed >= 0 ? typed : count + 1 + typed), count);
        var cells = _editor.CellsOf(line - 1);
        var columns = cells.Width + 1;
        if (parts.Count < 2 || ParseInt(parts[1].Trim()) is not { } column)
        {
            _line = new CodeGoToLine(line, null, parts.Count >= 2, columns);
            _lineTarget = new CodePosition(line - 1, 0);
            return;
        }
        var visible = (int)Math.Min(Math.Max(1, column >= 0 ? column : columns + column), columns);
        _line = new CodeGoToLine(line, visible, true, columns);
        _lineTarget = new CodePosition(line - 1, cells.ColumnAt(visible - 1));
    }

    /// <summary>
    /// <paramref name="text"/> as JavaScript's <c>parseInt(text, 10)</c> reads it: a sign, then the digits
    /// up to the first character that is not one, and null with none. Read as a double, as JavaScript's
    /// numbers are, so a run of digits past an <c>int</c> is a number too large rather than an overflow.
    /// </summary>
    private static double? ParseInt(string text)
    {
        var i = 0;
        var negative = false;
        if (i < text.Length && (text[i] == '+' || text[i] == '-'))
        {
            negative = text[i] == '-';
            i++;
        }
        var first = i;
        double value = 0;
        while (i < text.Length && text[i] >= '0' && text[i] <= '9')
        {
            value = value * 10 + (text[i] - '0');
            i++;
        }
        if (i == first) return null;
        return negative ? -value : value;
    }

    // ---- symbols ----------------------------------------------------------------------------------------

    private void Ask()
    {
        var generation = ++_generation;
        ProviderRequest.Cancel(_request, null);
        _symbols.Clear();
        _containers.Clear();
        var request = new CancellationTokenSource();
        _request = request;
        _asking = true;
        _ = AskAsync(generation, request.Token);
    }

    private async Task AskAsync(int generation, CancellationToken cancellation)
    {
        IReadOnlyList<CodeSymbol> answer;
        try
        {
            answer = await _editor.Symbols.RequestAsync(cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        UiThread.Run(() =>
        {
            if (generation != _generation) return;
            _asking = false;
            Flatten(answer);
            Refresh();
        });
    }

    /// <summary>
    /// The answer as VS Code's outline lists it (<c>asListOfDocumentSymbols</c>): every symbol and every one
    /// it holds, each with the name of the one that holds it, ordered by where its range starts and, of two
    /// that start together, the one that reaches further first, so a symbol comes before those it holds.
    /// </summary>
    private void Flatten(IReadOnlyList<CodeSymbol> answer)
    {
        var symbols = new List<CodeSymbol>();
        var containers = new List<string?>();
        Walk(answer, null, symbols, containers);
        var order = new List<int>();
        for (var i = 0; i < symbols.Count; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            var start = symbols[a].Range.Start.CompareTo(symbols[b].Range.Start);
            if (start != 0) return start;
            var end = symbols[b].Range.End.CompareTo(symbols[a].Range.End);
            return end != 0 ? end : a.CompareTo(b);
        });
        _symbols.Clear();
        _containers.Clear();
        foreach (var i in order)
        {
            _symbols.Add(symbols[i]);
            _containers.Add(containers[i]);
        }
    }

    private static void Walk(IReadOnlyList<CodeSymbol> level, string? container, List<CodeSymbol> symbols,
        List<string?> containers)
    {
        foreach (var symbol in level)
        {
            symbols.Add(symbol);
            containers.Add(container);
            Walk(symbol.Children, symbol.Name, symbols, containers);
        }
    }

    /// <summary>
    /// The symbols the text after <c>@</c> lists. Its first word is matched against the name and the others,
    /// each, against the container, their scores added; the whole text, its spaces left out, is tried
    /// against the name first, and when it matches the container is not asked (VS Code's
    /// <c>doGetSymbolPicks</c>). A <c>:</c> right after the <c>@</c>, which VS Code reads as "grouped by
    /// kind", is left out of the match. The best score first, then the document's order; with nothing typed,
    /// the document's order.
    /// </summary>
    private void Filter()
    {
        var query = _query.Substring(1).Trim();
        if (query.StartsWith(':')) query = query.Substring(1).Trim();
        var words = new List<string>();
        foreach (var word in query.Split(' '))
            if (word.Length > 0) words.Add(word);

        var found = new List<CodeGoToEntry>();
        var scores = new List<int>();
        for (var index = 0; index < _symbols.Count; index++)
        {
            var symbol = _symbols[index];
            var name = symbol.Name.Trim();
            var container = _containers[index];
            IReadOnlyList<int> matches = [];
            IReadOnlyList<int> containerMatches = [];
            var score = 0;
            if (words.Count > 0)
            {
                var whole = words.Count > 1 ? CodeFuzzyMatch.Of(string.Join("", words), name, anywhere: true) : null;
                if (whole is not null)
                {
                    score = whole.Score;
                    matches = whole.Positions;
                }
                else
                {
                    if (CodeFuzzyMatch.Of(words[0], name, anywhere: true) is not { } named) continue;
                    score = named.Score;
                    matches = named.Positions;
                    if (words.Count > 1)
                    {
                        if (container is null) continue;
                        var held = new List<int>();
                        var all = true;
                        for (var w = 1; w < words.Count && all; w++)
                        {
                            if (CodeFuzzyMatch.Of(words[w], container, anywhere: true) is { } part)
                            {
                                score += part.Score;
                                foreach (var at in part.Positions)
                                    if (!held.Contains(at)) held.Add(at);
                            }
                            else all = false;
                        }
                        if (!all) continue;
                        held.Sort();
                        containerMatches = held;
                    }
                }
            }
            found.Add(new CodeGoToEntry(name, symbol.Kind, symbol.Range, symbol.SelectionRange.Start)
            {
                Container = container,
                Deprecated = symbol.Deprecated,
                Matches = matches,
                ContainerMatches = containerMatches,
            });
            scores.Add(score);
        }
        // The best score first, and of two equal ones the earlier in the document, which is the order found.
        var order = new List<int>();
        for (var i = 0; i < found.Count; i++) order.Add(i);
        if (words.Count > 0) order.Sort((a, b) => scores[a] != scores[b] ? scores[b].CompareTo(scores[a]) : a.CompareTo(b));
        foreach (var i in order) _entries.Add(found[i]);
        if (_entries.Count == 0) return;

        _selected = 0;
        if (!_enclose || words.Count > 0) return;
        // The last one whose range holds the caret: the innermost, since a symbol comes before those it holds.
        var caret = _editor.Selection.Focus;
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var range = _entries[i].Range;
            if (range.Start.CompareTo(caret) <= 0 && caret.CompareTo(range.End) <= 0)
            {
                _selected = i;
                break;
            }
        }
    }

    // ---- the preview ------------------------------------------------------------------------------------

    /// <summary>What the code is centred on and washed now, centred again only when it moved.</summary>
    private void Previewed()
    {
        CodePosition? at = null;
        if (_mode == CodeGoToMode.Line && _lineTarget is { } line)
        {
            at = line;
            _preview = new CodeRange(line);
        }
        else if ((_mode == CodeGoToMode.Symbol || Lists(_mode)) && _selected >= 0 && _entries[_selected].Document is null)
        {
            at = _entries[_selected].Target;
            _preview = _entries[_selected].Range;
        }
        // A place in another document is not the editor's to show: the view stays where it is.
        else _preview = null;
        if (at is not { } shown || _revealed == shown) return;
        _revealed = shown;
        _editor.RevealInCenter(shown);
    }

    // ---- definitions and references (#1084) -------------------------------------------------------------

    /// <summary>
    /// Goes where what stands at the caret is defined, declared, typed or implemented, as
    /// <paramref name="kind"/> asks (F12, ⌘F12), when the definition providers answer: one place is gone to, several
    /// are listed (<see cref="CodeGoToMode.Definitions"/>), and none is said (<see cref="NotFound"/>). When the caret
    /// already stands in the only place answered, its references are found instead, VS Code's alternative command,
    /// for every kind but an implementation. Answers false, and asks nothing, with no provider.
    /// </summary>
    public bool Definition(CodeDefinitionKind kind) =>
        _editor.Definitions.Providers.Count > 0 && RequestPlaces(CodeGoToMode.Definitions, kind, muted: false);

    /// <summary>Finds the references of what stands at the caret (⇧F12), when the reference providers answer: one
    /// place is gone to, several are listed (<see cref="CodeGoToMode.References"/>), and none is said. Answers false,
    /// and asks nothing, with no provider.</summary>
    public bool References() =>
        _editor.References.Providers.Count > 0 && RequestPlaces(CodeGoToMode.References, CodeDefinitionKind.Definition, muted: false);

    /// <summary>Goes to the definition at the caret as a click on a link does: as F12, saying nothing when there
    /// is none, as VS Code mutes its message for the click.</summary>
    internal bool FollowLink() =>
        _editor.Definitions.Providers.Count > 0 && RequestPlaces(CodeGoToMode.Definitions, CodeDefinitionKind.Definition, muted: true);

    /// <summary>
    /// The place after the one last gone to in <see cref="Places"/> (F4): the next row while the list shows, and
    /// once it is closed the next place, the first after the last, gone to as a single answer is. Answers false,
    /// claiming nothing, with no place held.
    /// </summary>
    public bool NextPlace() => Walk(1);

    /// <summary>The place before the one last gone to (⇧F4), the last before the first.</summary>
    public bool PreviousPlace() => Walk(-1);

    private bool Walk(int step)
    {
        if (_open && Lists(_mode))
        {
            if (step > 0) SelectNext();
            else SelectPrevious();
            return true;
        }
        if (_places.Count == 0) return false;
        var count = _places.Count;
        _placeAt = _placeAt < 0 ? (step > 0 ? 0 : count - 1) : ((_placeAt + step) % count + count) % count;
        GoToPlace(_places[_placeAt], null);
        Changed?.Invoke();
        return true;
    }

    private bool RequestPlaces(CodeGoToMode mode, CodeDefinitionKind kind, bool muted)
    {
        Unsay();
        StopAsking();
        var generation = _placesGeneration;
        var request = new CancellationTokenSource();
        _placesRequest = request;
        var at = _editor.Selection.Focus;
        _ = AskPlaces(mode, kind, at, muted, generation, request.Token);
        return true;
    }

    private async Task AskPlaces(CodeGoToMode mode, CodeDefinitionKind kind, CodePosition at, bool muted, int generation,
        CancellationToken cancellation)
    {
        IReadOnlyList<CodeLocation> places;
        try
        {
            places = mode == CodeGoToMode.References
                ? await _editor.References.RequestAsync(at, cancellation)
                : await _editor.Definitions.RequestAsync(at, kind, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        UiThread.Run(() =>
        {
            if (generation != _placesGeneration) return;
            _placesRequest = null;
            Answered(mode, kind, at, muted, places);
        });
    }

    /// <summary>What an answer does, as VS Code's <c>SymbolNavigationAction</c> does with one.</summary>
    private void Answered(CodeGoToMode mode, CodeDefinitionKind kind, CodePosition at, bool muted,
        IReadOnlyList<CodeLocation> places)
    {
        if (places.Count == 0)
        {
            if (muted) return;
            var word = _editor.Definitions.WordUnder(at) ?? (at.Column > 0
                ? _editor.Definitions.WordUnder(at with { Column = at.Column - 1 })
                : null);
            _notFound = new CodeGoToNotFound(mode, kind, at, word is { } found ? _editor.Document.TextIn(found) : "");
            Changed?.Invoke();
            return;
        }
        if (places.Count == 1)
        {
            var only = places[0];
            // Already at the only definition: its references instead (alternativeDefinitionCommand), which an
            // implementation has none of (alternativeImplementationCommand is empty).
            var here = only.Document is null && only.Range.Start <= at && at <= only.Range.End;
            if (here && mode == CodeGoToMode.Definitions && kind != CodeDefinitionKind.Implementation
                && _editor.References.Providers.Count > 0)
            {
                RequestPlaces(CodeGoToMode.References, kind, muted);
                return;
            }
            GoToPlace(only, null);
            // An answer arrives after the key that asked: whoever draws the editor hears that the caret went.
            Changed?.Invoke();
            return;
        }
        OpenPlaces(mode, kind, places, at);
    }

    /// <summary>Lists <paramref name="places"/> in the field, the one nearest <paramref name="at"/> selected and
    /// previewed, and holds them for the walk.</summary>
    private void OpenPlaces(CodeGoToMode mode, CodeDefinitionKind kind, IReadOnlyList<CodeLocation> places, CodePosition at)
    {
        Begin();
        StopSymbols();
        _mode = mode;
        _kind = kind;
        _query = "";
        _enclose = false;
        _revealed = null;
        _places.Clear();
        _places.AddRange(places);
        _nearest = Nearest(at);
        _placeAt = _nearest;
        Refresh();
    }

    /// <summary>
    /// The place nearest <paramref name="at"/>, as VS Code's <c>ReferencesModel.nearestReference</c> finds it: one
    /// in the editor's document before any other, then the fewest lines away, a line counting a hundred columns,
    /// and of two as near the earlier. With none in the editor's document, the first.
    /// </summary>
    private int Nearest(CodePosition at)
    {
        var best = -1;
        var distance = 0;
        for (var i = 0; i < _places.Count; i++)
        {
            if (_places[i].Document is not null) continue;
            var start = _places[i].Range.Start;
            var away = Math.Abs(start.Line - at.Line) * 100 + Math.Abs(start.Column - at.Column);
            if (best >= 0 && away >= distance) continue;
            best = i;
            distance = away;
        }
        return best >= 0 ? best : 0;
    }

    /// <summary>
    /// The places the text matches, as rows, in the document's order: the whole text, trimmed, against what a row
    /// shows, by the completion's matcher. With nothing typed every place is listed, the nearest selected; with
    /// something typed, the first left.
    /// </summary>
    private void FilterPlaces()
    {
        var query = _query.Trim();
        var document = _editor.Document;
        for (var i = 0; i < _places.Count; i++)
        {
            var entry = PlaceEntry(_places[i], document);
            if (query.Length > 0)
            {
                if (CodeFuzzyMatch.Of(query, entry.Name, anywhere: true) is not { } match) continue;
                entry = entry with { Matches = match.Positions };
            }
            _entries.Add(entry);
            _entryPlaces.Add(i);
        }
        if (_entries.Count == 0) return;
        _selected = 0;
        var kept = _entryPlaces.IndexOf(_keepPlace);
        if (kept >= 0)
        {
            _selected = kept;
            return;
        }
        if (query.Length > 0) return;
        var nearest = _entryPlaces.IndexOf(_nearest);
        if (nearest >= 0) _selected = nearest;
    }

    /// <summary>
    /// The row of <paramref name="place"/>: in the editor's document, the text of its line with its leading white
    /// space left out, the place's characters on it marked; in another, the last part of the document's name. Both
    /// show where it stands, its line and its column counted from one.
    /// </summary>
    private static CodeGoToEntry PlaceEntry(CodeLocation place, CodeDocument document)
    {
        var start = place.Range.Start;
        var where = (start.Line + 1) + ":" + (start.Column + 1);
        if (place.Document is { } other)
            return new CodeGoToEntry(NameOf(other), null, place.Range, start) { Container = where, Document = other };
        var at = document.Clamp(start);
        var text = document.Line(at.Line);
        var lead = 0;
        while (lead < text.Length && char.IsWhiteSpace(text[lead])) lead++;
        var name = text.Substring(lead).TrimEnd();
        var end = place.Range.End;
        var to = (end.Line == at.Line ? end.Column : text.Length) - lead;
        var marked = new List<int>();
        for (var column = Math.Max(at.Column, lead) - lead; column < to && column < name.Length; column++) marked.Add(column);
        return new CodeGoToEntry(name, null, place.Range, start) { Container = where, Matches = marked };
    }

    /// <summary>The last part of a document's name, after its last <c>/</c> or <c>\</c>.</summary>
    private static string NameOf(string document)
    {
        var cut = Math.Max(document.LastIndexOf('/'), document.LastIndexOf('\\'));
        return cut >= 0 && cut < document.Length - 1 ? document.Substring(cut + 1) : document;
    }

    /// <summary>
    /// Goes to <paramref name="place"/>: in the editor's document, the caret to its start, recorded as a jump, and
    /// the place near the top of the view when it is out of it (VS Code's <c>NearTopIfOutsideViewport</c>); in
    /// another, the place handed to the app, the caret and the jump list left as they were, and the view given back
    /// to <paramref name="giveBack"/> when a list's preview moved it.
    /// </summary>
    private void GoToPlace(CodeLocation place, Rect? giveBack)
    {
        if (place.Document is not null)
        {
            if (giveBack is { } view) _editor.ScrollTo(view);
            _editor.RequestOpen(place);
            return;
        }
        var to = _editor.Document.Clamp(place.Range.Start);
        _editor.JumpList.Jump(() => _editor.Selection = new CodeRange(to));
        _editor.RevealNearTopIfOutsideView(to);
    }

    /// <summary>The places held, carried through what changed, as the jump list's are; an edit also drops a
    /// request under way and what was said, as the caret moving does.</summary>
    private void Carry(CodeChange change)
    {
        if (change.Replaced) return;
        StopAsking();
        Unsay();
        if (_places.Count == 0) return;
        foreach (var edit in change.Edits)
            for (var i = 0; i < _places.Count; i++)
                if (_places[i].Document is null) _places[i] = _places[i] with { Range = edit.Carry(_places[i].Range) };
        if (!_open || !Lists(_mode)) return;
        // The rows follow their places, and the one selected stays selected.
        _keepPlace = _selected >= 0 ? _entryPlaces[_selected] : -1;
        Refresh();
        _keepPlace = -1;
    }

    /// <summary>The caret moved: a request is for where it stood, and what was said was said there.</summary>
    private void Moved()
    {
        StopAsking();
        Unsay();
    }

    /// <summary>Drops the places asked for and not answered yet.</summary>
    private void StopAsking()
    {
        _placesGeneration++;
        ProviderRequest.Cancel(_placesRequest, null);
        _placesRequest = null;
    }

    /// <summary>Takes away what was said about nothing found. Answers whether something was.</summary>
    internal bool Unsay()
    {
        if (_notFound is null) return false;
        _notFound = null;
        Changed?.Invoke();
        return true;
    }
}
