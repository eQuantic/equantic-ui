namespace eQuantic.UI.Primitives;

/// <summary>
/// The navigation family's slot (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): Tabs, BottomNavigation,
/// NavigationRail, AppBar, Breadcrumb, Pagination and PageIndicator, every member OPTIONAL. A member left
/// null is the component's own default. The colour a selection takes from <see cref="IAppTheme.Colors"/>
/// stays there; a slot carries the colours the component chooses itself.
/// <para>
/// The bar and the rail share the members that mean the same thing on both (the unselected tint, the
/// pill's corner, the gap under it) and keep their own where their specifications differ: the bar draws
/// a 24dp glyph in a 56 by 26 pill under an 11/700 label, the rail a 20dp glyph in a 52 by 30 pill.
/// </para>
/// <para>
/// <em>How does Flutter solve it?</em> <c>TabBarTheme</c>, <c>NavigationBarThemeData</c>,
/// <c>NavigationRailThemeData</c> and <c>AppBarTheme</c> on <c>ThemeData</c>: nullable members the widget
/// resolves against its defaults.
/// </para>
/// </summary>
public sealed record NavigationTheme
{
    /// <summary>The tint of what is not selected: a tab's label, a destination's glyph and label
    /// (<see cref="IAppTheme.TextMuted"/>).</summary>
    public ColorToken? InactiveColor { get; init; }

    /// <summary>The fill a pressed tab and a hovered page number take (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? QuietFill { get; init; }

    /// <summary>The surface of the bar, of the rail and of a scrolled app bar
    /// (<see cref="IAppTheme.Surface"/>).</summary>
    public ColorToken? Background { get; init; }

    // ---- Tabs -----------------------------------------------------------------------------------

    /// <summary>The tab strip's height (48).</summary>
    public float? TabHeight { get; init; }

    /// <summary>A tab label's point size (14).</summary>
    public float? TabLabelSize { get; init; }

    /// <summary>A tab label's line height (18).</summary>
    public float? TabLabelLineHeight { get; init; }

    /// <summary>An unselected tab label's weight (semi-bold).</summary>
    public FontWeight? TabLabelWeight { get; init; }

    /// <summary>The selected tab label's weight (bold).</summary>
    public FontWeight? TabSelectedLabelWeight { get; init; }

    /// <summary>A tab label's tracking (0.1).</summary>
    public float? TabLabelTracking { get; init; }

    /// <summary>The selection indicator's thickness, and its track's (3).</summary>
    public float? IndicatorThickness { get; init; }

    /// <summary>How far in from each side of its tab the indicator starts (16).</summary>
    public float? IndicatorInset { get; init; }

    /// <summary>The radius of the indicator's two top corners (2).</summary>
    public float? IndicatorRadius { get; init; }

    // ---- The bar and the rail ---------------------------------------------------------------------

    /// <summary>The corner radius of a destination's pill (the theme's full shape).</summary>
    public float? PillRadius { get; init; }

    /// <summary>The opacity of the pill a pressed destination previews, the selection's own (0.4, spec B4).</summary>
    public float? PillPressOpacity { get; init; }

    /// <summary>The gap between a destination's pill and its label (2).</summary>
    public float? PillLabelGap { get; init; }

    /// <summary>The bottom navigation bar's height (56).</summary>
    public float? BarHeight { get; init; }

    /// <summary>The bar's glyph size (24), a size on the §07 whitelist (16, 20, 24 or 32): an icon of any
    /// other size throws when it is built.</summary>
    public float? BarIconSize { get; init; }

    /// <summary>The bar's pill width (56).</summary>
    public float? BarPillWidth { get; init; }

    /// <summary>The bar's pill height (26).</summary>
    public float? BarPillHeight { get; init; }

    /// <summary>The bar's label size (11).</summary>
    public float? BarLabelSize { get; init; }

    /// <summary>The bar's label line height (14).</summary>
    public float? BarLabelLineHeight { get; init; }

    /// <summary>The bar's label weight (bold).</summary>
    public FontWeight? BarLabelWeight { get; init; }

    /// <summary>The bar's label tracking (0.1).</summary>
    public float? BarLabelTracking { get; init; }

    /// <summary>The navigation rail's width (80).</summary>
    public float? RailWidth { get; init; }

    /// <summary>The rail's padding above and below its destinations (12).</summary>
    public float? RailPaddingY { get; init; }

    /// <summary>The gap between the rail's destinations, and below its header (8).</summary>
    public float? RailGap { get; init; }

