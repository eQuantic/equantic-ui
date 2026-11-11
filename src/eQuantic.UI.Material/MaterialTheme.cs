using eQuantic.UI.Primitives;

namespace eQuantic.UI.Material;

/// <summary>
/// Material Design 3 as a write-once <see cref="IAppTheme"/> — themes every shared component on BOTH
/// targets (web realizer → DOM+CSS, native realizer → Photon pixels) purely by swapping the theme,
/// exactly the surface a developer uses to brand eQuantic.UI. Two flavours:
/// <list type="bullet">
///   <item><see cref="Instance"/> — the fixed M3 BASELINE scheme (dynamic-color seed #6750A4).</item>
///   <item><see cref="FromSeed"/> — DYNAMIC COLOR: a full theme DERIVED from any seed color via HCT
///   tonal palettes (the real M3 algorithm — CAM16 hue/chroma + CIE L* tone).</item>
/// </list>
/// Color comes from <see cref="MaterialColors"/>; type/shape/elevation (seed-independent) live here.
/// </summary>
public sealed class MaterialTheme : IAppTheme
{
    private readonly MaterialColors _colors;

    private MaterialTheme(MaterialColors colors) => _colors = colors;

    /// <summary>The fixed M3 baseline theme (seed #6750A4).</summary>
    public static readonly MaterialTheme Instance = new(MaterialColors.Baseline);

    /// <summary>
    /// A Material theme DERIVED from a seed color — the M3 dynamic-color algorithm: the seed's HCT
    /// hue anchors the primary/secondary/tertiary/neutral tonal palettes, and every role reads its
    /// canonical M3 tone (e.g. brand blue #0050A0 → a coherent blue Material theme, light + dark).
    /// </summary>
    public static MaterialTheme FromSeed(Color seed) => new(MaterialColors.FromSeed(seed));

    // ---- Surfaces & text (delegated to the color scheme) -----------------------------------------
    public ColorToken Background => _colors.Background;
    public ColorToken Surface => _colors.Surface;
    public ColorToken SurfaceSubtle => _colors.SurfaceSubtle;
    public ColorToken SurfaceHighlight => _colors.SurfaceHighlight;
    public ColorToken Border => _colors.Border;
    public ColorToken BorderStrong => _colors.BorderStrong;
    public ColorToken TextPrimary => _colors.TextPrimary;
    public ColorToken TextSecondary => _colors.TextSecondary;
    public ColorToken TextMuted => _colors.TextMuted;
    public ColorToken TextInverse => _colors.TextInverse;
    public ColorToken FocusRing => _colors.FocusRing;
    public ColorToken LinkColor => _colors.LinkColor;
    public ColorToken Scrim => _colors.Scrim;

    public float DisabledOpacity => 0.38f;

    public VariantColors Colors(Variant variant)
    {
        var transparent = new ColorToken(Color.Transparent);
        return variant switch
        {
            Variant.Primary => _colors.Primary,
            Variant.Secondary => _colors.Secondary,
            Variant.Tertiary => _colors.Tertiary,
            Variant.Destructive => _colors.Destructive,
            Variant.Success => _colors.Success,
            Variant.Warning => _colors.Warning,
            Variant.Info => _colors.Info,
            Variant.Outline or Variant.Ghost => new VariantColors(
                Base: transparent, OnBase: TextPrimary, Pressed: SurfaceSubtle,
                Subtle: transparent, OnSubtle: TextPrimary),
            Variant.Link => new VariantColors(
                Base: transparent, OnBase: LinkColor, Pressed: transparent,
                Subtle: transparent, OnSubtle: LinkColor),
            _ => _colors.Primary,
        };
    }

