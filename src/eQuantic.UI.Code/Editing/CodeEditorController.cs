using System.Globalization;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// THE EDITOR, minus the pixels.
/// <para>
/// Everything an editing surface does — moving, selecting, typing, indenting, commenting, undoing
/// — is a method here, on a document and a selection, with no idea what any of it looks like. That
/// is what makes it testable without a screen, identical on both targets, and usable by an IDE
/// that wants to drive the editor from its own menu, its own key map or its own language server.
/// </para>
/// <para>
/// The app owns an instance. Every mutation raises <see cref="Changed"/> with the <see cref="CodeEdit"/>
/// that caused it, which is what a dirty flag, a language server and a diff all subscribe to —
/// never to keystrokes, because a paste and a refactor are edits nobody typed.
/// </para>
/// <para>
/// It is also the <see cref="ICodeSurfaceModel"/> a realizer drives: the host hands it keys, text
/// and pointers, and paints the rectangles it answers. So what a click means and where a caret is
/// drawn are decided here, once, for every host.
/// </para>
/// </summary>
public sealed class CodeEditorController : ICodeSurfaceModel
{
    private CodeDocument _document;
    private CodeRange _selection;

    public CodeEditorController(string text = "", ICodeLanguage? language = null)
    {
        _document = CodeDocument.FromText(text);
        _selection = new CodeRange(CodePosition.Start);
        Highlighter = new CodeHighlighter(language ?? CodeLanguages.PlainText);
    }

    /// <summary>The text, as lines. Replacing it is a full reset — a file was opened.</summary>
    public CodeDocument Document
    {
        get => _document;
        set
        {
            _document = value;
            _widths = null;
            _selection = new CodeRange(_document.Clamp(_selection.Focus));
            _composition = null;
            Highlighter.Invalidate();
            History.Clear();
            _revealVersion++;
            Changed?.Invoke(null);
        }
    }

    /// <summary>Where the caret is, and what it has selected.</summary>
    public CodeRange Selection
    {
        get => _selection;
        set => Select(value, keepCell: false);
    }

    /// <summary>
    /// Places the selection. Only a run of ↑ and ↓ keeps the cell it aims at
    /// (<paramref name="keepCell"/>); anything else that places the caret forgets it, a click, an
    /// undo and the app included, or ↓ after a click went back to the column the run before the
    /// click had aimed at.
    /// </summary>
    private void Select(CodeRange value, bool keepCell)
    {
        if (!keepCell) _desiredCell = -1;
        // A position INSIDE a text element is one no caret may hold: an edit from there splits the
        // element (a Backspace at column 1 of an emoji took its high half). A caret goes to the
        // element's start, where it is drawn, and a range grows to take in the elements it cuts,
        // keeping its direction.
        var anchor = _document.Clamp(value.Anchor);
        var focus = _document.Clamp(value.Focus);
        var backwards = new CodeRange(anchor, focus).Start != anchor;
        var next = anchor == focus
            ? new CodeRange(Boundary(anchor, after: false))
            : new CodeRange(Boundary(anchor, after: backwards), Boundary(focus, after: !backwards));
        if (next == _selection) return;
        // Moving the caret ENDS the typing run: the next character starts a new undo step,
        // because a person who moved and typed did two things.
        History.Break();
        _selection = next;
        _revealVersion++;
        SelectionChanged?.Invoke(next);
    }

    /// <summary>The caret — the moving end of the selection.</summary>
    public CodePosition Caret => _selection.Focus;

    public CodeHighlighter Highlighter { get; }
    public CodeHistory History { get; } = new();
    public CodeLanguageRules Rules => Highlighter.Language.Rules;

    /// <summary>Whether edits are refused — a viewer, a diff pane, a running debugger.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Whether the next Tab LEAVES the editor instead of indenting. Escape sets it — the one way out
    /// a keyboard user has from a surface that takes Tab — and any other key, or coming back into
    /// the editor, clears it (see <see cref="CodeKeymap"/>).
    /// </summary>
    public bool TabMovesFocus { get; set; }

    /// <summary>
    /// Raised after every change, with the edit that caused it — null when the whole document was
    /// replaced. THE event an IDE builds on.
    /// </summary>
    public event Action<CodeEdit?>? Changed;

    /// <summary>Raised when the caret or selection moved, however it moved.</summary>
    public event Action<CodeRange>? SelectionChanged;

    /// <summary>
    /// The column a vertical move AIMS at. Moving down through a short line and on to a long one
    /// returns to the column you started from — losing it on the short line is the classic bug,
    /// and remembering it is why this field exists.
    /// </summary>
    private int _desiredCell = -1;

    /// <summary>The cells of the lines read lately, by line: the caret, the bands and every click
    /// read them, several times a frame, and a line's cells change only with its text.</summary>
    private readonly Dictionary<int, CodeLineCells> _cells = new();

    /// <summary>
    /// Each line's width in cells, kept beside the document from the first time
    /// <see cref="WidestLine"/> is asked, and spliced by every edit rather than measured again. Every
    /// keystroke makes a new document, so a width kept per document measured the whole file again
    /// per key. Null until asked, and again whenever the document is replaced wholesale.
    /// </summary>
    private List<int>? _widths;
    private int _widthsTabs;
    private int _widest;

    /// <summary>
    /// How many cells the widest line of the document takes. A view is as wide as the widest line
    /// of the FILE, so its width does not breathe as the window scrolls, and measuring every line on
    /// every build cost a scroll step 14 ms over 50,000 lines on the web. An edit measures the lines
    /// it touched, and only an edit to the widest line looks at the others' widths again.
    /// </summary>
    public int WidestLine
    {
        get
        {
            var tabSize = Rules.IndentWidth;
            if (_widths is null || _widthsTabs != tabSize)
            {
                _widths = new List<int>(_document.LineCount);
                for (var line = 0; line < _document.LineCount; line++)
                    _widths.Add(CodeLineCells.WidthOf(_document.Line(line), tabSize));
                _widthsTabs = tabSize;
                _widest = Widest(_widths);
            }
            return _widest;
        }
    }