    /// <summary>A rail destination's height (56).</summary>
    public float? RailItemHeight { get; init; }

    /// <summary>The rail's glyph size (20), on the §07 whitelist.</summary>
    public float? RailIconSize { get; init; }

    /// <summary>The rail's pill width (52).</summary>
    public float? RailPillWidth { get; init; }

    /// <summary>The rail's pill height (30).</summary>
    public float? RailPillHeight { get; init; }

    /// <summary>The selected destination's label size, which the rail sets in its own type (12).</summary>
    public float? RailSelectedLabelSize { get; init; }

    /// <summary>The selected destination's label line height (16).</summary>
    public float? RailSelectedLabelLineHeight { get; init; }

    /// <summary>The selected destination's label weight (bold).</summary>
    public FontWeight? RailSelectedLabelWeight { get; init; }

    /// <summary>The width of the rail's trailing edge (1).</summary>
    public float? RailBorderWidth { get; init; }

    /// <summary>The colour of the rail's trailing edge (<see cref="IAppTheme.Border"/>).</summary>
    public ColorToken? RailBorderColor { get; init; }

    // ---- AppBar -----------------------------------------------------------------------------------

    /// <summary>The app bar's height (56).</summary>
    public float? AppBarHeight { get; init; }

    /// <summary>The app bar's horizontal padding (4).</summary>
    public float? AppBarPaddingX { get; init; }

    /// <summary>The app bar title's point size (20).</summary>
    public float? TitleSize { get; init; }

    /// <summary>The app bar title's line height (26).</summary>
    public float? TitleLineHeight { get; init; }

    /// <summary>The app bar title's weight (semi-bold).</summary>
    public FontWeight? TitleWeight { get; init; }

    /// <summary>The app bar title's colour (<see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? TitleColor { get; init; }

    /// <summary>The title's inset from the bar's start when nothing leads it (12).</summary>
    public float? TitleInset { get; init; }

    /// <summary>The space between the title and what leads or follows it (8).</summary>
    public float? TitleGap { get; init; }

    /// <summary>A scrolled app bar's elevation level (2).</summary>
    public int? ScrolledElevation { get; init; }

    // ---- Breadcrumb -------------------------------------------------------------------------------

    /// <summary>The gap between a crumb and a separator (4).</summary>
    public float? CrumbGap { get; init; }

    /// <summary>An ancestor crumb's colour (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? CrumbColor { get; init; }

    /// <summary>The current crumb's colour (<see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? CurrentCrumbColor { get; init; }

    /// <summary>The separator's size (16), on the §07 whitelist.</summary>
    public float? SeparatorSize { get; init; }

    /// <summary>The separator's colour (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? SeparatorColor { get; init; }

    // ---- Pagination and PageIndicator -------------------------------------------------------------

    /// <summary>The side of a page number's cell, and of the ellipsis' (30).</summary>
    public float? PageCellSize { get; init; }

    /// <summary>The gap between page numbers (3).</summary>
    public float? PageGap { get; init; }

    /// <summary>The gap between the previous and next steps and the numbers (8).</summary>
    public float? PaginationGap { get; init; }

    /// <summary>A page number cell's corner radius (the theme's small shape).</summary>
    public float? PageRadius { get; init; }

    /// <summary>A page number's colour, and a page indicator's "n of m" readout's
    /// (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? PageColor { get; init; }

    /// <summary>The ellipsis' colour (<see cref="IAppTheme.TextMuted"/>).</summary>
    public ColorToken? EllipsisColor { get; init; }

    /// <summary>A page indicator dot's side (6).</summary>
    public float? DotSize { get; init; }

    /// <summary>The current page's dot width, a capsule (18).</summary>
    public float? CurrentDotWidth { get; init; }

    /// <summary>The gap between the dots (4).</summary>
    public float? DotGap { get; init; }

    /// <summary>A dot's corner radius (the theme's full shape).</summary>
    public float? DotRadius { get; init; }