    // ---- Type scale — the M3 type scale (seed-independent) ---------------------------------------
    public TypeStyle Type(TypeRole role) => role switch
    {
        TypeRole.Display => new TypeStyle(45, 52, FontWeight.Regular, 0f, 1.2f),   // display-medium
        TypeRole.Heading => new TypeStyle(28, 36, FontWeight.Regular, 0f, 1.25f),  // headline-medium
        TypeRole.Title => new TypeStyle(22, 28, FontWeight.Regular, 0f, 1.3f),     // title-large
        TypeRole.BodyL => new TypeStyle(16, 24, FontWeight.Regular, 0.5f, 1.4f),   // body-large
        TypeRole.BodyM => new TypeStyle(14, 20, FontWeight.Regular, 0.25f, 1.4f),  // body-medium
        TypeRole.Label => new TypeStyle(14, 20, FontWeight.Medium, 0.1f, 1.3f),    // label-large
        TypeRole.Caption => new TypeStyle(12, 16, FontWeight.Regular, 0.4f, 1.3f), // body-small
        TypeRole.TitleSmall => new TypeStyle(14, 20, FontWeight.Medium, 0.1f, 1.3f),  // title-small
        TypeRole.LabelSmall => new TypeStyle(11, 16, FontWeight.Medium, 0.5f, 1.3f),  // label-small
        TypeRole.Overline => new TypeStyle(10, 14, FontWeight.Medium, 1.5f, 1.2f),    // M2 overline
        _ => new TypeStyle(14, 20, FontWeight.Regular, 0.25f, 1.4f),
    };

    // ---- Shape — the M3 shape scale (seed-independent) -------------------------------------------
    public float Shape(ShapeScale scale) => scale switch
    {
        ShapeScale.None => 0,
        ShapeScale.ExtraSmall => 4,
        ShapeScale.Small => 8,
        ShapeScale.Medium => 12,
        ShapeScale.Large => 16,
        ShapeScale.ExtraLarge => 28,
        _ => 999,
    };

    // ---- Slots (docs/ELEMENT-TREE-PLAN.md §3.10) — M3's component specifications -----------------
    // Material 3 states its sizes per component rather than as one ladder of controls, so the shared
    // `Controls` slot stays the design system's and each family's slot carries its component's: the
    // common button here (m3.material.io, Buttons, Specs), the form controls with their own slots.

    /// <summary>
    /// M3's common button: a fully rounded container, its label in Label large (14, weight 500,
    /// tracking 0.1), 24dp of padding and an 8dp gap at the medium size it specifies, a 1dp outline, and
    /// a text button's 12dp padding for the link variant. M3's icon is 18dp, which the icon ladder does not
    /// hold (§07: 16, 20, 24, 32, the sizes an icon is drawn crisp at), so it takes the next rung, 20.
    /// </summary>
    public ButtonTheme? Button { get; } = new()
    {
        Controls = new ControlsTheme
        {
            PaddingX = new DensityLadder { Comfortable = new SizeLadder { Medium = 24 } },
            LabelSize = new DensityLadder { Comfortable = new SizeLadder { Medium = 14 } },
            Gap = new SizeLadder { Medium = 8 },
            Icon = new SizeLadder { Medium = IconSize.Dense },
            Radius = new SizeLadder { Small = 999, Medium = 999, Large = 999, XLarge = 999 },
        },
        LabelWeight = FontWeight.Medium,
        LabelTracking = 0.1f,
        OutlineWidth = 1,
        LinkPaddingX = 12,
    };

    // Navigation and collections (E6d). Material 3 specifies tabs, the navigation bar and rail, the
    // top app bar, lists, badges and the date picker, and those slots carry its numbers. It specifies no
    // breadcrumb, pagination, page indicator, accordion, swipe action, data table, avatar, empty state
    // or rendered document, so those members, and the Table and Markdown slots, stay the components'
    // own. A colour is left to the roles the theme already maps.

