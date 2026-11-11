using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

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

    /// <summary>
    /// This document's own style, member by member ahead of the theme's <see cref="IAppTheme.Markdown"/>
    /// slot: a member left null is the slot's, and one the slot leaves null is the default, which reads
    /// from the theme.
    /// </summary>
    public MarkdownTheme? Style { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var style = Merge(Style, theme.Markdown);
        var blocks = MarkdownParser.Parse(Source);

        var column = new Column(gap: style.BlockGap ?? 14) { Width = SizeValue.Fill };
        foreach (var block in blocks)
            column.Add(BlockView(block, theme, style, context));
        return column;
    }

    /// <summary>The document's style and the theme's slot, the document's member winning wherever it has
    /// one (§3.10). A member both leave null stays null, and the view reading it supplies the default.</summary>
    private static MarkdownTheme Merge(MarkdownTheme? own, MarkdownTheme? slot)
    {
        if (own is null) return slot ?? new MarkdownTheme();
        if (slot is null) return own;
        return new MarkdownTheme
        {
            Heading1 = own.Heading1 ?? slot.Heading1,
            Heading2 = own.Heading2 ?? slot.Heading2,
            Heading3 = own.Heading3 ?? slot.Heading3,
            Heading4 = own.Heading4 ?? slot.Heading4,
            Body = own.Body ?? slot.Body,
            BlockGap = own.BlockGap ?? slot.BlockGap,
            CodeLineNumbers = own.CodeLineNumbers ?? slot.CodeLineNumbers,
            CodeInverse = own.CodeInverse ?? slot.CodeInverse,
            HeadingSpace = own.HeadingSpace ?? slot.HeadingSpace,
            SubheadingSpace = own.SubheadingSpace ?? slot.SubheadingSpace,
            HeadingColor = own.HeadingColor ?? slot.HeadingColor,
            BodyColor = own.BodyColor ?? slot.BodyColor,
            InlineCodeMaxSize = own.InlineCodeMaxSize ?? slot.InlineCodeMaxSize,
            InlineCodeColor = own.InlineCodeColor ?? slot.InlineCodeColor,
            QuoteIndent = own.QuoteIndent ?? slot.QuoteIndent,
            QuotePaddingY = own.QuotePaddingY ?? slot.QuotePaddingY,
            QuoteRuleWidth = own.QuoteRuleWidth ?? slot.QuoteRuleWidth,
            QuoteRuleColor = own.QuoteRuleColor ?? slot.QuoteRuleColor,
            ListGap = own.ListGap ?? slot.ListGap,
            MarkerGap = own.MarkerGap ?? slot.MarkerGap,
            ListIndent = own.ListIndent ?? slot.ListIndent,
            MarkerColor = own.MarkerColor ?? slot.MarkerColor,
            TableRadius = own.TableRadius ?? slot.TableRadius,
            TableBorderWidth = own.TableBorderWidth ?? slot.TableBorderWidth,
            TableBorderColor = own.TableBorderColor ?? slot.TableBorderColor,
            TableHeaderFill = own.TableHeaderFill ?? slot.TableHeaderFill,
            TableCellPaddingX = own.TableCellPaddingX ?? slot.TableCellPaddingX,
            TableHeaderPaddingY = own.TableHeaderPaddingY ?? slot.TableHeaderPaddingY,
            TableCellPaddingY = own.TableCellPaddingY ?? slot.TableCellPaddingY,
        };
    }

    /// <summary>The prose role, the one more than one view reads.</summary>
    private static TypeRole BodyRole(MarkdownTheme style) => style.Body ?? TypeRole.BodyM;

    /// <summary>
    /// A switch STATEMENT, not an expression — the same eqc lesson the documentation site's block
    /// view learned: an expression-bodied switch used to be emitted into the caller, where its
    /// parameters do not exist.
    /// </summary>
    private static VisualNode BlockView(MarkdownBlock block, IAppTheme theme, MarkdownTheme style,
        ComponentContext context)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
            {
                var role = style.Heading4 ?? TypeRole.Label;
                if (block.Level == 1) role = style.Heading1 ?? TypeRole.Heading;
                else if (block.Level == 2) role = style.Heading2 ?? TypeRole.Title;
                else if (block.Level == 3) role = style.Heading3 ?? TypeRole.TitleSmall;
                // Extra air above a section start — rhythm, not a magic constant per level.
                return new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Padding = new EdgeInsets(0, block.Level <= 2 ? style.HeadingSpace ?? 10 : style.SubheadingSpace ?? 6, 0, 0),
                }, new Text(block.Text, role, style.HeadingColor ?? theme.TextPrimary, maxLines: 0,
                    // The document's outline, from the only place that already knows it. A `##`
                    // said level two long before the framework could emit one; until now every
                    // heading in every rendered document was a span, which is the worst place for
                    // it — long prose is exactly what a reader skims by heading. The parser clamps
                    // to four, so the level is always one HTML has.
                    headingLevel: block.Level)
                { Key = block.Id });
            }

            case MarkdownBlockKind.List:
                return ListView(block, theme, style);

            case MarkdownBlockKind.Code:
                // A mermaid fence is a DIAGRAM, natively: the Mermaid component parses and draws
                // it with the same vocabulary as the rest of the page, and falls back to showing
                // the fence as code when the source speaks a grammar the subset doesn't.
                if (block.Lang == "mermaid") return new Mermaid(block.Raw);
                return new CodeBlock(block.Raw, block.Lang)
                {
                    ShowLineNumbers = style.CodeLineNumbers ?? false,
                    Inverse = style.CodeInverse ?? false,
                    OnCopy = () => context.GetService<ITextClipboard>()?.Write(block.Raw),
                };

            case MarkdownBlockKind.Quote:
                return new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Padding = new EdgeInsets(style.QuoteIndent ?? 16, style.QuotePaddingY ?? 2, 0, style.QuotePaddingY ?? 2),
                    BorderColor = style.QuoteRuleColor ?? theme.BorderStrong,
                    BorderWidth = style.QuoteRuleWidth ?? 3,
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
    private static VisualNode Paragraph(List<MarkdownRun> runs, IAppTheme theme, MarkdownTheme style)
    {
        var body = BodyRole(style);
        var prose = theme.Type(body);
        // Inline code sits a step below the prose size, mono, keeping the paragraph's line box so
        // the smaller run does not reopen the leading — the documented TextRun.StyleOverride case.
        var codeMax = style.InlineCodeMaxSize ?? 13.5f;
        var codeSize = prose.Size < codeMax ? prose.Size : codeMax;
        var code = new TypeStyle(codeSize, prose.LineHeight, prose.Weight, 0f, prose.MaxScale, true);

        var spans = new List<TextRun>();
        foreach (var run in runs)
        {
            spans.Add(new TextRun(run.Text)
            {
                Mono = run.Code,
                StyleOverride = run.Code ? code : null,
                Color = run.Code ? style.InlineCodeColor ?? theme.TextPrimary : null,
                Weight = run.Bold ? FontWeight.Bold : null,
                Italic = run.Italic,
                Destination = run.IsLink ? run.Href : null,
            });
        }
        return new Text("", body, style.BodyColor ?? theme.TextSecondary, maxLines: 0) { Spans = spans };
    }

    private static VisualNode ListView(MarkdownBlock block, IAppTheme theme, MarkdownTheme style)
    {
        var indent = style.ListIndent ?? Space.S5;
        var list = new Column(gap: style.ListGap ?? 8) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
        foreach (var item in block.Items)
        {
            var row = new Row(gap: style.MarkerGap ?? 10) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
            row.Add(new Box(new BoxStyle { Padding = new EdgeInsets(item.Depth * indent, 0, 0, 0) },
                new Text(item.Marker, BodyRole(style), style.MarkerColor ?? theme.TextMuted, maxLines: 1)));
            row.Add(new Box(new BoxStyle { Width = SizeValue.Fill },
                Paragraph(item.Runs, theme, style)));
            list.Add(row);
        }
        return list;
    }

    private static VisualNode TableView(MarkdownBlock block, IAppTheme theme, MarkdownTheme style)
    {
        var paddingX = style.TableCellPaddingX ?? 14;
        var headerY = style.TableHeaderPaddingY ?? 10;
        var cellY = style.TableCellPaddingY ?? 11;
        var tracks = new List<GridTrack>();
        for (var i = 0; i < block.Head.Count; i++) tracks.Add(GridTrack.Flex(1));

        var rows = new Column(gap: 0) { Width = SizeValue.Fill };

        var head = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };
        foreach (var cell in block.Head)
            head.Add(new Box(new BoxStyle { Padding = new EdgeInsets(paddingX, headerY, paddingX, headerY) },
                Paragraph(cell.Runs, theme, style)));
        rows.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Background = style.TableHeaderFill ?? theme.SurfaceSubtle }, head));

        foreach (var row in block.Rows)
        {
            rows.Add(new Divider());
            var grid = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };
            foreach (var cell in row.Cells)
                grid.Add(new Box(new BoxStyle { Padding = new EdgeInsets(paddingX, cellY, paddingX, cellY) },
                    Paragraph(cell.Runs, theme, style)));
            rows.Add(grid);
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            CornerRadius = new CornerRadii(style.TableRadius ?? 12),
            BorderColor = style.TableBorderColor ?? theme.Border,
            BorderWidth = style.TableBorderWidth ?? 1,
            Clip = true,
        }, rows);
    }
}
