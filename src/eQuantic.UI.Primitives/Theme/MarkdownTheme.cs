namespace eQuantic.UI.Primitives;

/// <summary>
/// The rendered document's slot (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): how a Markdown maps document
/// roles onto the type scale, its rhythm, and what its quotes, lists, inline code and tables look like,
/// every member OPTIONAL. A Markdown reads each member from its own <c>Style</c>, then from the theme's
/// slot, then from its default, so a document is styled the way the code palette and the data palette
/// are: once, by the theme, with an instance free to say otherwise.
/// <para>
/// Every default reads from the theme the app already selected, so a document in the default theme is a
/// document in the design system, and <c>MaterialTheme.FromSeed(…)</c> rebrands it with nothing else
/// changed.
/// </para>
/// <para>
/// <em>How does Flutter solve it?</em> <c>MarkdownStyleSheet</c>, which flutter_markdown builds from the
/// <c>ThemeData</c> above it and lets a widget override.
/// </para>
/// </summary>
public sealed record MarkdownTheme
{
    /// <summary>A level-one heading's role (<see cref="TypeRole.Heading"/>).</summary>
    public TypeRole? Heading1 { get; init; }

    /// <summary>A level-two heading's role (<see cref="TypeRole.Title"/>).</summary>
    public TypeRole? Heading2 { get; init; }

    /// <summary>A level-three heading's role (<see cref="TypeRole.TitleSmall"/>).</summary>
    public TypeRole? Heading3 { get; init; }

    /// <summary>A level-four heading's role (<see cref="TypeRole.Label"/>).</summary>
    public TypeRole? Heading4 { get; init; }

    /// <summary>The prose role: paragraphs, list items, quotes and table cells
    /// (<see cref="TypeRole.BodyM"/>).</summary>
    public TypeRole? Body { get; init; }

    /// <summary>The vertical rhythm between blocks (14).</summary>
    public float? BlockGap { get; init; }

    /// <summary>Line numbers on fenced code (false): a chat message's three-line sample wants no gutter,
    /// a documentation page can ask for one.</summary>
    public bool? CodeLineNumbers { get; init; }

    /// <summary>Fenced code on the inverse code surface whatever the theme's mode (false).</summary>
    public bool? CodeInverse { get; init; }

    /// <summary>The extra space above a level-one or level-two heading (10).</summary>
    public float? HeadingSpace { get; init; }

    /// <summary>The extra space above a level-three or level-four heading (6).</summary>
    public float? SubheadingSpace { get; init; }

    /// <summary>A heading's colour (<see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? HeadingColor { get; init; }

    /// <summary>The prose's colour (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? BodyColor { get; init; }

    /// <summary>Inline code's largest point size, which a smaller body keeps (13.5).</summary>
    public float? InlineCodeMaxSize { get; init; }

    /// <summary>Inline code's colour (<see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? InlineCodeColor { get; init; }

    /// <summary>How far in a quote's text starts from its rule (16).</summary>
    public float? QuoteIndent { get; init; }

    /// <summary>The padding above and below a quote (2).</summary>
    public float? QuotePaddingY { get; init; }

    /// <summary>The width of a quote's rule (3).</summary>
    public float? QuoteRuleWidth { get; init; }

    /// <summary>The colour of a quote's rule (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? QuoteRuleColor { get; init; }

    /// <summary>The gap between a list's items (8).</summary>
    public float? ListGap { get; init; }

    /// <summary>The gap between an item's marker and its text (10).</summary>
    public float? MarkerGap { get; init; }

    /// <summary>How far in each level of a nested list starts (20).</summary>
    public float? ListIndent { get; init; }

    /// <summary>A list marker's colour (<see cref="IAppTheme.TextMuted"/>).</summary>
    public ColorToken? MarkerColor { get; init; }

    /// <summary>A table's corner radius (12).</summary>
    public float? TableRadius { get; init; }

    /// <summary>A table's border width (1).</summary>
    public float? TableBorderWidth { get; init; }

    /// <summary>A table's border colour (<see cref="IAppTheme.Border"/>).</summary>
    public ColorToken? TableBorderColor { get; init; }

    /// <summary>The fill of a table's header (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? TableHeaderFill { get; init; }

    /// <summary>A table cell's horizontal padding (14).</summary>
    public float? TableCellPaddingX { get; init; }

    /// <summary>A header cell's vertical padding (10).</summary>
    public float? TableHeaderPaddingY { get; init; }

    /// <summary>A body cell's vertical padding (11).</summary>
    public float? TableCellPaddingY { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s. Null when
    /// both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static MarkdownTheme? Merge(MarkdownTheme? under, MarkdownTheme? over) =>
        under is null ? over : over is null ? under : new MarkdownTheme
        {
            Heading1 = over.Heading1 ?? under.Heading1,
            Heading2 = over.Heading2 ?? under.Heading2,
            Heading3 = over.Heading3 ?? under.Heading3,
            Heading4 = over.Heading4 ?? under.Heading4,
            Body = over.Body ?? under.Body,
            BlockGap = over.BlockGap ?? under.BlockGap,
            CodeLineNumbers = over.CodeLineNumbers ?? under.CodeLineNumbers,
            CodeInverse = over.CodeInverse ?? under.CodeInverse,
            HeadingSpace = over.HeadingSpace ?? under.HeadingSpace,
            SubheadingSpace = over.SubheadingSpace ?? under.SubheadingSpace,
            HeadingColor = over.HeadingColor ?? under.HeadingColor,
            BodyColor = over.BodyColor ?? under.BodyColor,
            InlineCodeMaxSize = over.InlineCodeMaxSize ?? under.InlineCodeMaxSize,
            InlineCodeColor = over.InlineCodeColor ?? under.InlineCodeColor,
            QuoteIndent = over.QuoteIndent ?? under.QuoteIndent,
            QuotePaddingY = over.QuotePaddingY ?? under.QuotePaddingY,
            QuoteRuleWidth = over.QuoteRuleWidth ?? under.QuoteRuleWidth,
            QuoteRuleColor = over.QuoteRuleColor ?? under.QuoteRuleColor,
            ListGap = over.ListGap ?? under.ListGap,
            MarkerGap = over.MarkerGap ?? under.MarkerGap,
            ListIndent = over.ListIndent ?? under.ListIndent,
            MarkerColor = over.MarkerColor ?? under.MarkerColor,
            TableRadius = over.TableRadius ?? under.TableRadius,
            TableBorderWidth = over.TableBorderWidth ?? under.TableBorderWidth,
            TableBorderColor = over.TableBorderColor ?? under.TableBorderColor,
            TableHeaderFill = over.TableHeaderFill ?? under.TableHeaderFill,
            TableCellPaddingX = over.TableCellPaddingX ?? under.TableCellPaddingX,
            TableHeaderPaddingY = over.TableHeaderPaddingY ?? under.TableHeaderPaddingY,
            TableCellPaddingY = over.TableCellPaddingY ?? under.TableCellPaddingY,
        };
}