    /// <summary>
    /// The widths after an edit replaced lines <paramref name="line"/> to <paramref name="line"/> +
    /// <paramref name="linesRemoved"/> with lines <paramref name="line"/> to <paramref name="line"/> +
    /// <paramref name="linesInserted"/> of the new document: those are measured, the rest are
    /// kept, and the widest is looked for again only when a line that was it is gone.
    /// </summary>
    private void WidthsChanged(int line, int linesInserted, int linesRemoved)
    {
        if (_widths is null) return;
        var old = _widths;
        var gone = Math.Min(linesRemoved + 1, old.Count - line);
        var lostTheWidest = false;
        for (var i = line; i < line + gone; i++)
            if (old[i] >= _widest) lostTheWidest = true;
        if (linesInserted == linesRemoved && gone == linesRemoved + 1)
        {
            // As many lines as before, which is nearly every keystroke: the widths change in place,
            // and nothing is copied.
            var widestHere = 0;
            for (var i = line; i <= line + linesInserted; i++)
            {
                var width = CodeLineCells.WidthOf(_document.Line(i), _widthsTabs);
                old[i] = width;
                if (width > widestHere) widestHere = width;
            }
            if (lostTheWidest && widestHere < _widest) _widest = Widest(old);
            else if (widestHere > _widest) _widest = widestHere;
            return;
        }
        // Lines came or went: the list is built again, as the document's own list of lines is by
        // every edit. Copied one by one, never inserted as a range: on the web a range becomes one
        // argument per item, and a paste of a large file would pass more than an engine takes.
        var next = new List<int>(old.Count - gone + linesInserted + 1);
        for (var i = 0; i < line; i++) next.Add(old[i]);
        var measuredWidest = 0;
        for (var i = line; i <= line + linesInserted; i++)
        {
            var width = CodeLineCells.WidthOf(_document.Line(i), _widthsTabs);
            if (width > measuredWidest) measuredWidest = width;
            next.Add(width);
        }
        for (var i = line + gone; i < old.Count; i++) next.Add(old[i]);
        _widths = next;
        if (lostTheWidest) _widest = Widest(next);
        else if (measuredWidest > _widest) _widest = measuredWidest;
    }

    private static int Widest(List<int> widths)
    {
        var widest = 0;
        foreach (var width in widths)
            if (width > widest) widest = width;
        return widest;
    }

    /// <summary>
    /// Where line <paramref name="line"/>'s columns land on the grid: tabs to their stops, wide
    /// characters two cells, every text element whole (see <see cref="CodeLineCells"/>). The tab
    /// stops are the language's indent width.
    /// </summary>
    public CodeLineCells CellsOf(int line)
    {
        var text = _document.Line(line);
        var tabSize = Rules.IndentWidth;
        if (_cells.TryGetValue(line, out var cells) && cells.Text == text && cells.TabSize == tabSize)
            return cells;
        cells = new CodeLineCells(text, tabSize);
        _cells[line] = cells;
        return cells;
    }

    /// <summary>
    /// The position one character BEFORE this one, which crosses a line break. A character is a
    /// text element: an emoji is one step and one Backspace, where a step of one UTF-16 unit left
    /// half of it behind. Through the line's cached cells (<see cref="CellsOf"/>), because every
    /// arrow and every Backspace asks, and segmenting the line anew each time is what a long line
    /// cannot afford.
    /// </summary>
    private CodePosition Before(CodePosition position)
    {
        var here = _document.Clamp(position);
        if (here.Column > 0) return here with { Column = CellsOf(here.Line).Previous(here.Column) };
        if (here.Line == 0) return CodePosition.Start;
        return new CodePosition(here.Line - 1, _document.Line(here.Line - 1).Length);
    }

    /// <summary>
    /// <paramref name="position"/> itself when it stands on a text element's boundary; inside one, the
    /// element's start, or its end when <paramref name="after"/>.
    /// </summary>
    private CodePosition Boundary(CodePosition position, bool after)
    {
        var cells = CellsOf(position.Line);
        if (position.Column <= 0 || position.Column >= cells.Text.Length) return position;
        var element = cells.ElementAt(cells.IndexOf(position.Column));
        if (element.Start == position.Column) return position;
        return position with { Column = after ? element.End : element.Start };
    }

    /// <summary>The position one character (one text element) AFTER this one.</summary>
    private CodePosition After(CodePosition position)
    {
        var here = _document.Clamp(position);
        if (here.Column < _document.Line(here.Line).Length)
            return here with { Column = CellsOf(here.Line).Next(here.Column) };
        if (here.Line == _document.LineCount - 1) return here;
        return new CodePosition(here.Line + 1, 0);
    }

    // ---- the surface: what a realizer drives and paints ---------------------------------------

    /// <summary>How wide a caret is drawn, in dp — one number for every host.</summary>
    public const float CaretWidth = 2;

    /// <summary>
    /// The grid the code is drawn on. The composing component measures it — the face, the density,
    /// the padding — and hands it over on every build; this is the only place that turns a position
    /// into a point with it, and a point back into a position.
    /// </summary>
    public CodeGrid Grid { get; set; } = CodeGrid.Default;

    /// <summary>Whether a press that began on the surface is still drawing a selection.</summary>
    private bool _dragging;

    /// <summary>
    /// The selection, as one band per line it covers from <paramref name="first"/> to
    /// <paramref name="last"/>, in the surface's own coordinates — drawn by the COMPONENT in the code's
    /// own layers, under the text and over the active line, so it reads the same on every target. A
    /// single rectangle over a multi-line range would cover the indentation of lines the range never
    /// touched, which is why these are per line.
    /// <para>
    /// Asked for the lines a view BUILDS, never for the whole range. A band is measured through its
    /// line's cells, and a select-all over 50,000 lines measured every one of them on every build (a
    /// scroll step builds) and kept all 50,000 maps, where the view drew fifty.
    /// </para>
    /// </summary>
    public IReadOnlyList<Rect> SelectionBandsIn(int first, int last)
    {
        var bands = new List<Rect>();
        if (_selection.IsEmpty) return bands;
        var start = _selection.Start;
        var end = _selection.End;
        for (var line = Math.Max(start.Line, first); line <= Math.Min(end.Line, last); line++)
        {
            var from = line == start.Line ? start.Column : 0;
            // One cell past the end of every line but the last: the band shows that the line
            // BREAK is held too, which is what makes a selection ending at column 0 of the next
            // line read as the whole line it is.
            // A line a fold hides has no row of its own to draw a band on: its band would lie over
            // the placeholder that stands for it, once for every line selected under it.
            if (Grid.Rows is { } rows && !rows.IsVisible(line)) continue;
            var cells = CellsOf(line);
            var fromCell = cells.CellOf(from);
            var toCell = line == end.Line ? cells.CellOf(end.Column) : cells.Width + 1;
            if (toCell <= fromCell) continue;
            var at = Grid.PointOf(line, fromCell);
            bands.Add(new Rect(at.X, at.Y, (toCell - fromCell) * Grid.Cell.Width, Grid.Cell.Height));
        }
        return bands;
    }

