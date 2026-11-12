namespace eQuantic.UI.Primitives;

/// <summary>
/// The list family's slot (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): ListItem, List, Divider, Accordion and
/// SwipeableRow, every member OPTIONAL. A member left null is the component's own default. The fill a
/// selected row takes from <see cref="IAppTheme.Colors"/> stays there, and so does a swipe action's.
/// <para>
/// A list's divider starts where the row above starts its text: the row's padding, its leading slot and
/// the gap after it. Those three are read from this slot by the row and by the list alike, so a theme
/// that widens the padding moves the hairline with it.
/// </para>
/// <para>
/// <em>How does Flutter solve it?</em> <c>ListTileThemeData</c>, <c>DividerThemeData</c> and
/// <c>ExpansionTileThemeData</c> on <c>ThemeData</c>: nullable members the widget resolves against its
/// defaults.
/// </para>
/// </summary>
public sealed record ListTheme
{
    /// <summary>The fill a hovered or pressed row and a hovered accordion header take
    /// (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? QuietFill { get; init; }

    // ---- ListItem ---------------------------------------------------------------------------------

    /// <summary>A one-line row's minimum height (52).</summary>
    public float? ItemHeight { get; init; }

    /// <summary>A row with a one-line subtitle's minimum height (68).</summary>
    public float? TwoLineItemHeight { get; init; }

    /// <summary>A row with a two-line subtitle's minimum height (88).</summary>
    public float? ThreeLineItemHeight { get; init; }

    /// <summary>A row's horizontal padding, where its text starts when nothing leads it (16).</summary>
    public float? ItemPaddingX { get; init; }

    /// <summary>A row's vertical padding (8).</summary>
    public float? ItemPaddingY { get; init; }

    /// <summary>The gap between a row's leading slot, its text and its trailing slot (12).</summary>
    public float? ItemGap { get; init; }

    /// <summary>The gap between a row's title and its subtitle (2).</summary>
    public float? TextGap { get; init; }

    /// <summary>A row title's point size (15).</summary>
    public float? TitleSize { get; init; }

    /// <summary>A row title's line height (20).</summary>
    public float? TitleLineHeight { get; init; }

    /// <summary>A row title's weight (medium).</summary>
    public FontWeight? TitleWeight { get; init; }

    /// <summary>A row title's colour (<see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? TitleColor { get; init; }

    /// <summary>A row subtitle's colour (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? SubtitleColor { get; init; }

    // ---- Divider ----------------------------------------------------------------------------------

    /// <summary>A divider's thickness (1).</summary>
    public float? DividerThickness { get; init; }

    /// <summary>A divider's colour (<see cref="IAppTheme.Border"/>).</summary>
    public ColorToken? DividerColor { get; init; }

    /// <summary>How far in a leading or middle divider's line starts (16).</summary>
    public float? DividerInset { get; init; }

    // ---- Accordion --------------------------------------------------------------------------------

    /// <summary>An accordion header's height (a large control's comfortable height, 48).</summary>
    public float? HeaderHeight { get; init; }

    /// <summary>An accordion section's horizontal padding, its header's and its content's (12).</summary>
    public float? SectionPaddingX { get; init; }

    /// <summary>The padding under an open section's content (12).</summary>
    public float? SectionPaddingBottom { get; init; }

    /// <summary>The gap between a header's title and its chevron (8).</summary>
    public float? HeaderGap { get; init; }

    /// <summary>The chevron's size (16), a size on the §07 whitelist (16, 20, 24 or 32): an icon of any
    /// other size throws when it is built.</summary>
    public float? ChevronSize { get; init; }

    /// <summary>The chevron's colour (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? ChevronColor { get; init; }

    // ---- SwipeableRow -----------------------------------------------------------------------------

    /// <summary>The revealed action's width, which is also how far the row slides open (96).</summary>
    public float? SwipeActionWidth { get; init; }

    /// <summary>The gap between the action's glyph and its label (3).</summary>
    public float? SwipeActionGap { get; init; }

    /// <summary>The action's glyph size (20), on the §07 whitelist.</summary>
    public float? SwipeIconSize { get; init; }

    /// <summary>The surface of the row that slides over the action (<see cref="IAppTheme.Surface"/>).</summary>
    public ColorToken? SwipeSurface { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s. Null when
    /// both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static ListTheme? Merge(ListTheme? under, ListTheme? over) =>
        under is null ? over : over is null ? under : new ListTheme
        {
            QuietFill = over.QuietFill ?? under.QuietFill,
            ItemHeight = over.ItemHeight ?? under.ItemHeight,
            TwoLineItemHeight = over.TwoLineItemHeight ?? under.TwoLineItemHeight,
            ThreeLineItemHeight = over.ThreeLineItemHeight ?? under.ThreeLineItemHeight,
            ItemPaddingX = over.ItemPaddingX ?? under.ItemPaddingX,
            ItemPaddingY = over.ItemPaddingY ?? under.ItemPaddingY,
            ItemGap = over.ItemGap ?? under.ItemGap,
            TextGap = over.TextGap ?? under.TextGap,
            TitleSize = over.TitleSize ?? under.TitleSize,
            TitleLineHeight = over.TitleLineHeight ?? under.TitleLineHeight,
            TitleWeight = over.TitleWeight ?? under.TitleWeight,
            TitleColor = over.TitleColor ?? under.TitleColor,
            SubtitleColor = over.SubtitleColor ?? under.SubtitleColor,
            DividerThickness = over.DividerThickness ?? under.DividerThickness,
            DividerColor = over.DividerColor ?? under.DividerColor,
            DividerInset = over.DividerInset ?? under.DividerInset,
            HeaderHeight = over.HeaderHeight ?? under.HeaderHeight,
            SectionPaddingX = over.SectionPaddingX ?? under.SectionPaddingX,
            SectionPaddingBottom = over.SectionPaddingBottom ?? under.SectionPaddingBottom,
            HeaderGap = over.HeaderGap ?? under.HeaderGap,
            ChevronSize = over.ChevronSize ?? under.ChevronSize,
            ChevronColor = over.ChevronColor ?? under.ChevronColor,
            SwipeActionWidth = over.SwipeActionWidth ?? under.SwipeActionWidth,
            SwipeActionGap = over.SwipeActionGap ?? under.SwipeActionGap,
            SwipeIconSize = over.SwipeIconSize ?? under.SwipeIconSize,
            SwipeSurface = over.SwipeSurface ?? under.SwipeSurface,
        };
}
