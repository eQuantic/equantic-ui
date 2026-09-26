using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// CODE, read. Syntax-coloured through the theme's palette, numbered in a gutter, with the marks an
/// IDE hands in over it — diagnostics, search matches, a debugger's current line.
/// <para>
/// Every line becomes a Row of coloured Text runs, which is why it needs no engine support beyond
/// the monospaced face: the same tree draws as GPU pixels and as DOM. The editable twin
/// (<c>CodeEditor</c>) is this surface plus a caret and a keyboard — the drawing is shared, because
/// what an editor shows between keystrokes is exactly this.
/// </para>
/// </summary>
public sealed class CodeBlock : StatelessComponent
{
    public CodeBlock(string code, string? language = null)
    {
        Document = CodeDocument.FromText(code);
        Language = CodeLanguages.For(language);
    }

    /// <summary>
    /// The advanced pair — an existing document and an already-resolved language (what
    /// <c>CodeEditor</c> hands over every rebuild). A FACTORY rather than a second constructor:
    /// the transpiled twin keeps ONE constructor shape, and a C# overload picked at compile time
    /// has no runtime discriminator on the other side — the string body ran against a document
    /// and died in the first browser that mounted an editor.
    /// </summary>
    public static CodeBlock Of(CodeDocument document, ICodeLanguage language) =>
        new("") { Document = document, Language = language };

    public CodeDocument Document { get; init; }
    public ICodeLanguage Language { get; init; }

    /// <summary>The numbers down the left. Off for a snippet in a paragraph, on for a file.</summary>
    public bool ShowLineNumbers { get; init; } = true;

    /// <summary>The number the first line carries — a fragment quoted from line 120 says 120.</summary>
    public int FirstLineNumber { get; init; } = 1;

    /// <summary>Caps the height and scrolls past it; 0 = as tall as the code.</summary>
    public float MaxHeight { get; init; }

    /// <summary>How many cells apart the tab stops are: the language's indent width, the number the
    /// editor's engine places its carets by (<c>CodeEditorController.CellsOf</c>).</summary>
    private int TabSize => Language.Rules.IndentWidth;

    /// <summary>
    /// Whether the block is the WHOLE widget — its own slab, its own viewport — or bare content
    /// that something outside frames and scrolls.
    /// <para>
    /// True for a snippet. False when an editor wraps it, and the reason is the caret: the marks
    /// are drawn against the surface that holds them, so a block scrolling INSIDE that surface puts
    /// the code in one coordinate space and the marks in another. Scroll a long line sideways and
    /// the text moves while the caret stays behind, and a click is read at the column it would have
    /// hit had nothing scrolled. Scrolling the surface instead keeps the marks, the code and the
    /// pointer in one space by construction — no compensation to keep in sync anywhere.
    /// </para>
    /// </summary>
    public bool Standalone { get; init; } = true;

    /// <summary>The size of the code itself; the gutter follows it.</summary>
    public SizeVariant Size { get; init; } = SizeVariant.Small;

    /// <summary>A dark slab in BOTH modes (a code sample in documentation), instead of the
    /// theme's surface. What the design system does when code is a figure, not a control.</summary>
    public bool Inverse { get; init; }

    /// <summary>Lines to point at — an error's line, a diff hunk, the statement being debugged.</summary>
    public IReadOnlyList<CodeGutterMarker> GutterMarkers { get; init; } = [];

    /// <summary>Ranges to mark: search matches, the symbol under the caret, a squiggle.</summary>
    public IReadOnlyList<CodeDecoration> Decorations { get; init; } = [];

    /// <summary>A line to wash — the caret's line in an editor, a debugger's stop.</summary>
    public int? ActiveLine { get; init; }

    /// <summary>
    /// The selection, as the engine measured it: one band per line, in the surface's own coordinates
    /// (<c>CodeEditorController.SelectionBands</c>). Drawn HERE, in the block's own mark layer —
    /// under the text and over the active line — rather than by a realizer on top of everything:
    /// painted from outside, a band sat under whatever layer the block raised (a decoration's) and
    /// vanished the moment the caret touched a bracket.
    /// </summary>
    public IReadOnlyList<Rect> SelectionBands { get; init; } = [];

    /// <summary>
    /// How many cells the widest line takes, when whoever composes the block keeps it across builds
    /// (an editor does: <c>CodeEditorController.WidestLine</c>, kept beside the document). Null, the
    /// default, has the block measure every line of the document, which a snippet can afford and a
    /// long file scrolled a step at a time cannot. 0 is a width like any other: a file of empty lines.
    /// </summary>
    public int? WidestLine { get; init; }

    /// <summary>
    /// The rows the lines are drawn on, when a row is not simply a line (docs/CODE-EDITOR-PLAN.md, the
    /// shape, §9): the padding that keeps two sides of a diff level, the lines a change removed drawn
    /// between the lines that replaced them, a run of unchanged lines folded into one row. Null, the
    /// default, draws a row per line. The window, its spacers, the gutter and every mark count in
    /// rows, and an editor places its caret on the same map (<c>CodeGrid.Rows</c>).
    /// </summary>
    public CodeRows? Rows { get; init; }

    /// <summary>The document a filler row shows its source line from: the original, in an inline
    /// diff, whose removed lines are drawn between the lines that replaced them.</summary>
    public CodeDocument? FillerDocument { get; init; }