    /// <inheritdoc />
    public IReadOnlyList<Rect> Carets => [CaretRect(Caret)];

    private int _revealVersion;

    /// <inheritdoc />
    public int RevealVersion => _revealVersion;

    private int _focusVersion;

    /// <inheritdoc />
    public int FocusVersion => _focusVersion;

    /// <summary>
    /// Asks for the keyboard: whichever host draws this editor gives it to the surface, as a click
    /// would, on its next frame (or its first, if it has not drawn it yet). What an IDE calls when a
    /// file opens or a panel over the code closes. A REQUEST, because focus is the host's to grant.
    /// </summary>
    public void RequestFocus() => _focusVersion++;

    /// <summary>Where a caret at <paramref name="position"/> is drawn, in the surface's coordinates.</summary>
    public Rect CaretRect(CodePosition position)
    {
        var at = Grid.PointOf(position.Line, CellsOf(position.Line).CellOf(position.Column));
        return new Rect(at.X, at.Y, CaretWidth, Grid.Cell.Height);
    }

    /// <summary>
    /// The (line, column) a point on the surface lands on: the row by division, the column through
    /// the line's cells (<see cref="CellsOf"/>), on the nearer side of whatever the point hit. So a
    /// click on the right half of a character, a tab or a wide character puts the caret after it,
    /// which is what makes a click feel aimed rather than approximate, and no click lands inside a
    /// text element. Past the end of a line it lands at the end; past the last line, on the last.
    /// </summary>
    public CodePosition PositionAt(Point point)
    {
        var target = _document.Clamp(new CodePosition(Math.Max(0, Grid.LineAt(point.Y)), 0)).Line;
        return new CodePosition(target, CellsOf(target).ColumnAt((point.X - Grid.Origin.X) / Grid.Cell.Width));
    }

    /// <inheritdoc />
    public bool HandleKey(string key, KeyModifiers modifiers, KeyboardConvention convention,
        ITextClipboard? clipboard) =>
        CodeKeymap.Handle(this, key, modifiers, convention, clipboard);

    /// <inheritdoc />
    public bool HandleText(string text)
    {
        if (ReadOnly || text.Length == 0) return false;
        TabMovesFocus = false;
        // An input method's commit REPLACES what it was composing: the composition comes out first,
        // so the commit is one ordinary edit from the document the composition began over — one undo
        // step, whatever the candidate window went through on the way.
        var committing = _composition is not null;
        EndComposition();
        // Element by element: a character goes through Type, which pairs brackets and quotes, and
        // an element of more than one unit (an emoji, a letter with its accent) goes in WHOLE, never
        // a half of a surrogate pair at a time.
        var typed = false;
        var starts = StringInfo.ParseCombiningCharacters(text);
        for (var i = 0; i < starts.Length; i++)
        {
            var end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            typed |= end - starts[i] == 1
                ? Type(text[starts[i]])
                : Edit(_selection, text.Substring(starts[i], end - starts[i]), true);
        }
        // A step of its OWN, after as well as before (SetComposition broke the run it began at):
        // the typing that follows a commit does not join it.
        if (committing) History.Break();
        return typed;
    }

    // ---- composition (an input method building text) -----------------------------------------

    /// <summary>Where the composition in flight sits in the document, or null when there is none —
    /// what the component underlines.</summary>
    public CodeRange? Composition => _composition;

    private CodeRange? _composition;

    /// <summary>What the composition replaced when it began (the selection typed over) — put back
    /// if it is cancelled.</summary>
    private string _compositionReplaced = "";

    /// <summary>The selection the composition began over — restored if it is cancelled.</summary>
    private CodeRange _compositionSelection;

    /// <summary>
    /// The composition an input method is building. It lives IN the document while it grows — so
    /// the line reflows around it, the highlighter colours it and the caret sits after it, exactly
    /// as the committed text will — but none of its steps reaches the undo history: a commit is
    /// recorded as one edit (<see cref="HandleText"/>), and a cancellation leaves no trace at all.
    /// </summary>
    public bool SetComposition(string text)
    {
        if (ReadOnly) return false;
        if (_composition is not { } current)
        {
            if (text.Length == 0) return false;
            // The run of typing it began at ends here, or the commit would coalesce with it and one
            // undo would take both.
            History.Break();
            // Composing over a selection replaces it, as typing does.
            _compositionSelection = _selection;
            var over = new CodeRange(_document.Clamp(_selection.Start), _document.Clamp(_selection.End));
            _compositionReplaced = _document.TextIn(over);
            _composition = ReplaceUnrecorded(over, text);
            return true;
        }

        if (text.Length == 0)
        {
            // Cancelled: the document goes back to what it was before the composition began.
            ReplaceUnrecorded(current, _compositionReplaced);
            _composition = null;
            _selection = new CodeRange(_document.Clamp(_compositionSelection.Anchor),
                _document.Clamp(_compositionSelection.Focus));
            _revealVersion++;
            SelectionChanged?.Invoke(_selection);
            return true;
        }

        _composition = ReplaceUnrecorded(current, text);
        return true;
    }

    /// <summary>Takes the composition out again, restoring what it replaced, so an edit can be made
    /// from the document as it was. Nothing happens when nothing is composing.</summary>
    private void EndComposition()
    {
        if (_composition is not { } current) return;
        ReplaceUnrecorded(current, _compositionReplaced);
        _composition = null;
        _selection = new CodeRange(_document.Clamp(_compositionSelection.Anchor),
            _document.Clamp(_compositionSelection.Focus));
    }

    /// <summary>
    /// A replacement that is REAL — the document, the colours and every listener see it — but that
    /// the undo history does not: a composition's intermediate steps. Answers the range the text
    /// now occupies.
    /// </summary>
    private CodeRange ReplaceUnrecorded(CodeRange range, string text)
    {
        var ordered = new CodeRange(_document.Clamp(range.Start), _document.Clamp(range.End));
        var removed = _document.TextIn(ordered);
        var before = _selection;
        var next = _document.Replace(ordered, text, out var caret);
        var line = ordered.Start.Line;
        var linesRemoved = ordered.End.Line - ordered.Start.Line;
        var linesInserted = caret.Line - ordered.Start.Line;

        _document = next;
        _selection = new CodeRange(caret);
        Highlighter.LineChanged(_document, line, linesInserted, linesRemoved);
        WidthsChanged(line, linesInserted, linesRemoved);
        _revealVersion++;
        _desiredCell = -1;
        var edit = new CodeEdit(ordered, removed, text, before, _selection, false);
        Changed?.Invoke(edit);
        SelectionChanged?.Invoke(_selection);
        return new CodeRange(ordered.Start, caret);
    }

