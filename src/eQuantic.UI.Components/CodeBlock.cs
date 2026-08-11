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
        /// <summary>Where column 0 begins: past the gutter and the code's own left padding.</summary>
        public float ContentLeft => GutterWidth + Space.S3;

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
        var style = metrics.Style;
        var lineHeight = metrics.LineHeight;
        var gutterWidth = metrics.GutterWidth;

        var ink = Inverse ? CodeInk : theme.TextPrimary;
        var surface = Inverse ? CodeSlab : theme.SurfaceSubtle;

        // The WINDOW: the lines the viewport can show, plus a margin either side so a scroll of one
        // line does not have to build anything. Above and below it, one spacer each, so the content
        // is as tall as the file and the scrollbar tells the truth.
        var (first, last) = Window(lineHeight);

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
        for (var index = 0; index < Document.LineCount; index++)
            widest = Math.Max(widest, Document.Line(index).Length);
        var codeWidth = gutterWidth + widest * metrics.ColumnWidth + metrics.ColumnWidth;

        var lines = new Column(gap: 0) { Width = SizeValue.Fill };
        if (first > 0) lines.Add(Spacer.Fixed(first * lineHeight));
        for (var index = first; index <= last; index++)
        {
            lines.Add(LineRow(context, highlighter, index, style, lineHeight, gutterWidth, ink, theme));
        }
        if (last < Document.LineCount - 1)
            lines.Add(Spacer.Fixed((Document.LineCount - 1 - last) * lineHeight));

        // Decorations are RANGES, and a range is a rectangle: the same column arithmetic the caret
        // uses. Drawn UNDER the lines, in one layer, so the code reads through them — and drawn
        // here rather than in a realizer so a read-only block gets them too.
        VisualNode content = lines;
        if (Decorations.Count > 0)
        {
            var decorated = new Stack { Width = SizeValue.Fill };
            var marks = new Stack { Width = SizeValue.Fill };
            foreach (var decoration in Decorations)
            {
                foreach (var mark in Marks(decoration, metrics, theme))
                    marks.Add(mark);
            }
            decorated.Add(marks);
            decorated.Add(lines);
            content = decorated;
        }

        // A NUMBER, not a Fill: the two targets disagree about what filling means inside a sideways
        // scroll view. A page resolves 100% against the scroller; Photon measures the content
        // unbounded on the scroll axis, which is the point of a scroll view — so a Fill there has
        // nothing to fill and collapses to the code. Taking the width the viewport REPORTED settles
        // it in one arithmetic both realizers already agree on.
        VisualNode body = new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(MathF.Max(codeWidth, ViewportWidth)),
            Padding = EdgeInsets.Symmetric(0, Space.S3),
        }, content);

        // Bare CONTENT, exactly as wide as the code: no slab, no viewport, no corner. An editor
        // frames and scrolls this from outside, and a block that framed itself here would paint a
        // second slab inside the first and clip the lines the scroll was there to reach.
        if (!Standalone) return body;

        // Long lines scroll sideways rather than wrapping: a wrapped line of code has lost the one
        // thing its indentation was telling you.
        body = new ScrollView(body, ScrollAxis.Horizontal) { Width = SizeValue.Fill };
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

        if (Caption is null && OnCopy is null) return slab;

        // The caption and the copy button ride ABOVE the code, in the TRAILING corner — over the
        // ragged right edge of code rather than over its first line, which always has text in it.
        var corner = new Row(gap: Space.S2) { Width = SizeValue.Fill, Cross = CrossAlign.Center };
        corner.Add(new Spacer(1));
        if (Caption is { } caption)
        {
            corner.Add(new Text(caption, TypeRole.LabelSmall,
                Inverse ? CodeInkMuted : theme.TextMuted, maxLines: 1) { Mono = true });
        }
        if (OnCopy is { } copy)
        {
            corner.Add(new IconButton(Icons.Copy, "Copy code")
            {
                Size = SizeVariant.Small,
                OnPressed = copy,
            });
        }

        var layers = new Stack { Width = SizeValue.Fill };
        layers.Add(slab);
        layers.Add(new Positioned(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(Space.S3, Space.S2),
        }, corner), top: 0, start: 0));
        return layers;
    }

    private VisualNode LineRow(ComponentContext context, CodeHighlighter highlighter, int index,
        TypeStyle style, float lineHeight, float gutterWidth, ColorToken ink, IAppTheme theme)
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Height = lineHeight, Cross = CrossAlign.Center };

        if (ShowLineNumbers)
        {
            var marker = MarkerFor(index);
            var numbers = new Row(gap: Space.S1)
            {
                Width = SizeValue.Fixed(gutterWidth),
                Height = SizeValue.Fill,
                Main = MainAlign.End,
                Cross = CrossAlign.Center,
            };
            if (marker is { } mark)
            {
                numbers.Add(new Box(new BoxStyle
                {
                    Width = 7,
                    Height = 7,
                    Background = GutterColor(mark.Kind, theme),
                    CornerRadius = new CornerRadii(Radius.Full),
                }));
            }
            numbers.Add(new Text((FirstLineNumber + index).ToString(), TypeRole.LabelSmall,
                Inverse ? CodeInkMuted : theme.TextMuted, maxLines: 1)
            {
                Mono = true,
                Tabular = true,
                StyleOverride = style with { Weight = FontWeight.Regular },
            });

            row.Add(OnGutterPressed is { } pressed
                ? new Pressable(numbers, () => pressed(index)) { Label = $"Line {FirstLineNumber + index}" }
                : numbers);
        }

        var code = new Row(gap: 0) { Height = SizeValue.Fill, Cross = CrossAlign.Center };
        var text = Document.Line(index);
        var tokens = highlighter.TokensFor(Document, index);
        var at = 0;
        foreach (var token in tokens)
        {
            if (token.Start > at) code.Add(Run(text[at..token.Start], ink, style));
            code.Add(Run(text[token.Start..Math.Min(token.End, text.Length)],
                Inverse ? InverseCode(token.Kind, theme) : theme.Code(token.Kind), style));
            at = Math.Min(token.End, text.Length);
        }
        if (at < text.Length) code.Add(Run(text[at..], ink, style));
        // An empty line still needs its height, and a space is the cheapest way to say so.
        if (text.Length == 0) code.Add(Run(" ", ink, style));

        row.Add(new Box(new BoxStyle { Padding = EdgeInsets.Symmetric(Space.S3, 0) }, code));

        // The wash is the ACTIVE line only now: a decoration is a range, drawn column-accurately
        // over the whole block rather than as a full-width stripe on whatever line it starts on.
        var active = ActiveLine == index;
        if (!active) return row;

        // On the INVERSE slab the theme's light-mode tokens are near-white, and a near-white wash
        // over dark code reads as a rendering fault rather than "you are here". The slab has its
        // own pair, one shade off itself.
        var wash = Inverse ? CodeSlabActive : theme.Colors(Variant.Primary).Subtle;
        return new Box(new BoxStyle { Width = SizeValue.Fill, Background = wash }, row);
    }

    /// <summary>
    /// The first and last line to BUILD. With no viewport reported yet the answer is "all of them",
    /// which is right for a snippet and for the first frame — the window narrows as soon as layout
    /// has said how tall the box turned out to be.
    /// </summary>
    private (int First, int Last) Window(float lineHeight)
    {
        if (ViewportHeight <= 0 || lineHeight <= 0) return (0, Document.LineCount - 1);
        const int margin = 8;   // a scroll of one line builds nothing
        var first = Math.Max(0, (int)MathF.Floor(ViewportOffset / lineHeight) - margin);
        var visible = (int)MathF.Ceiling(ViewportHeight / lineHeight) + margin * 2;
        return (first, Math.Min(Document.LineCount - 1, first + visible));
    }

    /// <summary>
    /// One decoration as positioned rectangles — one per line it spans, like the selection band, and
    /// for the same reason: a single rectangle over a multi-line range would cover the indentation
    /// of lines the range never touched.
    /// </summary>
    private IEnumerable<VisualNode> Marks(CodeDecoration decoration, CodeMetrics metrics, IAppTheme theme)
    {
        var start = Document.Clamp(decoration.Range.Start);
        var end = Document.Clamp(decoration.Range.End);
        var color = decoration.Color ?? DefaultColor(decoration.Kind, theme);
        if (Inverse) color = new ColorToken(color.Dark, color.Dark);

        for (var line = start.Line; line <= end.Line; line++)
        {
            var from = line == start.Line ? start.Column : 0;
            var to = line == end.Line ? end.Column : Document.Line(line).Length;
            if (to <= from) continue;

            var left = metrics.ContentLeft + from * metrics.ColumnWidth;
            var top = line * metrics.LineHeight;
            var width = (to - from) * metrics.ColumnWidth;

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

                _ => new Positioned(new Box(new BoxStyle
                {
                    Width = width, Height = metrics.LineHeight, Background = color,
                    CornerRadius = new CornerRadii(2),
                }), top: top, start: left),
            };
        }
    }

    private static ColorToken DefaultColor(CodeDecorationKind kind, IAppTheme theme) => kind switch
    {
        CodeDecorationKind.Squiggle => theme.Colors(Variant.Destructive).Base,
        CodeDecorationKind.Outline => theme.BorderStrong,
        CodeDecorationKind.Strike => theme.TextMuted,
        _ => theme.Colors(Variant.Warning).Subtle,
    };

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