    /// <summary>What colours <see cref="FillerDocument"/>, kept across builds like
    /// <see cref="Highlighter"/>. Null has the block make its own.</summary>
    public CodeHighlighter? FillerHighlighter { get; init; }

    /// <summary>Ranges of <see cref="FillerDocument"/> to mark where its lines are drawn: a removed
    /// line's wash, and the words it lost.</summary>
    public IReadOnlyList<CodeDecoration> FillerDecorations { get; init; } = [];

    /// <summary>The wash of a row that shows no line: a diff's padding, the gap a patch left out, a
    /// folded run. Null leaves them the slab's own.</summary>
    public ColorToken? FillerColor { get; init; }

    /// <summary>A press on a folded run's row, with the first line it hides: a diff opens it.</summary>
    public Action<int>? OnPlaceholderPressed { get; init; }

    /// <summary>How much of the selection's ink shows — the band sits under the text, so it only has
    /// to be seen, never read through.</summary>
    public const float SelectionAlpha = 0.28f;

    /// <summary>Shown top-right, over the code — a file name, a language label.</summary>
    public string? Caption { get; init; }

    /// <summary>Puts a Copy button in the corner when the app can reach a clipboard.</summary>
    public Action? OnCopy { get; init; }

    /// <summary>A press on a gutter row — where an IDE toggles a breakpoint.</summary>
    public Action<int>? OnGutterPressed { get; init; }

    /// <summary>
    /// The grid to draw on, when a caller has already measured it. An editor places its caret at
    /// <c>contentTop + line × lineHeight</c>; if this block measures a different line height —
    /// which it will the moment the two are built with contexts of different density — the caret
    /// sits between lines and drifts further with every line down the file.
    /// </summary>
    public CodeMetrics? Metrics { get; init; }

    /// <summary>Reused across frames by an editor, so colouring stays incremental. Null = the
    /// block makes its own, which is right for a snippet that never changes.</summary>
    public CodeHighlighter? Highlighter { get; init; }

    /// <summary>
    /// Where the viewport currently sits, and how tall it is. Given both, only the lines you can SEE
    /// are built, and the rest of the document's height is a spacer — which is the difference between
    /// a file that opens and a file that hangs the frame. Zero height = build everything, which is
    /// right for a snippet and for the first frame, before layout has told anyone how tall it is.
    /// </summary>
    public float ViewportOffset { get; init; }
    public float ViewportHeight { get; init; }

    /// <summary>
    /// How wide the viewport turned out to be. The content is never NARROWER than this, so a click
    /// in the empty space to the right of a short line still lands on that line — and never wider
    /// than it needs to be, so a long line scrolls instead of stretching the pane it sits in.
    /// Zero = as wide as the code, which is right for the first frame and for a snippet.
    /// </summary>
    public float ViewportWidth { get; init; }

    /// <summary>Reported when the viewport moves or resizes — an editor feeds these back in.</summary>
    public Action<float>? OnScrolled { get; init; }
    public Action<float>? OnViewportChanged { get; init; }

    /// <summary>
    /// Where the code SITS, in dp. The editable twin needs exactly these numbers to place a caret,
    /// and two independent calculations of them would drift by a pixel and then by a character —
    /// so there is one, and both surfaces read it.
    /// </summary>
    public readonly record struct CodeMetrics(
        TypeStyle Style, float LineHeight, float ColumnWidth, float GutterWidth)
    {
        /// <summary>
        /// Where column 0 begins, measured from the CODE's own box — the code's left padding, and
        /// nothing else.
        /// <para>
        /// It used to include the gutter, because the gutter used to sit inside the thing that
        /// scrolls. That is what made a pinned gutter impossible: scrolling sideways slid real code
        /// under the numbers, and an opaque column hid it. The gutter is a sibling of the code now,
        /// outside the scroll, so every column sum starts where the code starts — the caret, the
        /// selection and every decoration at once, which is why this is one property and not three.
        /// </para>
        /// </summary>
        public float ContentLeft => Space.S3;

        /// <summary>Where line 0 begins: the slab's vertical padding.</summary>
        public float ContentTop => Space.S3;
    }