    // ---- focus --------------------------------------------------------------------------------

    /// <inheritdoc />
    public void FocusChanged(bool focused)
    {
        // Leaving or arriving ends the typing run either way: the next character is a new step.
        History.Break();
        TabMovesFocus = false;
        if (focused) return;
        // A composition cannot survive the keyboard leaving: the platform has already dropped it,
        // and text still underlined in the document would claim an input method nobody is using.
        if (_composition is not null) SetComposition("");
        _dragging = false;
    }

    /// <summary>
    /// What a pointer MEANS on a code surface, for every host: a press places the caret (Shift
    /// extends the selection to it instead), two select the word under it, three the line; a drag
    /// that began with a single press moves the selection's focus with the pointer while the anchor
    /// stays where the press was. A drag after a double click keeps the word — the second press
    /// already said what to select.
    /// </summary>
    public bool HandlePointer(PointerPhase phase, Point position, KeyModifiers modifiers, int clicks)
    {
        switch (phase)
        {
            case PointerPhase.Down:
            {
                var at = PositionAt(position);
                if (clicks >= 3) SelectLine(at.Line);
                else if (clicks == 2) SelectWord(at);
                else if ((modifiers & KeyModifiers.Shift) != 0) Selection = new CodeRange(_selection.Anchor, at);
                else Selection = new CodeRange(at);
                _dragging = clicks < 2;
                return true;
            }
            case PointerPhase.Move:
            {
                if (!_dragging) return false;
                var at = PositionAt(position);
                if (at == _selection.Focus) return false;
                Selection = new CodeRange(_selection.Anchor, at);
                return true;
            }
            case PointerPhase.Up:
                _dragging = false;
                return false;
            default:
                return false;
        }
    }

    // ---- editing ------------------------------------------------------------------------------

    /// <summary>Replaces a range with text — the primitive every other edit is written in, and the
    /// one an IDE calls to apply a refactor, a formatter or a language server's edit.</summary>
    public bool Apply(CodeRange range, string text) => Edit(range, text, false);

    /// <summary>
    /// The one door every change to the document goes through. <paramref name="typed"/> says a
    /// person typed it, which is what lets undo join it to the run before (see <see cref="CodeEdit.Typed"/>).
    /// </summary>
    private bool Edit(CodeRange range, string text, bool typed)
    {
        if (ReadOnly) return false;

        var ordered = new CodeRange(_document.Clamp(range.Start), _document.Clamp(range.End));
        var removed = _document.TextIn(ordered);
        if (removed.Length == 0 && text.Length == 0) return false;

        var before = _selection;
        var next = _document.Replace(ordered, text, out var caret);
        var line = ordered.Start.Line;
        var linesRemoved = ordered.End.Line - ordered.Start.Line;
        var linesInserted = caret.Line - ordered.Start.Line;

        _document = next;
        _selection = new CodeRange(caret);
        _revealVersion++;
        var edit = new CodeEdit(ordered, removed, text, before, _selection, typed);
        History.Record(edit);
        Highlighter.LineChanged(_document, line, linesInserted, linesRemoved);
        WidthsChanged(line, linesInserted, linesRemoved);

        Changed?.Invoke(edit);
        SelectionChanged?.Invoke(_selection);
        _desiredCell = -1;
        return true;
    }

    /// <summary>Types text at the caret, replacing the selection — including a paste.</summary>
    public bool Insert(string text) => Apply(_selection, text);

    /// <summary>
    /// Types one character, with the pairing behaviours a code editor is expected to have: an
    /// opening bracket auto-closes, a closing bracket typed over its own auto-inserted twin steps
    /// over it instead of doubling it, and a quote around a selection wraps rather than replaces.
    /// </summary>
    public bool Type(char c)
    {
        if (ReadOnly) return false;
        CodeLanguageRules rules = Rules;

        // Wrapping a selection in a pair keeps the selection — that is the point of doing it.
        if (!_selection.IsEmpty)
        {
            foreach (var (open, close) in rules.Brackets)
            {
                if (c != open) continue;
                var text = _document.TextIn(_selection);
                return Edit(_selection, open + text + close, true);
            }
            foreach (var quote in rules.Quotes)
            {
                if (c != quote) continue;
                var text = _document.TextIn(_selection);
                return Edit(_selection, quote + text + quote, true);
            }
        }

        var line = _document.Line(Caret.Line);
        var after = Caret.Column < line.Length ? line[Caret.Column] : '\0';

        // Typing the closing half over the one that was auto-inserted just steps over it.
        foreach (var (_, close) in rules.Brackets)
        {
            if (c == close && after == close)
            {
                Selection = new CodeRange(Caret with { Column = Caret.Column + 1 });
                return true;
            }
        }
        foreach (var quote in rules.Quotes)
        {
            if (c == quote && after == quote)
            {
                Selection = new CodeRange(Caret with { Column = Caret.Column + 1 });
                return true;
            }
        }

        // Auto-close, but only where a closing character would not be in the way of real text.
        foreach (var (open, close) in rules.Brackets)
        {
            if (c != open) continue;
            if (after == '\0' || char.IsWhiteSpace(after) || rules.Brackets.Any(p => p.Close == after))
            {
                if (!Edit(_selection, $"{open}{close}", true)) return false;
                Selection = new CodeRange(Caret with { Column = Caret.Column - 1 });
                return true;
            }
        }
        foreach (var quote in rules.Quotes)
        {
            if (c != quote) continue;
            var before = Caret.Column > 0 ? line[Caret.Column - 1] : '\0';
            // Never inside a word: an apostrophe in `don't` is not an opening quote.
            if (CodeDocument.IsWordChar(before) || CodeDocument.IsWordChar(after)) break;
            if (after == '\0' || char.IsWhiteSpace(after))
            {
                if (!Edit(_selection, $"{quote}{quote}", true)) return false;
                Selection = new CodeRange(Caret with { Column = Caret.Column - 1 });
                return true;
            }
        }

        // A closing bracket typed where only indentation stands before it steps back one level, to
        // the block it closes: what the language's OutdentOn has always said, and nothing read.
        if (_selection.IsEmpty && rules.OutdentOn.Contains(c))
        {
            var indent = line[..Caret.Column];
            if (indent.Length > 0 && indent.Trim().Length == 0)
            {
                var off = StepOff(indent);
                return Edit(new CodeRange(new CodePosition(Caret.Line, 0), Caret), indent[off..] + c, true);
            }
        }

        return Edit(_selection, c.ToString(), true);
    }

