using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// CODE, written. The same surface <see cref="CodeBlock"/> draws, plus a caret, a selection and a
/// keyboard — which is the whole difference between reading code and editing it.
/// <para>
/// The controller is STATE: it holds the document, the selection and the undo history, and it has
/// to outlive the rebuild that every keystroke causes. That is why this is a stateful component and
/// why the surface node carries the controller itself rather than a copy of what it currently says.
/// </para>
/// <para>
/// An app that wants to own the text passes <c>OnChanged</c> and gets the document back after every
/// edit; an app that just wants an editor can ignore it and read <see cref="Editor"/> when it needs
/// to. Both are ordinary — an editor is not a form field, and forcing every keystroke through the
/// app would make undo the app's problem too.
/// </para>
/// </summary>
public sealed class CodeEditor : StatefulComponent
{
    private CodeEditorController? _editor;
    private bool _findOpen;
    private string _findText = "";
    private float _offset;
    private float _viewport;
    private float _viewportWidth;

    public CodeEditor(string code = "", string? language = null)
    {
        InitialCode = code;
        LanguageName = language;
    }

    /// <summary>The text the editor OPENS with. Later changes to it are ignored — the document is
    /// the editor's own from the first keystroke, and reopening it is a new editor.</summary>
    public string InitialCode { get; init; }

    /// <summary>Which language colours it — a name or an extension ("csharp", ".ts", "python").</summary>
    public string? LanguageName { get; set; }

    /// <summary>Raised after every edit, with the whole document. Null = the app is not tracking it.</summary>
    public Action<string>? OnChanged { get; set; }

    /// <summary>Raised when the caret or the selection moves — a status bar's cue.</summary>
    public Action<CodeRange>? OnSelectionChanged { get; set; }

    public bool ShowLineNumbers { get; set; } = true;
    public int FirstLineNumber { get; set; } = 1;
    /// <summary>
    /// How tall the editor is. Hug, the default, is as tall as the code, up to
    /// <see cref="MaxHeight"/> when one is set. Fill takes the height its place gives it, which is
    /// how an IDE's pane uses it, and a fixed height is that many dp. Anything but an uncapped Hug
    /// scrolls the code inside it and builds only the lines in view.
    /// </summary>
    public SizeValue Height { get; set; } = SizeValue.Hug;

    /// <summary>The most a Hug editor grows to before it scrolls, in dp. 0 = no cap.</summary>
    public float MaxHeight { get; set; }
    public SizeVariant Size { get; set; } = SizeVariant.Small;
    public bool Inverse { get; set; }
    public bool ReadOnly { get; set; }
    public bool Autofocus { get; set; }
    public string? Caption { get; set; }

    /// <summary>Lines to point at — an error's line, a breakpoint, a diff hunk.</summary>
    public IReadOnlyList<CodeGutterMarker> GutterMarkers { get; set; } = [];

    /// <summary>Ranges to mark — search matches, a squiggle. The editor ADDS to these: the bracket
    /// under the caret, and the matches of whatever is being searched for.</summary>
    public IReadOnlyList<CodeDecoration> Decorations { get; set; } = [];

    /// <summary>Outlines the bracket the caret is against and the one it pairs with. On by default:
    /// it is how you find the end of a block without counting.</summary>
    public bool MatchBrackets { get; set; } = true;

    /// <summary>What the APP searches for: an IDE with a find UI of its own sets it. Every match is
    /// washed and the one the caret is on outlined, while the editor's own find bar is closed or its
    /// field empty; null or empty marks nothing.</summary>
    public string? Search { get; set; }

    public bool SearchMatchCase { get; set; }

    /// <summary>A press on a gutter row — where an IDE toggles a breakpoint.</summary>
    public Action<int>? OnGutterPressed { get; set; }

    /// <summary>The live editor: the document, the selection, and every command. An IDE reaches for
    /// this to run its own — a formatter, a refactor, a language server's edit — and they undo like
    /// anything the person typed, because they go through the same primitive.</summary>
    public CodeEditorController Editor => _editor ??= Create();

    private CodeEditorController Create()
    {
        var editor = new CodeEditorController(InitialCode, CodeLanguages.For(LanguageName))
        {
            ReadOnly = ReadOnly,
        };
        _toldDocument = editor.Document;
        _toldSelection = editor.Selection;
        return editor;
    }

    /// <summary>The document and the selection the app was last told about (see <see cref="Notify"/>).</summary>
    private CodeDocument? _toldDocument;
    private CodeRange _toldSelection;

