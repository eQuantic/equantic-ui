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

    /// <summary>What FIND is looking for. Every match is washed and the current one outlined; null
    /// or empty means the find bar is closed and nothing is marked.</summary>
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
        return editor;
    }

    /// <summary>
    /// Everything the block should mark: what the app handed in, every search match, and the bracket
    /// pair under the caret. Computed per frame from the model rather than stored, because all three
    /// are functions of where the caret is — and a stored copy would be one keystroke behind.
    /// </summary>
    private IReadOnlyList<CodeDecoration> Marks(CodeEditorController editor)
    {
        var needle = _findOpen && _findText.Length > 0 ? _findText : Search;
        if (needle is not { Length: > 0 } && !MatchBrackets) return Decorations;

        var marks = new List<CodeDecoration>(Decorations);
        if (needle is { Length: > 0 } search)
        {
            var current = editor.Selection;
            foreach (var match in editor.FindAll(search, SearchMatchCase))
            {
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

    public override VisualNode Build(ComponentContext context)
    {
        var editor = Editor;
        editor.ReadOnly = ReadOnly;
        // The controller's OWN highlighter, kept across frames: a keystroke re-colours the line it
        // touched and stops, where a fresh one per frame would re-tokenize the file per character.
        var highlighter = editor.Highlighter;

        var metrics = CodeBlock.MetricsFor(context, Size, ShowLineNumbers,
            FirstLineNumber + editor.Document.LineCount - 1);

        // The empty-string constructor + inits, NOT the (document, language) pair as arguments:
        // the transpiled twin has one constructor whose body is the string shape, and the property
        // assignment lands after it on both sides. CodeBlock.Of is this same move, packaged.
        var block = new CodeBlock("")
        {
            Document = editor.Document,
            Language = highlighter.Language,
            Decorations = Marks(editor),
            ShowLineNumbers = ShowLineNumbers,
            FirstLineNumber = FirstLineNumber,
            MaxHeight = MaxHeight,
            Size = Size,
            Inverse = Inverse,
            Caption = Caption,
            GutterMarkers = GutterMarkers,
            OnGutterPressed = OnGutterPressed,
            Highlighter = highlighter,
            // THE grid — the same numbers the surface places the caret and the band on.
            Metrics = metrics,
            // The window the block builds. Both numbers come back from layout, so the first frame
            // builds everything and every frame after builds what you can see.
            ViewportOffset = _offset,
            ViewportHeight = _viewport,
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
            // The caret's line is washed while the editor holds it — the one piece of state the
            // read-only block cannot know about.
            ActiveLine = editor.Caret.Line,
        };

        VisualNode surface = new CodeSurface(block, editor)
        {
            ContentTop = metrics.ContentTop,
            LineHeight = metrics.LineHeight,
            ContentLeft = metrics.ContentLeft,
            ColumnWidth = metrics.ColumnWidth,
            Autofocus = Autofocus,
            Label = Caption ?? "Code editor",
            // The marks write with the BLOCK's ink, not the page's — see CodeBlock.InkFor.
            CaretColor = CodeBlock.InkFor(Inverse, context.Theme),
            SelectionColor = CodeBlock.SelectionFor(Inverse, context.Theme),
            // The controller mutates outside the tree, so the rebuild has to be asked for. This is
            // the seam: everything the surface does ends here, and here is where the app hears it.
            OnChanged = () => SetState(() =>
            {
                OnChanged?.Invoke(editor.Document.Text);
                OnSelectionChanged?.Invoke(editor.Selection);
            }),
        };

        // ⌘F is UI, not a model command, so it is not in the keymap: it is a chord that is live
        // because this subtree is on screen, which is what Shortcut already means.
        surface = new Shortcut(surface, new KeyChord("f", KeyModifiers.Command),
            () => SetState(() => _findOpen = true));

        if (!_findOpen) return surface;

        var layers = new Stack { Width = SizeValue.Fill };
        layers.Add(surface);
        layers.Add(new Positioned(FindBar(context, editor), top: Space.S2, end: Space.S2));
        return layers;
    }

    /// <summary>
    /// The find bar: what to look for, how many there are, and the two ways through them. It floats
    /// over the top-right corner rather than pushing the code down — code that jumps when you open
    /// find has lost the line you were looking at.
    /// </summary>
    private VisualNode FindBar(ComponentContext context, CodeEditorController editor)
    {
        var theme = context.Theme;
        var matches = _findText.Length > 0 ? editor.FindAll(_findText, SearchMatchCase).Count : 0;
        var index = 0;
        if (matches > 0)
        {
            var current = editor.Selection;
            var all = editor.FindAll(_findText, SearchMatchCase);
            for (var i = 0; i < all.Count; i++)
                if (all[i].Start.Equals(current.Start)) { index = i + 1; break; }
        }

        void Step(bool forward)
        {
            if (_findText.Length == 0) return;
            var match = editor.FindNext(_findText, SearchMatchCase, backward: !forward);
            if (match is { } found) SetState(() => editor.Selection = found);
        }

        var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        row.Add(new Box(new BoxStyle { Width = 168 }, new TextEntry(_findText,
            value => SetState(() => _findText = value))
        {
            Placeholder = "Find",
            Autofocus = true,
            OnSubmit = () => Step(true),
        }));
        row.Add(new Text(matches == 0 ? (_findText.Length == 0 ? "" : "0") : $"{index}/{matches}",
            TypeRole.LabelSmall, theme.TextMuted, maxLines: 1)
        {
            Tabular = true,
        });
        row.Add(new IconButton(Icons.ChevronUp, "Previous match")
        {
            Size = SizeVariant.Small,
            OnPressed = () => Step(false),
        });
        row.Add(new IconButton(Icons.ChevronDown, "Next match")
        {
            Size = SizeVariant.Small,
            OnPressed = () => Step(true),
        });
        row.Add(new IconButton(Icons.Close, "Close find")
        {
            Size = SizeVariant.Small,
            OnPressed = () => SetState(() => { _findOpen = false; _findText = ""; }),
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