    /// <summary>
    /// Enter, with the indentation a reader expects: the new line inherits the current one's, and
    /// gains a level when the line ends on something that opens one. Between a pair — the caret
    /// sitting inside <c>{}</c> — it opens the block and puts the closing brace on its own line.
    /// </summary>
    public bool InsertNewLine()
    {
        if (ReadOnly) return false;
        CodeLanguageRules rules = Rules;
        var line = _document.Line(Caret.Line);
        var indent = _document.IndentOf(Caret.Line);
        var step = rules.InsertSpaces ? new string(' ', rules.IndentWidth) : "\t";

        var beforeCaret = line[..Math.Min(Caret.Column, line.Length)].TrimEnd();
        var afterCaret = Caret.Column < line.Length ? line[Caret.Column..].TrimStart() : string.Empty;

        var opens = beforeCaret.Length > 0 && rules.IndentAfter.Contains(beforeCaret[^1]);
        var closesNext = afterCaret.Length > 0 && rules.OutdentOn.Contains(afterCaret[0]);

        if (opens && closesNext)
        {
            // Between the pair: an empty indented line, and the closer drops below it.
            if (!Apply(_selection, $"\n{indent}{step}\n{indent}")) return false;
            Selection = new CodeRange(new CodePosition(Caret.Line - 1, indent.Length + step.Length));
            return true;
        }
        return Apply(_selection, "\n" + indent + (opens ? step : string.Empty));
    }

    /// <summary>Backspace: the selection if there is one, else the character before the caret —
    /// and a whole indent step when the caret sits in leading whitespace, because that is what put
    /// it there.</summary>
    public bool DeleteBackward(CodeMotion motion = CodeMotion.Character)
    {
        if (ReadOnly) return false;
        if (!_selection.IsEmpty) return Apply(_selection, string.Empty);

        if (motion == CodeMotion.Word)
        {
            var start = MoveTo(Caret, CodeMotion.Word, CodeDirection.Backward);
            return Apply(new CodeRange(start, Caret), string.Empty);
        }

        // Everything left of the caret on its line (⌘⌫). At column zero there is nothing to its left
        // on this line, so it joins the line above, as a plain Backspace would.
        if (motion == CodeMotion.LineBoundary && Caret.Column > 0)
            return Apply(new CodeRange(Caret with { Column = 0 }, Caret), string.Empty);

        var line = _document.Line(Caret.Line);
        var indent = _document.IndentOf(Caret.Line).Length;
        if (Caret.Column > 0 && Caret.Column <= indent && Rules.InsertSpaces)
        {
            // Back to the previous stop ON SCREEN, over a tab in the indent too.
            var width = Rules.IndentWidth;
            var cells = CellsOf(Caret.Line);
            var cell = cells.CellOf(Caret.Column);
            var stop = cell % width == 0 ? cell - width : cell - cell % width;
            return Apply(new CodeRange(Caret with { Column = cells.ColumnAt(stop) }, Caret), string.Empty);
        }

        // Deleting the opening half of an auto-inserted pair takes the closing half with it.
        if (Caret.Column > 0 && Caret.Column < line.Length)
        {
            var before = line[Caret.Column - 1];
            var after = line[Caret.Column];
            var paired = Rules.Brackets.Any(p => p.Open == before && p.Close == after)
                || (Rules.Quotes.Contains(before) && before == after);
            if (paired)
            {
                return Apply(new CodeRange(Caret with { Column = Caret.Column - 1 },
                    Caret with { Column = Caret.Column + 1 }), string.Empty);
            }
        }

        var previous = Before(Caret);
        return previous != Caret && Apply(new CodeRange(previous, Caret), string.Empty);
    }

    /// <summary>Delete forward — the key labelled Delete on a full keyboard, fn+Backspace on a Mac.</summary>
    public bool DeleteForward(CodeMotion motion = CodeMotion.Character)
    {
        if (ReadOnly) return false;
        if (!_selection.IsEmpty) return Apply(_selection, string.Empty);

        var to = motion == CodeMotion.Word
            ? MoveTo(Caret, CodeMotion.Word, CodeDirection.Forward)
            : After(Caret);
        return to != Caret && Apply(new CodeRange(Caret, to), string.Empty);
    }

    // ---- indentation and comments -------------------------------------------------------------

    /// <summary>
    /// Tab. With a selection it indents every line it touches (that is what Tab means in an
    /// editor); with a caret it inserts one step — to the NEXT tab stop, not a fixed number of
    /// spaces, so a column stays a column.
    /// </summary>
    public bool Indent()
    {
        if (ReadOnly) return false;
        if (_selection.IsEmpty)
        {
            if (!Rules.InsertSpaces) return Apply(_selection, "\t");
            // The stop is ON SCREEN: counted in columns, a caret after a tab or a wide character
            // stopped short of it or ran past it.
            var width = Rules.IndentWidth;
            var cell = CellsOf(Caret.Line).CellOf(Caret.Column);
            return Apply(_selection, new string(' ', width - cell % width));
        }
        return ShiftLines(add: true);
    }

    /// <summary>Shift+Tab: takes one step off every selected line, and off the caret's line when
    /// there is no selection.</summary>
    public bool Outdent() => !ReadOnly && ShiftLines(add: false);

    private bool ShiftLines(bool add)
    {
        var step = Rules.InsertSpaces ? new string(' ', Rules.IndentWidth) : "\t";
        var first = _selection.Start.Line;
        var last = _selection.End.Line;
        // A selection that ends at column 0 does not include that line — the same rule every
        // editor uses, and the reason shift+down then Tab does not indent one line too many.
        if (last > first && _selection.End.Column == 0) last--;

        // What each line gains (Tab) or loses (Shift+Tab), all of it at column 0: the selection's
        // two ends move by their OWN line's change, which is what keeps it what it was.
        var lines = new List<string>();
        var changes = new List<int>();
        for (var line = first; line <= last; line++)
        {
            var text = _document.Line(line);
            var change = add ? (text.Length == 0 ? 0 : step.Length) : StepOff(text);
            lines.Add(add ? (change == 0 ? text : step + text) : text[change..]);
            changes.Add(change);
        }

        // Read BEFORE the edit: the edit leaves the caret at its end, which is what the selection
        // used to be rebuilt from.
        var anchor = _selection.Anchor;
        var focus = _selection.Focus;
        var range = new CodeRange(new CodePosition(first, 0),
            new CodePosition(last, _document.Line(last).Length));
        if (!Apply(range, string.Join("\n", lines))) return false;

        Selection = new CodeRange(ShiftedBy(anchor, first, last, changes, add),
            ShiftedBy(focus, first, last, changes, add));
        return true;
    }