    /// <summary>The colour of a dot that is not the current page's (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? DotColor { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s. Null when
    /// both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static NavigationTheme? Merge(NavigationTheme? under, NavigationTheme? over) =>
        under is null ? over : over is null ? under : new NavigationTheme
        {
            InactiveColor = over.InactiveColor ?? under.InactiveColor,
            QuietFill = over.QuietFill ?? under.QuietFill,
            Background = over.Background ?? under.Background,
            TabHeight = over.TabHeight ?? under.TabHeight,
            TabLabelSize = over.TabLabelSize ?? under.TabLabelSize,
            TabLabelLineHeight = over.TabLabelLineHeight ?? under.TabLabelLineHeight,
            TabLabelWeight = over.TabLabelWeight ?? under.TabLabelWeight,
            TabSelectedLabelWeight = over.TabSelectedLabelWeight ?? under.TabSelectedLabelWeight,
            TabLabelTracking = over.TabLabelTracking ?? under.TabLabelTracking,
            IndicatorThickness = over.IndicatorThickness ?? under.IndicatorThickness,
            IndicatorInset = over.IndicatorInset ?? under.IndicatorInset,
            IndicatorRadius = over.IndicatorRadius ?? under.IndicatorRadius,
            PillRadius = over.PillRadius ?? under.PillRadius,
            PillPressOpacity = over.PillPressOpacity ?? under.PillPressOpacity,
            PillLabelGap = over.PillLabelGap ?? under.PillLabelGap,
            BarHeight = over.BarHeight ?? under.BarHeight,
            BarIconSize = over.BarIconSize ?? under.BarIconSize,
            BarPillWidth = over.BarPillWidth ?? under.BarPillWidth,
            BarPillHeight = over.BarPillHeight ?? under.BarPillHeight,
            BarLabelSize = over.BarLabelSize ?? under.BarLabelSize,
            BarLabelLineHeight = over.BarLabelLineHeight ?? under.BarLabelLineHeight,
            BarLabelWeight = over.BarLabelWeight ?? under.BarLabelWeight,
            BarLabelTracking = over.BarLabelTracking ?? under.BarLabelTracking,
            RailWidth = over.RailWidth ?? under.RailWidth,
            RailPaddingY = over.RailPaddingY ?? under.RailPaddingY,
            RailGap = over.RailGap ?? under.RailGap,
            RailItemHeight = over.RailItemHeight ?? under.RailItemHeight,
            RailIconSize = over.RailIconSize ?? under.RailIconSize,
            RailPillWidth = over.RailPillWidth ?? under.RailPillWidth,
            RailPillHeight = over.RailPillHeight ?? under.RailPillHeight,
            RailSelectedLabelSize = over.RailSelectedLabelSize ?? under.RailSelectedLabelSize,
            RailSelectedLabelLineHeight = over.RailSelectedLabelLineHeight ?? under.RailSelectedLabelLineHeight,
            RailSelectedLabelWeight = over.RailSelectedLabelWeight ?? under.RailSelectedLabelWeight,
            RailBorderWidth = over.RailBorderWidth ?? under.RailBorderWidth,
            RailBorderColor = over.RailBorderColor ?? under.RailBorderColor,
            AppBarHeight = over.AppBarHeight ?? under.AppBarHeight,
            AppBarPaddingX = over.AppBarPaddingX ?? under.AppBarPaddingX,
            TitleSize = over.TitleSize ?? under.TitleSize,
            TitleLineHeight = over.TitleLineHeight ?? under.TitleLineHeight,
            TitleWeight = over.TitleWeight ?? under.TitleWeight,
            TitleColor = over.TitleColor ?? under.TitleColor,
            TitleInset = over.TitleInset ?? under.TitleInset,
            TitleGap = over.TitleGap ?? under.TitleGap,
            ScrolledElevation = over.ScrolledElevation ?? under.ScrolledElevation,
            CrumbGap = over.CrumbGap ?? under.CrumbGap,
            CrumbColor = over.CrumbColor ?? under.CrumbColor,
            CurrentCrumbColor = over.CurrentCrumbColor ?? under.CurrentCrumbColor,
            SeparatorSize = over.SeparatorSize ?? under.SeparatorSize,
            SeparatorColor = over.SeparatorColor ?? under.SeparatorColor,
            PageCellSize = over.PageCellSize ?? under.PageCellSize,
            PageGap = over.PageGap ?? under.PageGap,
            PaginationGap = over.PaginationGap ?? under.PaginationGap,
            PageRadius = over.PageRadius ?? under.PageRadius,
            PageColor = over.PageColor ?? under.PageColor,
            EllipsisColor = over.EllipsisColor ?? under.EllipsisColor,
            DotSize = over.DotSize ?? under.DotSize,
            CurrentDotWidth = over.CurrentDotWidth ?? under.CurrentDotWidth,
            DotGap = over.DotGap ?? under.DotGap,
            DotRadius = over.DotRadius ?? under.DotRadius,
            DotColor = over.DotColor ?? under.DotColor,
        };
}