    /// <summary>The metrics for a block of these settings, measured through the context.</summary>
    public static CodeMetrics MetricsFor(ComponentContext context, SizeVariant size,
        bool showLineNumbers, int lastLineNumber)
    {
        var style = TypeStyle.OfSize(Sizing.LabelSize(size, context.Density),
            FontWeight.Regular) with { Mono = true };
        // The gutter is as wide as its widest number — measured, not guessed, because a file with
        // 1000 lines needs a column a file with 10 does not.
        var gutter = showLineNumbers
            ? MathF.Ceiling(context.MeasureText(lastLineNumber.ToString() + "0", style)) + Space.S3
            : 0;
        return new CodeMetrics(style, MathF.Round(style.LineHeight * 1.15f),
            context.MonoAdvance(style), gutter);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var highlighter = Highlighter ?? new CodeHighlighter(Language);
        // The EDITOR's numbers when it has them: it already measured to place a caret, and a caret
        // measured against one grid over lines drawn on another is a caret that drifts down the
        // file. One measurement, one grid, whatever the two contexts happen to say.
        var metrics = Metrics ?? MetricsFor(context, Size, ShowLineNumbers,
            FirstLineNumber + Document.LineCount - 1);
        var lineHeight = metrics.LineHeight;

        var ink = Inverse ? CodeInk : theme.TextPrimary;
        var surface = Inverse ? CodeSlab : theme.SurfaceSubtle;

        // The WINDOW: the rows the viewport can show, plus a margin either side so a scroll of one
        // row does not have to build anything. Above and below it, one spacer each, so the content
        // is as tall as the file and the scrollbar tells the truth. A row is a line until Rows says
        // otherwise, and the lines the window holds are the ones anything is marked on.
        var (first, last) = Window(lineHeight);
        var (firstLine, lastLine) = LinesIn(first, last);

        // The viewport's width, and NEVER less than the longest line's.
        //
        // Filling alone was why the sideways scroll never scrolled: a scroll view whose content is
        // exactly its own size has nothing to move, so long lines were simply cut off, and a click
        // past the cut sent the caret somewhere the reader could not see. Sizing to the code alone
        // is the opposite mistake — a short file would end where its longest line ends, and a click
        // in the empty space to the right of it would land on nothing at all.
        //
        // Measured from the widest line in the FILE rather than the widest one on screen: the width
        // must not change as the window scrolls, or the content would breathe under the reader.
        var widest = 0;
        if (WidestLine is { } known) widest = known;
        else
        {
            for (var index = 0; index < Document.LineCount; index++)
                widest = Math.Max(widest, CodeLineCells.WidthOf(Document.Line(index), TabSize));
            // The lines drawn from another document are code on these rows too.
            if (FillerDocument is { } fillers)
            {
                for (var index = 0; index < fillers.LineCount; index++)
                    widest = Math.Max(widest, CodeLineCells.WidthOf(fillers.Line(index), TabSize));
            }
        }
        var codeWidth = widest * metrics.ColumnWidth + metrics.ColumnWidth;

        // The other document's own colouring: a highlighter keeps one document's state, and sharing
        // one between two would re-colour both from the top on every build.
        var fillerHighlighter = FillerDocument is null ? null : FillerHighlighter ?? new CodeHighlighter(Language);
        var lines = new Column(gap: 0) { Width = SizeValue.Fill };
        if (first > 0) lines.Add(Spacer.Fixed(first * lineHeight));
        for (var row = first; row <= last; row++)
            lines.Add(RowView(RowAt(row), highlighter, fillerHighlighter, metrics, ink, theme));
        if (last < RowCount - 1)
            lines.Add(Spacer.Fixed((RowCount - 1 - last) * lineHeight));

        // The slab's own padding above the first line and below the last, on the LINES rather than on
        // the block: the mark layer beside them starts at the surface's corner, so every rectangle
        // the engine answers — in the surface's own coordinates — lands where it says.
        VisualNode content = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(0, Space.S3),
        }, lines);

        var width = MathF.Max(codeWidth, ViewportWidth);

        // The MARK LAYER, under the lines. Everything that marks the code without being it lives
        // here, in one paint order, so it is the same order on every target: the active line's wash,
        // then the matches, then the selection, then the outlines and the lines under the text. The
        // text is painted after all of it and reads over it; only the caret, which must blink, is
        // left to the realizer, which paints it on top.
        var marks = new Stack { Width = SizeValue.Fill };
        // A changed line's wash is under everything else, so the words that changed, the caret's
        // line and the selection all read over it.
        AddMarks(marks, LinePass, metrics, theme, first, last, firstLine, lastLine, width);
        if (ActiveLine is { } activeLine && activeLine >= 0 && activeLine < Document.LineCount && Shows(activeLine))
        {
            marks.Add(new Positioned(new Box(new BoxStyle
            {
                Width = width,
                Height = lineHeight,
                Background = Inverse ? CodeSlabActive : theme.Colors(Variant.Primary).Subtle,
            }), top: metrics.ContentTop + RowOf(activeLine) * lineHeight, start: 0));
        }
        AddMarks(marks, HighlightPass, metrics, theme, first, last, firstLine, lastLine, width);
        if (SelectionBands.Count > 0)
        {
            // …and the selection's bands, one per line, drawn for the lines in the window only: a
            // select-all over a long file was a band per line of it, each build.
            var band = SelectionFor(Inverse, theme).WithOpacity(SelectionAlpha);
            var windowTop = metrics.ContentTop + first * lineHeight;
            var windowBottom = metrics.ContentTop + (last + 1) * lineHeight;
            foreach (var rect in SelectionBands)
            {
                if (rect.Y + rect.Height <= windowTop || rect.Y >= windowBottom) continue;
                marks.Add(new Positioned(new Box(new BoxStyle
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Background = band,
                    CornerRadius = new CornerRadii(1),
                }), top: rect.Y, start: rect.X));
            }
        }
        AddMarks(marks, OutlinePass, metrics, theme, first, last, firstLine, lastLine, width);
        if (marks.Children.Count > 0)
        {
            var layered = new Stack { Width = SizeValue.Fill };
            layered.Add(marks);
            layered.Add(content);
            content = layered;
        }

        // A NUMBER, not a Fill: the two targets disagree about what filling means inside a sideways
        // scroll view. A page resolves 100% against the scroller; Photon measures the content
        // unbounded on the scroll axis, which is the point of a scroll view — so a Fill there has
        // nothing to fill and collapses to the code. Taking the width the viewport REPORTED settles
        // it in one arithmetic both realizers already agree on.
        VisualNode body = new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(width),
        }, content);

        // Bare CONTENT, exactly as wide as the code: no slab, no viewport, no corner. An editor
        // frames and scrolls this from outside, and a block that framed itself here would paint a
        // second slab inside the first and clip the lines the scroll was there to reach.
        if (!Standalone) return body;

        // Long lines scroll sideways rather than wrapping: a wrapped line of code has lost the one
        // thing its indentation was telling you.
        body = new ScrollView(body, ScrollAxis.Horizontal) { Width = SizeValue.Fill };

        // THE GUTTER IS A SIBLING of that scroll, never a passenger in it. Inside, the numbers slid
        // away with the code and a reader lost the number of the line they were reading; overlaid on
        // top, the code slid UNDER them and the opaque column hid it. Beside it, both stay true and
        // neither has to compensate for the other.
        if (ShowLineNumbers)
        {
            var withGutter = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
            withGutter.Add(Gutter(context));
            withGutter.Add(new Flexible(body));
            body = withGutter;
        }
        if (MaxHeight > 0)
        {
            body = new Box(new BoxStyle { Width = SizeValue.Fill, MaxHeight = MaxHeight },
                new ScrollView(body)
                {
                    Width = SizeValue.Fill,
                    OnScrolled = OnScrolled,
                    OnViewportChanged = OnViewportChanged,
                });
        }

        var slab = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            Clip = true,
        }, body);

        if (Corner(Caption, OnCopy, Inverse, theme) is not { } corner) return slab;

        var layers = new Stack { Width = SizeValue.Fill };
        layers.Add(slab);
        layers.Add(corner);
        return layers;
    }

    /// <summary>
    /// The caption and the copy button, as a layer over the slab: ABOVE the code, in the TRAILING
    /// corner, over the ragged right edge of code rather than over its first line, which always has
    /// text in it. Null when there is nothing to put there. The editor draws its caption with it too.
    /// <para>
    /// The layer is as wide as what it holds. It was a row as wide as the slab with a spacer pushing
    /// the two to the end, which drew the same and lay over the whole first line: on the web a press
    /// there landed on the row, so the text under it could not be selected, and in an editor the
    /// first line could not be clicked into.
    /// </para>
    /// </summary>
    public static VisualNode? Corner(string? caption, Action? onCopy, bool inverse, IAppTheme theme)
    {
        if (caption is null && onCopy is null) return null;

        var corner = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        if (caption is { } text)
        {
            corner.Add(new Text(text, TypeRole.LabelSmall,
                inverse ? CodeInkMuted : theme.TextMuted, maxLines: 1) { Mono = true });
        }
        if (onCopy is { } copy)
        {
            corner.Add(new IconButton(new Icon(Icons.Copy), SdkStrings.CopyCode)
            {
                Size = SizeVariant.Small,
                OnPressed = copy,
            });
        }

        return new Positioned(new Box(new BoxStyle
        {
            Padding = EdgeInsets.Symmetric(Space.S3, Space.S2),
        }, corner), top: 0, end: 0);
    }

    /// <summary>
    /// The line numbers, as a column of their OWN — beside the code, outside whatever scrolls it.
    /// <para>
    /// It belongs INSIDE whatever scrolls the file down and OUTSIDE whatever scrolls it across, and
    /// that nesting is the entire feature: the numbers travel down with the code and stay put as it
    /// slides across. Two shapes were tried before this one and both were wrong in the same way —
    /// inside the sideways scroll the numbers left with the code, and overlaid on top of it the code
    /// slid UNDER them and an opaque column hid real characters.
    /// </para>
    /// <para>
    /// Built over the same window and the same spacers as the code, so the two columns stay
    /// row-for-row together however far the file is scrolled. <paramref name="numberOf"/> says what a
    /// row's number is, when it is not its line's counted from <see cref="FirstLineNumber"/>: a patch
    /// numbers its lines as the file does, and an inline diff's second column numbers the lines of
    /// the other document. Null, for a row that shows none.
    /// </para>
    /// </summary>
    public VisualNode Gutter(ComponentContext context, Func<CodeRow, string?>? numberOf = null)
    {
        var theme = context.Theme;
        var metrics = Metrics ?? MetricsFor(context, Size, ShowLineNumbers,
            FirstLineNumber + Document.LineCount - 1);
        var lineHeight = metrics.LineHeight;
        var (first, last) = Window(lineHeight);

        var column = new Column(gap: 0) { Width = SizeValue.Fixed(metrics.GutterWidth) };
        // The code's own top padding, so line 0 starts at the same height in both columns.
        column.Add(Spacer.Fixed(Space.S3));
        if (first > 0) column.Add(Spacer.Fixed(first * lineHeight));
        for (var row = first; row <= last; row++)
            column.Add(GutterCell(RowAt(row), numberOf, metrics, theme));
        if (last < RowCount - 1)
            column.Add(Spacer.Fixed((RowCount - 1 - last) * lineHeight));
        return column;
    }

    private VisualNode GutterCell(CodeRow shown, Func<CodeRow, string?>? numberOf, CodeMetrics metrics, IAppTheme theme)
    {
        var isLine = shown.Kind == CodeRowKind.Line;
        var index = shown.Line;
        var label = numberOf is { } number ? number(shown) : isLine ? (FirstLineNumber + index).ToString() : null;
        var numbers = new Row(gap: Space.S1)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Main = MainAlign.End,
            Cross = CrossAlign.Center,
        };
        if (isLine && MarkerFor(index) is { } mark)
        {
            numbers.Add(new Box(new BoxStyle
            {
                Width = 7,
                Height = 7,
                Background = GutterColor(mark.Kind, theme),
                CornerRadius = new CornerRadii(Radius.Full),
            }));
        }
        if (label is { } text)
        {
            numbers.Add(new Text(text, TypeRole.LabelSmall,
                Inverse ? CodeInkMuted : theme.TextMuted, maxLines: 1)
            {
                Mono = true,
                Tabular = true,
                StyleOverride = metrics.Style with { Weight = FontWeight.Regular },
            });
        }

        // The active line's wash crosses the gutter too: a "you are here" stripe that stops at the
        // first digit reads as a rendering fault rather than as an answer.
        var cell = new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(metrics.GutterWidth),
            Height = SizeValue.Fixed(metrics.LineHeight),
            // The GAP between the numbers and the code belongs to the gutter, which does not move.
            // Leaving it to the code's own left padding worked only until you scrolled: that padding
            // is inside the thing that scrolls, so it slid away and the digits ended up touching the
            // first character of every line.
            Padding = new EdgeInsets(0, 0, Space.S3, 0),
            // A row that shows no line carries the wash its code does, so the stripe crosses both.
            Background = !isLine ? FillerColor
                : ActiveLine == index ? (Inverse ? CodeSlabActive : theme.Colors(Variant.Primary).Subtle)
                : null,
        }, numbers);

        return isLine && OnGutterPressed is { } pressed
            ? new Pressable(cell, () => pressed(index)) { Label = SdkStrings.LineNumbered(FirstLineNumber + index) }
            : cell;
    }

    /// <summary>What one row draws: a line of the document, a line of the other one, a label, a folded
    /// run, or nothing.</summary>
    private VisualNode RowView(CodeRow shown, CodeHighlighter highlighter, CodeHighlighter? fillerHighlighter,
        CodeMetrics metrics, ColorToken ink, IAppTheme theme)
    {
        if (shown.Kind == CodeRowKind.Line)
            return LineRow(Document, highlighter, CellsOf(shown.Line), shown.Line, metrics, ink, theme);
        if (shown.Kind == CodeRowKind.Placeholder) return PlaceholderRow(shown, metrics, theme);
        if (shown.SourceLine >= 0 && FillerDocument is { } fillers && fillerHighlighter is { } colours
            && shown.SourceLine < fillers.LineCount)
            return LineRow(fillers, colours, FillerCellsOf(shown.SourceLine), shown.SourceLine, metrics, ink, theme);
        return FillerRow(shown, metrics, theme);
    }

    /// <summary>A row that belongs to no line: padding, or what a label says is not there. Its wash is
    /// its own, since nothing is marked under it.</summary>
    private VisualNode FillerRow(CodeRow shown, CodeMetrics metrics, IAppTheme theme)
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Height = metrics.LineHeight, Cross = CrossAlign.Center };
        if (shown.Label is { } label)
            row.Add(new Box(new BoxStyle { Padding = EdgeInsets.Symmetric(Space.S3, 0) }, Muted(label, metrics, theme)));
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = metrics.LineHeight,
            Background = FillerColor,
        }, row);
    }

    /// <summary>A folded run, as one row that says what it hides and opens on a press.</summary>
    private VisualNode PlaceholderRow(CodeRow shown, CodeMetrics metrics, IAppTheme theme)
    {
        // What it hides, in words, when the view that folded it said nothing: the row is a control,
        // and it was named with a mark that assistive tech cannot announce (a bare "⋯").
        var label = shown.Label ?? SdkStrings.HiddenLines(shown.Count);
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Height = metrics.LineHeight, Cross = CrossAlign.Center };
        row.Add(new Box(new BoxStyle { Padding = EdgeInsets.Symmetric(Space.S3, 0) },
            Muted(label, metrics, theme)));
        VisualNode box = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = metrics.LineHeight,
            Background = FillerColor,
        }, row);
        return OnPlaceholderPressed is { } pressed
            ? new Pressable(box, () => pressed(shown.Line)) { Label = label }
            : box;
    }

    private VisualNode Muted(string text, CodeMetrics metrics, IAppTheme theme) =>
        new Text(text, TypeRole.LabelSmall, Inverse ? CodeInkMuted : theme.TextMuted, maxLines: 1)
        {
            Mono = true,
            StyleOverride = metrics.Style,
        };

    private VisualNode LineRow(CodeDocument document, CodeHighlighter highlighter, CodeLineCells cells, int index,
        CodeMetrics metrics, ColorToken ink, IAppTheme theme)
    {
        var style = metrics.Style;
        var lineHeight = metrics.LineHeight;
        var columnWidth = metrics.ColumnWidth;
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Height = lineHeight, Cross = CrossAlign.Center };

        // No numbers here: the gutter is a sibling of this whole column now (see Gutter), so a row
        // is CODE, and column zero is where the row begins.
        var code = new Row(gap: 0) { Height = SizeValue.Fill, Cross = CrossAlign.Center };
        var text = document.Line(index);
        var tokens = highlighter.TokensFor(document, index);
        var at = 0;
        foreach (var token in tokens)
        {
            // A token is only a colour, and the TEXT says what is drawn: one that outlived its text
            // or overlaps the one before is cut to what is left of the line, so no character is
            // drawn twice and none past the end of its line.
            var start = Math.Clamp(token.Start, at, text.Length);
            var end = Math.Clamp(token.End, start, text.Length);
            if (start > at) AddSpan(code, cells, at, start, ink, style, columnWidth);
            AddSpan(code, cells, start, end,
                Inverse ? InverseCode(token.Kind, theme) : theme.Code(token.Kind), style, columnWidth);
            at = end;
        }
        if (at < text.Length) AddSpan(code, cells, at, text.Length, ink, style, columnWidth);
        // An empty line still needs its height, and a space is the cheapest way to say so.
        if (text.Length == 0) code.Add(Run(" ", ink, style));

        row.Add(new Box(new BoxStyle { Padding = EdgeInsets.Symmetric(Space.S3, 0) }, code));

        // No wash here, not even for the active line: a row's background would paint OVER every mark
        // under it — the selection on the line you are on first of all. The active line's wash is the
        // first thing in the mark layer instead (see Build). On the INVERSE slab the theme's
        // light-mode tokens are near-white, which is why the slab has its own pair for it.
        return row;
    }

    /// <summary>The cells of the lines this build reads, by line: the line it draws and every mark on
    /// it read the same map, built once. A block is built anew with each build of what holds it.</summary>
    private readonly Dictionary<int, CodeLineCells> _cells = new();

    private CodeLineCells CellsOf(int line)
    {
        if (_cells.TryGetValue(line, out var cells)) return cells;
        cells = new CodeLineCells(Document.Line(line), TabSize);
        _cells[line] = cells;
        return cells;
    }

    /// <summary>The cells of the other document's lines this build draws, as <see cref="CellsOf"/>.</summary>
    private readonly Dictionary<int, CodeLineCells> _fillerCells = new();

    private CodeLineCells FillerCellsOf(int line)
    {
        if (_fillerCells.TryGetValue(line, out var cells)) return cells;
        cells = new CodeLineCells(FillerDocument!.Line(line), TabSize);
        _fillerCells[line] = cells;
        return cells;
    }

    /// <summary>How many rows the block draws: a row per line, until <see cref="Rows"/> says otherwise.</summary>
    private int RowCount => Rows?.RowCount ?? Document.LineCount;

    private CodeRow RowAt(int row) => Rows is { } rows ? rows.RowAt(row) : new CodeRow(CodeRowKind.Line, row);

    private int RowOf(int line) => Rows?.RowOf(line) ?? line;

    /// <summary>Whether <paramref name="line"/> has a row of its own, which a folded one has not.</summary>
    private bool Shows(int line) => Rows?.IsVisible(line) ?? true;

    /// <summary>The first and last line of the document the rows from <paramref name="first"/> to
    /// <paramref name="last"/> hold: the lines anything is marked on.</summary>
    private (int First, int Last) LinesIn(int first, int last)
    {
        if (Rows is not { } rows) return (first, last);
        if (last < first) return (0, -1);
        return (rows.LineAtRow(first), rows.LineAtRow(last));
    }

    /// <summary>The first and last row this block builds (see <see cref="WindowOf"/>).</summary>
    private (int First, int Last) Window(float lineHeight) =>
        WindowOf(RowCount, lineHeight, ViewportOffset, ViewportHeight);

    /// <summary>
    /// The first and last line to BUILD, of <paramref name="lineCount"/> lines scrolled
    /// <paramref name="offset"/> into a viewport <paramref name="viewportHeight"/> tall. With no
    /// viewport reported yet the answer is "all of them", which is right for a snippet and for the
    /// first frame — the window narrows as soon as layout has said how tall the box turned out to be.
    /// An editor asks it too, for the only lines it measures anything on: the selection's bands and
    /// the matches in view.
    /// </summary>
    internal static (int First, int Last) WindowOf(int lineCount, float lineHeight, float offset, float viewportHeight)
    {
        if (viewportHeight <= 0 || lineHeight <= 0) return (0, lineCount - 1);
        const int margin = 8;   // a scroll of one line builds nothing
        var first = Math.Max(0, (int)MathF.Floor(offset / lineHeight) - margin);
        var visible = (int)MathF.Ceiling(viewportHeight / lineHeight) + margin * 2;
        return (first, Math.Min(lineCount - 1, first + visible));
    }

    /// <summary>The passes of the mark layer, bottom first: a changed line's wash, then the washes of
    /// ranges (matches, a diff's words), then the outlines and the lines under and through the text.
    /// The caret's line lies between the first two, and the selection between the last two.</summary>
    private const int LinePass = 0;
    private const int HighlightPass = 1;
    private const int OutlinePass = 2;

    private static int PassOf(CodeDecorationKind kind) => kind switch
    {
        CodeDecorationKind.Line => LinePass,
        CodeDecorationKind.Highlight => HighlightPass,
        _ => OutlinePass,
    };

    /// <summary>
    /// One pass of the mark layer: the decorations of <paramref name="pass"/> on the lines the window
    /// holds, then the filler decorations of the same pass on the rows the window draws the other
    /// document's lines on.
    /// </summary>
    private void AddMarks(Stack marks, int pass, CodeMetrics metrics, IAppTheme theme, int first, int last,
        int firstLine, int lastLine, float width)
    {
        foreach (var decoration in Decorations)
        {
            if (PassOf(decoration.Kind) != pass) continue;
            foreach (var mark in Marks(decoration, Document, line => CellsOf(line),
                         line => Shows(line) ? RowOf(line) : -1, firstLine, lastLine, metrics, theme, width))
                marks.Add(mark);
        }
        if (FillerDocument is not { } fillers || FillerDecorations.Count == 0) return;

        // The other document's lines this window draws, and the row each is drawn on.
        var sources = new List<int>();
        var rows = new List<int>();
        var lowest = int.MaxValue;
        var highest = -1;
        for (var row = first; row <= last; row++)
        {
            var shown = RowAt(row);
            if (shown.Kind != CodeRowKind.Filler || shown.SourceLine < 0) continue;
            sources.Add(shown.SourceLine);
            rows.Add(row);
            lowest = Math.Min(lowest, shown.SourceLine);
            highest = Math.Max(highest, shown.SourceLine);
        }
        if (sources.Count == 0) return;
        foreach (var decoration in FillerDecorations)
        {
            if (PassOf(decoration.Kind) != pass) continue;
            foreach (var mark in Marks(decoration, fillers, line => FillerCellsOf(line),
                         line => RowOfSource(sources, rows, line), lowest, highest, metrics, theme, width))
                marks.Add(mark);
        }
    }

    /// <summary>The row the window draws the other document's <paramref name="line"/> on, or -1.</summary>
    private static int RowOfSource(List<int> sources, List<int> rows, int line)
    {
        for (var i = 0; i < sources.Count; i++)
            if (sources[i] == line) return rows[i];
        return -1;
    }

    /// <summary>
    /// One decoration as positioned rectangles — one per line it spans, like the selection band, and
    /// for the same reason: a single rectangle over a multi-line range would cover the indentation
    /// of lines the range never touched. Each line is drawn on the row <paramref name="rowOf"/> says,
    /// and not at all when it says -1 (a line a fold hides).
    /// </summary>
    private IEnumerable<VisualNode> Marks(CodeDecoration decoration, CodeDocument document,
        Func<int, CodeLineCells> cellsOf, Func<int, int> rowOf, int first, int last, CodeMetrics metrics,
        IAppTheme theme, float rowWidth)
    {
        var start = document.Clamp(decoration.Range.Start);
        var end = document.Clamp(decoration.Range.End);
        var color = decoration.Color ?? DefaultColor(decoration.Kind, theme);
        if (Inverse) color = new ColorToken(color.Dark, color.Dark);
        var whole = decoration.Kind == CodeDecorationKind.Line;
        // A line wash takes every line the range touches, and not a last one it only reaches at its
        // first column: a range of whole lines ends where the next one starts.
        var lastTouched = whole && end.Line > start.Line && end.Column == 0 ? end.Line - 1 : end.Line;

        // Only the lines the window builds: a mark on a line nobody can see is a box and a map of
        // its line for nothing, and a search over a long file marked every line of it each build.
        for (var line = Math.Max(start.Line, first); line <= Math.Min(lastTouched, last); line++)
        {
            var row = rowOf(line);
            if (row < 0) continue;
            var top = metrics.ContentTop + row * metrics.LineHeight;
            if (whole)
            {
                // The whole row, as wide as the code: a line a diff added or removed.
                yield return new Positioned(new Box(new BoxStyle
                {
                    Width = rowWidth, Height = metrics.LineHeight, Background = color,
                }), top: top, start: 0);
                continue;
            }

            var from = line == start.Line ? start.Column : 0;
            var to = line == end.Line ? end.Column : document.Line(line).Length;
            if (to <= from) continue;

            // Through the line's CELLS, the way the engine places its caret: a match after a tab
            // or across a wide character is drawn where the characters are.
            var cells = cellsOf(line);
            var left = metrics.ContentLeft + cells.CellOf(from) * metrics.ColumnWidth;
            var width = (cells.CellOf(to) - cells.CellOf(from)) * metrics.ColumnWidth;

            yield return decoration.Kind switch
            {
                // A box AROUND the range: what a matching bracket wears, because a wash would hide
                // the character the box is pointing at.
                CodeDecorationKind.Outline => new Positioned(new Box(new BoxStyle
                {
                    Width = width, Height = metrics.LineHeight,
                    BorderWidth = 1, BorderColor = color,
                    CornerRadius = new CornerRadii(2),
                }), top: top, start: left),

                // A line UNDER the range — a diagnostic. (Wavy needs a shader; a 2dp rule reads the
                // same at this size and costs nothing.)
                CodeDecorationKind.Squiggle => new Positioned(new Box(new BoxStyle
                {
                    Width = width, Height = 2, Background = color,
                }), top: top + metrics.LineHeight - 2, start: left),

                // A line THROUGH it — deleted in a diff, unreachable code.
                CodeDecorationKind.Strike => new Positioned(new Box(new BoxStyle
                {
                    Width = width, Height = 1, Background = color,
                }), top: top + metrics.LineHeight / 2, start: left),

                // A thin line UNDER it in the code's own ink — text an input method is composing.
                CodeDecorationKind.Underline => new Positioned(new Box(new BoxStyle
                {
                    Width = width, Height = 1, Background = color,
                }), top: top + metrics.LineHeight - 2, start: left),

                _ => new Positioned(new Box(new BoxStyle
                {
                    Width = width, Height = metrics.LineHeight, Background = color,
                    CornerRadius = new CornerRadii(2),
                }), top: top, start: left),
            };
        }
    }

    private ColorToken DefaultColor(CodeDecorationKind kind, IAppTheme theme) => kind switch
    {
        CodeDecorationKind.Squiggle => theme.Colors(Variant.Destructive).Base,
        CodeDecorationKind.Outline => theme.BorderStrong,
        CodeDecorationKind.Strike => theme.TextMuted,
        CodeDecorationKind.Underline => InkFor(Inverse, theme),
        CodeDecorationKind.Line => theme.Colors(Variant.Primary).Subtle,
        _ => theme.Colors(Variant.Warning).Subtle,
    };

    /// <summary>
    /// The columns from <paramref name="from"/> up to <paramref name="to"/>, drawn on the cells the
    /// engine counts (<see cref="CodeLineCells"/>): a tab as the spaces up to its stop, and a wide
    /// element in a box two cells wide, so a fallback font's advance cannot move the rest of the line
    /// off the grid the caret is placed on. Everything else goes out as one run. An element split by
    /// a token boundary is drawn with the span it BEGINS in.
    /// </summary>
    private static void AddSpan(Row code, CodeLineCells cells, int from, int to, ColorToken color,
        TypeStyle style, float columnWidth)
    {
        if (to <= from) return;
        var run = "";
        for (var i = cells.IndexOf(from); i < cells.Count; i++)
        {
            var element = cells.ElementAt(i);
            if (element.Start >= to) break;
            if (element.Start < from) continue;
            var text = cells.Text.Substring(element.Start, element.End - element.Start);
            if (text == "\t")
            {
                for (var space = 0; space < element.Width; space++) run += " ";
            }
            else if (element.Width == 2)
            {
                if (run.Length > 0) code.Add(Run(run, color, style));
                run = "";
                code.Add(new Box(new BoxStyle { Width = 2 * columnWidth }, Run(text, color, style)));
            }
            else run += text;
        }
        if (run.Length > 0) code.Add(Run(run, color, style));
    }

    private static VisualNode Run(string content, ColorToken color, TypeStyle style) =>
        new Text(content, TypeRole.LabelSmall, color, maxLines: 1)
        {
            Mono = true,
            StyleOverride = style,
        };

    private CodeGutterMarker? MarkerFor(int line)
    {
        foreach (var marker in GutterMarkers)
            if (marker.Line == line) return marker;
        return null;
    }

    private ColorToken GutterColor(CodeGutterKind kind, IAppTheme theme)
    {
        var token = GutterToken(kind, theme);
        // The DARK half in both modes on the inverse slab, for the same reason the tokens use it.
        return Inverse ? new ColorToken(token.Dark, token.Dark) : token;
    }

    private static ColorToken GutterToken(CodeGutterKind kind, IAppTheme theme) => kind switch
    {
        CodeGutterKind.Breakpoint => theme.Colors(Variant.Destructive).Base,
        CodeGutterKind.BreakpointDisabled => theme.BorderStrong,
        CodeGutterKind.Error => theme.Colors(Variant.Destructive).Base,
        CodeGutterKind.Warning => theme.Colors(Variant.Warning).Base,
        CodeGutterKind.Added => theme.Colors(Variant.Success).Base,
        CodeGutterKind.Modified => theme.Colors(Variant.Info).Base,
        CodeGutterKind.Removed => theme.Colors(Variant.Destructive).Subtle,
        _ => theme.Colors(Variant.Primary).Base,
    };

    /// <summary>
    /// The slab and its ink are FIXED colours, not tokens: a code figure in documentation reads the
    /// same in both modes, which is exactly why a design puts one there.
    /// </summary>
    private static readonly ColorToken CodeSlab = new(new Color(0x10, 0x14, 0x18, 0xFF));
    private static readonly ColorToken CodeInk = new(new Color(0xC9, 0xD4, 0xDE, 0xFF));
    private static readonly ColorToken CodeInkMuted = new(new Color(0x7C, 0x8A, 0x99, 0xFF));

    /// <summary>The slab, one shade lighter — the caret's line.</summary>
    private static readonly ColorToken CodeSlabActive = new(new Color(0x1B, 0x22, 0x2B, 0xFF));

    /// <summary>
    /// The ink this block writes with. The CARET has to be the same ink — on an inverse slab the
    /// theme's text colour is the slab's own darkness, and a caret painted with it is invisible on
    /// exactly the surface people type into. So the editor asks here instead of asking the theme.
    /// </summary>
    public static ColorToken InkFor(bool inverse, IAppTheme theme) =>
        inverse ? CodeInk : theme.TextPrimary;

    /// <summary>The selection band's colour, by the same rule: on the dark slab the DARK half of
    /// the focus ring is used in both modes (see <see cref="InverseCode"/>).</summary>
    public static ColorToken SelectionFor(bool inverse, IAppTheme theme) =>
        inverse ? new ColorToken(theme.FocusRing.Dark, theme.FocusRing.Dark) : theme.FocusRing;

    /// <summary>The slab under the code — asked for by the same editor that asks for the ink, for
    /// the same reason: when the block is bare content the frame is built outside it, and a frame
    /// painted from the page's theme is the wrong colour on exactly the inverse slab.</summary>
    public static ColorToken SurfaceFor(bool inverse, IAppTheme theme) =>
        inverse ? CodeSlab : theme.SurfaceSubtle;

    /// <summary>On the dark slab the theme's light-mode colours would vanish, so the DARK half of
    /// each token is used in both modes.</summary>
    private static ColorToken InverseCode(CodeTokenKind kind, IAppTheme theme)
    {
        var token = theme.Code(kind);
        return new ColorToken(token.Dark, token.Dark);
    }
}