    /// <summary>Where a selection's end goes when the lines it may be on gained or lost
    /// <paramref name="changes"/> at column 0 (see <see cref="Shifted"/>).</summary>
    private static CodePosition ShiftedBy(CodePosition position, int first, int last, List<int> changes,
        bool add)
    {
        if (position.Line < first || position.Line > last) return position;
        var change = changes[position.Line - first];
        return new CodePosition(position.Line,
            add ? Shifted(position.Column, 0, 0, change) : Shifted(position.Column, 0, change, 0));
    }

    /// <summary>
    /// Where <paramref name="column"/> ends up after its line had <paramref name="removed"/> units
    /// taken out at <paramref name="at"/> and <paramref name="inserted"/> put in there: before the
    /// change it stays, inside what went it lands where that began, after it it moves with the text.
    /// A column exactly at <paramref name="at"/> stays before what was put in, which is what keeps a
    /// selection of whole lines whole when they are indented.
    /// </summary>
    private static int Shifted(int column, int at, int removed, int inserted) =>
        column <= at ? column : column < at + removed ? at : column - removed + inserted;

    /// <summary>How much of one step of indentation <paramref name="text"/> begins with: a tab is a
    /// whole step, and a line indented by fewer spaces than a step gives up all of them.</summary>
    private int StepOff(string text)
    {
        if (text.Length > 0 && text[0] == '\t') return 1;
        var off = 0;
        while (off < text.Length && off < Rules.IndentWidth && text[off] == ' ') off++;
        return off;
    }

    /// <summary>
    /// ⌘/ — comments the selected lines, or un-comments them when they are ALL commented already.
    /// A language with no line comment does nothing, which is the only correct answer for JSON.
    /// </summary>
    public bool ToggleLineComment()
    {
        if (ReadOnly || Rules.LineComment is not { } marker) return false;

        var first = _selection.Start.Line;
        var last = _selection.End.Line;
        if (last > first && _selection.End.Column == 0) last--;

        var allCommented = true;
        for (var line = first; line <= last; line++)
        {
            var text = _document.Line(line).TrimStart();
            if (text.Length == 0) continue;
            if (!text.StartsWith(marker, StringComparison.Ordinal)) { allCommented = false; break; }
        }

        // Each line's change as (where, how much went, how much came), so the selection's ends can
        // follow their own line's markers instead of collapsing to the caret the edit leaves.
        var lines = new List<string>();
        var ats = new List<int>();
        var removals = new List<int>();
        var insertions = new List<int>();
        for (var line = first; line <= last; line++)
        {
            var text = _document.Line(line);
            if (allCommented)
            {
                var at = text.IndexOf(marker, StringComparison.Ordinal);
                if (at < 0)
                {
                    lines.Add(text);
                    ats.Add(0);
                    removals.Add(0);
                    insertions.Add(0);
                    continue;
                }
                var after = at + marker.Length;
                if (after < text.Length && text[after] == ' ') after++;
                lines.Add(text[..at] + text[after..]);
                ats.Add(at);
                removals.Add(after - at);
                insertions.Add(0);
            }
            else
            {
                var indent = _document.IndentOf(line);
                lines.Add(text.Length == 0 ? marker + " " : indent + marker + " " + text[indent.Length..]);
                ats.Add(text.Length == 0 ? 0 : indent.Length);
                removals.Add(0);
                insertions.Add(marker.Length + 1);
            }
        }

        var anchor = _selection.Anchor;
        var focus = _selection.Focus;
        var range = new CodeRange(new CodePosition(first, 0),
            new CodePosition(last, _document.Line(last).Length));
        if (!Apply(range, string.Join("\n", lines))) return false;

        Selection = new CodeRange(Commented(anchor, first, last, ats, removals, insertions),
            Commented(focus, first, last, ats, removals, insertions));
        return true;
    }

    /// <summary>Where a selection's end goes when the lines it may be on had their markers put in
    /// or taken out (see <see cref="Shifted"/>).</summary>
    private static CodePosition Commented(CodePosition position, int first, int last, List<int> ats,
        List<int> removals, List<int> insertions)
    {
        if (position.Line < first || position.Line > last) return position;
        var index = position.Line - first;
        return new CodePosition(position.Line,
            Shifted(position.Column, ats[index], removals[index], insertions[index]));
    }

    // ---- movement -----------------------------------------------------------------------------

    /// <summary>
    /// Moves the caret, optionally EXTENDING the selection — the one method behind every arrow,
    /// Home, End, PageUp and their shifted twins.
    /// </summary>
    public void Move(CodeMotion motion, CodeDirection direction, bool extend = false, int pageLines = 20)
    {
        // A plain arrow with a selection collapses to its edge rather than moving from the caret —
        // pressing → with text selected puts you after it, not one character further on.
        if (!extend && !_selection.IsEmpty && motion == CodeMotion.Character)
        {
            Selection = new CodeRange(direction == CodeDirection.Forward ? _selection.End : _selection.Start);
            return;
        }

        var target = MoveTo(Caret, motion, direction, pageLines);
        Select(extend ? _selection with { Focus = target } : new CodeRange(target),
            keepCell: motion is CodeMotion.Line or CodeMotion.Page);
    }

    /// <summary>Where a movement LANDS, without moving anything — an IDE computing a jump.</summary>
    public CodePosition MoveTo(CodePosition from, CodeMotion motion, CodeDirection direction,
        int pageLines = 20)
    {
        var forward = direction == CodeDirection.Forward;
        switch (motion)
        {
            case CodeMotion.Character:
                _desiredCell = -1;
                return forward ? After(from) : Before(from);

            case CodeMotion.Word:
                _desiredCell = -1;
                return WordStep(from, forward);

            case CodeMotion.Line:
            {
                // The remembered CELL is what makes a run of ↓ through ragged lines come back to
                // where it started instead of collapsing to the shortest one, and what keeps it in
                // the same place on screen across a line indented with tabs.
                if (_desiredCell < 0) _desiredCell = CellsOf(from.Line).CellOf(from.Column);
                var line = VisibleLineFrom(from.Line, forward ? 1 : -1);
                return new CodePosition(line, CellsOf(line).ColumnAt(_desiredCell));
            }

            case CodeMotion.Page:
            {
                if (_desiredCell < 0) _desiredCell = CellsOf(from.Line).CellOf(from.Column);
                var line = VisibleLineFrom(from.Line, forward ? pageLines : -pageLines);
                return new CodePosition(line, CellsOf(line).ColumnAt(_desiredCell));
            }

            case CodeMotion.LineBoundary:
                _desiredCell = -1;
                return forward ? _document.LineEnd(from) : _document.LineStart(from);

            default:
                _desiredCell = -1;
                return forward ? _document.End : CodePosition.Start;
        }
    }