    /// <summary>
    /// Tells the app what CHANGED since it was last told: the document after an edit, the selection
    /// after a move. Both were raised together for anything the surface did, so an arrow reached the
    /// app as an edit (an app re-reading the document on every change did it for every arrow), and
    /// the find bar's steps moved the selection without a word.
    /// </summary>
    private void Notify(CodeEditorController editor)
    {
        if (editor.Document != _toldDocument)
        {
            _toldDocument = editor.Document;
            OnChanged?.Invoke(editor.Document.Text);
        }
        if (editor.Selection != _toldSelection)
        {
            _toldSelection = editor.Selection;
            OnSelectionChanged?.Invoke(editor.Selection);
        }
    }

    /// <summary>
    /// Everything the block should mark: what the app handed in, the search matches on the lines from
    /// <paramref name="first"/> to <paramref name="last"/>, and the bracket pair under the caret.
    /// Computed per frame from the model rather than stored, because all three are functions of
    /// where the caret is — and a stored copy would be one keystroke behind.
    /// <para>
    /// Only the matches in the window become marks. A search over a long file finds tens of
    /// thousands, and every one of them was a decoration the block walked twice to throw away, on
    /// every build.
    /// </para>
    /// </summary>
    private IReadOnlyList<CodeDecoration> Marks(CodeEditorController editor, IReadOnlyList<CodeRange> matches,
        int first, int last)
    {
        if (matches.Count == 0 && !MatchBrackets && editor.Composition is null) return Decorations;

        var marks = new List<CodeDecoration>(Decorations);
        // The text an input method is still composing is IN the document, and says so: underlined,
        // in the code's own ink, until it is committed or cancelled.
        if (editor.Composition is { } composition)
            marks.Add(new CodeDecoration(composition, CodeDecorationKind.Underline));
        if (matches.Count > 0)
        {
            var current = editor.Selection;
            for (var i = FirstEndingOnOrAfter(matches, first); i < matches.Count && matches[i].Start.Line <= last; i++)
            {
                var match = matches[i];
                // The one the caret is ON wears the outline; the rest are washed. Without that,
                // "next match" moves something nobody can see.
                marks.Add(new CodeDecoration(match,
                    match.Start.Equals(current.Start) && match.End.Equals(current.End)
                        ? CodeDecorationKind.Outline
                        : CodeDecorationKind.Highlight));
            }
        }
        if (MatchBrackets && editor.BracketAtCaret() is { } pair)
        {
            marks.Add(new CodeDecoration(
                new CodeRange(pair.Here, pair.Here with { Column = pair.Here.Column + 1 }),
                CodeDecorationKind.Outline));
            marks.Add(new CodeDecoration(
                new CodeRange(pair.There, pair.There with { Column = pair.There.Column + 1 }),
                CodeDecorationKind.Outline));
        }
        return marks;
    }

    /// <summary>
    /// Adopt the fresh configuration when the reconciler retains this instance across a parent
    /// rebuild — the hook every stateful component needs and this one did not have.
    /// <para>
    /// The base implementation does nothing, which is right for a component whose props never
    /// change, and silently wrong for one whose props do: a page that recomputed
    /// <see cref="Decorations"/> (diagnostics as you type), <see cref="Search"/> or
    /// <see cref="GutterMarkers"/> handed them to a NEW node the reconciler then threw away in
    /// favour of the retained one. The parent rebuilt, its own text updated, and the editor kept
    /// drawing the first frame's configuration forever — with nothing anywhere to say so.
    /// </para>
    /// <para>
    /// <see cref="InitialCode"/> is deliberately NOT adopted: the editor takes its text when it
    /// opens and the document is its own from then on, which is the whole reason it is called
    /// <em>initial</em>. Adopting it would overwrite what someone is typing on every parent build.
    /// </para>
    /// </summary>
    public override void AdoptConfig(UiComponent next)
    {
        if (next is not CodeEditor fresh) return;
        LanguageName = fresh.LanguageName;
        OnChanged = fresh.OnChanged;
        OnSelectionChanged = fresh.OnSelectionChanged;
        ShowLineNumbers = fresh.ShowLineNumbers;
        FirstLineNumber = fresh.FirstLineNumber;
        Height = fresh.Height;
        MaxHeight = fresh.MaxHeight;
        Size = fresh.Size;
        Inverse = fresh.Inverse;
        ReadOnly = fresh.ReadOnly;
        Autofocus = fresh.Autofocus;
        Caption = fresh.Caption;
        GutterMarkers = fresh.GutterMarkers;
        Decorations = fresh.Decorations;
        MatchBrackets = fresh.MatchBrackets;
        Search = fresh.Search;
        SearchMatchCase = fresh.SearchMatchCase;
        OnGutterPressed = fresh.OnGutterPressed;
    }

