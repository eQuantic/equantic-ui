using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// How <see cref="Markdown"/> maps document roles onto the app's type scale — the
/// MarkdownStyleSheet idea, sized to what actually varies. Every default reads from the theme the
/// app already selected, so "basic styling" and "styled by the design system" are the same thing:
/// the neutral look IS the default theme, and <c>MaterialTheme.FromSeed(…)</c> rebrands a rendered
/// document with nothing else changed.
/// </summary>
public sealed class MarkdownStyle
{
    public TypeRole Heading1 { get; init; } = TypeRole.Heading;
    public TypeRole Heading2 { get; init; } = TypeRole.Title;
    public TypeRole Heading3 { get; init; } = TypeRole.TitleSmall;
    public TypeRole Heading4 { get; init; } = TypeRole.Label;

    /// <summary>The prose role — paragraphs, list items, quotes, table cells.</summary>
    public TypeRole Body { get; init; } = TypeRole.BodyM;

    /// <summary>Vertical rhythm between blocks (dp).</summary>
    public float BlockGap { get; init; } = 14;

    /// <summary>Line numbers on fenced code. Off by default: a chat message's three-line sample
    /// doesn't want a gutter; a documentation page can ask for one.</summary>
    public bool CodeLineNumbers { get; init; }

    /// <summary>Fenced code on the inverse (dark) code surface regardless of theme mode.</summary>
    public bool CodeInverse { get; init; }
}

/// <summary>
/// Markdown, rendered as the design system — <c>Markdown(source)</c> and a document comes out in
/// the app's own theme, because every block lowers to the components the app is already made of:
/// headings are <see cref="Primitives.Text"/> at type-scale roles, fenced code is
/// <see cref="CodeBlock"/> (highlighted by the same rules the CodeEditor uses), rules are
/// <see cref="Divider"/>, tables are <see cref="Primitives.Grid"/> rows. There is no second
/// "basic" renderer to drift from: the default look is the default theme.
/// <para>
/// A paragraph is ONE <see cref="Primitives.Text"/> with <see cref="TextRun"/> spans, not a row of
/// Texts — a row breaks between runs instead of between words, so a sentence with three code spans
/// would wrap at the spans. Links ride <see cref="TextRun.Destination"/>, which the web realizer
/// navigates; Photon draws them as prose today (the run-level hit-testing fence
/// <see cref="TextRun.Destination"/> already names).
/// </para>
/// <para>
/// The SAME parser runs everywhere — C# on the server and on Photon, its transpiled twin in the
/// browser — so SSR and hydration agree on the tree by construction, and a source that changes
/// client-side (a chat message streaming in) re-renders without asking the server.
/// </para>
/// </summary>
public sealed class Markdown : StatelessComponent
{
    public Markdown(string source)
    {
        Source = source;
    }

    public string Source { get; init; }

    /// <summary>Role mapping and rhythm; null takes the defaults, which read from the theme.</summary>
    public MarkdownStyle? Style { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var style = Style ?? new MarkdownStyle();
        var blocks = MarkdownParser.Parse(Source);

        var column = new Column(gap: style.BlockGap) { Width = SizeValue.Fill };
        foreach (var block in blocks)
            column.Add(BlockView(block, theme, style, context));
        return column;
    }

