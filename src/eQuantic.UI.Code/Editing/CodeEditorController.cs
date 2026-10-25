using System.Globalization;
using System.Text;
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
/// The app owns an instance. Every mutation raises <see cref="Changed"/> with the <see cref="CodeChange"/>
/// that made it, which is what a dirty flag, a language server and a diff all subscribe to —
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
    private bool _readOnly;

    public CodeEditorController(string text = "", ICodeLanguage? language = null)
    {
        _document = CodeDocument.FromText(text);
        _selection = new CodeRange(CodePosition.Start);
        Highlighter = new CodeHighlighter(language ?? CodeLanguages.PlainText);
        Completion = new CodeCompletion(this);
        Diagnostics = new CodeDiagnostics(this);
        Hover = new CodeHoverSession(this);
        SignatureHelp = new CodeSignatureHelpSession(this);
        Snippet = new CodeSnippetSession(this);
    }

    /// <summary>The text, as lines. Replacing it is a full reset — a file was opened.</summary>
    public CodeDocument Document
    {
        get => _document;
        set
        {
            Completion.Dismiss();
            _document = value;
            _widths = null;
            _selection = new CodeRange(_document.Clamp(_selection.Focus));
            // A file opened has one caret.
            _others.Clear();
            _composition = null;
            Highlighter.Invalidate();
            History.Clear();
            Diagnostics.Reset();
            _revealVersion++;
            Raise(new CodeChange([], Replaced: true), moved: false);
        }
    }

    /// <summary>Where the caret is, and what it has selected: the primary caret's selection, with several
    /// (<see cref="Selections"/>). Setting it leaves that one selection, every other caret gone.</summary>
    public CodeRange Selection
    {
        get => _selection;
        set
        {
            var had = _others.Count > 0;
            _others.Clear();
            Select(value, keepCell: false, moved: had);
        }
    }

    /// <summary>
    /// The carets besides the primary, in document order (#1012). Empty with one caret, which is nearly
    /// always, and then every command runs as it ran before there were several.
    /// </summary>
    private readonly List<CodeCaret> _others = [];

    /// <summary>
    /// Every selection, the primary first and the others in document order (#1012). Two never overlap, and
    /// a caret never stands where another selection begins or ends: the carets that meet are made one, as
    /// VS Code merges its cursors. The primary is the selection placed last, as Rider's primary caret is:
    /// the one brought into view, the one an input method composes at, and the one the completion list,
    /// the panel and the card follow. Placing them makes the first the primary.
    /// </summary>
    public IReadOnlyList<CodeRange> Selections
    {
        get
        {
            var all = new List<CodeRange>(_others.Count + 1) { _selection };
            foreach (var other in _others) all.Add(other.Selection);
            return all;
        }
        set
        {
            if (value.Count == 0) return;
            _others.Clear();
            for (var i = 1; i < value.Count; i++) _others.Add(new CodeCaret(OnElementEdges(value[i]), -1));
            // Made one where they meet BEFORE anyone hears of them: heard first, the primary a listener read
            // was not the one the merge left.
            _occurrence = null;
            Placed(new CodeCaret(value[0], -1));
        }
    }

    /// <summary>
    /// Places the primary selection, the others where they are. Only a run of ↑ and ↓ keeps the cell it
    /// aims at (<paramref name="keepCell"/>); anything else that places the caret forgets it, a click, an
    /// undo and the app included, or ↓ after a click went back to the column the run before the click
    /// had aimed at. <paramref name="moved"/> says something else moved with it, the other carets gone,
    /// and is heard even when the primary stays.
    /// </summary>
    private void Select(CodeRange value, bool keepCell, bool moved = false)
    {
        if (!keepCell) _desiredCell = -1;
        var next = OnElementEdges(value);
        if (next == _selection && !moved) return;
        _occurrence = null;
        // Moving the caret ENDS the typing run: the next character starts a new undo step,
        // because a person who moved and typed did two things.
        _selection = next;
        _revealVersion++;
        // Inside a command's step, the move is heard once the step is made, with its edits, and it ends the
        // run once the step is recorded: ended here, the step that moved the caret started a run.
        if (_step is not null)
        {
            _stepMoved = true;
            return;
        }
        History.Break();
        SelectionChanged?.Invoke(next);
    }

    /// <summary>
    /// Puts the carets in document order and makes one of any that overlap, or that meet where one of
    /// them is a caret, as VS Code merges its cursors: two selections that only touch stay two, the text
    /// between them none. The primary stays the primary, grown by whatever it was merged with.
    /// </summary>
    private void Merge()
    {
        if (_others.Count == 0) return;
        var carets = new List<CodeCaret>(_others) { new CodeCaret(_selection, _desiredCell) };
        var primary = carets.Count - 1;
        var order = new List<int>();
        for (var i = 0; i < carets.Count; i++) order.Add(i);
        order.Sort((a, b) => carets[a].Selection.Start.CompareTo(carets[b].Selection.Start));

        var merged = new List<CodeCaret>();
        var primaryAt = -1;
        foreach (var i in order)
        {
            var caret = carets[i];
            if (merged.Count > 0)
            {
                var last = merged[^1].Selection;
                var next = caret.Selection;
                var meets = last.IsEmpty || next.IsEmpty ? next.Start <= last.End : next.Start < last.End;
                if (meets)
                {
                    var end = next.End > last.End ? next.End : last.End;
                    // The one that keeps its direction is the primary, if either is; the later otherwise.
                    var keeps = i == primary ? next : merged.Count - 1 == primaryAt ? last : next;
                    var backwards = keeps.Focus < keeps.Anchor;
                    var range = backwards ? new CodeRange(end, last.Start) : new CodeRange(last.Start, end);
                    merged[^1] = new CodeCaret(range, i == primary ? caret.Cell : merged[^1].Cell);
                    if (i == primary) primaryAt = merged.Count - 1;
                    continue;
                }
            }
            if (i == primary) primaryAt = merged.Count;
            merged.Add(caret);
        }

        _others.Clear();
        for (var i = 0; i < merged.Count; i++)
        {
            if (i == primaryAt)
            {
                _selection = merged[i].Selection;
                _desiredCell = merged[i].Cell;
            }
            else _others.Add(merged[i]);
        }
    }

    /// <summary>
    /// <paramref name="value"/> in the document, with no end inside a text element. A position INSIDE one
    /// is one no caret may hold: an edit from there splits the element (a Backspace at column 1 of an
    /// emoji took its high half). A caret goes to the element's start, where it is drawn, and a range
    /// grows to take in the elements it cuts, keeping its direction. Every selection is placed through
    /// it, the one an edit leaves included.
    /// </summary>
    private CodeRange OnElementEdges(CodeRange value)
    {
        var anchor = _document.Clamp(value.Anchor);
        var focus = _document.Clamp(value.Focus);
        var backwards = new CodeRange(anchor, focus).Start != anchor;
        return anchor == focus
            ? new CodeRange(Boundary(anchor, after: false))
            : new CodeRange(Boundary(anchor, after: backwards), Boundary(focus, after: !backwards));
    }

    /// <summary>The caret — the moving end of the selection.</summary>
    public CodePosition Caret => _selection.Focus;

    public CodeHighlighter Highlighter { get; }
    public CodeHistory History { get; } = new();

    /// <summary>
    /// The completion this editor offers as a word is typed: its providers, and the list while one
    /// is open. An IDE adds its language service to <see cref="CodeCompletion.Providers"/>.
    /// </summary>
    public CodeCompletion Completion { get; }

    /// <summary>
    /// The problems this editor shows: what the app reports and what its providers answer as the text
    /// changes, carried through every edit. An IDE adds its compiler to
    /// <see cref="CodeDiagnostics.Providers"/>, or reports a build's output itself.
    /// </summary>
    public CodeDiagnostics Diagnostics { get; }

    /// <summary>The card that explains the code under the pointer or the caret. An IDE adds its language
    /// service to <see cref="CodeHoverSession.Providers"/>.</summary>
    public CodeHoverSession Hover { get; }

    /// <summary>The panel that explains the call being written: its signatures and the argument the caret is
    /// on. An IDE adds its language service to <see cref="CodeSignatureHelpSession.Providers"/>.</summary>
    public CodeSignatureHelpSession SignatureHelp { get; }

    /// <summary>The snippet being filled in, and the stop the carets stand at: what Tab, ⇧Tab and Escape walk
    /// and end (#1013). A snippet starts one through <see cref="InsertSnippet"/>.</summary>
    public CodeSnippetSession Snippet { get; }

    /// <summary>The first key of a chord the keymap is waiting for the second of (⌘K, for ⌘K ⌘I), or
    /// null. Any key that is not a modifier ends the wait.</summary>
    internal KeyChord? PendingChord { get; set; }

    /// <summary>
    /// An app's own key bindings, tried before the defaults (<see cref="CodeKeymap.Defaults"/>): a chord
    /// bound here is the app's alone, and one bound to <see cref="CodeCommand.None"/> is given back to the
    /// host. Empty to begin with.
    /// </summary>
    public IList<CodeKeyBinding> KeyBindings { get; } = [];

    public CodeLanguageRules Rules => Highlighter.Language.Rules;

    /// <summary>Whether edits are refused — a viewer, a diff pane, a running debugger. Turning it on
    /// closes a list showing and drops an answer still on its way, since a read-only editor
    /// completes nothing.</summary>
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            _readOnly = value;
            if (value) Completion.Dismiss();
        }
    }

    /// <summary>
    /// Whether the next Tab LEAVES the editor instead of indenting. Escape sets it — the one way out
    /// a keyboard user has from a surface that takes Tab — and any other key, or coming back into
    /// the editor, clears it (see <see cref="CodeKeymap"/>).
    /// </summary>
    public bool TabMovesFocus { get; set; }

    /// <summary>
    /// Raised after every change, once, with the edits that made it (<see cref="CodeChange"/>): one for
    /// a character typed, two for a line moved, and as many as it took back for an undo, in the order
    /// made, each with the selection the change found and the one it left. THE event an IDE builds on.
    /// The document is the change's result by the time it is raised. A change a listener makes while it
    /// hears one is already in the document and is heard after it, by every listener, so a range read
    /// from the document while a change is heard is ahead of the changes heard so far: the editor's own
    /// problems follow each replacement the moment the document takes it, as its colours do.
    /// </summary>
    public event Action<CodeChange>? Changed;

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
        foreach (var selection in SelectionsIn(first, last)) BandsOf(selection, first, last, bands);
        return bands;
    }

    /// <summary>Every selection that reaches lines <paramref name="first"/> to <paramref name="last"/>, the
    /// carets included, in document order: what a view builds marks for (#1012).</summary>
    public IReadOnlyList<CodeRange> SelectionsIn(int first, int last)
    {
        var all = new List<CodeRange>();
        var placed = false;
        foreach (var other in _others)
        {
            if (!placed && _selection.Start < other.Selection.Start)
            {
                if (Reaches(_selection, first, last)) all.Add(_selection);
                placed = true;
            }
            if (Reaches(other.Selection, first, last)) all.Add(other.Selection);
        }
        if (!placed && Reaches(_selection, first, last)) all.Add(_selection);
        return all;
    }

    /// <summary>The lines of lines <paramref name="first"/> to <paramref name="last"/> a caret stands on, each
    /// once, in order: the lines a view washes (#1012).</summary>
    public IReadOnlyList<int> CaretLinesIn(int first, int last)
    {
        var lines = new List<int>();
        foreach (var selection in SelectionsIn(first, last))
        {
            var line = selection.Focus.Line;
            if (line < first || line > last || (lines.Count > 0 && lines[^1] == line)) continue;
            lines.Add(line);
        }
        return lines;
    }

    private static bool Reaches(CodeRange selection, int first, int last) =>
        selection.End.Line >= first && selection.Start.Line <= last;

    /// <summary>One selection's bands on lines <paramref name="first"/> to <paramref name="last"/>, added to
    /// <paramref name="bands"/>.</summary>
    private void BandsOf(CodeRange selection, int first, int last, List<Rect> bands)
    {
        if (selection.IsEmpty) return;
        var start = selection.Start;
        var end = selection.End;
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
    }

    /// <inheritdoc />
    public IReadOnlyList<Rect> Carets
    {
        get
        {
            var carets = new List<Rect>(_others.Count + 1) { CaretRect(Caret) };
            foreach (var other in _others) carets.Add(CaretRect(other.Selection.Focus));
            return carets;
        }
    }

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
    /// text element. Past the end of a line it lands at the end; below every row the grid draws, at
    /// the end of the document, as a press under the code does in any editor. A diff's fillers after
    /// the last line are rows, and land on the last line as any filler lands on its line.
    /// </summary>
    public CodePosition PositionAt(Point point)
    {
        var rows = Grid.Rows?.RowCount ?? _document.LineCount;
        if (point.Y >= Grid.Origin.Y + rows * Grid.Cell.Height) return _document.End;
        var target = _document.Clamp(new CodePosition(Math.Max(0, Grid.LineAt(point.Y)), 0)).Line;
        return new CodePosition(target, CellsOf(target).ColumnAt((point.X - Grid.Origin.X) / Grid.Cell.Width));
    }

    /// <summary>
    /// The character a point on the surface is over, or null when it is over none: left of the code (the
    /// gutter), past the end of its line, above the first row or under the last, or on a row that is no
    /// line of the document (a diff's filler, a folded run). What a pointer resting there explains; a
    /// press asks <see cref="PositionAt"/>, which lands between characters.
    /// </summary>
    public CodePosition? CharacterAt(Point point)
    {
        var rows = Grid.Rows?.RowCount ?? _document.LineCount;
        if (point.X < Grid.Origin.X || point.Y < Grid.Origin.Y) return null;
        var row = (int)MathF.Floor((point.Y - Grid.Origin.Y) / Grid.Cell.Height);
        if (row >= rows) return null;
        if (Grid.Rows is { } map && map.RowAt(row).Kind != CodeRowKind.Line) return null;
        var line = Grid.LineAt(point.Y);
        if (line < 0 || line >= _document.LineCount) return null;
        var column = CellsOf(line).ColumnUnder((point.X - Grid.Origin.X) / Grid.Cell.Width);
        return column < 0 ? null : new CodePosition(line, column);
    }

    /// <inheritdoc />
    public bool HandleKey(string key, KeyModifiers modifiers, KeyboardConvention convention,
        ITextClipboard? clipboard) =>
        CodeKeymap.Handle(this, key, modifiers, convention, clipboard);

    /// <summary>
    /// Runs <paramref name="command"/> as its key would, with no key: what a menu, a command palette or an
    /// app's own shortcut calls. Answers whether it did anything, which is whether the key would have been
    /// claimed: a list's command with no list showing does nothing and answers false. The clipboard is the
    /// one a copy writes and a paste reads; with none, they do nothing, and a host whose clipboard arrives
    /// as events of its own (a browser's copy, cut and paste) keeps the key for those events.
    /// </summary>
    public bool Execute(CodeCommand command, ITextClipboard? clipboard = null)
    {
        switch (command)
        {
            case CodeCommand.CursorLeft: return Step(CodeMotion.Character, CodeDirection.Backward, false);
            case CodeCommand.CursorLeftSelect: return Step(CodeMotion.Character, CodeDirection.Backward, true);
            case CodeCommand.CursorRight: return Step(CodeMotion.Character, CodeDirection.Forward, false);
            case CodeCommand.CursorRightSelect: return Step(CodeMotion.Character, CodeDirection.Forward, true);
            case CodeCommand.CursorUp: return Step(CodeMotion.Line, CodeDirection.Backward, false);
            case CodeCommand.CursorUpSelect: return Step(CodeMotion.Line, CodeDirection.Backward, true);
            case CodeCommand.CursorDown: return Step(CodeMotion.Line, CodeDirection.Forward, false);
            case CodeCommand.CursorDownSelect: return Step(CodeMotion.Line, CodeDirection.Forward, true);
            case CodeCommand.CursorWordLeft: return Step(CodeMotion.Word, CodeDirection.Backward, false);
            case CodeCommand.CursorWordLeftSelect: return Step(CodeMotion.Word, CodeDirection.Backward, true);
            case CodeCommand.CursorWordRight: return Step(CodeMotion.Word, CodeDirection.Forward, false);
            case CodeCommand.CursorWordRightSelect: return Step(CodeMotion.Word, CodeDirection.Forward, true);
            case CodeCommand.CursorLineStart: return Step(CodeMotion.LineBoundary, CodeDirection.Backward, false);
            case CodeCommand.CursorLineStartSelect: return Step(CodeMotion.LineBoundary, CodeDirection.Backward, true);
            case CodeCommand.CursorLineEnd: return Step(CodeMotion.LineBoundary, CodeDirection.Forward, false);
            case CodeCommand.CursorLineEndSelect: return Step(CodeMotion.LineBoundary, CodeDirection.Forward, true);
            case CodeCommand.CursorDocumentStart: return Step(CodeMotion.DocumentBoundary, CodeDirection.Backward, false);
            case CodeCommand.CursorDocumentStartSelect: return Step(CodeMotion.DocumentBoundary, CodeDirection.Backward, true);
            case CodeCommand.CursorDocumentEnd: return Step(CodeMotion.DocumentBoundary, CodeDirection.Forward, false);
            case CodeCommand.CursorDocumentEndSelect: return Step(CodeMotion.DocumentBoundary, CodeDirection.Forward, true);
            case CodeCommand.CursorPageUp: return Step(CodeMotion.Page, CodeDirection.Backward, false);
            case CodeCommand.CursorPageUpSelect: return Step(CodeMotion.Page, CodeDirection.Backward, true);
            case CodeCommand.CursorPageDown: return Step(CodeMotion.Page, CodeDirection.Forward, false);
            case CodeCommand.CursorPageDownSelect: return Step(CodeMotion.Page, CodeDirection.Forward, true);
            case CodeCommand.SelectAll:
                SelectAll();
                return true;
            case CodeCommand.NewLine: return InsertNewLine();
            case CodeCommand.DeleteLeft: return DeleteBackward(CodeMotion.Character);
            case CodeCommand.DeleteRight: return DeleteForward(CodeMotion.Character);
            case CodeCommand.DeleteWordLeft: return DeleteBackward(CodeMotion.Word);
            case CodeCommand.DeleteWordRight: return DeleteForward(CodeMotion.Word);
            case CodeCommand.DeleteToLineStart: return DeleteBackward(CodeMotion.LineBoundary);
            case CodeCommand.Indent: return !ReadOnly && !TabMovesFocus && Indent();
            case CodeCommand.Outdent: return !ReadOnly && !TabMovesFocus && Outdent();
            case CodeCommand.ToggleLineComment: return ToggleLineComment();
            case CodeCommand.Undo: return Undo();
            case CodeCommand.Redo: return Redo();
            case CodeCommand.MoveLinesUp: return MoveLines(CodeDirection.Backward);
            case CodeCommand.MoveLinesDown: return MoveLines(CodeDirection.Forward);
            case CodeCommand.CopyLinesUp: return CopyLines(CodeDirection.Backward);
            case CodeCommand.CopyLinesDown: return CopyLines(CodeDirection.Forward);
            case CodeCommand.DeleteLines: return DeleteLines();
            case CodeCommand.JoinLines: return JoinLines();
            case CodeCommand.SortLinesAscending: return SortLines(descending: false);
            case CodeCommand.SortLinesDescending: return SortLines(descending: true);
            case CodeCommand.InsertLineAbove: return InsertLine(CodeDirection.Backward);
            case CodeCommand.InsertLineBelow: return InsertLine(CodeDirection.Forward);
            case CodeCommand.AddCursorAbove: return AddCursor(CodeDirection.Backward);
            case CodeCommand.AddCursorBelow: return AddCursor(CodeDirection.Forward);
            case CodeCommand.AddNextOccurrence: return AddNextOccurrence();
            case CodeCommand.SelectAllOccurrences: return SelectAllOccurrences();
            case CodeCommand.RemoveSecondaryCursors: return DroppingAnUnshownList(RemoveSecondaryCursors());
            case CodeCommand.NextTabStop: return !ReadOnly && !TabMovesFocus && Snippet.Next();
            case CodeCommand.PreviousTabStop: return !ReadOnly && !TabMovesFocus && Snippet.Previous();
            case CodeCommand.LeaveSnippet: return DroppingAnUnshownList(Snippet.Leave());
            case CodeCommand.Copy:
                if (clipboard is null) return false;
                clipboard.Write(CopyText());
                return true;
            case CodeCommand.Cut:
                if (clipboard is null) return false;
                // The copy reaches the clipboard before anything is deleted: a clipboard that refuses the
                // text leaves the document as it was. Copying first takes what the cut takes, the whole
                // line included when nothing is selected.
                clipboard.Write(CopyText());
                Cut();
                return true;
            case CodeCommand.Paste:
                // Claimed even when it finds nothing to paste: going on to another command could only do
                // something nobody asked for.
                if (clipboard is null) return false;
                if (clipboard.Read() is { Length: > 0 } pasted) Paste(pasted);
                return true;
            case CodeCommand.CompletionShow: return Completion.Invoke();
            case CodeCommand.CompletionNext: return Completion.IsOpen && Completion.Move(1);
            case CodeCommand.CompletionPrevious: return Completion.IsOpen && Completion.Move(-1);
            case CodeCommand.CompletionNextPage: return Completion.IsOpen && Completion.MovePage(1);
            case CodeCommand.CompletionPreviousPage: return Completion.IsOpen && Completion.MovePage(-1);
            case CodeCommand.CompletionAccept: return Completion.IsOpen && Completion.Accept();
            case CodeCommand.CompletionAcceptChangingText:
                return Completion.IsOpen && Completion.AcceptChangesText && Completion.Accept();
            case CodeCommand.CompletionHide: return Completion.IsOpen && Completion.Dismiss();
            case CodeCommand.SignatureHelpShow: return SignatureHelp.Invoke();
            // A card showing hides the panel, and keys of a panel nobody can see would walk it unseen.
            case CodeCommand.SignatureHelpNext: return !Hover.IsOpen && SignatureHelp.Move(1);
            case CodeCommand.SignatureHelpPrevious: return !Hover.IsOpen && SignatureHelp.Move(-1);
            case CodeCommand.SignatureHelpHide: return DroppingAnUnshownList(SignatureHelp.Dismiss());
            // False when no card could show (a list showing, nothing to explain at the caret), so the next
            // command a chord names runs.
            case CodeCommand.HoverShow: return Hover.ShowAt(Caret);
            case CodeCommand.HoverHide: return DroppingAnUnshownList(Hover.Dismiss());
            case CodeCommand.ProblemNext: return Diagnostics.Next(backward: false) is not null;
            case CodeCommand.ProblemPrevious: return Diagnostics.Next(backward: true) is not null;
            case CodeCommand.ReleaseKeyboard:
                // A list asked for and not yet showing is dropped: Escape means it was not wanted.
                Completion.Dismiss();
                TabMovesFocus = true;
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// What closing the card or the panel does to a completion list asked for and not yet showing, when it
    /// closed one (<paramref name="closed"/>): it drops the list too. Escape means nothing over the caret
    /// was wanted, and the list's answer, landing after the key, opened a list nobody had asked to see
    /// (#1009's third review round). A list showing is left to its own command.
    /// </summary>
    private bool DroppingAnUnshownList(bool closed)
    {
        if (closed && !Completion.IsOpen) Completion.Dismiss();
        return closed;
    }

    /// <summary>
    /// Whether <paramref name="command"/> would do something now: a menu greys an item out from it, and the
    /// keymap waits for the second half of a chord only when its command could run. The clipboard's
    /// commands answer true, since the clipboard they are given is the caller's.
    /// </summary>
    public bool CanExecute(CodeCommand command) => command switch
    {
        CodeCommand.None or CodeCommand.ReleaseKeyboard => false,
        CodeCommand.NewLine or CodeCommand.Cut or CodeCommand.Paste => !ReadOnly,
        // A deletion has something to take: a selection, or text on its side of the caret. ⌘⌫ at column
        // zero of a later line joins it to the line above, as Backspace does. With several carets, one of
        // them always has.
        CodeCommand.DeleteLeft or CodeCommand.DeleteWordLeft or CodeCommand.DeleteToLineStart =>
            !ReadOnly && (_others.Count > 0 || !_selection.IsEmpty || Caret != CodePosition.Start),
        CodeCommand.DeleteRight or CodeCommand.DeleteWordRight =>
            !ReadOnly && (_others.Count > 0 || !_selection.IsEmpty || Caret != _document.End),
        // A language with no line comment (JSON, plain text) has nothing to toggle.
        CodeCommand.ToggleLineComment => !ReadOnly && Rules.LineComment is not null,
        CodeCommand.Indent or CodeCommand.Outdent => !ReadOnly && !TabMovesFocus,
        CodeCommand.Undo => !ReadOnly && History.CanUndo,
        CodeCommand.Redo => !ReadOnly && History.CanRedo,
        CodeCommand.CompletionShow => !ReadOnly && Completion.Providers.Count > 0,
        // A list of one entry has nowhere to walk: Move and MovePage refuse it, and the arrows are the
        // overloads' or the caret's.
        CodeCommand.CompletionNext or CodeCommand.CompletionPrevious or CodeCommand.CompletionNextPage
            or CodeCommand.CompletionPreviousPage => Completion.IsOpen && Completion.Items.Count > 1,
        CodeCommand.CompletionAccept or CodeCommand.CompletionHide => Completion.IsOpen,
        CodeCommand.CompletionAcceptChangingText => Completion.IsOpen && Completion.AcceptChangesText,
        CodeCommand.SignatureHelpShow => SignatureHelp.Providers.Count > 0,
        CodeCommand.SignatureHelpNext or CodeCommand.SignatureHelpPrevious =>
            !Hover.IsOpen && SignatureHelp.Help is { } help && help.Signatures.Count > 1,
        CodeCommand.SignatureHelpHide => SignatureHelp.IsOpen,
        CodeCommand.HoverShow => Hover.CanShowAt(Caret),
        CodeCommand.HoverHide => Hover.IsOpen,
        CodeCommand.ProblemNext or CodeCommand.ProblemPrevious => Diagnostics.Items.Count > 0,
        // A move needs a line to pass, a delete something to take, a join a line to join, and a sort
        // lines out of order: the primary's, which is all a command with several carets is asked about.
        CodeCommand.MoveLinesUp => !ReadOnly && (_others.Count > 0 || SelectedLines().First > 0),
        CodeCommand.MoveLinesDown => !ReadOnly && (_others.Count > 0 || _selection.End.Line < _document.LineCount - 1),
        CodeCommand.CopyLinesUp or CodeCommand.CopyLinesDown or CodeCommand.InsertLineAbove
            or CodeCommand.InsertLineBelow => !ReadOnly,
        CodeCommand.DeleteLines => !ReadOnly && (_document.LineCount > 1 || _document.Line(0).Length > 0),
        CodeCommand.JoinLines => !ReadOnly && (_others.Count > 0 || CanJoin()),
        CodeCommand.RemoveSecondaryCursors => _others.Count > 0,
        CodeCommand.NextTabStop => !ReadOnly && !TabMovesFocus && Snippet.IsActive,
        CodeCommand.PreviousTabStop => !ReadOnly && !TabMovesFocus && Snippet.HasPrevious,
        CodeCommand.LeaveSnippet => Snippet.IsActive,
        CodeCommand.SortLinesAscending => !ReadOnly && (_others.Count > 0 || Unsorted(descending: false)),
        CodeCommand.SortLinesDescending => !ReadOnly && (_others.Count > 0 || Unsorted(descending: true)),
        _ => true,
    };

    /// <summary>Whether the primary selection's lines are out of order, for a sort to have something to do.</summary>
    private bool Unsorted(bool descending)
    {
        var (first, last) = SelectedLines();
        return !LinesInOrder(first, last, descending);
    }

    /// <summary>Whether ⌃J has lines to join: the selection's, or the caret's line and a line after it.</summary>
    private bool CanJoin()
    {
        var (first, last) = JoinedLines();
        return last > first || first < _document.LineCount - 1;
    }

    /// <summary>
    /// The lines ⌃J joins: every line the selection touches, the one it ends at the start of included, as
    /// VS Code's join takes them, where the other line operations leave that line out.
    /// </summary>
    private (int First, int Last) JoinedLines() => (_selection.Start.Line, _selection.End.Line);

    /// <summary>A move of the caret, which always claims its key.</summary>
    private bool Step(CodeMotion motion, CodeDirection direction, bool extend)
    {
        Move(motion, direction, extend);
        return true;
    }

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
        // A character the selected completion commits on accepts it first, and is then typed after
        // it: `Console.` takes `Console` and goes on to its members. Only once the composition is
        // out: taking it out writes back over the range it was recorded at, and an entry accepted
        // before that had moved the text the range named.
        if (text.Length == 1 && Completion.AcceptsOn(text[0])) Completion.Accept();
        // Element by element: a character goes through Type, which pairs brackets and quotes, and
        // an element of more than one unit (an emoji, a letter with its accent) goes in WHOLE, never
        // a half of a surrogate pair at a time.
        var typed = false;
        var starts = StringInfo.ParseCombiningCharacters(text);
        for (var i = 0; i < starts.Length; i++)
        {
            var end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            var element = text.Substring(starts[i], end - starts[i]);
            typed |= element.Length == 1
                ? Type(element[0])
                : AtEveryCaret(() => Edit(_selection, element, true));
        }
        // A step of its OWN, after as well as before (SetComposition broke the run it began at):
        // the typing that follows a commit does not join it.
        if (committing) History.Break();
        if (typed)
        {
            Completion.Typed(text[text.Length - 1]);
            SignatureHelp.Typed(text[text.Length - 1]);
        }
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

        _document = next;
        _selection = new CodeRange(caret);
        _revealVersion++;
        _desiredCell = -1;
        var edit = new CodeEdit(ordered, removed, next.TextIn(new CodeRange(ordered.Start, caret)), before,
            _selection, false);
        Took(edit);
        // A composition is made at the primary caret, and the other carets keep their text as it moves.
        if (_others.Count > 0) CarryOthers(edit);
        Raise(new CodeChange([edit]));
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
        // A list and a card belong to the editor being typed in, and this one no longer is; so does a
        // chord half typed.
        Completion.Dismiss();
        Hover.Dismiss();
        SignatureHelp.Dismiss();
        PendingChord = null;
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
                // A press closes a card, as it does in every editor, even one that leaves the caret
                // where it was, and ends a chord half typed.
                Hover.Dismiss();
                PendingChord = null;
                // A folded run's row opens on a press, which the component drawing it answers: the
                // caret stays. It landed in the run's first line, at the column pressed, and revealing
                // it slid a diff's side sideways as the run opened.
                if (OnPlaceholder(position)) return false;
                var at = PositionAt(position);
                var alt = (modifiers & KeyModifiers.Alt) != 0;
                if (alt && clicks < 2 && (modifiers & KeyModifiers.Shift) != 0)
                {
                    // A column from the primary's anchor, as the press found it, to the cell pressed.
                    var anchor = _selection.Anchor;
                    _column = new CodeCaret(new CodeRange(anchor), CellsOf(anchor.Line).CellOf(anchor.Column));
                    SelectColumn(at.Line, (position.X - Grid.Origin.X) / Grid.Cell.Width);
                    _dragging = true;
                    return true;
                }
                if (alt && clicks < 2)
                {
                    ToggleCaret(at);
                    _dragging = true;
                    return true;
                }
                if (alt && clicks == 2)
                {
                    // ⌥ and a double press adds the word, as VS Code's does, where it left that word alone.
                    _others.Add(new CodeCaret(_selection, _desiredCell));
                    Placed(new CodeCaret(WordRange(at), -1));
                    return true;
                }
                if (clicks >= 3) SelectLine(at.Line);
                else if (clicks == 2) SelectWord(at);
                else if ((modifiers & KeyModifiers.Shift) != 0) Selection = new CodeRange(_selection.Anchor, at);
                else Selection = new CodeRange(at);
                _dragging = clicks < 2;
                return true;
            }
            case PointerPhase.Move:
            {
                // A drag that crosses a folded run does not enter it.
                if (!_dragging || OnPlaceholder(position)) return false;
                var at = PositionAt(position);
                if (_column is not null)
                {
                    SelectColumn(at.Line, (position.X - Grid.Origin.X) / Grid.Cell.Width);
                    return true;
                }
                if (at == _selection.Focus) return false;
                // The primary's focus follows the pointer, and a caret ⌥ added keeps the others.
                Select(new CodeRange(_selection.Anchor, at), keepCell: false);
                return true;
            }
            case PointerPhase.Up:
            case PointerPhase.Cancel:
                _dragging = false;
                _column = null;
                return false;
            // A pointer passing over the code: the hover waits for it to rest on something to explain,
            // and the card it shows arrives through the hover's own event, not as a change of the model.
            case PointerPhase.Hover:
                Hover.PointerAt(CharacterAt(position));
                return false;
            case PointerPhase.Exit:
                Hover.PointerLeft();
                return false;
            default:
                return false;
        }
    }

    /// <summary>Whether <paramref name="point"/> is on a row that stands for a folded run.</summary>
    private bool OnPlaceholder(Point point)
    {
        if (Grid.Rows is not { } rows) return false;
        var row = (int)MathF.Floor((point.Y - Grid.Origin.Y) / Grid.Cell.Height);
        return row >= 0 && row < rows.RowCount && rows.RowAt(row).Kind == CodeRowKind.Placeholder;
    }

    // ---- editing ------------------------------------------------------------------------------

    /// <summary>Replaces a range with text — the primitive every other edit is written in, and the
    /// one an IDE calls to apply a refactor, a formatter or a language server's edit.</summary>
    public bool Apply(CodeRange range, string text) => Edit(range, text, false);

    /// <summary>
    /// The one door every change to the document goes through. <paramref name="typed"/> says a
    /// person typed it, which is what lets undo join it to the run before (see <see cref="CodeEdit.Typed"/>).
    /// <paramref name="after"/> is the selection the edit leaves, the caret at the end of what it wrote when
    /// none is given, and the history records it as the edit's selection after: redo of a line moved
    /// selects the lines again, where it came back to that caret alone.
    /// </summary>
    private bool Edit(CodeRange range, string text, bool typed, CodeRange? after = null)
    {
        if (ReadOnly) return false;

        var ordered = new CodeRange(_document.Clamp(range.Start), _document.Clamp(range.End));
        var removed = _document.TextIn(ordered);
        if (removed.Length == 0 && text.Length == 0) return false;

        var before = _selection;
        var found = _step is null ? Selections : [];
        var next = _document.Replace(ordered, text, out var caret);

        _document = next;
        _selection = after is { } left ? OnElementEdges(left) : new CodeRange(caret);
        _revealVersion++;
        // The edit carries the text the document HOLDS, its line breaks as the document broke them.
        // Handed on raw, a pasted lone CR was one line to the history and two to the document, and
        // undo after pasting "x\ry" into "abc" left "a" and "ybc" (#600).
        var edit = new CodeEdit(ordered, removed, next.TextIn(new CodeRange(ordered.Start, caret)), before,
            _selection, typed);
        Took(edit);
        _desiredCell = -1;
        // Inside a command's step, the edit is heard with the others once the step is made.
        if (_step is { } step) step.Add(edit);
        else
        {
            // An edit made outside a command at every caret (an app's, a language server's) carries the
            // other carets, as it carries a problem.
            if (_others.Count > 0) CarryOthers(edit);
            History.Record(new CodeStep([edit], found, Selections));
            Raise(new CodeChange([edit]));
        }
        return true;
    }

    /// <summary>The carets besides the primary where they stand once <paramref name="edit"/> is made, made one
    /// where they meet.</summary>
    private void CarryOthers(CodeEdit edit)
    {
        for (var i = 0; i < _others.Count; i++)
            _others[i] = _others[i] with { Selection = edit.Carry(_others[i].Selection) };
        Merge();
    }

    /// <summary>The edits of the command being made while it makes several as ONE undo step (#1031); null
    /// otherwise, when every edit is a step of its own.</summary>
    private List<CodeEdit>? _step;

    /// <summary>
    /// Runs <paramref name="edits"/>, which make their edits through <see cref="Edit"/> one after another,
    /// and records what they made as one undo step, which undo takes back last first. Each edit brings the
    /// colours and the widths up to date as it is made, and <see cref="Changed"/> is raised once all are
    /// made, with all of them: a listener that edits while it hears them makes its edit on the text the
    /// step left, as a step of its own, where, raised in between, its edit moved the text the next edit's
    /// range was taken from, and that edit deleted the wrong line. Answers what <paramref name="edits"/>
    /// answers, or true once any edit is made; inside another step, it is part of that one.
    /// </summary>
    private bool AsOneStep(Func<bool> edits)
    {
        if (_step is not null) return edits();
        var step = new List<CodeEdit>();
        var before = _selection;
        var found = Selections;
        _step = step;
        _stepMoved = false;
        try
        {
            // An edit made is a step made, whatever the edits after it answered: the document changed.
            return edits() || step.Count > 0;
        }
        finally
        {
            _step = null;
            History.Record(new CodeStep(step, found, Selections));
            // A caret the command placed itself ends the run, once the step is recorded (see Select).
            if (_stepMoved) History.Break();
            if (step.Count > 0) Raise(new CodeChange(Heard(step, before)));
            else if (_stepMoved) SelectionChanged?.Invoke(_selection);
        }
    }

    /// <summary>Whether a command placed the primary caret itself while its step was being made.</summary>
    private bool _stepMoved;

    /// <summary>Where the next caret's text begins while a command runs at every caret, so a command that
    /// reaches forward, a word deleted to the right, stops short of it; null otherwise.</summary>
    private CodePosition? _ahead;

    /// <summary>
    /// Runs <paramref name="command"/>, written for the primary caret, at every caret (#1012): from the last
    /// to the first, so each runs on text no caret before it in the document has moved yet, and as ONE undo
    /// step. Each caret stands where its own run left it, carried through the edits the carets before it
    /// made afterwards. With one caret, it is the command.
    /// </summary>
    private bool AtEveryCaret(Func<bool> command)
    {
        if (_others.Count == 0) return command();
        var carets = CaretsInOrder(out var primary);
        var units = new List<List<CodeCaret>>();
        foreach (var caret in carets) units.Add(new List<CodeCaret> { caret });
        return AtEvery(units, primary, 0, unit =>
        {
            _selection = units[unit][0].Selection;
            _desiredCell = units[unit][0].Cell;
            var made = command();
            return (made, new List<CodeCaret> { new CodeCaret(_selection, _desiredCell) });
        });
    }

    /// <summary>
    /// Runs <paramref name="command"/>, a line command, once for each block of carets (#1012): the carets
    /// whose lines, by <paramref name="lines"/>, share a line make one block, and so do the ones whose lines
    /// only follow each other when <paramref name="touching"/>, as a line moved past another block's line
    /// would move that block's text. The command is handed the block's carets, in document order, makes its
    /// edits, and answers where each caret stands in the document it left, or null when it changed nothing.
    /// From the last block to the first, as ONE undo step; with one caret, its lines are the one block.
    /// </summary>
    private bool AtEveryBlock(Func<CodeRange, (int First, int Last)> lines, bool touching,
        Func<List<CodeCaret>, List<CodeCaret>?> command)
    {
        var carets = CaretsInOrder(out var primary);
        var units = new List<List<CodeCaret>>();
        var last = -1;
        var primaryUnit = 0;
        var primaryIndex = 0;
        for (var i = 0; i < carets.Count; i++)
        {
            var taken = lines(carets[i].Selection);
            if (units.Count > 0 && taken.First <= last + (touching ? 1 : 0)) units[^1].Add(carets[i]);
            else units.Add(new List<CodeCaret> { carets[i] });
            if (taken.Last > last) last = taken.Last;
            if (i == primary)
            {
                primaryUnit = units.Count - 1;
                primaryIndex = units[^1].Count - 1;
            }
        }
        return AtEvery(units, primaryUnit, primaryIndex, unit =>
        {
            var left = command(units[unit]);
            return left is null ? (false, units[unit]) : (true, left);
        });
    }

    /// <summary>The lines a block of <paramref name="carets"/> takes, from the first caret's first line to the
    /// furthest last line, as a line operation takes them (<see cref="SelectedLines"/>).</summary>
    private static (int First, int Last) BlockLines(List<CodeCaret> carets)
    {
        var first = int.MaxValue;
        var last = -1;
        foreach (var caret in carets)
        {
            var taken = SelectedLinesOf(caret.Selection);
            if (taken.First < first) first = taken.First;
            if (taken.Last > last) last = taken.Last;
        }
        return (first, last);
    }

    /// <summary>Every caret, the primary among them, in document order, and the primary's place in it.</summary>
    private List<CodeCaret> CaretsInOrder(out int primary)
    {
        var carets = new List<CodeCaret>(_others);
        primary = 0;
        while (primary < carets.Count && carets[primary].Selection.Start < _selection.Start) primary++;
        carets.Insert(primary, new CodeCaret(_selection, _desiredCell));
        return carets;
    }

    /// <summary>
    /// Runs <paramref name="run"/> for each unit of carets, a caret or a block, from the last unit to the
    /// first, as ONE undo step (#1012), and places the carets each run answered (see <see cref="Place"/>).
    /// A unit whose text a later unit's edit already took is not run: its carets go where that edit left
    /// their text, and are made one with the caret that took it, where running it again on the text the
    /// other left took a word too many.
    /// </summary>
    private bool AtEvery(List<List<CodeCaret>> units, int primaryUnit, int primaryIndex,
        Func<int, (bool Made, List<CodeCaret> Left)> run) =>
        AsOneStep(() =>
        {
            // Cleared once the step has taken every caret as the ones it found: each unit runs as the only
            // one, and an edit it makes carries no other caret.
            _others.Clear();
            var step = _step!;
            var count = units.Count;
            var left = new List<List<CodeCaret>>();
            var from = new List<int>();
            var to = new List<int>();
            for (var unit = 0; unit < count; unit++)
            {
                left.Add(units[unit]);
                from.Add(0);
                to.Add(0);
            }
            var any = false;
            var reached = false;
            var earliest = CodePosition.Start;
            try
            {
                for (var unit = count - 1; unit >= 0; unit--)
                {
                    from[unit] = step.Count;
                    to[unit] = step.Count;
                    var carets = units[unit];
                    if (reached && earliest < carets[^1].Selection.End)
                    {
                        var carried = new List<CodeCaret>();
                        foreach (var caret in carets)
                        {
                            var range = caret.Selection;
                            foreach (var edit in step) range = edit.Carry(range);
                            carried.Add(new CodeCaret(range, -1));
                        }
                        left[unit] = carried;
                        continue;
                    }
                    _ahead = unit + 1 < count ? left[unit + 1][0].Selection.Start : null;
                    var (made, placed) = run(unit);
                    any = any || made;
                    left[unit] = placed;
                    to[unit] = step.Count;
                    for (var i = from[unit]; i < to[unit]; i++)
                    {
                        if (reached && step[i].Range.Start >= earliest) continue;
                        earliest = step[i].Range.Start;
                        reached = true;
                    }
                }
            }
            finally
            {
                // Never left behind for a command at one caret, whatever a run threw.
                _ahead = null;
            }
            Place(left, from, to, primaryUnit, primaryIndex);
            return any;
        });

    /// <summary>
    /// Places the carets a command left at every unit, <paramref name="left"/>, each in the document as the
    /// units after it left it: carried through the edits the units before it made, which all lie before it,
    /// in one pass. A unit's edits move what follows them by a number of lines, and the rest of their last
    /// line by a number of columns; composed from the first unit on, that is one line, one line count and
    /// one column count, so ⇧⌘L over thousands of words places them all in time linear in their number.
    /// Then the carets that meet are made one.
    /// </summary>
    private void Place(List<List<CodeCaret>> left, List<int> from, List<int> to, int primaryUnit, int primaryIndex)
    {
        var step = _step!;
        var placed = new List<CodeCaret>();
        var primary = default(CodeCaret);
        // What the units before this one did, as a map from the document this unit left to the one the
        // command leaves, for whatever follows them: lines move by `lines`, and the rest of line `shifted`
        // by `columns`.
        var shifted = -1;
        var lines = 0;
        var columns = 0;
        for (var unit = 0; unit < left.Count; unit++)
        {
            var carets = left[unit];
            for (var i = 0; i < carets.Count; i++)
            {
                var anchor = carets[i].Selection.Anchor;
                var focus = carets[i].Selection.Focus;
                var caret = carets[i] with
                {
                    Selection = new CodeRange(
                        new CodePosition(anchor.Line + lines, anchor.Column + (anchor.Line == shifted ? columns : 0)),
                        new CodePosition(focus.Line + lines, focus.Column + (focus.Line == shifted ? columns : 0))),
                };
                // A block made one of its carets answers fewer carets than it was handed: the primary is
                // then the first it answered.
                if (unit == primaryUnit && i == (primaryIndex < carets.Count ? primaryIndex : 0)) primary = caret;
                else placed.Add(caret);
            }
            if (unit + 1 == left.Count) break;
            // Where this unit's own edits take the start of the next unit's text.
            var probe = left[unit + 1][0].Selection.Start;
            var image = probe;
            for (var i = from[unit]; i < to[unit]; i++) image = step[i].Carry(new CodeRange(image)).Start;
            columns = image.Column - probe.Column + (image.Line == shifted ? columns : 0);
            lines += image.Line - probe.Line;
            shifted = probe.Line;
        }

        // Held to the document the command left, and to its text elements' edges, as an edit's selection is.
        _selection = OnElementEdges(primary.Selection);
        _desiredCell = primary.Cell;
        _others.Clear();
        foreach (var caret in placed) _others.Add(caret with { Selection = OnElementEdges(caret.Selection) });
        Merge();
    }

    /// <summary>The changes made and not heard yet, in the order made, while <see cref="Changed"/> is being
    /// raised, and whether any of them moved the selection.</summary>
    private readonly List<CodeChange> _unheard = [];
    private bool _raising;
    private bool _unheardMoved;

    /// <summary>
    /// Raises <see cref="Changed"/> with <paramref name="change"/>, and then, when <paramref name="moved"/>,
    /// <see cref="SelectionChanged"/>, with the selection as it stands. A change a listener makes while it
    /// hears one waits for it, and every listener hears it after it: raised at once, the listeners after
    /// that one heard it before the change it answered, and carried a range through the two in the wrong
    /// order.
    /// </summary>
    private void Raise(CodeChange change, bool moved = true)
    {
        _unheard.Add(change);
        _unheardMoved = _unheardMoved || moved;
        if (_raising) return;
        _raising = true;
        try
        {
            for (var i = 0; i < _unheard.Count; i++) Changed?.Invoke(_unheard[i]);
        }
        finally
        {
            _unheard.Clear();
            _raising = false;
        }
        if (!_unheardMoved) return;
        _unheardMoved = false;
        SelectionChanged?.Invoke(_selection);
    }

    /// <summary><paramref name="made"/> as a listener hears them: each with the selection the command found,
    /// <paramref name="before"/>, and the one it left, where each held the selection of the moment it was
    /// made, which no listener saw, or the selection the history recorded for it, long gone.</summary>
    private List<CodeEdit> Heard(IReadOnlyList<CodeEdit> made, CodeRange before)
    {
        var heard = new List<CodeEdit>();
        foreach (var edit in made) heard.Add(edit with { SelectionBefore = before, SelectionAfter = _selection });
        return heard;
    }

    /// <summary>Types text at every caret, replacing each selection — including a paste.</summary>
    public bool Insert(string text) => AtEveryCaret(() => Edit(_selection, text, false));

    /// <summary>
    /// Types one character at every caret, with the pairing behaviours a code editor is expected to have:
    /// an opening bracket auto-closes, a closing bracket typed over its own auto-inserted twin steps
    /// over it instead of doubling it, and a quote around a selection wraps rather than replaces.
    /// </summary>
    public bool Type(char c) => AtEveryCaret(() => TypeAtCaret(c));

    /// <summary><see cref="Type"/> at the primary caret alone.</summary>
    private bool TypeAtCaret(char c)
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
                Select(new CodeRange(Caret with { Column = Caret.Column + 1 }), keepCell: false);
                return true;
            }
        }
        foreach (var quote in rules.Quotes)
        {
            if (c == quote && after == quote)
            {
                Select(new CodeRange(Caret with { Column = Caret.Column + 1 }), keepCell: false);
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
                Select(new CodeRange(Caret with { Column = Caret.Column - 1 }), keepCell: false);
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
                Select(new CodeRange(Caret with { Column = Caret.Column - 1 }), keepCell: false);
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
    /// Enter, at every caret, with the indentation a reader expects: the new line inherits the current
    /// one's, and gains a level when the line ends on something that opens one. Between a pair — the
    /// caret sitting inside <c>{}</c> — it opens the block and puts the closing brace on its own line.
    /// </summary>
    public bool InsertNewLine() => AtEveryCaret(NewLineAtCaret);

    /// <summary><see cref="InsertNewLine"/> at the primary caret alone.</summary>
    private bool NewLineAtCaret()
    {
        if (ReadOnly) return false;
        CodeLanguageRules rules = Rules;
        var line = _document.Line(Caret.Line);
        var indent = _document.IndentOf(Caret.Line);
        var step = IndentStep;

        var afterCaret = Caret.Column < line.Length ? line[Caret.Column..].TrimStart() : string.Empty;

        var opens = OpensBlock(line[..Math.Min(Caret.Column, line.Length)]);
        var closesNext = afterCaret.Length > 0 && rules.OutdentOn.Contains(afterCaret[0]);

        if (opens && closesNext)
        {
            // Between the pair: an empty indented line, and the closer drops below it.
            if (!Edit(_selection, $"\n{indent}{step}\n{indent}", false)) return false;
            Select(new CodeRange(new CodePosition(Caret.Line - 1, indent.Length + step.Length)), keepCell: false);
            return true;
        }
        return Edit(_selection, "\n" + indent + (opens ? step : string.Empty), false);
    }

    /// <summary>One step of indentation, as Tab writes it: the language's spaces, or a tab.</summary>
    private string IndentStep => Rules.InsertSpaces ? new string(' ', Rules.IndentWidth) : "\t";

    /// <summary>Whether a new line after <paramref name="text"/> gains a step: the text ends, white space
    /// aside, on what opens a block.</summary>
    private bool OpensBlock(string text)
    {
        var trimmed = text.TrimEnd();
        return trimmed.Length > 0 && Rules.IndentAfter.Contains(trimmed[^1]);
    }

    /// <summary>Backspace, at every caret: the selection if there is one, else the character before the
    /// caret — and a whole indent step when the caret sits in leading whitespace, because that is what put
    /// it there.</summary>
    public bool DeleteBackward(CodeMotion motion = CodeMotion.Character) =>
        AtEveryCaret(() => DeleteBackwardAtCaret(motion));

    /// <summary><see cref="DeleteBackward"/> at the primary caret alone.</summary>
    private bool DeleteBackwardAtCaret(CodeMotion motion)
    {
        if (ReadOnly) return false;
        if (!_selection.IsEmpty) return Edit(_selection, string.Empty, false);

        if (motion == CodeMotion.Word)
        {
            var start = MoveTo(Caret, CodeMotion.Word, CodeDirection.Backward);
            return Edit(new CodeRange(start, Caret), string.Empty, false);
        }

        // Everything left of the caret on its line (⌘⌫). At column zero there is nothing to its left
        // on this line, so it joins the line above, as a plain Backspace would.
        if (motion == CodeMotion.LineBoundary && Caret.Column > 0)
            return Edit(new CodeRange(Caret with { Column = 0 }, Caret), string.Empty, false);

        var line = _document.Line(Caret.Line);
        var indent = _document.IndentOf(Caret.Line).Length;
        if (Caret.Column > 0 && Caret.Column <= indent && Rules.InsertSpaces)
        {
            // Back to the previous stop ON SCREEN, over a tab in the indent too.
            var width = Rules.IndentWidth;
            var cells = CellsOf(Caret.Line);
            var cell = cells.CellOf(Caret.Column);
            var stop = cell % width == 0 ? cell - width : cell - cell % width;
            return Edit(new CodeRange(Caret with { Column = cells.ColumnAt(stop) }, Caret), string.Empty, false);
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
                return Edit(new CodeRange(Caret with { Column = Caret.Column - 1 },
                    Caret with { Column = Caret.Column + 1 }), string.Empty, false);
            }
        }

        var previous = Before(Caret);
        return previous != Caret && Edit(new CodeRange(previous, Caret), string.Empty, false);
    }

    /// <summary>Delete forward, at every caret — the key labelled Delete on a full keyboard, fn+Backspace on
    /// a Mac.</summary>
    public bool DeleteForward(CodeMotion motion = CodeMotion.Character) =>
        AtEveryCaret(() => DeleteForwardAtCaret(motion));

    /// <summary><see cref="DeleteForward"/> at the primary caret alone. With several carets it stops where the
    /// next caret's text begins, as VS Code keeps the first of two deletions that would overlap: a word
    /// deleted to the right otherwise ran on into the text the next caret's own deletion had joined to it.</summary>
    private bool DeleteForwardAtCaret(CodeMotion motion)
    {
        if (ReadOnly) return false;
        if (!_selection.IsEmpty) return Edit(_selection, string.Empty, false);

        var to = motion == CodeMotion.Word
            ? MoveTo(Caret, CodeMotion.Word, CodeDirection.Forward)
            : After(Caret);
        if (_ahead is { } ahead && to > ahead) to = ahead < Caret ? Caret : ahead;
        return to != Caret && Edit(new CodeRange(Caret, to), string.Empty, false);
    }

    // ---- indentation and comments -------------------------------------------------------------

    /// <summary>
    /// Tab. With a selection it indents every line it touches (that is what Tab means in an
    /// editor); with a caret it inserts one step — to the NEXT tab stop, not a fixed number of
    /// spaces, so a column stays a column. With several carets, a step at each while none selects
    /// anything, and every caret's lines indented once any does.
    /// </summary>
    public bool Indent()
    {
        if (ReadOnly) return false;
        if (!_selection.IsEmpty) return ShiftLines(add: true);
        foreach (var other in _others)
            if (!other.Selection.IsEmpty) return ShiftLines(add: true);
        return AtEveryCaret(IndentAtCaret);
    }

    /// <summary>One step of indentation typed at the primary caret alone.</summary>
    private bool IndentAtCaret()
    {
        if (!Rules.InsertSpaces) return Edit(_selection, "\t", false);
        // The stop is ON SCREEN: counted in columns, a caret after a tab or a wide character
        // stopped short of it or ran past it.
        var width = Rules.IndentWidth;
        var cell = CellsOf(Caret.Line).CellOf(Caret.Column);
        return Edit(_selection, new string(' ', width - cell % width), false);
    }

    /// <summary>Shift+Tab: takes one step off every selected line, and off the caret's line when
    /// there is no selection.</summary>
    public bool Outdent() => !ReadOnly && ShiftLines(add: false);

    /// <summary>
    /// Tab and Shift+Tab over lines: every caret's lines gain or lose one step at column 0, each block of
    /// lines once, an edit a line, so a problem on a line travels with its text, where one replacement of
    /// every line took it to the replacement's ends. Claimed whatever it changed, so Shift+Tab with nothing
    /// to take off stays in the editor.
    /// </summary>
    private bool ShiftLines(bool add)
    {
        AtEveryBlock(SelectedLinesOf, touching: false, carets => ShiftBlock(carets, add));
        return true;
    }

    /// <summary>One block's lines shifted: what each line gains (Tab) or loses (Shift+Tab), all of it at
    /// column 0, and each selection's two ends moved by their OWN line's change, which is what keeps it what
    /// it was. Null when no line changes.</summary>
    private List<CodeCaret>? ShiftBlock(List<CodeCaret> carets, bool add)
    {
        var (first, last) = BlockLines(carets);
        var step = IndentStep;
        var changes = new List<int>();
        var changed = false;
        for (var line = first; line <= last; line++)
        {
            var text = _document.Line(line);
            var change = add ? (text.Length == 0 ? 0 : step.Length) : StepOff(text);
            changes.Add(change);
            changed = changed || change > 0;
        }
        if (!changed) return null;
        // The last line first, so each edit is made on lines no edit before it moved.
        for (var line = last; line >= first; line--)
        {
            var change = changes[line - first];
            if (change == 0) continue;
            var start = new CodePosition(line, 0);
            if (add) Edit(new CodeRange(start), step, false);
            else Edit(new CodeRange(start, new CodePosition(line, change)), string.Empty, false);
        }
        var left = new List<CodeCaret>();
        foreach (var caret in carets)
        {
            left.Add(caret with
            {
                Selection = new CodeRange(ShiftedBy(caret.Selection.Anchor, first, last, changes, add),
                    ShiftedBy(caret.Selection.Focus, first, last, changes, add)),
            });
        }
        return left;
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
    /// ⌘/ — comments every caret's lines, or un-comments them when they are ALL commented already, each
    /// block of lines deciding for itself, as each of VS Code's cursors does. An edit a line, so a problem
    /// travels with its text. A language with no line comment does nothing, which is the only correct
    /// answer for JSON.
    /// </summary>
    public bool ToggleLineComment()
    {
        if (ReadOnly || Rules.LineComment is not { } marker) return false;
        AtEveryBlock(SelectedLinesOf, touching: false, carets => CommentBlock(carets, marker));
        return true;
    }

    /// <summary>One block's lines commented, or un-commented when every one of them that holds text is.
    /// Null when no line changes.</summary>
    private List<CodeCaret>? CommentBlock(List<CodeCaret> carets, string marker)
    {
        var (first, last) = BlockLines(carets);
        var allCommented = true;
        for (var line = first; line <= last; line++)
        {
            var text = _document.Line(line).TrimStart();
            if (text.Length == 0) continue;
            if (!text.StartsWith(marker, StringComparison.Ordinal)) { allCommented = false; break; }
        }

        // Each line's change as (where, how much went, how much came), so the selections' ends can follow
        // their own line's markers instead of collapsing to the caret an edit leaves.
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
                    ats.Add(0);
                    removals.Add(0);
                    insertions.Add(0);
                    continue;
                }
                var after = at + marker.Length;
                if (after < text.Length && text[after] == ' ') after++;
                ats.Add(at);
                removals.Add(after - at);
                insertions.Add(0);
            }
            else
            {
                ats.Add(text.Length == 0 ? 0 : _document.IndentOf(line).Length);
                removals.Add(0);
                insertions.Add(marker.Length + 1);
            }
        }

        var changed = false;
        // The last line first, so each edit is made on lines no edit before it moved.
        for (var line = last; line >= first; line--)
        {
            var index = line - first;
            if (removals[index] == 0 && insertions[index] == 0) continue;
            changed = true;
            var at = new CodePosition(line, ats[index]);
            Edit(new CodeRange(at, at with { Column = ats[index] + removals[index] }),
                insertions[index] > 0 ? marker + " " : string.Empty, false);
        }
        if (!changed) return null;
        var left = new List<CodeCaret>();
        foreach (var caret in carets)
        {
            left.Add(caret with
            {
                Selection = new CodeRange(Commented(caret.Selection.Anchor, first, last, ats, removals, insertions),
                    Commented(caret.Selection.Focus, first, last, ats, removals, insertions)),
            });
        }
        return left;
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
    /// Home, End, PageUp and their shifted twins. Every caret moves, each by its own line and the cell it
    /// aims at, and the carets that meet are made one.
    /// </summary>
    public void Move(CodeMotion motion, CodeDirection direction, bool extend = false, int pageLines = 20) =>
        AtEveryCaret(() =>
        {
            MoveCaret(motion, direction, extend, pageLines);
            return true;
        });

    /// <summary><see cref="Move"/> for the primary caret alone.</summary>
    private void MoveCaret(CodeMotion motion, CodeDirection direction, bool extend, int pageLines)
    {
        // A plain arrow with a selection collapses to its edge rather than moving from the caret —
        // pressing → with text selected puts you after it, not one character further on.
        if (!extend && !_selection.IsEmpty && motion == CodeMotion.Character)
        {
            Select(new CodeRange(direction == CodeDirection.Forward ? _selection.End : _selection.Start), keepCell: false);
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
    public void SelectWord(CodePosition at) => Selection = WordRange(at);

    /// <summary>The word at <paramref name="at"/>, widened to whole text elements; empty on an empty line.</summary>
    private CodeRange WordRange(CodePosition at)
    {
        var word = _document.WordAt(at);
        if (word.IsEmpty) return word;
        var cells = CellsOf(word.Start.Line);
        return new CodeRange(
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
    /// none — copying with nothing selected takes the line, in every editor worth using. With several
    /// carets, every selection's text, a line each, or every caret's line when none selects anything,
    /// as VS Code copies them (#1012); a paste at as many carets gives each its own again.</summary>
    public string CopyText()
    {
        if (_others.Count > 0) return CopyEvery();
        _copied = null;
        if (!_selection.IsEmpty)
        {
            _wholeLineCopy = null;
            return _document.TextIn(_selection);
        }
        var line = _document.Line(Caret.Line) + "\n";
        _wholeLineCopy = line;
        return line;
    }

    /// <summary>What every caret copies, in document order, remembered for a paste at as many carets.</summary>
    private string CopyEvery()
    {
        var carets = CaretsInOrder(out _);
        var lines = true;
        foreach (var caret in carets)
            if (!caret.Selection.IsEmpty) lines = false;
        // A piece for every caret, two carets on one line taking it twice, so a paste at as many carets
        // gives each its own: taken once, the pieces were fewer than the carets, and the lines went in
        // whole, in the middle of every caret's line.
        var pieces = new List<string>();
        foreach (var caret in carets)
            pieces.Add(lines ? _document.Line(caret.Selection.Focus.Line) : _document.TextIn(caret.Selection));
        var text = lines ? string.Join("\n", pieces) + "\n" : string.Join("\n", pieces);
        _copied = pieces;
        _copiedText = text;
        _wholeLineCopy = lines ? text : null;
        return text;
    }

    /// <summary>The pieces the last copy with several carets took, one for each caret, and the text it put on
    /// the clipboard; null after any other copy.</summary>
    private List<string>? _copied;
    private string _copiedText = "";

    /// <summary>Cut is copy, then delete — and with no selection it takes the whole line. With several carets,
    /// every caret cuts its own, as one undo step.</summary>
    public string Cut()
    {
        var text = CopyText();
        AtEveryCaret(() =>
        {
            if (_selection.IsEmpty) SelectLine(Caret.Line);
            return Edit(_selection, string.Empty, false);
        });
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
        if (_others.Count > 0) return PasteEvery(text);
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

    /// <summary>
    /// A paste at several carets, as VS Code spreads one (#1012): what a copy at as many carets took goes back
    /// a piece to each, and so does a text with a line for each caret, its one last line break aside; any
    /// other text goes whole to every caret. Whole lines a copy took with nothing selected go in as lines of
    /// their own, each above its caret's line.
    /// </summary>
    private bool PasteEvery(string text)
    {
        var carets = CaretsInOrder(out var primary);
        var normalized = CodeDocument.FromText(text).Text;
        List<string>? pieces = null;
        if (_copied is { } copied && normalized == _copiedText && copied.Count == carets.Count) pieces = copied;
        else
        {
            var body = normalized.EndsWith('\n') ? normalized[..^1] : normalized;
            var lines = CodeDocument.FromText(body).Lines;
            if (lines.Count == carets.Count)
            {
                pieces = [];
                foreach (var line in lines) pieces.Add(line);
            }
        }
        if (pieces is not null && _wholeLineCopy is { } whole && normalized == whole)
        {
            // Whole lines go in above each caret's line, the lines of the carets one line holds in one edit:
            // made a caret at a time, the first one's line pushed the next caret on that line along, and it
            // read as a caret whose text was taken.
            var piece = new Dictionary<CodeRange, string>();
            for (var i = 0; i < carets.Count; i++) piece[carets[i].Selection] = pieces[i];
            return AtEveryBlock(SelectedLinesOf, touching: false, block =>
            {
                var first = BlockLines(block).First;
                var text = new StringBuilder();
                foreach (var caret in block) text.Append(piece[caret.Selection]).Append('\n');
                Edit(new CodeRange(new CodePosition(first, 0)), text.ToString(), false);
                return MovedBy(block, block.Count);
            });
        }
        var units = new List<List<CodeCaret>>();
        foreach (var caret in carets) units.Add(new List<CodeCaret> { caret });
        return AtEvery(units, primary, 0, unit =>
        {
            _selection = units[unit][0].Selection;
            _desiredCell = -1;
            var text = pieces is null ? normalized : pieces[unit];
            return (Edit(_selection, text, false), new List<CodeCaret> { new CodeCaret(_selection, -1) });
        });
    }

    /// <summary>
    /// An accepted completion at every caret, as VS Code applies one suggestion at each of its cursors
    /// (#1012): before each caret, as many characters as the entry replaces before the primary, and after
    /// it as many as it replaces after, on the caret's own line. A caret that already holds the text only
    /// steps past it.
    /// </summary>
    internal bool ReplaceAroundEveryCaret(int before, int after, string text) =>
        AtEveryCaret(() =>
        {
            var caret = Caret;
            var line = _document.Line(caret.Line);
            var range = new CodeRange(caret with { Column = Math.Max(0, caret.Column - before) },
                caret with { Column = Math.Min(line.Length, caret.Column + after) });
            if (_document.TextIn(range) != text) return Edit(range, text, false);
            Select(new CodeRange(range.End), keepCell: false);
            return true;
        });

    /// <summary>Whether there are carets besides the primary: what a completion asks before it accepts at
    /// them all.</summary>
    internal bool HasOtherCarets => _others.Count > 0;

    // ---- snippets (#1013) -------------------------------------------------------------------------

    /// <summary>
    /// Inserts <paramref name="snippet"/> in place of every selection, a copy at each caret, as one undo step,
    /// and starts its session at its first stop (<see cref="Snippet"/>): what an app's own command calls, a
    /// toolbar's "insert a loop". A snippet whose only stop is <c>$0</c>, or with none, leaves a caret at
    /// <c>$0</c>, or at its end, and starts none. False when the editor reads only.
    /// </summary>
    public bool InsertSnippet(CodeSnippet snippet) => InsertCopies(snippet, () => _selection);

    /// <summary>A snippet accepted from the completion at every caret, as an entry of text is
    /// (<see cref="ReplaceAroundEveryCaret"/>): before each caret as many characters as the entry replaces
    /// before the primary, and after it as many as it replaces after, on the caret's own line.</summary>
    internal bool InsertSnippetAround(CodeSnippet snippet, int before, int after) =>
        InsertCopies(snippet, () =>
        {
            var caret = Caret;
            var line = _document.Line(caret.Line);
            return new CodeRange(caret with { Column = Math.Max(0, caret.Column - before) },
                caret with { Column = Math.Min(line.Length, caret.Column + after) });
        });

    /// <summary>
    /// The insertion itself: at every caret, the copy of <paramref name="snippet"/> that caret's variables and
    /// line make, over the range <paramref name="at"/> answers there, its caret left at the copy's start, which
    /// the placement of #1012 carries through the copies inserted before it in the document. Each stop is then
    /// the copy's start and where the stop lies in the copy, which no other copy's edit reaches into. The
    /// carets go to the first stop inside the step, so redo selects it again, and the session starts once the
    /// step is heard, so it does not carry its stops through the edits that made them.
    /// </summary>
    private bool InsertCopies(CodeSnippet snippet, Func<CodeRange> at)
    {
        if (ReadOnly) return false;
        Snippet.End();
        var copies = new List<CodeSnippetExpansion>();
        var stops = new List<CodeSnippetStop>();
        var made = AsOneStep(() =>
        {
            AtEveryCaret(() =>
            {
                var range = at();
                var start = _document.Clamp(range.Start);
                var copy = CodeSnippetExpansion.Of(snippet, name => SnippetVariable(name, range),
                    _document.IndentOf(start.Line), IndentStep);
                copies.Add(copy);
                var edited = Edit(range, copy.Text, false);
                Select(new CodeRange(start), keepCell: false, moved: true);
                return edited;
            });
            // The copies were made from the last caret to the first; the carets stand at their starts.
            copies.Reverse();
            var carets = CaretsInOrder(out var primary);
            if (carets.Count != copies.Count) return false;
            stops.AddRange(StopsOf(copies, carets, primary));
            PlaceSelections(stops[0].Ranges);
            return true;
        });
        if (stops.Count > 0 && !stops[0].Final) Snippet.Start(stops);
        return made;
    }

    /// <summary>
    /// The stops of the copies of a snippet that start where <paramref name="carets"/> stand, in the order Tab
    /// walks them: their numbers ascending, then <c>$0</c>, or the end of every copy when the snippet has none.
    /// Each stop's places are every place it is written in every copy, its first place in the primary caret's
    /// copy first.
    /// </summary>
    private static List<CodeSnippetStop> StopsOf(List<CodeSnippetExpansion> copies, List<CodeCaret> carets, int primary)
    {
        var byIndex = new Dictionary<int, CodeSnippetStop>();
        for (var k = 0; k < copies.Count; k++)
        {
            var start = carets[k].Selection.Start;
            foreach (var place in copies[k].Places)
            {
                if (!byIndex.TryGetValue(place.Stop, out var stop))
                {
                    stop = new CodeSnippetStop(place.Stop, place.Parent, place.Stop == 0);
                    byIndex[place.Stop] = stop;
                }
                if (stop.Choices is null && place.Choices is not null) stop.Choices = place.Choices;
                var range = new CodeRange(CodeSnippetPlace.At(start, place.Start), CodeSnippetPlace.At(start, place.End));
                if (k == primary && !stop.HasPrimary)
                {
                    stop.Ranges.Insert(0, range);
                    stop.HasPrimary = true;
                }
                else stop.Ranges.Add(range);
            }
        }
        var order = new List<int>(byIndex.Keys);
        order.Remove(0);
        order.Sort();
        var stops = new List<CodeSnippetStop>();
        foreach (var index in order) stops.Add(byIndex[index]);
        if (byIndex.TryGetValue(0, out var final)) stops.Add(final);
        else
        {
            var end = new CodeSnippetStop(0, -1, final: true);
            for (var k = 0; k < copies.Count; k++)
            {
                var range = new CodeRange(CodeSnippetPlace.At(carets[k].Selection.Start, copies[k].End));
                if (k == primary) end.Ranges.Insert(0, range);
                else end.Ranges.Add(range);
            }
            stops.Add(end);
        }
        return stops;
    }

    /// <summary>The selections <paramref name="ranges"/> make, the first the primary, placed inside a step:
    /// heard with its edits once it is made.</summary>
    private void PlaceSelections(IReadOnlyList<CodeRange> ranges)
    {
        _others.Clear();
        for (var i = 1; i < ranges.Count; i++) _others.Add(new CodeCaret(OnElementEdges(ranges[i]), -1));
        Select(ranges[0], keepCell: false, moved: true);
        Merge();
    }

    /// <summary>What a snippet's variable <paramref name="name"/> is for the copy that replaces
    /// <paramref name="range"/>, or null for a name the editor does not know.</summary>
    private string? SnippetVariable(string name, CodeRange range) => name switch
    {
        "TM_SELECTED_TEXT" => _document.TextIn(range),
        "TM_CURRENT_LINE" => _document.Line(range.Start.Line),
        "TM_CURRENT_WORD" => _document.TextIn(WordRange(range.Start)),
        "TM_LINE_INDEX" => range.Start.Line.ToString(),
        "TM_LINE_NUMBER" => (range.Start.Line + 1).ToString(),
        _ => null,
    };

    /// <summary>Writes <paramref name="text"/> over every one of <paramref name="ranges"/>, as one undo step,
    /// a caret after each: a snippet's choice taken.</summary>
    internal bool ReplaceEvery(IReadOnlyList<CodeRange> ranges, string text)
    {
        if (ReadOnly || ranges.Count == 0) return false;
        return AsOneStep(() =>
        {
            PlaceSelections(ranges);
            return AtEveryCaret(() => Edit(_selection, text, false));
        });
    }

    // ---- carets (#1012) -------------------------------------------------------------------------

    /// <summary>
    /// ⌥⌘↑ and ⌥⌘↓: adds a caret on the line above every caret (<see cref="CodeDirection.Backward"/>) or below
    /// it, at the cell it aims at, as VS Code adds one for each of its cursors; a caret that would stand where
    /// one is, is made one with it. The caret added furthest that way is the primary, the one brought into
    /// view and the one the next press goes on from, as Rider's primary caret is the one placed last. False
    /// when none could be added: every caret on the first line going up, or on the last going down.
    /// </summary>
    public bool AddCursor(CodeDirection direction)
    {
        var carets = CaretsInOrder(out _);
        var up = direction == CodeDirection.Backward;
        var added = new List<CodeCaret>();
        foreach (var caret in carets)
        {
            var from = caret.Selection.Focus;
            var line = VisibleLineFrom(from.Line, up ? -1 : 1);
            if (line == from.Line) continue;
            var cell = caret.Cell >= 0 ? caret.Cell : CellsOf(from.Line).CellOf(from.Column);
            added.Add(new CodeCaret(new CodeRange(new CodePosition(line, CellsOf(line).ColumnAt(cell))), cell));
        }
        if (added.Count == 0) return false;
        var primary = up ? added[0] : added[^1];
        _others.Clear();
        foreach (var caret in carets) _others.Add(caret);
        foreach (var caret in added)
            if (caret != primary) _others.Add(caret);
        Placed(primary);
        return true;
    }

    /// <summary>The text ⌘D found by expanding an empty caret to its word, and so searches whole and by its
    /// case; null once the primary's selection is anything else.</summary>
    private string? _occurrence;

    /// <summary>
    /// ⌘D: with the primary empty, selects the word at every empty caret; then each press adds the next
    /// occurrence of the primary's text after the primary, wrapping at the document's end, as the new primary.
    /// A word found from a caret is searched whole and by its case, as VS Code searches it, and a selection
    /// made otherwise by its text, its case kept. Claimed while there is a word or a selection to search,
    /// though every occurrence is already selected, so the key never reaches a browser's bookmarks.
    /// </summary>
    public bool AddNextOccurrence()
    {
        if (_selection.IsEmpty)
        {
            var carets = CaretsInOrder(out var at);
            var words = new List<CodeCaret>();
            foreach (var caret in carets)
                words.Add(caret.Selection.IsEmpty ? new CodeCaret(WordRange(caret.Selection.Focus), -1) : caret);
            var primary = words[at];
            if (primary.Selection.IsEmpty) return false;
            _others.Clear();
            for (var i = 0; i < words.Count; i++)
                if (i != at) _others.Add(words[i]);
            Placed(primary);
            _occurrence = _document.TextIn(primary.Selection);
            return true;
        }
        var needle = _document.TextIn(_selection);
        var whole = _occurrence == needle;
        var matches = Occurrences(needle, whole);
        var taken = new HashSet<CodeRange>();
        foreach (var selection in Selections) taken.Add(new CodeRange(selection.Start, selection.End));
        CodeRange? next = null;
        foreach (var match in matches)
        {
            if (match.Start < _selection.End || taken.Contains(match)) continue;
            next = match;
            break;
        }
        if (next is null)
        {
            foreach (var match in matches)
            {
                if (taken.Contains(match)) continue;
                next = match;
                break;
            }
        }
        if (next is not { } found) return true;
        _others.Add(new CodeCaret(_selection, _desiredCell));
        Placed(new CodeCaret(found, -1));
        _occurrence = whole ? needle : null;
        return true;
    }

    /// <summary>
    /// ⇧⌘L: selects every occurrence of the primary's text, or of the word at the primary caret, searched as
    /// ⌘D searches it, the one the primary stood in the primary. False with no word at the caret.
    /// </summary>
    public bool SelectAllOccurrences()
    {
        var target = _selection.IsEmpty ? WordRange(Caret) : new CodeRange(_selection.Start, _selection.End);
        if (target.IsEmpty) return false;
        var needle = _document.TextIn(target);
        var whole = _selection.IsEmpty || _occurrence == needle;
        var matches = Occurrences(needle, whole);
        if (matches.Count == 0) return false;
        var primary = 0;
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i] != target) continue;
            primary = i;
            break;
        }
        _others.Clear();
        for (var i = 0; i < matches.Count; i++)
            if (i != primary) _others.Add(new CodeCaret(matches[i], -1));
        Placed(new CodeCaret(matches[primary], -1));
        _occurrence = whole ? needle : null;
        return true;
    }

    /// <summary>Every occurrence of <paramref name="needle"/>, by its case, in document order; when
    /// <paramref name="whole"/>, only those that cut no word at either edge.</summary>
    private List<CodeRange> Occurrences(string needle, bool whole)
    {
        var occurrences = new List<CodeRange>();
        foreach (var match in FindAll(needle, matchCase: true))
            if (!whole || Whole(match)) occurrences.Add(match);
        return occurrences;
    }

    /// <summary>Whether <paramref name="match"/>, on one line, cuts no word at either of its edges.</summary>
    private bool Whole(CodeRange match)
    {
        var line = _document.Line(match.Start.Line);
        var start = match.Start.Column;
        var end = match.End.Column;
        var cutsStart = start > 0 && start < line.Length && CodeDocument.IsWordChar(line[start - 1])
            && CodeDocument.IsWordChar(line[start]);
        var cutsEnd = end > 0 && end < line.Length && CodeDocument.IsWordChar(line[end - 1])
            && CodeDocument.IsWordChar(line[end]);
        return !cutsStart && !cutsEnd;
    }

    /// <summary>Escape with several carets: every caret but the primary taken away. False with one caret, so
    /// Escape goes on to what it means next.</summary>
    public bool RemoveSecondaryCursors()
    {
        if (_others.Count == 0) return false;
        _others.Clear();
        History.Break();
        _revealVersion++;
        SelectionChanged?.Invoke(_selection);
        return true;
    }

    /// <summary>
    /// Makes <paramref name="primary"/> the primary caret, the other carets as <see cref="_others"/> holds them,
    /// made one where they meet: a caret added, by key, by pointer or by occurrence. It is brought into view,
    /// it ends the typing run, and the selection is heard to change.
    /// </summary>
    private void Placed(CodeCaret primary)
    {
        _selection = OnElementEdges(primary.Selection);
        _desiredCell = primary.Cell;
        Merge();
        History.Break();
        _revealVersion++;
        SelectionChanged?.Invoke(_selection);
    }

    /// <summary>⌥ and a press: a caret where it lands, the new primary, or, while there are others, the caret it
    /// lands on taken away.</summary>
    private void ToggleCaret(CodePosition at)
    {
        var carets = CaretsInOrder(out var primary);
        if (carets.Count > 1)
        {
            for (var i = 0; i < carets.Count; i++)
            {
                var selection = carets[i].Selection;
                if (at < selection.Start || selection.End < at) continue;
                carets.RemoveAt(i);
                // The primary taken away: the caret before it in the document is the primary now, or the first.
                var next = i == primary ? (i > 0 ? i - 1 : 0) : (primary > i ? primary - 1 : primary);
                _others.Clear();
                for (var k = 0; k < carets.Count; k++)
                    if (k != next) _others.Add(carets[k]);
                Placed(carets[next]);
                return;
            }
        }
        _others.Add(new CodeCaret(_selection, _desiredCell));
        Placed(new CodeCaret(new CodeRange(at), -1));
    }

    /// <summary>Where a column selection began: the line and the cell of the primary's anchor when ⇧⌥ was
    /// pressed; null while none is being made.</summary>
    private CodeCaret? _column;

    /// <summary>
    /// ⇧⌥ and a press or a drag: one selection on every line from the column's start to the line pressed, each
    /// from the start's cell to the cell pressed, as VS Code's column selection makes them; a line that ends
    /// before the box's left edge has none, unless every line does, and then each has a caret at its end. The
    /// one on the line pressed is the primary.
    /// </summary>
    private void SelectColumn(int line, float cell)
    {
        if (_column is not { } start) return;
        var anchorLine = start.Selection.Anchor.Line;
        var anchorCell = start.Cell;
        var focusCell = (int)MathF.Round(cell);
        var first = Math.Min(anchorLine, line);
        var last = Math.Max(anchorLine, line);
        var left = Math.Min(anchorCell, focusCell);
        var reached = false;
        for (var at = first; at <= last; at++)
            if ((Grid.Rows is not { } rows || rows.IsVisible(at)) && CellsOf(at).Width >= left) reached = true;
        var column = new List<CodeCaret>();
        var primary = -1;
        for (var at = first; at <= last; at++)
        {
            if (Grid.Rows is { } rows && !rows.IsVisible(at)) continue;
            var cells = CellsOf(at);
            if (reached && cells.Width < left) continue;
            if (at == line) primary = column.Count;
            column.Add(new CodeCaret(new CodeRange(new CodePosition(at, cells.ColumnAt(anchorCell)),
                new CodePosition(at, cells.ColumnAt(focusCell))), focusCell));
        }
        // Every line of the box folded away: nothing to select.
        if (column.Count == 0) return;
        // The line pressed may be one too short to have a selection: the one nearest it is the primary then.
        if (primary < 0) primary = line >= anchorLine ? column.Count - 1 : 0;
        _others.Clear();
        for (var i = 0; i < column.Count; i++)
            if (i != primary) _others.Add(column[i]);
        Placed(column[primary]);
    }

    // ---- lines --------------------------------------------------------------------------------

    /// <summary>
    /// The lines a line operation takes, Tab and ⌘/ among them: the selection's, or the caret's. A
    /// selection that ends at column 0 does not include that line — the same rule every editor uses, and
    /// the reason shift+down then Tab does not indent one line too many.
    /// </summary>
    private (int First, int Last) SelectedLines() => SelectedLinesOf(_selection);

    /// <summary>The lines a line operation takes for <paramref name="range"/> (see <see cref="SelectedLines"/>).</summary>
    private static (int First, int Last) SelectedLinesOf(CodeRange range)
    {
        var first = range.Start.Line;
        var last = range.End.Line;
        if (last > first && range.End.Column == 0) last--;
        return (first, last);
    }

    /// <summary>Lines <paramref name="first"/> to <paramref name="last"/>, from the first's start to the last's
    /// end.</summary>
    private CodeRange WholeLines(int first, int last) =>
        new(new CodePosition(first, 0), new CodePosition(last, _document.Line(last).Length));

    /// <summary>The text of lines <paramref name="first"/> to <paramref name="last"/>, a line each.</summary>
    private List<string> LinesOf(int first, int last)
    {
        var lines = new List<string>();
        for (var line = first; line <= last; line++) lines.Add(_document.Line(line));
        return lines;
    }

    /// <summary>The selection moved <paramref name="lines"/> lines down, or up when negative, each end at its
    /// column.</summary>
    private CodeRange SelectionMovedBy(int lines) =>
        new(_selection.Anchor with { Line = _selection.Anchor.Line + lines },
            _selection.Focus with { Line = _selection.Focus.Line + lines });

    /// <summary>
    /// ⌥↑ and ⌥↓: moves the lines every caret takes one line up (<see cref="CodeDirection.Backward"/>) or
    /// down, past the line beside them, and the carets with them, as one undo step. The carets whose lines
    /// share or follow each other move as one block. False when no block can move: up from the first line,
    /// or down from the last.
    /// </summary>
    public bool MoveLines(CodeDirection direction)
    {
        if (ReadOnly) return false;
        EndComposition();
        var up = direction == CodeDirection.Backward;
        return AtEveryBlock(SelectedLinesOf, touching: true, carets => MoveBlock(carets, up));
    }

    /// <summary>One block of lines moved past the line beside it, and its carets with it; null when it cannot
    /// move.</summary>
    private List<CodeCaret>? MoveBlock(List<CodeCaret> carets, bool up)
    {
        var (first, last) = BlockLines(carets);
        // Down, while the selection reaches the last line, its end at the start of that line included, the
        // move is refused, as VS Code's MoveLinesCommand refuses it: that end would have no line to stand at
        // once the block passed it, and at the start of an empty last line it would leave that line out of
        // the next move.
        // The line passed is the one written again, on the block's other side, and deleted where it was, as
        // one step: the block's lines are never rewritten, so a problem on them travels with them, where one
        // replacement over both took it to the replacement's ends (#1031). The later edit first, so the
        // earlier one is made on text it did not move. Neither writes where the block's lines start or end,
        // so a problem of no width there, a missing semicolon at a line's end, stays on them too: the passed
        // line goes in at the start of the line after the block and comes out from the start of its own,
        // with its line break, where written after the block's last line it took that problem along. Only at
        // the document's end, with no line after the block, is it written there.
        if (up ? first == 0 : carets[^1].Selection.End.Line == _document.LineCount - 1) return null;
        if (up)
        {
            var passed = _document.Line(first - 1);
            var atEnd = last + 1 == _document.LineCount;
            var at = atEnd ? new CodePosition(last, _document.Line(last).Length) : new CodePosition(last + 1, 0);
            var written = atEnd ? "\n" + passed : passed + "\n";
            Edit(new CodeRange(at), written, false);
            Edit(new CodeRange(new CodePosition(first - 1, 0), new CodePosition(first, 0)), string.Empty, false);
        }
        else
        {
            var below = _document.Line(last + 1);
            var taken = last + 2 == _document.LineCount
                ? new CodeRange(new CodePosition(last, _document.Line(last).Length), new CodePosition(last + 1, below.Length))
                : new CodeRange(new CodePosition(last + 1, 0), new CodePosition(last + 2, 0));
            Edit(taken, string.Empty, false);
            Edit(new CodeRange(new CodePosition(first, 0)), below + "\n", false);
        }
        return MovedBy(carets, up ? -1 : 1);
    }

    /// <summary><paramref name="carets"/> moved <paramref name="lines"/> lines down, or up when negative, each
    /// end at its column.</summary>
    private static List<CodeCaret> MovedBy(List<CodeCaret> carets, int lines)
    {
        var moved = new List<CodeCaret>();
        foreach (var caret in carets)
        {
            var selection = caret.Selection;
            moved.Add(caret with
            {
                Selection = new CodeRange(selection.Anchor with { Line = selection.Anchor.Line + lines },
                    selection.Focus with { Line = selection.Focus.Line + lines }),
            });
        }
        return moved;
    }

    /// <summary>
    /// ⇧⌥↑ and ⇧⌥↓: copies the lines every caret takes above (<see cref="CodeDirection.Backward"/>) or below
    /// themselves, as one undo step, the carets on the upper copy going up and on the lower one going down.
    /// </summary>
    public bool CopyLines(CodeDirection direction)
    {
        if (ReadOnly) return false;
        EndComposition();
        var up = direction == CodeDirection.Backward;
        return AtEveryBlock(SelectedLinesOf, touching: false, carets =>
        {
            var (first, last) = BlockLines(carets);
            var block = string.Join("\n", LinesOf(first, last));
            if (up)
            {
                Edit(new CodeRange(new CodePosition(first, 0)), block + "\n", false);
                return carets;
            }
            Edit(new CodeRange(new CodePosition(last, _document.Line(last).Length)), "\n" + block, false);
            return MovedBy(carets, last - first + 1);
        });
    }

    /// <summary>
    /// ⇧⌘K: deletes the lines every caret takes, a line break with them, as one undo step. A block's caret goes
    /// to the line that takes their place, at the column its first caret had, or to the line before when the
    /// last lines go. False when there is nothing to delete, a document of one empty line.
    /// </summary>
    public bool DeleteLines()
    {
        if (ReadOnly) return false;
        EndComposition();
        return AtEveryBlock(SelectedLinesOf, touching: false, carets =>
        {
            var (first, last) = BlockLines(carets);
            var column = carets[0].Selection.Focus.Column;
            CodeRange left;
            if (last < _document.LineCount - 1)
            {
                var next = _document.Line(last + 1);
                if (!Edit(new CodeRange(new CodePosition(first, 0), new CodePosition(last + 1, 0)), string.Empty, false))
                    return null;
                left = new CodeRange(new CodePosition(first, Math.Min(column, next.Length)));
            }
            else if (first > 0)
            {
                var before = _document.Line(first - 1);
                if (!Edit(new CodeRange(new CodePosition(first - 1, before.Length), new CodePosition(last, _document.Line(last).Length)),
                        string.Empty, false))
                    return null;
                left = new CodeRange(new CodePosition(first - 1, Math.Min(column, before.Length)));
            }
            else
            {
                if (!Edit(WholeLines(first, last), string.Empty, false)) return null;
                left = new CodeRange(CodePosition.Start);
            }
            return new List<CodeCaret> { new CodeCaret(left, -1) };
        });
    }

    /// <summary>
    /// ⌃J: joins the lines each caret's selection touches into one, or the caret's line with the next when
    /// nothing spanning lines is selected, as one undo step and as VS Code's <c>JoinLinesAction</c> joins them:
    /// each next line's leading spaces and tabs go, one space joins the text unless the text before is empty
    /// or ends with a space or a tab, which collapse to one space, and a next line of spaces and tabs alone
    /// adds nothing. Spaces and tabs alone, as VS Code's: a no-break space is text, in a string literal as
    /// anywhere. With nothing selected the caret stands where the lines met. Carets whose joins would share a
    /// line join as one selection from the first to the last, as VS Code merges them. False on the last line
    /// with nothing to join it to.
    /// </summary>
    public bool JoinLines()
    {
        if (ReadOnly) return false;
        EndComposition();
        return AtEveryBlock(JoinedLinesOf, touching: false, carets =>
        {
            _selection = carets.Count == 1
                ? carets[0].Selection
                : new CodeRange(carets[0].Selection.Start, carets[^1].Selection.End);
            return JoinSelected() ? new List<CodeCaret> { new CodeCaret(_selection, -1) } : null;
        });
    }

    /// <summary>The lines a join of <paramref name="range"/> touches: its own, and the next one when it holds
    /// one line.</summary>
    private static (int First, int Last) JoinedLinesOf(CodeRange range) =>
        (range.Start.Line, range.End.Line > range.Start.Line ? range.End.Line : range.Start.Line + 1);

    /// <summary>⌃J for the primary selection alone.</summary>
    private bool JoinSelected()
    {
        var (first, last) = JoinedLines();
        if (last == first)
        {
            if (first == _document.LineCount - 1) return false;
            last = first + 1;
        }
        // The joined line is written once, from its parts: appended to the text so far, each line copied all
        // the lines before it, 32 GB of copies for 12,000 lines of 80 characters. The white space a join
        // collapses is always the last part's end, since every part after the first line starts with text,
        // so only that part is cut.
        var parts = new List<string> { _document.Line(first) };
        var length = parts[0].Length;
        // How far before the joined text's end the last join happened, VS Code's columnDeltaOffset.
        var met = 0;
        // Where the selection's end stands in the joined text, once its line is joined; none while its
        // line adds nothing.
        var end = -1;
        // Where the first line's trailing spaces and tabs began, when the first join collapsed them to one
        // space; none while they stand. Only the first join can: every later one collapses what an earlier
        // one appended.
        var cut = -1;
        var appended = false;
        for (var line = first + 1; line <= last; line++)
        {
            var text = _document.Line(line);
            var rest = text.TrimStart(' ', '\t');
            if (rest.Length == 0)
            {
                met = 0;
                continue;
            }
            var space = length > 0;
            var tail = parts[parts.Count - 1];
            if (space && tail[^1] is ' ' or '\t')
            {
                space = false;
                var kept = tail.TrimEnd(' ', '\t');
                parts[parts.Count - 1] = kept;
                length -= tail.Length - kept.Length;
                parts.Add(" ");
                length++;
                if (!appended) cut = length - 1;
            }
            if (space)
            {
                parts.Add(" ");
                length++;
            }
            parts.Add(rest);
            length += rest.Length;
            appended = true;
            met = rest.Length + (space ? 1 : 0);
            if (line == _selection.End.Line)
                end = length - rest.Length + Math.Max(0, _selection.End.Column - (text.Length - rest.Length));
        }
        var joined = string.Concat(parts);
        CodeRange after;
        if (_selection.IsEmpty)
        {
            after = new CodeRange(new CodePosition(first, joined.Length - met));
        }
        else if (_selection.Start.Line == _selection.End.Line)
        {
            // An end in the white space the join collapsed stands after the one space left: the white space
            // it held is gone, and the text after it was never selected.
            after = new CodeRange(new CodePosition(first, Collapsed(_selection.Anchor.Column, cut)),
                new CodePosition(first, Collapsed(_selection.Focus.Column, cut)));
        }
        else
        {
            // The end keeps its place in its text, as VS Code's join keeps it, so the text after it stays
            // unselected: one in the indentation that went stands where the text begins, and one on a blank
            // line that added nothing at the joined text's end. The start, on the first line, goes through
            // that line's collapsed white space as a selection on it does. The direction is the selection's.
            var from = new CodePosition(first, Collapsed(_selection.Start.Column, cut));
            var to = new CodePosition(first, end < 0 ? joined.Length : end);
            after = _selection.Anchor == _selection.Start ? new CodeRange(from, to) : new CodeRange(to, from);
        }
        return Edit(WholeLines(first, last), joined, false, after);
    }

    /// <summary>A column of a join's first line once its trailing white space, from <paramref name="cut"/>,
    /// collapsed to one space: one in that white space stands after the space; none other moves.</summary>
    private static int Collapsed(int column, int cut) => cut >= 0 && column > cut ? Math.Min(column, cut + 1) : column;

    /// <summary>
    /// Sorts the lines every caret takes, ordinally and case aside with ties broken ordinally, as one undo
    /// step, the carets kept: the same order on both targets, where a culture's collation is not (VS Code
    /// sorts by the locale's). Each block of lines is sorted on its own. False when no block has two lines out
    /// of order.
    /// </summary>
    public bool SortLines(bool descending)
    {
        if (ReadOnly) return false;
        EndComposition();
        return AtEveryBlock(SelectedLinesOf, touching: false, carets =>
        {
            var (first, last) = BlockLines(carets);
            if (LinesInOrder(first, last, descending)) return null;
            var sorted = LinesOf(first, last);
            sorted.Sort((a, b) => descending ? CompareLines(b, a) : CompareLines(a, b));
            return Edit(WholeLines(first, last), string.Join("\n", sorted), false) ? carets : null;
        });
    }

    /// <summary>
    /// Whether lines <paramref name="first"/> to <paramref name="last"/> are already in order, one line or
    /// none included: each beside the next in the order a sort gives, which is one pass where a sort is
    /// several. The order is total, a tie being the same line twice, so lines in order are the lines a sort
    /// would write.
    /// </summary>
    private bool LinesInOrder(int first, int last, bool descending)
    {
        for (var line = first; line < last; line++)
        {
            var order = CompareLines(_document.Line(line), _document.Line(line + 1));
            if (descending ? order < 0 : order > 0) return false;
        }
        return true;
    }

    private static int CompareLines(string a, string b)
    {
        var aside = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return aside != 0 ? aside : string.CompareOrdinal(a, b);
    }

    /// <summary>
    /// ⌘↩ and ⇧⌘↩: opens a line below every caret's line, or above it (<see cref="CodeDirection.Backward"/>),
    /// wherever the caret stands on its line, as one undo step. The new line is indented as the caret's, and
    /// below a line that opens a block one step more, as Enter indents; the caret goes to its indent.
    /// </summary>
    public bool InsertLine(CodeDirection direction)
    {
        if (ReadOnly) return false;
        EndComposition();
        return AtEveryCaret(() => InsertLineAtCaret(direction));
    }

    /// <summary><see cref="InsertLine"/> at the primary caret alone.</summary>
    private bool InsertLineAtCaret(CodeDirection direction)
    {
        var line = Caret.Line;
        var indent = _document.IndentOf(line);
        if (direction == CodeDirection.Backward)
            return Edit(new CodeRange(new CodePosition(line, 0)), indent + "\n", false,
                new CodeRange(new CodePosition(line, indent.Length)));
        var text = _document.Line(line);
        var opened = indent + (OpensBlock(text) ? IndentStep : string.Empty);
        return Edit(new CodeRange(new CodePosition(line, text.Length)), "\n" + opened, false,
            new CodeRange(new CodePosition(line + 1, opened.Length)));
    }

    // ---- history ------------------------------------------------------------------------------

    public bool Undo()
    {
        if (ReadOnly) return false;
        EndComposition();
        if (History.Undo(_document, out var selections, out var made) is null) return false;
        Replay(made, selections);
        return true;
    }

    public bool Redo()
    {
        if (ReadOnly) return false;
        EndComposition();
        if (History.Redo(_document, out var selections, out var made) is null) return false;
        Replay(made, selections);
        return true;
    }

    /// <summary>
    /// Makes the replacements an undo or a redo made, one after another as the history made them, each
    /// bringing the colours, the widths and the problems up to date (<see cref="Took"/>), where the colours
    /// and the widths were thrown away and measured again over the whole file. Once all are made and the selection the step restores is in
    /// place, they are raised as a step's edits are, one change, so a listener that carries a range through
    /// an edit (a problem's squiggle, a language server) hears every replacement, where it could only start
    /// over after every undo, and one that edits as it hears them edits the text the step left.
    /// </summary>
    private void Replay(IReadOnlyList<CodeEdit> made, IReadOnlyList<CodeRange> selections)
    {
        var before = _selection;
        foreach (var edit in made)
        {
            _document = _document.Replace(edit.Range, edit.InsertedText, out _);
            Took(edit);
        }
        // Every caret the step found, or left, the primary first.
        _selection = OnElementEdges(selections[0]);
        _others.Clear();
        for (var i = 1; i < selections.Count; i++) _others.Add(new CodeCaret(OnElementEdges(selections[i]), -1));
        Merge();
        _revealVersion++;
        _desiredCell = -1;
        Raise(new CodeChange(Heard(made, before)));
    }

    /// <summary>
    /// What every replacement brings up to date the moment the document takes it, an edit, a composition's
    /// step, an undo and a redo alike: the colours and the widths of the lines it touched, and the problems,
    /// which so describe the text as it stands at every moment, as the colours do (#1058). Carried only when
    /// the change was heard, a problem a listener reported while one was being heard, against the text every
    /// change so far had made, went through the changes still waiting to be heard a second time.
    /// </summary>
    private void Took(CodeEdit edit)
    {
        var line = edit.Range.Start.Line;
        var linesInserted = edit.InsertedRange.End.Line - line;
        var linesRemoved = edit.Range.End.Line - line;
        Highlighter.LineChanged(_document, line, linesInserted, linesRemoved);
        WidthsChanged(line, linesInserted, linesRemoved);
        Diagnostics.Carry(edit);
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