    /// <summary>What find is looking for: the bar's text while the bar is open, else what the app
    /// asked for through <see cref="Search"/>.</summary>
    private string? Needle => _findOpen && _findText.Length > 0 ? _findText : Search;

    /// <summary>The matches of the last search, and what they were found for: the document, the
    /// needle and the case rule. A document is immutable, so its reference says whether the text
    /// changed.</summary>
    private IReadOnlyList<CodeRange> _matches = [];
    private CodeDocument? _matchedIn;
    private string? _matchedFor;
    private bool _matchedCase;

    /// <summary>
    /// Every match of <paramref name="needle"/>, found again only when the document, the needle or
    /// the case rule changed. Every build scanned the whole file, and a build is a keystroke, a
    /// caret blink's worth of state and every step of a scroll: over 50,000 lines, 4 ms a step before
    /// anything was drawn.
    /// </summary>
    private IReadOnlyList<CodeRange> MatchesOf(CodeEditorController editor, string? needle)
    {
        if (needle is not { Length: > 0 }) return [];
        if (editor.Document != _matchedIn || needle != _matchedFor || SearchMatchCase != _matchedCase)
        {
            _matches = editor.FindAll(needle, SearchMatchCase);
            _matchedIn = editor.Document;
            _matchedFor = needle;
            _matchedCase = SearchMatchCase;
        }
        return _matches;
    }

