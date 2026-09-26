using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// Two versions of a text, and what differs between them (docs/CODE-EDITOR-PLAN.md, slice 2b, and the
/// shape, §9). Side by side, the two sides level at every change, or inline, the lines a change removed
/// drawn between the lines that replaced them. A run of unchanged lines longer than the context folds
/// into one row that opens on a press; the toolbar, F7 and Shift+F7 step from one change to the next;
/// and each side is numbered as its file is.
/// <para>
/// Both sides are code surfaces over an editor controller, so a reader selects and copies from either,
/// and both are in ONE vertical scroll view, so they cannot drift apart; each has its own sideways
/// scroll. The modified side edits when the diff is of two texts and not read-only, and it is compared
/// again after every edit. A patch's view (<see cref="OfPatch"/>) reads only: its sides are the lines
/// the patch quotes, not the files.
/// </para>
/// </summary>
public sealed class CodeDiff : StatefulComponent
{
    /// <summary>How much of a changed word's ink shows over its line's wash.</summary>
    public const float WordAlpha = 0.3f;

    private CodeEditorController? _original;
    private CodeEditorController? _modified;
    private string? _openedOriginal;
    private string? _openedModified;
    private CodePatchFile? _openedPatch;
    private CodeDiffSource? _source;
    private CodeDocument? _comparedOriginal;
    private CodeDocument? _comparedModified;
    private CodeDiffLayout? _layout;
    private CodeDocument? _toldDocument;
    private readonly HashSet<int> _expanded = new();
    private bool _inlineChosen;
    private bool _inlineChoice;
    private float _offset;
    private float _viewport;
    private float _originalWidth;
    private float _modifiedWidth;

    public CodeDiff(string original = "", string modified = "", string? language = null)
    {
        Original = original;
        Modified = modified;
        LanguageName = language;
    }

    /// <summary>The view of one file of a patch, as <c>git diff</c> writes it (<see cref="CodePatch"/>).
    /// A factory rather than a second constructor, for the one constructor shape a twin has.</summary>
    public static CodeDiff OfPatch(CodePatchFile file, string? language = null) =>
        new("", "", language) { Patch = file };

    /// <summary>The text the diff compares against. A new one is compared again as it arrives.</summary>
    public string Original { get; set; }

    /// <summary>
    /// The text compared with <see cref="Original"/>. An editable diff OPENS with it, and the document
    /// is its own from the first keystroke, as an editor's is (<c>CodeEditor.InitialCode</c>); one that
    /// reads only takes a new one as it arrives.
    /// </summary>
    public string Modified { get; set; }

    /// <summary>One file of a patch, whose quoted lines are the two sides. A different one is opened
    /// again: parse the patch once and keep its files, or every build of the page reopens it.</summary>
    public CodePatchFile? Patch { get; set; }

    /// <summary>Which language colours both sides — a name or an extension ("csharp", ".ts").</summary>
    public string? LanguageName { get; set; }

    /// <summary>
    /// Whether it opens inline rather than side by side. The toolbar switches between the two, and a
    /// new value from the app wins over the toolbar's choice.
    /// </summary>
    public bool Inline { get; set; }

    /// <summary>How many unchanged lines stay in view either side of a change.</summary>
    public int Context { get; set; } = CodeDiffLayout.DefaultContext;

    /// <summary>Whether the modified side refuses edits. A patch's view always does.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Raised after every edit of the modified side, with its whole text.</summary>
    public Action<string>? OnChanged { get; set; }

    /// <summary>How tall the diff is: Hug (as tall as its rows, up to <see cref="MaxHeight"/>), Fill (the
    /// place it is given, as an IDE's pane does) or a fixed height. Anything but an uncapped Hug scrolls
    /// and builds only the rows in view.</summary>
    public SizeValue Height { get; set; } = SizeValue.Hug;

    /// <summary>The most a Hug diff grows to before it scrolls, in dp. 0 = no cap.</summary>
    public float MaxHeight { get; set; }

    public SizeVariant Size { get; set; } = SizeVariant.Small;
    public bool Inverse { get; set; }