    /// <summary>
    /// The line <paramref name="steps"/> lines of the VIEW away from <paramref name="line"/>: lines a
    /// fold hides are not counted and not landed on, as an editor steps over a fold, and a step past
    /// either end stops at the last line that is drawn. With no rows every line is drawn, and a step
    /// is a line.
    /// </summary>
    private int VisibleLineFrom(int line, int steps)
    {
        var last = _document.LineCount - 1;
        if (Grid.Rows is not { } rows) return Math.Clamp(line + steps, 0, last);
        var direction = steps < 0 ? -1 : 1;
        var here = line;
        var left = Math.Abs(steps);
        for (var next = line + direction; left > 0 && next >= 0 && next <= last; next += direction)
        {
            if (!rows.IsVisible(next)) continue;
            here = next;
            left--;
        }
        return here;
    }

    /// <summary>
    /// A word step, over whole TEXT ELEMENTS: an element is a word character when its first character
    /// is, so the accent written as a mark after its letter belongs to the letter's word. It read one
    /// unit at a time and stopped between the e of a decomposed café and its accent, a column no
    /// caret should hold.
    /// </summary>
    private CodePosition WordStep(CodePosition from, bool forward)
    {
        var here = _document.Clamp(from);
        var line = _document.Line(here.Line);
        var cells = CellsOf(here.Line);

        if (forward)
        {
            if (here.Column >= line.Length) return After(here);
            var i = here.Column;
            // Skip what we are on, then the whitespace after it — one press lands on the next word.
            if (CodeDocument.IsWordChar(line[i]))
                while (i < line.Length && CodeDocument.IsWordChar(line[i])) i = cells.Next(i);
            else if (!char.IsWhiteSpace(line[i]))
                while (i < line.Length && !CodeDocument.IsWordChar(line[i])
                       && !char.IsWhiteSpace(line[i])) i = cells.Next(i);
            while (i < line.Length && char.IsWhiteSpace(line[i])) i = cells.Next(i);
            return here with { Column = i };
        }

        if (here.Column == 0) return Before(here);
        // The element that ends at a column begins at Previous(column), and its first character is
        // what it is.
        var back = here.Column;
        while (back > 0 && char.IsWhiteSpace(line[cells.Previous(back)])) back = cells.Previous(back);
        if (back > 0 && CodeDocument.IsWordChar(line[cells.Previous(back)]))
            while (back > 0 && CodeDocument.IsWordChar(line[cells.Previous(back)])) back = cells.Previous(back);
        else
            while (back > 0 && !CodeDocument.IsWordChar(line[cells.Previous(back)])
                   && !char.IsWhiteSpace(line[cells.Previous(back)])) back = cells.Previous(back);
        return here with { Column = back };
    }

    // ---- selection ----------------------------------------------------------------------------

    public void SelectAll() => Selection = new CodeRange(CodePosition.Start, _document.End);

    /// <summary>The word under a position — a double click.</summary>
    /// <summary>
    /// The word at <paramref name="at"/> — a double click — widened to whole text elements, so the
    /// accent written as a mark after the word's last letter is in it.
    /// </summary>
    public void SelectWord(CodePosition at)
    {
        var word = _document.WordAt(at);
        if (word.IsEmpty)
        {
            Selection = word;
            return;
        }
        var cells = CellsOf(word.Start.Line);
        Selection = new CodeRange(
            word.Start with { Column = cells.ElementAt(cells.IndexOf(word.Start.Column)).Start },
            word.End with { Column = cells.Next(word.End.Column - 1) });
    }

    /// <summary>The whole line — a triple click.</summary>
    public void SelectLine(int line)
    {
        var last = Math.Clamp(line, 0, _document.LineCount - 1);
        Selection = new CodeRange(new CodePosition(last, 0),
            last + 1 < _document.LineCount
                ? new CodePosition(last + 1, 0)
                : new CodePosition(last, _document.Line(last).Length));
    }

    /// <summary>What a copy puts on the clipboard: the selection, or the whole line when there is
    /// none — copying with nothing selected takes the line, in every editor worth using.</summary>
    public string CopyText()
    {
        if (!_selection.IsEmpty)
        {
            _wholeLineCopy = null;
            return _document.TextIn(_selection);
        }
        var line = _document.Line(Caret.Line) + "\n";
        _wholeLineCopy = line;
        return line;
    }

    /// <summary>Cut is copy, then delete — and with no selection it takes the whole line.</summary>
    public string Cut()
    {
        var text = CopyText();
        if (_selection.IsEmpty) SelectLine(Caret.Line);
        Apply(_selection, string.Empty);
        return text;
    }

    /// <summary>
    /// The LINE the last copy or cut took with nothing selected, or null. The clipboard carries text
    /// and nothing else, so this is how a paste recognises its own whole-line copy: a line copied that
    /// way goes back in as a line, above the caret's, never into the middle of one.
    /// </summary>
    private string? _wholeLineCopy;

    /// <summary>
    /// Text from the clipboard, placed the way a paste places it: over the selection, or — when it is
    /// the whole line a copy with nothing selected just took — as a line of its own ABOVE the caret's,
    /// with the caret staying where it was in its text. Every editor worth using pastes a copied line
    /// that way; inserting it at the caret splits the line you were on in two.
    /// </summary>
    public bool Paste(string text)
    {
        if (ReadOnly || text.Length == 0) return false;
        TabMovesFocus = false;
        EndComposition();
        var normalized = CodeDocument.FromText(text).Text;
        if (_selection.IsEmpty && _wholeLineCopy is { } line && normalized == line)
        {
            var caret = Caret;
            var lineStart = new CodePosition(caret.Line, 0);
            if (!Apply(new CodeRange(lineStart), normalized)) return false;
            Selection = new CodeRange(new CodePosition(caret.Line + 1, caret.Column));
            return true;
        }
        return Insert(text);
    }

    // ---- history ------------------------------------------------------------------------------

