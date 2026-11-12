namespace eQuantic.UI.Primitives;

/// <summary>
/// The badge family's slot (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): Badge, Avatar and EmptyState, every
/// member OPTIONAL. A member left null is the component's own default. The tints a variant badge and an
/// avatar's initials take from <see cref="IAppTheme.Colors"/> stay there.
/// <para>
/// A ringed badge grows by its ring on every side, so its sizes are derived rather than slotted: a dot
/// of 8 with a ring of 2 is 12, a count of 16 is 20. The avatar's ladders are <see cref="Sizing"/>'s, and
/// a list row whose leading slot is an avatar reads the same rung to place its divider.
/// </para>
/// <para>
/// <em>How does Flutter solve it?</em> <c>BadgeThemeData</c> on <c>ThemeData</c>: the small and large
/// sizes, the padding, the text style and the colours, nullable.
/// </para>
/// </summary>
public sealed record BadgeTheme
{
    /// <summary>The colour of the ring a badge and an avatar's status dot draw around themselves
    /// (<see cref="IAppTheme.Surface"/>).</summary>
    public ColorToken? RingColor { get; init; }

    /// <summary>The width of that ring (2).</summary>
    public float? RingWidth { get; init; }

    // ---- Badge ------------------------------------------------------------------------------------

    /// <summary>A dot badge's side, without its ring (8).</summary>
    public float? DotSize { get; init; }

    /// <summary>A count badge's height and minimum width, without its ring (16).</summary>
    public float? CountHeight { get; init; }

    /// <summary>A count badge's horizontal padding (4).</summary>
    public float? CountPaddingX { get; init; }

    /// <summary>A count's point size (10).</summary>
    public float? CountLabelSize { get; init; }

    /// <summary>A count's line height (12).</summary>
    public float? CountLabelLineHeight { get; init; }

    /// <summary>A count's weight (bold).</summary>
    public FontWeight? CountLabelWeight { get; init; }

    /// <summary>A neutral badge's fill (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? NeutralFill { get; init; }

    /// <summary>A neutral badge's count colour (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? NeutralColor { get; init; }

    // ---- Avatar -----------------------------------------------------------------------------------

    /// <summary>An avatar's side per size (<see cref="Sizing.Avatar"/>: 24, 32, 40 and 56).</summary>
    public SizeLadder? AvatarSize { get; init; }

    /// <summary>An avatar's initials' point size per size (<see cref="Sizing.AvatarInitials"/>: 10, 13, 16
    /// and 22).</summary>
    public SizeLadder? AvatarLabelSize { get; init; }

    /// <summary>The initials' weight (semi-bold).</summary>
    public FontWeight? AvatarLabelWeight { get; init; }

    /// <summary>The person glyph's size per size, when an avatar has no initials (16, 20, 24 and 32), on
    /// the §07 whitelist (16, 20, 24 or 32): an icon of any other size throws when it is built.</summary>
    public SizeLadder? AvatarGlyphSize { get; init; }

    /// <summary>The fill of an avatar without initials (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? AvatarFill { get; init; }

    /// <summary>The person glyph's colour (<see cref="IAppTheme.TextMuted"/>).</summary>
    public ColorToken? AvatarGlyphColor { get; init; }

    /// <summary>An offline status dot's fill (<see cref="IAppTheme.TextMuted"/>). An online one is the
    /// success variant's.</summary>
    public ColorToken? OfflineColor { get; init; }

    // ---- EmptyState -------------------------------------------------------------------------------

    /// <summary>The side of the well an empty state's icon sits in (64).</summary>
    public float? WellSize { get; init; }

    /// <summary>The well's fill (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? WellFill { get; init; }

    /// <summary>The well's icon size (32), on the §07 whitelist.</summary>
    public float? WellIconSize { get; init; }

    /// <summary>The well's icon colour (<see cref="IAppTheme.TextMuted"/>).</summary>
    public ColorToken? WellIconColor { get; init; }

    /// <summary>An empty state's horizontal padding (16).</summary>
    public float? EmptyPaddingX { get; init; }

