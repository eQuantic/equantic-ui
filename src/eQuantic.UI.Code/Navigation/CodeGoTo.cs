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

    internal CodeGoTo(CodeEditorController editor) => _editor = editor;

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

    /// <summary>The symbols the text lists, in <see cref="CodeGoToMode.Symbol"/>, the best first.</summary>
    public IReadOnlyList<CodeGoToEntry> Entries => _entries;

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
    /// (the completion's list, the card, the panel) closes, as the keyboard leaves the code.
    /// </summary>
    public void Open(CodeGoToMode mode)
    {
        if (!_open)
        {
            _open = true;
            _view = _editor.View;
            _editor.Completion.Dismiss();
            _editor.Hover.Dismiss();
            _editor.SignatureHelp.Dismiss();
        }
        Read(mode switch { CodeGoToMode.Line => ":", CodeGoToMode.Symbol => "@", _ => "" }, opening: true);
    }

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
    /// starts, centred, recorded as a jump, the field closed and the keyboard given to the code. With nothing
    /// to go to it does nothing, and the field stays, as VS Code's does.
    /// </summary>
    public bool Accept()
    {
        if (!_open) return false;
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
    /// (<see cref="Type"/>). A chord with ⌘, ⌃ or ⌥ is the host's too.
    /// </summary>
    internal bool Key(string key, KeyModifiers modifiers)
    {
        if ((modifiers & (KeyModifiers.Command | KeyModifiers.Control | KeyModifiers.Alt)) != 0) return false;
        switch (key)
        {
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

    /// <summary>A new document: the field closes, with nothing to give back.</summary>
    internal void Reset()
    {
        if (!_open) return;
        End();
        Changed?.Invoke();
    }

    private void End()
    {
        _open = false;
        _generation++;
        ProviderRequest.Cancel(_request, null);
        _request = null;
        _asking = false;
        _query = "";
        _mode = CodeGoToMode.None;
        _line = null;
        _lineTarget = null;
        _entries = [];
        _selected = -1;
        _preview = null;
        _revealed = null;
        _symbols.Clear();
        _containers.Clear();
    }

    private void Read(string query, bool opening)
    {
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
            if (_mode != CodeGoToMode.Symbol)
            {
                _generation++;
                ProviderRequest.Cancel(_request, null);
                _request = null;
                _asking = false;
                _symbols.Clear();
                _containers.Clear();
            }
        }
        Refresh();
    }

    /// <summary>Reads the text again for its mode, selects, centres and raises.</summary>
    private void Refresh()
    {
        _line = null;
        _lineTarget = null;
        _entries = [];
        _selected = -1;
        if (_mode == CodeGoToMode.Line) ReadLine();
        else if (_mode == CodeGoToMode.Symbol) Filter();
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
        else if (_mode == CodeGoToMode.Symbol && _selected >= 0)
        {
            at = _entries[_selected].Target;
            _preview = _entries[_selected].Range;
        }
        else _preview = null;
        if (at is not { } shown || _revealed == shown) return;
        _revealed = shown;
        _editor.RevealInCenter(shown);
    }
}