    /// <summary>
    /// A switch STATEMENT, not an expression — the same eqc lesson the documentation site's block
    /// view learned: an expression-bodied switch used to be emitted into the caller, where its
    /// parameters do not exist.
    /// </summary>
    private static VisualNode BlockView(MarkdownBlock block, IAppTheme theme, MarkdownStyle style,
        ComponentContext context)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
            {
                var role = style.Heading4;
                if (block.Level == 1) role = style.Heading1;
                else if (block.Level == 2) role = style.Heading2;
                else if (block.Level == 3) role = style.Heading3;
                // Extra air above a section start — rhythm, not a magic constant per level.
                return new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Padding = new EdgeInsets(0, block.Level <= 2 ? 10 : 6, 0, 0),
                }, new Text(block.Text, role, theme.TextPrimary, maxLines: 0) { Key = block.Id });
            }

            case MarkdownBlockKind.List:
                return ListView(block, theme, style);

            case MarkdownBlockKind.Code:
                return new CodeBlock(block.Raw, block.Lang)
                {
                    ShowLineNumbers = style.CodeLineNumbers,
                    Inverse = style.CodeInverse,
                    OnCopy = () => context.GetService<ITextClipboard>()?.Write(block.Raw),
                };

            case MarkdownBlockKind.Quote:
                return new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Padding = new EdgeInsets(16, 2, 0, 2),
                    BorderColor = theme.BorderStrong,
                    BorderWidth = 3,
                    BorderSides = BorderSides.Start,
                }, Paragraph(block.Runs, theme, style));

            case MarkdownBlockKind.Table:
                return TableView(block, theme, style);

            case MarkdownBlockKind.Rule:
                return new Divider();

            default:
                return Paragraph(block.Runs, theme, style);
        }
    }

    /// <summary>One Text with spans — inline emphasis, code and links without breaking the line
    /// into flex items (see the class remarks for why a Row of Texts cannot carry prose).</summary>
    private static VisualNode Paragraph(List<MarkdownRun> runs, IAppTheme theme, MarkdownStyle style)
    {
        var prose = theme.Type(style.Body);
        // Inline code sits a step below the prose size, mono, keeping the paragraph's line box so
        // the smaller run does not reopen the leading — the documented TextRun.StyleOverride case.
        var codeSize = prose.Size < 13.5f ? prose.Size : 13.5f;
        var code = new TypeStyle(codeSize, prose.LineHeight, prose.Weight, 0f, prose.MaxScale, true);

        var spans = new List<TextRun>();
        foreach (var run in runs)
        {
            // Italic parses and survives in the model, but paints upright today: TypeStyle has
            // no slant axis (weight and mono are its only variances), so neither TextRun nor the
            // rasterizers can say it yet. A fence of the type system, not of this component —
            // when TypeStyle grows the axis, this loop is the only line that changes.
            spans.Add(new TextRun(run.Text)
            {
                Mono = run.Code,
                StyleOverride = run.Code ? code : null,
                Color = run.Code ? theme.TextPrimary : null,
                Weight = run.Bold ? FontWeight.Bold : null,
                Destination = run.IsLink ? run.Href : null,
            });
        }
        return new Text("", style.Body, theme.TextSecondary, maxLines: 0) { Spans = spans };
    }

    private static VisualNode ListView(MarkdownBlock block, IAppTheme theme, MarkdownStyle style)
    {
        var list = new Column(gap: 8) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
        foreach (var item in block.Items)
        {
            var row = new Row(gap: 10) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
            row.Add(new Box(new BoxStyle { Padding = new EdgeInsets(item.Depth * Space.S5, 0, 0, 0) },
                new Text(item.Marker, style.Body, theme.TextMuted, maxLines: 1)));
            row.Add(new Box(new BoxStyle { Width = SizeValue.Fill },
                Paragraph(item.Runs, theme, style)));
            list.Add(row);
        }
        return list;
    }

    private static VisualNode TableView(MarkdownBlock block, IAppTheme theme, MarkdownStyle style)
    {
        var tracks = new List<GridTrack>();
        for (var i = 0; i < block.Head.Count; i++) tracks.Add(GridTrack.Flex(1));

        var rows = new Column(gap: 0) { Width = SizeValue.Fill };

        var head = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };
        foreach (var cell in block.Head)
            head.Add(new Box(new BoxStyle { Padding = new EdgeInsets(14, 10, 14, 10) },
                Paragraph(cell.Runs, theme, style)));
        rows.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Background = theme.SurfaceSubtle }, head));

        foreach (var row in block.Rows)
        {
            rows.Add(new Divider());
            var grid = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };
            foreach (var cell in row.Cells)
                grid.Add(new Box(new BoxStyle { Padding = new EdgeInsets(14, 11, 14, 11) },
                    Paragraph(cell.Runs, theme, style)));
            rows.Add(grid);
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            CornerRadius = new CornerRadii(12),
            BorderColor = theme.Border,
            BorderWidth = 1,
            Clip = true,
        }, rows);
    }
}