    /// <summary>The index of the first match that ends on <paramref name="line"/> or after it. The
    /// matches are in document order and never overlap, so their ends are in order too.</summary>
    private static int FirstEndingOnOrAfter(IReadOnlyList<CodeRange> matches, int line)
    {
        var low = 0;
        var high = matches.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (matches[middle].End.Line < line) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var editor = Editor;
        editor.ReadOnly = ReadOnly;
        // The controller's OWN highlighter, kept across frames: a keystroke re-colours the line it
        // touched and stops, where a fresh one per frame would re-tokenize the file per character.
        var highlighter = editor.Highlighter;

        var metrics = CodeBlock.MetricsFor(context, Size, ShowLineNumbers,
            FirstLineNumber + editor.Document.LineCount - 1);

        // THE grid, handed to the engine — the only thing that turns a position into a point. The
        // block draws the lines on these same numbers, so the caret and the glyphs cannot disagree.
        // BEFORE the block reads the selection's bands: read first, they were drawn on the grid of
        // the build before, which on the first frame is the default one.
        editor.Grid = new CodeGrid(new Point(metrics.ContentLeft, metrics.ContentTop),
            new Size(metrics.ColumnWidth, metrics.LineHeight));

        // A BOUNDED editor scrolls the code inside it and builds only the lines in view: one that
        // fills its place (an IDE's pane), one of a fixed height, and one capped by MaxHeight. Only
        // an uncapped Hug grows with the code, and builds all of it, because all of it is on screen.
        var bounded = Height.Kind != SizeKind.Hug;
        var windowed = bounded || MaxHeight > 0;
        if (!windowed)
        {
            // An editor with no viewport shows every line. The window it had while it had one went
            // on limiting the build: a Fill editor switched to Hug went on building lines 1168 to
            // 1220 of 2000, and every other line was blank. And a viewport mounted again later
            // starts at the top, where this offset has to start too.
            _offset = 0;
            _viewport = 0;
        }
        // The lines this build draws, and the only ones anything is measured on: the selection's
        // bands and the matches are asked for between these two, not over the whole file.
        var (first, last) = CodeBlock.WindowOf(editor.Document.LineCount, metrics.LineHeight, _offset, _viewport);

        // Every match, found once per search: the marks and the bar's count both read it.
        var matches = MatchesOf(editor, Needle);

        // The empty-string constructor + inits, NOT the (document, language) pair as arguments:
        // the transpiled twin has one constructor whose body is the string shape, and the property
        // assignment lands after it on both sides. CodeBlock.Of is this same move, packaged.
        var block = new CodeBlock("")
        {
            Document = editor.Document,
            Language = highlighter.Language,
            Decorations = Marks(editor, matches, first, last),
            ShowLineNumbers = ShowLineNumbers,
            FirstLineNumber = FirstLineNumber,
            // Bare content — see CodeBlock.Standalone. The slab and both scroll views are built
            // below, OUTSIDE the surface, so the marks travel with the code instead of being
            // painted against a box the code slid out from under.
            Standalone = false,
            Size = Size,
            Inverse = Inverse,
            GutterMarkers = GutterMarkers,
            OnGutterPressed = OnGutterPressed,
            Highlighter = highlighter,
            // THE grid — the same numbers the surface places the caret and the band on.
            Metrics = metrics,
            // The window the block builds. Both numbers come back from layout, so the first frame
            // builds everything and every frame after builds what you can see.
            ViewportOffset = _offset,
            ViewportHeight = _viewport,
            ViewportWidth = _viewportWidth,
            // The caret's line is washed while the editor holds it, and the selection is drawn under
            // the text — the two pieces of state the read-only block cannot know about. Both are the
            // ENGINE's measurements, on the grid handed to it above.
            ActiveLine = editor.Caret.Line,
            SelectionBands = editor.SelectionBandsIn(first, last),
            WidestLine = editor.WidestLine,
        };

        VisualNode surface = new CodeSurface(block, editor)
        {
            Autofocus = Autofocus,
            Label = Caption ?? SdkStrings.CodeEditor,
            // The caret writes with the BLOCK's ink, not the page's — see CodeBlock.InkFor.
            CaretColor = CodeBlock.InkFor(Inverse, context.Theme),
            // The controller mutates outside the tree, so the rebuild has to be asked for. This is
            // the seam: everything the surface does ends here, and here is where the app hears it.
            OnChanged = () => SetState(() => Notify(editor)),
        };

        // The viewport lives OUT HERE, around the surface, rather than inside the block. One
        // coordinate space is the whole point: the surface travels with the code, so the caret and
        // the selection — which are drawn against it — travel with it too, and a pointer lands on
        // the column it is pointing at however far the file has been scrolled.
        //
        // Note the order: the SCROLLERS fill, the SURFACE hugs the code. That is what keeps a long
        // line from widening the pane it sits in — the viewport takes the width it is given and the
        // line overflows INSIDE it, which is the whole reason a scroll view is here at all.
        VisualNode viewport = new ScrollView(surface, ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fill,
            // How wide it turned out to be, handed back to the block so the code is never narrower
            // than the space you can click in.
            OnViewportChanged = width =>
            {
                if (MathF.Abs(width - _viewportWidth) < 1) return;
                SetState(() => _viewportWidth = width);
            },
        };

        // THE GUTTER, beside the sideways scroll and inside the vertical one below: the numbers
        // travel down the file with the code and stay put as it slides across. The block's own
        // instance builds them, over the same window, so there is one set of numbers built from one
        // measurement — and ContentLeft counts from where the CODE begins, not from where the gutter
        // does, which is what makes this arrangement possible at all.
        if (ShowLineNumbers)
        {
            var withGutter = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
            withGutter.Add(block.Gutter(context));
            withGutter.Add(new Flexible(viewport));
            viewport = withGutter;
        }

        if (windowed)
        {
            viewport = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = bounded ? SizeValue.Fill : SizeValue.Hug,
                // A Hug editor's cap is the viewport's; a bounded one's caps the whole editor below.
                MaxHeight = !bounded && MaxHeight > 0 ? SizeValue.Fixed(MaxHeight) : SizeValue.Hug,
            }, new ScrollView(viewport)
            {
                Width = SizeValue.Fill,
                Height = bounded ? SizeValue.Fill : SizeValue.Hug,
                // The window the block builds, reported from the viewport that actually moves.
                OnScrolled = offset =>
                {
                    if (MathF.Abs(offset - _offset) < 1) return;
                    SetState(() => _offset = offset);
                },
                OnViewportChanged = height =>
                {
                    if (MathF.Abs(height - _viewport) < 1) return;
                    SetState(() => _viewport = height);
                },
            });
        }

        // The slab the block used to paint for itself. It is out here now because it has to be the
        // VIEWPORT's frame, not the content's: a slab as wide as the file would round its corners
        // somewhere off screen.
        surface = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = bounded ? SizeValue.Fill : SizeValue.Hug,
            Background = CodeBlock.SurfaceFor(Inverse, context.Theme),
            CornerRadius = new CornerRadii(context.Theme.Shape(ShapeScale.Medium)),
            Clip = true,
        }, viewport);

        // ⌘F is UI, not a model command, so it is not in the keymap: it is a chord that is live
        // because this subtree is on screen, which is what Shortcut already means.
        surface = new Shortcut(surface, new KeyChord("f", KeyModifiers.Command),
            () => SetState(() => _findOpen = true));

        // THE LAYERS, always: the code first, then its caption in the corner, then the find bar over
        // both. The code is the first layer whether or not anything is over it, so it keeps its place
        // in the tree. Opening find used to wrap it in a Stack it did not have before, which made it
        // a new surface to every host: the scroll went back to the top, "next" revealed nothing
        // (a surface seen for the first time is where it opened), and on Photon the keyboard pointed
        // at a path nothing had any more.
        // A bounded editor with a cap takes the height its place gives it up to the cap, slab and
        // all. The cap was the inner viewport's alone, and a Fill editor in a pane taller than it
        // drew an empty slab below the code.
        var capped = bounded && MaxHeight > 0;
        var layers = new Stack { Width = SizeValue.Fill, Height = capped ? SizeValue.Fill : Height };
        layers.Add(surface);
        if (CodeBlock.Corner(Caption, null, Inverse, context.Theme) is { } corner) layers.Add(corner);
        if (_findOpen)
        {
            // Escape closes it wherever the keyboard is, in the bar or in the code, which is what
            // it means in every editor: a chord live while the bar is on screen.
            // The bar counts what ITS field looks for: with the field empty, the matches of the app's own
            // Search still mark the code, and are no count of anything typed.
            IReadOnlyList<CodeRange> found = _findText.Length > 0 ? matches : [];
            layers.Add(new Positioned(new Shortcut(FindBar(context, editor, found), KeyChord.Escape,
                () => CloseFind(editor)), top: Space.S2, end: Space.S2));
        }
        if (!capped) return layers;
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Height,
            MaxHeight = SizeValue.Fixed(MaxHeight),
        }, layers);
    }

    /// <summary>Closes the bar and gives the code the keyboard back, which is where it came from
    /// or where it is anyway.</summary>
    private void CloseFind(CodeEditorController editor) => SetState(() =>
    {
        _findOpen = false;
        _findText = "";
        editor.RequestFocus();
    });

    /// <summary>
    /// The find bar: what to look for, how many there are, and the two ways through them. It floats
    /// over the top-right corner rather than pushing the code down — code that jumps when you open
    /// find has lost the line you were looking at.
    /// </summary>
    private VisualNode FindBar(ComponentContext context, CodeEditorController editor,
        IReadOnlyList<CodeRange> matches)
    {
        var theme = context.Theme;
        // Which match the caret is on, by halving: the matches are in document order, and a
        // search over a long file has tens of thousands of them.
        var index = 0;
        var current = editor.Selection.Start;
        var low = 0;
        var high = matches.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (matches[middle].Start.CompareTo(current) < 0) low = middle + 1;
            else high = middle;
        }
        if (low < matches.Count && matches[low].Start.Equals(current)) index = low + 1;

        // A STEP selects the match, which reveals it (the selection's own rule), and the app hears
        // that the selection moved, as it does when a key moves it.
        void Step(bool forward)
        {
            if (_findText.Length == 0) return;
            // Through the matches the bar already holds: FindNext would search the file again.
            if (editor.NextOf(matches, backward: !forward) is not { } found) return;
            SetState(() =>
            {
                editor.Selection = found;
                Notify(editor);
            });
        }

        var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        row.Add(new Box(new BoxStyle { Width = 168 }, new TextEntry(_findText,
            value => SetState(() => _findText = value))
        {
            Placeholder = SdkStrings.Find,
            Label = SdkStrings.Find,
            Autofocus = true,
            OnSubmit = () => Step(true),
        }));
        row.Add(new Text(matches.Count == 0 ? (_findText.Length == 0 ? "" : "0") : $"{index}/{matches.Count}",
            TypeRole.LabelSmall, theme.TextMuted, maxLines: 1)
        {
            Tabular = true,
        });
        row.Add(new IconButton(new Icon(Icons.ChevronUp), SdkStrings.PreviousMatch)
        {
            Size = SizeVariant.Small,
            OnPressed = () => Step(false),
        });
        row.Add(new IconButton(new Icon(Icons.ChevronDown), SdkStrings.NextMatch)
        {
            Size = SizeVariant.Small,
            OnPressed = () => Step(true),
        });
        row.Add(new IconButton(new Icon(Icons.Close), SdkStrings.CloseFind)
        {
            Size = SizeVariant.Small,
            OnPressed = () => CloseFind(editor),
        });

        return new Box(new BoxStyle
        {
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
            Shadow = theme.Elevation(2),
        }, row);
    }
}