    /// <summary>An empty state's vertical padding (48).</summary>
    public float? EmptyPaddingY { get; init; }

    /// <summary>The space between the well and the title (16).</summary>
    public float? WellGap { get; init; }

    /// <summary>The space between the title and the body (6).</summary>
    public float? BodyGap { get; init; }

    /// <summary>The space between the body and the actions (20).</summary>
    public float? ActionsGap { get; init; }

    /// <summary>The gap between the two actions (8).</summary>
    public float? ActionSpacing { get; init; }

    /// <summary>An empty state title's point size (20).</summary>
    public float? EmptyTitleSize { get; init; }

    /// <summary>An empty state title's line height (26).</summary>
    public float? EmptyTitleLineHeight { get; init; }

    /// <summary>An empty state title's weight (semi-bold).</summary>
    public FontWeight? EmptyTitleWeight { get; init; }

    /// <summary>An empty state title's colour (<see cref="IAppTheme.TextPrimary"/>).</summary>
    public ColorToken? EmptyTitleColor { get; init; }

    /// <summary>An empty state body's colour (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? EmptyBodyColor { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s, a ladder merged rung by rung. Null when
    /// both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static BadgeTheme? Merge(BadgeTheme? under, BadgeTheme? over) =>
        under is null ? over : over is null ? under : new BadgeTheme
        {
            RingColor = over.RingColor ?? under.RingColor,
            RingWidth = over.RingWidth ?? under.RingWidth,
            DotSize = over.DotSize ?? under.DotSize,
            CountHeight = over.CountHeight ?? under.CountHeight,
            CountPaddingX = over.CountPaddingX ?? under.CountPaddingX,
            CountLabelSize = over.CountLabelSize ?? under.CountLabelSize,
            CountLabelLineHeight = over.CountLabelLineHeight ?? under.CountLabelLineHeight,
            CountLabelWeight = over.CountLabelWeight ?? under.CountLabelWeight,
            NeutralFill = over.NeutralFill ?? under.NeutralFill,
            NeutralColor = over.NeutralColor ?? under.NeutralColor,
            AvatarSize = SizeLadder.Merge(under.AvatarSize, over.AvatarSize),
            AvatarLabelSize = SizeLadder.Merge(under.AvatarLabelSize, over.AvatarLabelSize),
            AvatarLabelWeight = over.AvatarLabelWeight ?? under.AvatarLabelWeight,
            AvatarGlyphSize = SizeLadder.Merge(under.AvatarGlyphSize, over.AvatarGlyphSize),
            AvatarFill = over.AvatarFill ?? under.AvatarFill,
            AvatarGlyphColor = over.AvatarGlyphColor ?? under.AvatarGlyphColor,
            OfflineColor = over.OfflineColor ?? under.OfflineColor,
            WellSize = over.WellSize ?? under.WellSize,
            WellFill = over.WellFill ?? under.WellFill,
            WellIconSize = over.WellIconSize ?? under.WellIconSize,
            WellIconColor = over.WellIconColor ?? under.WellIconColor,
            EmptyPaddingX = over.EmptyPaddingX ?? under.EmptyPaddingX,
            EmptyPaddingY = over.EmptyPaddingY ?? under.EmptyPaddingY,
            WellGap = over.WellGap ?? under.WellGap,
            BodyGap = over.BodyGap ?? under.BodyGap,
            ActionsGap = over.ActionsGap ?? under.ActionsGap,
            ActionSpacing = over.ActionSpacing ?? under.ActionSpacing,
            EmptyTitleSize = over.EmptyTitleSize ?? under.EmptyTitleSize,
            EmptyTitleLineHeight = over.EmptyTitleLineHeight ?? under.EmptyTitleLineHeight,
            EmptyTitleWeight = over.EmptyTitleWeight ?? under.EmptyTitleWeight,
            EmptyTitleColor = over.EmptyTitleColor ?? under.EmptyTitleColor,
            EmptyBodyColor = over.EmptyBodyColor ?? under.EmptyBodyColor,
        };
}
