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

    public CodeBlock(CodeDocument document, ICodeLanguage language)
    {
        Document = document;
        Language = language;
    }

    public CodeDocument Document { get; init; }
    public ICodeLanguage Language { get; init; }

    /// <summary>The numbers down the left. Off for a snippet in a paragraph, on for a file.</summary>
    public bool ShowLineNumbers { get; init; } = true;

    /// <summary>The number the first line carries — a fragment quoted from line 120 says 120.</summary>
    public int FirstLineNumber { get; init; } = 1;

    /// <summary>Caps the height and scrolls past it; 0 = as tall as the code.</summary>
    public float MaxHeight { get; init; }

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

    /// <summary>Reused across frames by an editor, so colouring stays incremental. Null = the
    /// block makes its own, which is right for a snippet that never changes.</summary>
    public CodeHighlighter? Highlighter { get; init; }

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
        var metrics = MetricsFor(context, Size, ShowLineNumbers,
            FirstLineNumber + Document.LineCount - 1);
        var style = metrics.Style;
        var lineHeight = metrics.LineHeight;
        var gutterWidth = metrics.GutterWidth;

        var ink = Inverse ? CodeInk : theme.TextPrimary;
        var surface = Inverse ? CodeSlab : theme.SurfaceSubtle;

        var lines = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var index = 0; index < Document.LineCount; index++)
        {
            lines.Add(LineRow(context, highlighter, index, style, lineHeight, gutterWidth, ink, theme));
        }

        VisualNode body = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(0, Space.S3),
        }, lines);

        // Long lines scroll sideways rather than wrapping: a wrapped line of code has lost the one
        // thing its indentation was telling you.
        body = new ScrollView(body, ScrollAxis.Horizontal) { Width = SizeValue.Fill };
        if (MaxHeight > 0)
        {
            body = new Box(new BoxStyle { Width = SizeValue.Fill, MaxHeight = MaxHeight },
                new ScrollView(body) { Width = SizeValue.Fill });
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

        var active = ActiveLine == index;
        var decoration = DecorationFor(index);
        if (!active && decoration is null) return row;

        // On the INVERSE slab the theme's light-mode tokens are near-white, and a near-white wash
        // over dark code reads as a rendering fault rather than "you are here". The slab has its
        // own pair, one shade off itself.
        var wash = decoration?.Color is { } marked
            ? (Inverse ? new ColorToken(marked.Dark, marked.Dark) : marked)
            : Inverse
                ? (active ? CodeSlabActive : CodeSlabMarked)
                : (active ? theme.Colors(Variant.Primary).Subtle : theme.SurfaceHighlight);

        return new Box(new BoxStyle { Width = SizeValue.Fill, Background = wash }, row);
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

    private CodeDecoration? DecorationFor(int line)
    {
        foreach (var decoration in Decorations)
            if (decoration.Range.Start.Line <= line && decoration.Range.End.Line >= line)
                return decoration;
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

    /// <summary>The slab, one shade lighter — the caret's line, and a marked one.</summary>
    private static readonly ColorToken CodeSlabActive = new(new Color(0x1B, 0x22, 0x2B, 0xFF));
    private static readonly ColorToken CodeSlabMarked = new(new Color(0x16, 0x1C, 0x24, 0xFF));

    /// <summary>On the dark slab the theme's light-mode colours would vanish, so the DARK half of
    /// each token is used in both modes.</summary>
    private static ColorToken InverseCode(CodeTokenKind kind, IAppTheme theme)
    {
        var token = theme.Code(kind);
        return new ColorToken(token.Dark, token.Dark);
    }
}