    /// <summary>What the original side is called: its file, as a review names it.</summary>
    public string? OriginalCaption { get; set; }

    /// <summary>What the modified side is called.</summary>
    public string? ModifiedCaption { get; set; }

    /// <summary>The bar above the code: the caption, the counts, the steps and the switch of view.</summary>
    public bool ShowToolbar { get; set; } = true;

    /// <summary>The modified side's editor, which an IDE drives like any editor's.</summary>
    public CodeEditorController Editor
    {
        get
        {
            OpenWhatChanged();
            return _modified!;
        }
    }

    private bool Editable => Patch is null && !ReadOnly;

    private bool ShowsInline => _inlineChosen ? _inlineChoice : Inline;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not CodeDiff fresh) return;
        // The app's own choice of view wins over the toolbar's, when it makes a new one.
        if (fresh.Inline != Inline) _inlineChosen = false;
        Original = fresh.Original;
        Modified = fresh.Modified;
        Patch = fresh.Patch;
        LanguageName = fresh.LanguageName;
        Inline = fresh.Inline;
        Context = fresh.Context;
        ReadOnly = fresh.ReadOnly;
        OnChanged = fresh.OnChanged;
        Height = fresh.Height;
        MaxHeight = fresh.MaxHeight;
        Size = fresh.Size;
        Inverse = fresh.Inverse;
        OriginalCaption = fresh.OriginalCaption;
        ModifiedCaption = fresh.ModifiedCaption;
        ShowToolbar = fresh.ShowToolbar;
    }

    /// <summary>
    /// Opens the two sides again where what they show changed: a new patch, a new original, or a new
    /// modified text for a side that reads only. A side opened again starts at the first change, which
    /// it reveals.
    /// </summary>
    private void OpenWhatChanged()
    {
        var language = CodeLanguages.For(LanguageName);
        if (Patch is { } patch)
        {
            if (_original is not null && ReferenceEquals(patch, _openedPatch)) return;
            var source = CodeDiffSource.FromPatch(patch);
            // The editors hold the documents the source measured, and never a reading of their text:
            // a patch keeps a bare carriage return inside its line, which a text read again splits,
            // and the editor then had a line more than the rows and the numbers were counted on.
            _original = new CodeEditorController(language: language) { ReadOnly = true, Document = source.Original };
            _modified = new CodeEditorController(language: language) { ReadOnly = true, Document = source.Modified };
            _openedPatch = patch;
            _source = source;
            _comparedOriginal = null;
            _comparedModified = null;
            _expanded.Clear();
            StartAtTheFirstChange(source);
            return;
        }
        var opened = false;
        if (_original is null || _openedPatch is not null || Original != _openedOriginal)
        {
            _original = new CodeEditorController(Original, language) { ReadOnly = true };
            _openedOriginal = Original;
            _expanded.Clear();
            opened = true;
        }
        if (_modified is null || _openedPatch is not null || (!Editable && Modified != _openedModified))
        {
            _modified = new CodeEditorController(Modified, language);
            _openedModified = Modified;
            _toldDocument = _modified.Document;
            opened = true;
        }
        _openedPatch = null;
        _modified.ReadOnly = !Editable;
        if (opened) StartAtTheFirstChange(SourceOf());
    }

    /// <summary>What the two sides are, compared again only when a document changed.</summary>
    private CodeDiffSource SourceOf()
    {
        if (_openedPatch is not null && _source is not null) return _source;
        var original = _original!.Document;
        var modified = _modified!.Document;
        if (_source is null || original != _comparedOriginal || modified != _comparedModified)
        {
            _source = CodeDiffSource.FromDocuments(original, modified);
            _comparedOriginal = original;
            _comparedModified = modified;
        }
        return _source;
    }

    /// <summary>Both carets on the first change, which reveals it: what a reader opens a diff for.</summary>
    private void StartAtTheFirstChange(CodeDiffSource source)
    {
        if (source.Changes.Count == 0) return;
        var change = source.Changes[0];
        _modified!.Selection = new CodeRange(new CodePosition(Math.Min(change.ModifiedStart, _modified.Document.LineCount - 1), 0));
        _original!.Selection = new CodeRange(new CodePosition(Math.Min(change.OriginalStart, _original.Document.LineCount - 1), 0));
    }

    private CodeDiffLayout LayoutOf(CodeDiffSource source, bool inline) => inline
        ? CodeDiffLayout.Inline(source.Changes, source.OriginalLineCount, source.ModifiedLineCount, Context, _expanded,
            source.Gaps, count => SdkStrings.UnchangedLines(count))
        : CodeDiffLayout.SideBySide(source.Changes, source.OriginalLineCount, source.ModifiedLineCount, Context, _expanded,
            source.Gaps, count => SdkStrings.UnchangedLines(count));

    /// <summary>Tells the app about an edit of the modified side, once per new document.</summary>
    private void Notify()
    {
        var document = _modified!.Document;
        if (document == _toldDocument) return;
        _toldDocument = document;
        OnChanged?.Invoke(document.Text);
    }

    public override VisualNode Build(ComponentContext context)
    {
        OpenWhatChanged();
        var theme = context.Theme;
        var original = _original!;
        var modified = _modified!;
        var source = SourceOf();
        var inline = ShowsInline;
        var layout = LayoutOf(source, inline);
        // A caret never stands in a folded line: a fold that holds it opens, which is how a search, a
        // press on a placeholder or a step reaches the lines inside one.
        var opened = false;
        if (layout.FoldOfModified(modified.Caret.Line) is { } holding)
        {
            _expanded.Add(holding.OriginalLine);
            opened = true;
        }
        if (!inline && layout.FoldOfOriginal(original.Caret.Line) is { } held)
        {
            _expanded.Add(held.OriginalLine);
            opened = true;
        }
        if (opened) layout = LayoutOf(source, inline);
        _layout = layout;

        var lastNumber = Math.Max(
            source.OriginalLineCount > 0 ? source.OriginalNumber(source.OriginalLineCount - 1) : 1,
            source.ModifiedLineCount > 0 ? source.ModifiedNumber(source.ModifiedLineCount - 1) : 1);
        var metrics = CodeBlock.MetricsFor(context, Size, true, lastNumber);
        var origin = new Point(metrics.ContentLeft, metrics.ContentTop);
        var cell = new Size(metrics.ColumnWidth, metrics.LineHeight);
        modified.Grid = new CodeGrid(origin, cell, layout.Modified);
        original.Grid = new CodeGrid(origin, cell, layout.Original);

        var bounded = Height.Kind != SizeKind.Hug;
        var windowed = bounded || MaxHeight > 0;
        if (!windowed)
        {
            _offset = 0;
            _viewport = 0;
        }
        var (first, last) = CodeBlock.WindowOf(layout.Modified.RowCount, metrics.LineHeight, _offset, _viewport);

        var added = theme.Colors(Variant.Success);
        var removed = theme.Colors(Variant.Destructive);
        var filler = theme.Border.WithOpacity(0.35f);
        var modifiedLines = layout.Modified.LinesIn(first, last);
        // Inline, the removed lines are drawn from the original, between the lines that replaced them.
        var fillerLines = inline ? layout.Modified.SourceLinesIn(first, last) : (First: 0, Last: -1);
        var modifiedBlock = new CodeBlock("")
        {
            Document = modified.Document,
            Language = modified.Highlighter.Language,
            Highlighter = modified.Highlighter,
            Rows = layout.Modified,
            Decorations = MarksOf(source, true, modifiedLines.First, modifiedLines.Last, added.Subtle,
                added.Base.WithOpacity(WordAlpha), modified.Composition),
            FillerDocument = inline ? original.Document : null,
            FillerHighlighter = inline ? original.Highlighter : null,
            FillerDecorations = inline
                ? MarksOf(source, false, fillerLines.First, fillerLines.Last, removed.Subtle, removed.Base.WithOpacity(WordAlpha), null)
                : [],
            FillerColor = filler,
            OnPlaceholderPressed = line => SetState(() => OpenFoldOf(line, true)),
            Standalone = false,
            Size = Size,
            Inverse = Inverse,
            Metrics = metrics,
            ViewportOffset = _offset,
            ViewportHeight = _viewport,
            ViewportWidth = _modifiedWidth,
            SelectionBands = modified.SelectionBandsIn(modifiedLines.First, modifiedLines.Last),
            WidestLine = inline ? Math.Max(modified.WidestLine, original.WidestLine) : modified.WidestLine,
        };
        var ink = CodeBlock.InkFor(Inverse, theme);
        VisualNode modifiedSurface = new CodeSurface(modifiedBlock, modified)
        {
            Label = ModifiedCaption ?? SdkStrings.DiffModified,
            CaretColor = ink,
            OnChanged = () => SetState(() => Notify()),
        };
        var modifiedScroll = new ScrollView(modifiedSurface, ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fill,
            OnViewportChanged = width =>
            {
                if (MathF.Abs(width - _modifiedWidth) < 1) return;
                SetState(() => _modifiedWidth = width);
            },
        };

        var body = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Stretch };
        if (inline)
        {
            // Two columns of numbers: the original's, beside the lines it had and the ones it lost,
            // and the modified's, beside the lines it has.
            body.Add(modifiedBlock.Gutter(context, row => OriginalNumberOf(source, row)));
            body.Add(modifiedBlock.Gutter(context,
                row => row.Kind == CodeRowKind.Line ? source.ModifiedNumber(row.Line).ToString() : null));
            body.Add(new Flexible(modifiedScroll));
        }
        else
        {
            var originalLines = layout.Original.LinesIn(first, last);
            var originalBlock = new CodeBlock("")
            {
                Document = original.Document,
                Language = original.Highlighter.Language,
                Highlighter = original.Highlighter,
                Rows = layout.Original,
                Decorations = MarksOf(source, false, originalLines.First, originalLines.Last, removed.Subtle,
                    removed.Base.WithOpacity(WordAlpha), null),
                FillerColor = filler,
                OnPlaceholderPressed = line => SetState(() => OpenFoldOf(line, false)),
                Standalone = false,
                Size = Size,
                Inverse = Inverse,
                Metrics = metrics,
                ViewportOffset = _offset,
                ViewportHeight = _viewport,
                ViewportWidth = _originalWidth,
                SelectionBands = original.SelectionBandsIn(originalLines.First, originalLines.Last),
                WidestLine = original.WidestLine,
            };
            VisualNode originalSurface = new CodeSurface(originalBlock, original)
            {
                Label = OriginalCaption ?? SdkStrings.DiffOriginal,
                CaretColor = ink,
                // A read-only side still moves its caret and its selection, which have to be drawn.
                OnChanged = () => SetState(() => { }),
            };
            var originalScroll = new ScrollView(originalSurface, ScrollAxis.Horizontal)
            {
                Width = SizeValue.Fill,
                OnViewportChanged = width =>
                {
                    if (MathF.Abs(width - _originalWidth) < 1) return;
                    SetState(() => _originalWidth = width);
                },
            };
            var originalSide = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
            originalSide.Add(originalBlock.Gutter(context,
                row => row.Kind == CodeRowKind.Line ? source.OriginalNumber(row.Line).ToString() : null));
            originalSide.Add(new Flexible(originalScroll));
            var modifiedSide = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
            modifiedSide.Add(modifiedBlock.Gutter(context,
                row => row.Kind == CodeRowKind.Line ? source.ModifiedNumber(row.Line).ToString() : null));
            modifiedSide.Add(new Flexible(modifiedScroll));

            body.Add(new Flexible(originalSide));
            body.Add(new Box(new BoxStyle { Width = 1, Background = theme.Border }));
            body.Add(new Flexible(modifiedSide));
        }

        VisualNode viewport = body;
        if (windowed)
        {
            // ONE vertical scroll for both sides, so they cannot drift apart.
            viewport = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = bounded ? SizeValue.Fill : SizeValue.Hug,
                MaxHeight = !bounded && MaxHeight > 0 ? SizeValue.Fixed(MaxHeight) : SizeValue.Hug,
            }, new ScrollView(body)
            {
                Width = SizeValue.Fill,
                Height = bounded ? SizeValue.Fill : SizeValue.Hug,
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

        VisualNode frame = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = bounded ? SizeValue.Fill : SizeValue.Hug,
            Background = CodeBlock.SurfaceFor(Inverse, theme),
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            Clip = true,
        }, viewport);

        var whole = new Column(gap: Space.S2) { Width = SizeValue.Fill, Height = bounded ? SizeValue.Fill : SizeValue.Hug };
        if (ShowToolbar) whole.Add(Toolbar(theme, source, inline));
        whole.Add(bounded ? new Flexible(frame) : frame);

        // The ways from one change to the next: JetBrains' F7 and VS Code's Alt+F5, and back with Shift.
        // This diff's own, answered while the keyboard is in it: of two diffs on one page, the one in
        // use steps (page-wide, the last one mounted did, wherever the keyboard was).
        VisualNode shortcuts = new Shortcut(whole, new KeyChord("F7"), () => StepTo(true)) { FocusScoped = true };
        shortcuts = new Shortcut(shortcuts, new KeyChord("F7", KeyModifiers.Shift), () => StepTo(false)) { FocusScoped = true };
        shortcuts = new Shortcut(shortcuts, new KeyChord("F5", KeyModifiers.Alt), () => StepTo(true)) { FocusScoped = true };
        shortcuts = new Shortcut(shortcuts, new KeyChord("F5", KeyModifiers.Alt | KeyModifiers.Shift), () => StepTo(false)) { FocusScoped = true };

        var capped = bounded && MaxHeight > 0;
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Height,
            MaxHeight = capped ? SizeValue.Fixed(MaxHeight) : SizeValue.Hug,
        }, shortcuts);
    }

    /// <summary>
    /// The toolbar: what the diff shows, how many lines it added and removed, the steps through its
    /// changes and the switch between side by side and inline. The counts are written as +n and −n,
    /// which read without the colour they also wear.
    /// </summary>
    private VisualNode Toolbar(IAppTheme theme, CodeDiffSource source, bool inline)
    {
        var addedLines = 0;
        var removedLines = 0;
        foreach (var change in source.Changes)
        {
            addedLines += change.ModifiedCount;
            removedLines += change.OriginalCount;
        }
        var bar = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        var title = ModifiedCaption ?? OriginalCaption;
        bar.Add(new Flexible(new Text(title ?? "", TypeRole.Label, theme.TextSecondary, maxLines: 1) { Mono = true }));
        bar.Add(new Text($"+{addedLines}", TypeRole.LabelSmall, theme.Colors(Variant.Success).Base, maxLines: 1) { Tabular = true });
        bar.Add(new Text($"−{removedLines}", TypeRole.LabelSmall, theme.Colors(Variant.Destructive).Base, maxLines: 1) { Tabular = true });
        var none = source.Changes.Count == 0;
        bar.Add(new IconButton(new Icon(Icons.ChevronUp), SdkStrings.PreviousChange)
        {
            Size = SizeVariant.Small,
            Disabled = none,
            OnPressed = () => StepTo(false),
        });
        bar.Add(new IconButton(new Icon(Icons.ChevronDown), SdkStrings.NextChange)
        {
            Size = SizeVariant.Small,
            Disabled = none,
            OnPressed = () => StepTo(true),
        });
        bar.Add(new Button(inline ? SdkStrings.ShowSideBySide : SdkStrings.ShowInline, Variant.Ghost, SizeVariant.Small)
        {
            OnPressed = () => SetState(() =>
            {
                _inlineChosen = true;
                _inlineChoice = !inline;
            }),
        });
        return bar;
    }

    /// <summary>
    /// Both carets onto the next change after the modified caret, or the one before it, wrapping at
    /// either end as an IDE's step does. Moving the caret is what reveals the change, on both sides.
    /// </summary>
    private void StepTo(bool forward)
    {
        if (_source is not { Changes.Count: > 0 } source || _modified is not { } modified || _original is not { } original)
            return;
        // Where a change puts the caret: its first line, or the last one for a change that removed the
        // end of the text and so starts past it. Compared where it is PUT: compared where it starts,
        // that change stood after the caret the step had left on it, and every step stayed there.
        int LineOf(CodeLineChange change) => Math.Min(change.ModifiedStart, modified.Document.LineCount - 1);
        var caret = modified.Caret.Line;
        var target = forward ? source.Changes[0] : source.Changes[source.Changes.Count - 1];
        if (forward)
        {
            foreach (var change in source.Changes)
            {
                if (LineOf(change) <= caret) continue;
                target = change;
                break;
            }
        }
        else
        {
            for (var i = source.Changes.Count - 1; i >= 0; i--)
            {
                if (LineOf(source.Changes[i]) >= caret) continue;
                target = source.Changes[i];
                break;
            }
        }
        SetState(() =>
        {
            modified.Selection = new CodeRange(new CodePosition(LineOf(target), 0));
            original.Selection = new CodeRange(new CodePosition(Math.Min(target.OriginalStart, original.Document.LineCount - 1), 0));
        });
    }

    /// <summary>
    /// Opens the fold a press on either side's placeholder names, by its original line, and gives that
    /// side the keyboard. The row pressed is gone once the run opens, and on the web the focus fell
    /// with it to the page, where the diff's own keys do not answer: F7 did nothing until the reader
    /// clicked back into the code.
    /// </summary>
    private void OpenFoldOf(int line, bool modifiedSide)
    {
        if (_layout is not { } layout) return;
        var fold = modifiedSide ? layout.FoldOfModified(line) : layout.FoldOfOriginal(line);
        if (fold is { } found) _expanded.Add(found.OriginalLine);
        (modifiedSide ? _modified : _original)?.RequestFocus();
    }

    /// <summary>
    /// The marks of one side on the lines from <paramref name="first"/> to <paramref name="last"/>:
    /// a wash across every line a change added (or removed), and a stronger one over the words that
    /// changed inside it. The modified side's also underline an input method's composition.
    /// </summary>
    private static List<CodeDecoration> MarksOf(CodeDiffSource source, bool modifiedSide, int first, int last,
        ColorToken line, ColorToken word, CodeRange? composition)
    {
        var marks = new List<CodeDecoration>();
        var document = modifiedSide ? source.Modified : source.Original;
        foreach (var change in source.Changes)
        {
            var start = modifiedSide ? change.ModifiedStart : change.OriginalStart;
            var count = modifiedSide ? change.ModifiedCount : change.OriginalCount;
            if (count == 0 || start + count - 1 < first || start > last) continue;
            var end = Math.Min(start + count - 1, document.LineCount - 1);
            marks.Add(new CodeDecoration(new CodeRange(new CodePosition(start, 0),
                new CodePosition(end, document.Line(end).Length)), CodeDecorationKind.Line) { Color = line });
            foreach (var inner in change.Inner)
                marks.Add(new CodeDecoration(modifiedSide ? inner.Modified : inner.Original) { Color = word });
        }
        if (composition is { } composing) marks.Add(new CodeDecoration(composing, CodeDecorationKind.Underline));
        return marks;
    }

    /// <summary>The original number an inline view's first column shows beside a row: an unchanged
    /// line's own, a removed line's, and none beside a line a change added.</summary>
    private static string? OriginalNumberOf(CodeDiffSource source, CodeRow row)
    {
        if (row.Kind == CodeRowKind.Filler && row.SourceLine >= 0) return source.OriginalNumber(row.SourceLine).ToString();
        if (row.Kind != CodeRowKind.Line) return null;
        var line = source.OriginalLineOf(row.Line);
        return line < 0 ? null : source.OriginalNumber(line).ToString();
    }
}