    /// <summary>
    /// M3's navigation (Tabs, Navigation bar, Navigation rail and Top app bar, Specs): a 48dp tab strip
    /// under a 3dp indicator with 3dp corners, its label in Title small (14 over 20, weight 500); an 80dp
    /// bar of 64 by 32 indicators around 24dp icons, 4dp above a Label medium label (12 over 16, tracking
    /// 0.5); an 80dp rail of 56 by 32 indicators around 24dp icons, with no edge; and a 64dp small top
    /// app bar titled in Title large (22 over 28, weight 400).
    /// </summary>
    public NavigationTheme? Navigation { get; } = new()
    {
        TabHeight = 48,
        TabLabelSize = 14,
        TabLabelLineHeight = 20,
        TabLabelWeight = FontWeight.Medium,
        TabSelectedLabelWeight = FontWeight.Medium,
        TabLabelTracking = 0.1f,
        IndicatorThickness = 3,
        IndicatorRadius = 3,
        PillLabelGap = 4,
        BarHeight = 80,
        BarIconSize = IconSize.Md,
        BarPillWidth = 64,
        BarPillHeight = 32,
        BarLabelSize = 12,
        BarLabelLineHeight = 16,
        BarLabelWeight = FontWeight.Medium,
        BarLabelTracking = 0.5f,
        RailWidth = 80,
        RailIconSize = IconSize.Md,
        RailPillWidth = 56,
        RailPillHeight = 32,
        RailSelectedLabelSize = 12,
        RailSelectedLabelLineHeight = 16,
        RailBorderWidth = 0,
        AppBarHeight = 64,
        TitleSize = 22,
        TitleLineHeight = 28,
        TitleWeight = FontWeight.Regular,
    };

    /// <summary>
    /// M3's list (Lists, Specs): 56, 72 and 88dp items, 16dp between the leading element and the text,
    /// and the headline in Body large (16 over 24, weight 400).
    /// </summary>
    public ListTheme? List { get; } = new()
    {
        ItemHeight = 56,
        TwoLineItemHeight = 72,
        ThreeLineItemHeight = 88,
        ItemGap = 16,
        TitleSize = 16,
        TitleLineHeight = 24,
        TitleWeight = FontWeight.Regular,
    };

    /// <summary>
    /// M3's badge (Badges, Specs): a 6dp small badge, and a large badge 16dp tall, padded 4, its count in
    /// Label small (11 over 16, weight 500).
    /// </summary>
    public BadgeTheme? Badge { get; } = new()
    {
        DotSize = 6,
        CountHeight = 16,
        CountPaddingX = 4,
        CountLabelSize = 11,
        CountLabelLineHeight = 16,
        CountLabelWeight = FontWeight.Medium,
    };

    /// <summary>
    /// M3's docked date picker (Date pickers, Specs): 40dp days, today outlined at 1dp, and the container
    /// at the large corner (16) and elevation 3, with no outline.
    /// </summary>
    public CalendarTheme? Calendar { get; } = new()
    {
        CellSize = 40,
        TodayRingWidth = 1,
        PanelRadius = 16,
        PanelBorderWidth = 0,
        PanelElevation = 3,
    };

    // ---- Elevation — the M3 levels (seed-independent) --------------------------------------------
    public ShadowSpec Elevation(int level) => level switch
    {
        <= 0 => new ShadowSpec(0, 0, 0, new ColorToken(Color.Transparent)),
        1 => new ShadowSpec(1, 3, 0, Shadow(0.15f, 0.36f)),
        2 => new ShadowSpec(2, 6, 0, Shadow(0.16f, 0.40f)),
        3 => new ShadowSpec(4, 8, 0, Shadow(0.18f, 0.44f)),
        4 => new ShadowSpec(6, 10, 0, Shadow(0.20f, 0.48f)),
        _ => new ShadowSpec(8, 12, 0, Shadow(0.22f, 0.52f)),
    };

    private static ColorToken Shadow(float lightAlpha, float darkAlpha) => new(
        Color.FromRgba(0, 0, 0, (byte)MathF.Round(lightAlpha * 255)),
        Color.FromRgba(0, 0, 0, (byte)MathF.Round(darkAlpha * 255)));
}