    public bool Undo()
    {
        if (ReadOnly) return false;
        EndComposition();
        var next = History.Undo(_document, out var selection, out var replaced, out var written);
        if (next is null) return false;
        _document = next;
        _revealVersion++;
        _selection = new CodeRange(next.Clamp(selection.Anchor), next.Clamp(selection.Focus));
        _desiredCell = -1;
        // One replacement, as an edit is: the colours and the widths follow the lines it touched,
        // where both were thrown away and measured again over the whole file.
        var line = replaced.Start.Line;
        var linesInserted = written.End.Line - written.Start.Line;
        var linesRemoved = replaced.End.Line - replaced.Start.Line;
        Highlighter.LineChanged(_document, line, linesInserted, linesRemoved);
        WidthsChanged(line, linesInserted, linesRemoved);
        Changed?.Invoke(null);
        SelectionChanged?.Invoke(_selection);
        return true;
    }

    public bool Redo()
    {
        if (ReadOnly) return false;
        EndComposition();
        var next = History.Redo(_document, out var selection, out var replaced, out var written);
        if (next is null) return false;
        _document = next;
        _revealVersion++;
        _selection = new CodeRange(next.Clamp(selection.Anchor), next.Clamp(selection.Focus));
        _desiredCell = -1;
        // One replacement, as an edit is: the colours and the widths follow the lines it touched,
        // where both were thrown away and measured again over the whole file.
        var line = replaced.Start.Line;
        var linesInserted = written.End.Line - written.Start.Line;
        var linesRemoved = replaced.End.Line - replaced.Start.Line;
        Highlighter.LineChanged(_document, line, linesInserted, linesRemoved);
        WidthsChanged(line, linesInserted, linesRemoved);
        Changed?.Invoke(null);
        SelectionChanged?.Invoke(_selection);
        return true;
    }

    // ---- finding ------------------------------------------------------------------------------

    /// <summary>
    /// Every match of <paramref name="needle"/> — what a find bar highlights and steps through.
    /// Plain text, not a regular expression: an IDE that wants regex brings its own matcher and
    /// hands the ranges back as decorations.
    /// </summary>
    public IReadOnlyList<CodeRange> FindAll(string needle, bool matchCase = false)
    {
        var matches = new List<CodeRange>();
        if (needle.Length == 0) return matches;
        // Case folded into the STRINGS rather than carried as a comparison flag: the flag is a .NET
        // concept with no twin in `indexOf`, so a case-insensitive search would have quietly become
        // a case-sensitive one on the web half. Folding means the same thing on both.
        var pin = matchCase ? needle : needle.ToLowerInvariant();

        for (var line = 0; line < _document.LineCount; line++)
        {
            var raw = _document.Line(line);
            var text = matchCase ? raw : raw.ToLowerInvariant();
            var at = text.IndexOf(pin, StringComparison.Ordinal);
            while (at >= 0)
            {
                matches.Add(new CodeRange(new CodePosition(line, at),
                    new CodePosition(line, at + needle.Length)));
                at = at + pin.Length <= text.Length
                    ? text.IndexOf(pin, at + pin.Length, StringComparison.Ordinal)
                    : -1;
            }
        }
        return matches;
    }

    /// <summary>The match AFTER the caret, wrapping to the top — the Enter of a find bar.</summary>
    public CodeRange? FindNext(string needle, bool matchCase = false, bool backward = false) =>
        NextOf(FindAll(needle, matchCase), backward);

    /// <summary>
    /// The one of <paramref name="matches"/> after the selection, wrapping to the first, or before
    /// it, wrapping to the last, when <paramref name="backward"/>. The matches are in document order
    /// and never overlap, as <see cref="FindAll"/> answers them, so they are searched by halving: a
    /// find bar steps through the list it already holds, where each Enter searched the whole file
    /// again.
    /// </summary>
    public CodeRange? NextOf(IReadOnlyList<CodeRange> matches, bool backward = false)
    {
        if (matches.Count == 0) return null;
        var low = 0;
        var high = matches.Count;
        if (backward)
        {
            // The first match that ends past the selection's start; the one before it is the answer.
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (matches[middle].End <= _selection.Start) low = middle + 1;
                else high = middle;
            }
            return low > 0 ? matches[low - 1] : matches[matches.Count - 1];
        }
        // The first match that starts at or after the selection's end.
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (matches[middle].Start < _selection.End) low = middle + 1;
            else high = middle;
        }
        return low < matches.Count ? matches[low] : matches[0];
    }

    /// <summary>
    /// The bracket that PAIRS with the one at a position, or null — what draws the outline around
    /// a matching brace and what ⌘⇧\ jumps to. Counts nesting, so it lands on the right one.
    /// </summary>
    /// <summary>
    /// The bracket pair the CARET is against — the one an editor outlines. A caret sits BETWEEN
    /// characters, so it belongs to the bracket on either side of it, and the one behind wins
    /// (having just typed `)`, that is the one you mean).
    /// </summary>
    public (CodePosition Here, CodePosition There)? BracketAtCaret()
    {
        var caret = Caret;
        if (caret.Column > 0)
        {
            var behind = caret with { Column = caret.Column - 1 };
            if (MatchingBracket(behind) is { } match) return (behind, match);
        }
        return MatchingBracket(caret) is { } ahead ? (caret, ahead) : null;
    }

    public CodePosition? MatchingBracket(CodePosition at)
    {
        var here = _document.Clamp(at);
        var line = _document.Line(here.Line);
        if (here.Column >= line.Length) return null;
        var c = line[here.Column];

        foreach (var (open, close) in Rules.Brackets)
        {
            if (c == open) return ScanForBracket(here, open, close, forward: true);
            if (c == close) return ScanForBracket(here, close, open, forward: false);
        }
        return null;
    }

    /// <summary>
    /// The bracket that closes (or opens) the one at <paramref name="from"/>, by depth. A SCAN, not
    /// a caret: a bracket is one code unit and never part of a surrogate pair, so the text is read
    /// unit by unit. Stepping it the way the caret steps built every line's text elements on the
    /// way, and this runs on every frame the caret is on a bracket, across the whole file.
    /// </summary>
    private CodePosition? ScanForBracket(CodePosition from, char same, char other, bool forward)
    {
        var depth = 0;
        var line = from.Line;
        var column = from.Column;
        while (true)
        {
            var text = _document.Line(line);
            while (forward ? column < text.Length : column >= 0)
            {
                if (column < text.Length)
                {
                    var c = text[column];
                    if (c == same) depth++;
                    else if (c == other)
                    {
                        depth--;
                        if (depth == 0) return new CodePosition(line, column);
                    }
                }
                column += forward ? 1 : -1;
            }

            line += forward ? 1 : -1;
            if (line < 0 || line >= _document.LineCount) return null;
            column = forward ? 0 : _document.Line(line).Length - 1;
        }
    }
}
