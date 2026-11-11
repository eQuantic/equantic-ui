
namespace eQuantic.UI.Primitives;

/// <summary>
/// The default Photon theme — every value transcribed from the design system
/// (docs/design/Photon DS - Foundations.dc.html §01–§06, and docs/design/tokens.json,
/// which HandoffTokenPinTests compares against every value below; WCAG ratios verified by
/// <c>DesignTokenTests</c>). Brand anchors: eQuantic blue #0050A0 (Primary), eQuantic green #80B85C
/// (Success, darkened to clear AA in light mode).
/// </summary>
public sealed class PhotonTheme : IAppTheme
{
    public static readonly PhotonTheme Instance = new();

    // ---- Surfaces & structure (§01) -------------------------------------------------------------
    public ColorToken Background { get; } = Token(0xF5F6F8, 0x0C0F13);
    public ColorToken Surface { get; } = Token(0xFFFFFF, 0x14181E);
    public ColorToken SurfaceSubtle { get; } = Token(0xEFF1F4, 0x1C232B);
    public ColorToken SurfaceHighlight { get; } = new(Rgba(0xFFFFFF, 140), Rgba(0xFFFFFF, 18)); // 55% / 7%
    public ColorToken Border { get; } = Token(0xE2E5EA, 0x2A323D);
    public ColorToken BorderStrong { get; } = Token(0xC9CED6, 0x4C5664);

    // ---- Text tiers (§01) ------------------------------------------------------------------------
    public ColorToken TextPrimary { get; } = Token(0x171B21, 0xF2F4F7);
    public ColorToken TextSecondary { get; } = Token(0x4B5563, 0xC7D1DC);
    public ColorToken TextMuted { get; } = Token(0x5F6B7A, 0xACB7C5);
    public ColorToken TextInverse { get; } = Token(0xFFFFFF, 0x171B21);

    // ---- Interaction chrome (§01) ----------------------------------------------------------------
    public ColorToken FocusRing { get; } = Token(0x0050A0, 0x81BAF3);
    public ColorToken LinkColor { get; } = Token(0x0050A0, 0x81BAF3);
    public ColorToken Scrim { get; } = new(Rgba(0x0B0E12, 102), Rgba(0x000000, 143)); // 40% / 56%

    public float DisabledOpacity => 0.38f;

    // ---- Variants (§01 interactive table) --------------------------------------------------------
    private static readonly VariantColors PrimaryColors = new(
        Base: Token(0x0050A0, 0x76BAFF),
        OnBase: Token(0xFFFFFF, 0x06263F),
        Pressed: Token(0x00427F, 0x9BCDFF),
        Subtle: Token(0xE8F1FA, 0x0F2740),
        OnSubtle: Token(0x003E7E, 0xA8CDF2));

    private static readonly VariantColors SecondaryColors = new(
        Base: Token(0xE9EDF2, 0x242C36),
        OnBase: Token(0x3A4350, 0xD7DDE5),
        Pressed: Token(0xDCE2EA, 0x2E3844),
        Subtle: Token(0xE9EDF2, 0x242C36),      // = Base (already subtle)
        OnSubtle: Token(0x3A4350, 0xD7DDE5));   // = OnBase

    private static readonly VariantColors DestructiveColors = new(
        Base: Token(0xB42318, 0xFF958C),
        OnBase: Token(0xFFFFFF, 0x3B0704),
        Pressed: Token(0x8F1D1D, 0xFEC0BB),
        Subtle: Token(0xFCEBEA, 0x3A1210),
        OnSubtle: Token(0x8F1D1D, 0xF4A9A4));

    private static readonly VariantColors SuccessColors = new(
        Base: Token(0x3B7A22, 0x89C562),
        OnBase: Token(0xFFFFFF, 0x12290A),
        Pressed: Token(0x2C5E17, 0x9FD279),
        Subtle: Token(0xEDF6E6, 0x16290C),
        OnSubtle: Token(0x2C5E17, 0xB5DB97));

    private static readonly VariantColors WarningColors = new(
        Base: Token(0x8A5A00, 0xE8B04B),
        OnBase: Token(0xFFFFFF, 0x2E1B02),
        Pressed: Token(0x6E4700, 0xF0C06B),
        Subtle: Token(0xFBF3DF, 0x2E2008),
        OnSubtle: Token(0x7A5200, 0xEECF8F));

    private static readonly VariantColors InfoColors = new(
        Base: Token(0x0C6C86, 0x4DC4DF),
        OnBase: Token(0xFFFFFF, 0x062B33),
        Pressed: Token(0x0A5468, 0x6FD1E7),
        Subtle: Token(0xE4F3F8, 0x0A2A32),
        OnSubtle: Token(0x0A5468, 0x9FDCEB));

    // Tertiary — Photon has no distinct tertiary palette; it reuses Info (the contrasting accent).
    private static readonly VariantColors TertiaryColors = InfoColors;

    public VariantColors Colors(Variant variant)
    {
        var transparent = new ColorToken(Color.Transparent);
        return variant switch
        {
            Variant.Primary => PrimaryColors,
            Variant.Secondary => SecondaryColors,
            Variant.Destructive => DestructiveColors,
            Variant.Success => SuccessColors,
            Variant.Warning => WarningColors,
            Variant.Info => InfoColors,
            Variant.Tertiary => TertiaryColors,
            // Derived (§01): transparent fill + Text tokens; pressed fill = SurfaceSubtle.
            Variant.Outline or Variant.Ghost => new VariantColors(
                Base: transparent,
                OnBase: TextPrimary,
                Pressed: SurfaceSubtle,
                Subtle: transparent,
                OnSubtle: TextPrimary),
            // Link is the audited exception the handoff's own colour rules name: "Base colors are
            // fills for onBase text; never use Base as text on Surface (Link is the audited
            // exception)". So its INK lives in Base and its pressed ink in Pressed — the contrast
            // figures beside them in the token file are measured against the background, as text.
            //
            // Nothing fills a Link (Button.cs excludes it from the fill and from the pressed fill),
            // so the slots carry the ink without painting a blue rectangle. Base used to be
            // transparent with the ink only in OnBase, which read as defensive and cost the pressed
            // ink entirely: #00427F simply did not exist in the theme, and the pressed text swap
            // had nothing to swap TO.
            Variant.Link => new VariantColors(
                Base: LinkColor,
                OnBase: LinkColor,
                Pressed: Token(0x00427F, 0xA8CDF2),
                Subtle: transparent,
                OnSubtle: LinkColor),
            _ => PrimaryColors,
        };
    }

    // ---- Type scale (§02) ------------------------------------------------------------------------
    public TypeStyle Type(TypeRole role) => role switch
    {
        TypeRole.Display => new TypeStyle(34, 40, FontWeight.ExtraBold, -0.4f, 1.15f),
        TypeRole.Heading => new TypeStyle(28, 34, FontWeight.Bold, -0.3f, 1.15f),
        TypeRole.Title => new TypeStyle(20, 26, FontWeight.SemiBold, -0.2f, 1.25f),
        TypeRole.BodyL => new TypeStyle(17, 24, FontWeight.Regular, 0f, 1.3f),
        TypeRole.BodyM => new TypeStyle(15, 20, FontWeight.Regular, 0f, 1.3f),
        TypeRole.Label => new TypeStyle(13, 16, FontWeight.SemiBold, 0.1f, 1.3f),
        TypeRole.Caption => new TypeStyle(12, 16, FontWeight.Medium, 0.2f, 1.3f),
        TypeRole.TitleSmall => new TypeStyle(15, 20, FontWeight.Bold, -0.1f, 1.3f),
        TypeRole.LabelSmall => new TypeStyle(11, 15, FontWeight.Medium, 0.1f, 1.3f),
        TypeRole.Overline => new TypeStyle(10, 14, FontWeight.ExtraBold, 1f, 1.2f),
        _ => new TypeStyle(15, 20, FontWeight.Regular, 0f, 1.3f),
    };

    // ---- Elevation (§05) -------------------------------------------------------------------------
    // Light: #0F1720 at the listed alpha; dark: pure black, alphas from the spec's concrete values
    // (shadows must fight darker canvases). Exactly one shadow per node.
    public ShadowSpec Elevation(int level) => level switch
    {
        <= 0 => new ShadowSpec(0, 0, 0, new ColorToken(Color.Transparent)),
        1 => new ShadowSpec(1, 3, 0, Shadow(0.10f, 0.45f)),
        2 => new ShadowSpec(2, 8, 0, Shadow(0.12f, 0.48f)),
        3 => new ShadowSpec(6, 16, -2, Shadow(0.16f, 0.52f)),
        4 => new ShadowSpec(12, 28, -4, Shadow(0.20f, 0.56f)),
        _ => new ShadowSpec(20, 44, -6, Shadow(0.26f, 0.62f)),
    };

    // ---- Shape (§04) — Photon's radius ladder, now theme-sourced -------------------------------
    public float Shape(ShapeScale scale) => scale switch
    {
        ShapeScale.None => 0,
        ShapeScale.ExtraSmall => Radius.Xs,
        ShapeScale.Small => Radius.Sm,
        ShapeScale.Medium => Radius.Md,
        ShapeScale.Large => Radius.Lg,
        ShapeScale.ExtraLarge => Radius.Xl,
        _ => Radius.Full,
    };

    private static ColorToken Shadow(float lightAlpha, float darkAlpha) => new(
        Rgba(0x0F1720, (byte)MathF.Round(lightAlpha * 255)),
        Rgba(0x000000, (byte)MathF.Round(darkAlpha * 255)));

    // ---- Slots (docs/ELEMENT-TREE-PLAN.md §3.10) ------------------------------------------------------
    // Today's constants, now the design system's rather than each component's. The ladder is Sizing's
    // own, read rung by rung, so the slot and the fallback cannot disagree under this theme.

    /// <summary>The control ladder (spec §09): <see cref="Sizing"/>, every rung of it.</summary>
    public ControlsTheme? Controls { get; } = new()
    {
        Height = ByDensity(Sizing.Height),
        PaddingX = ByDensity(Sizing.PaddingX),
        LabelSize = ByDensity(Sizing.LabelSize),
        Gap = BySize(Sizing.Gap),
        Icon = BySize(Sizing.Icon),
        Radius = BySize(Sizing.Radius),
    };

    /// <summary>The Button (spec A12): a 64dp floor, a semi-bold label at 0.1 tracking, a 1dp outline in
    /// BorderStrong, a link padded by 6, and the quiet variants filling with SurfaceSubtle.</summary>
    public ButtonTheme? Button => field ??= new ButtonTheme
    {
        MinWidth = Sizing.ButtonMinWidth,
        LabelWeight = FontWeight.SemiBold,
        LabelTracking = 0.1f,
        OutlineWidth = 1,
        LinkPaddingX = 6,
        OutlineColor = BorderStrong,
        QuietFill = SurfaceSubtle,
    };

    // Navigation and collections (E6d): what each component drew, now the design system's. A colour is
    // the role the component chose, cached on first read since a role is an instance member.

    /// <summary>The navigation family (spec B3, B4, C16): a 48dp tab strip under a 3dp indicator inset 16,
    /// a 56dp bar of 56 by 26 pills, an 80dp rail of 52 by 30 pills with a 1dp trailing edge, a 56dp app
    /// bar, 30dp page cells and 6dp dots.</summary>
    public NavigationTheme? Navigation => field ??= new NavigationTheme
    {
        InactiveColor = TextMuted,
        QuietFill = SurfaceSubtle,
        Background = Surface,
        TabHeight = 48,
        TabLabelSize = 14,
        TabLabelLineHeight = 18,
        TabLabelWeight = FontWeight.SemiBold,
        TabSelectedLabelWeight = FontWeight.Bold,
        TabLabelTracking = 0.1f,
        IndicatorThickness = 3,
        IndicatorInset = Space.S4,
        IndicatorRadius = 2,
        PillRadius = Radius.Full,
        PillLabelGap = 2,
        BarHeight = 56,
        BarIconSize = IconSize.Md,
        BarPillWidth = 56,
        BarPillHeight = 26,
        BarLabelSize = 11,
        BarLabelLineHeight = 14,
        BarLabelWeight = FontWeight.Bold,
        BarLabelTracking = 0.1f,
        RailWidth = 80,
        RailPaddingY = Space.S3,
        RailGap = Space.S2,
        RailItemHeight = 56,
        RailIconSize = IconSize.Dense,
        RailPillWidth = 52,
        RailPillHeight = 30,
        RailSelectedLabelSize = 12,
        RailSelectedLabelLineHeight = 16,
        RailSelectedLabelWeight = FontWeight.Bold,
        RailBorderWidth = 1,
        RailBorderColor = Border,
        AppBarHeight = 56,
        AppBarPaddingX = 4,
        TitleSize = 20,
        TitleLineHeight = 26,
        TitleWeight = FontWeight.SemiBold,
        TitleColor = TextPrimary,
        TitleInset = Space.S3,
        TitleGap = Space.S2,
        ScrolledElevation = 2,
        CrumbGap = Space.S1,
        CrumbColor = TextSecondary,
        CurrentCrumbColor = TextPrimary,
        SeparatorSize = IconSize.Sm,
        SeparatorColor = BorderStrong,
        PageCellSize = 30,
        PageGap = 3,
        PaginationGap = Space.S2,
        PageRadius = Radius.Sm,
        PageColor = TextSecondary,
        EllipsisColor = TextMuted,
        DotSize = 6,
        CurrentDotWidth = 18,
        DotGap = Space.S1,
        DotRadius = Radius.Full,
        DotColor = BorderStrong,
    };

    /// <summary>The list family (spec A7, B2): 52, 68 and 88dp rows padded 16 by 8 with 12 between their
    /// slots, a 15/20 medium title, a 1dp Border hairline inset 16, an accordion padded 12, and a 96dp swipe
    /// action.</summary>
    public ListTheme? List => field ??= new ListTheme
    {
        QuietFill = SurfaceSubtle,
        ItemHeight = 52,
        TwoLineItemHeight = 68,
        ThreeLineItemHeight = 88,
        ItemPaddingX = Space.S4,
        ItemPaddingY = Space.S2,
        ItemGap = Space.S3,
        TextGap = 2,
        TitleSize = 15,
        TitleLineHeight = 20,
        TitleWeight = FontWeight.Medium,
        TitleColor = TextPrimary,
        SubtitleColor = TextSecondary,
        DividerThickness = 1,
        DividerColor = Border,
        DividerInset = Space.S4,
        HeaderHeight = Sizing.Height(SizeVariant.Large),
        SectionPaddingX = Space.S3,
        SectionPaddingBottom = Space.S3,
        HeaderGap = Space.S2,
        ChevronSize = IconSize.Sm,
        ChevronColor = TextSecondary,
        SwipeActionWidth = 96,
        SwipeActionGap = 3,
        SwipeIconSize = IconSize.Dense,
        SwipeSurface = Surface,
    };

    /// <summary>The table family: cells padded 12 by 8, a Table's 44dp rows 12 apart, and a DataTable's
    /// 52dp bordered rows under a filled header in capitals.</summary>
    public TableTheme? Table => field ??= new TableTheme
    {
        CellPaddingX = Space.S3,
        CellPaddingY = Space.S2,
        HoverFill = SurfaceSubtle,
        ColumnGap = Space.S3,
        RowMinHeight = 44,
        TableHeaderColor = TextSecondary,
        DataRowHeight = 52,
        SelectionColumnWidth = 44,
        HeaderFill = SurfaceSubtle,
        DataHeaderColor = TextMuted,
        SortedHeaderColor = TextPrimary,
        HeaderUppercase = true,
        HeaderIconGap = Space.S1,
        SortIconSize = IconSize.Sm,
        BorderWidth = 1,
        BorderColor = Border,
    };

    /// <summary>The badge family (spec B6, B7): an 8dp dot and a 16dp count ringed 2dp in Surface, the
    /// avatar's own ladder, and an empty state's 64dp well over a 20/26 semi-bold title.</summary>
    public BadgeTheme? Badge => field ??= new BadgeTheme
    {
        RingColor = Surface,
        RingWidth = 2,
        DotSize = 8,
        CountHeight = 16,
        CountPaddingX = Space.S1,
        CountLabelSize = 10,
        CountLabelLineHeight = 12,
        CountLabelWeight = FontWeight.Bold,
        NeutralFill = SurfaceSubtle,
        NeutralColor = TextSecondary,
        AvatarSize = BySize(Sizing.Avatar),
        AvatarLabelSize = BySize(Sizing.AvatarInitials),
        AvatarLabelWeight = FontWeight.SemiBold,
        AvatarGlyphSize = new SizeLadder { Small = IconSize.Sm, Medium = IconSize.Dense, Large = IconSize.Md, XLarge = IconSize.Lg },
        AvatarFill = SurfaceSubtle,
        AvatarGlyphColor = TextMuted,
        OfflineColor = TextMuted,
        WellSize = 64,
        WellFill = SurfaceSubtle,
        WellIconSize = IconSize.Lg,
        WellIconColor = TextMuted,
        EmptyPaddingX = Space.S4,
        EmptyPaddingY = Space.S12,
        WellGap = Space.S4,
        BodyGap = 6,
        ActionsGap = Space.S5,
        ActionSpacing = Space.S2,
        EmptyTitleSize = 20,
        EmptyTitleLineHeight = 26,
        EmptyTitleWeight = FontWeight.SemiBold,
        EmptyTitleColor = TextPrimary,
        EmptyBodyColor = TextSecondary,
    };

    /// <summary>The calendar family (spec C15): 44dp day cells under a 28dp row of weekday names, a 1.5dp
    /// ring for today, and the DatePicker's panel at the medium corner, elevation 2, padded 12.</summary>
    public CalendarTheme? Calendar => field ??= new CalendarTheme
    {
        CellSize = 44,
        WeekdayHeight = 28,
        HeaderGap = Space.S1,
        Gap = Space.S2,
        TitleRole = TypeRole.TitleSmall,
        TitleColor = TextPrimary,
        WeekdayRole = TypeRole.Caption,
        WeekdayColor = TextMuted,
        DayRole = TypeRole.BodyM,
        DayColor = TextPrimary,
        DayHoverFill = SurfaceSubtle,
        TodayRingWidth = 1.5f,
        PanelFill = Surface,
        PanelRadius = Radius.Md,
        PanelBorderWidth = 1,
        PanelBorderColor = Border,
        PanelElevation = 2,
        PanelPadding = Space.S3,
        OpenerIconSize = IconSize.Dense,
        OpenerColor = TextSecondary,
    };

    /// <summary>The rendered document: headings on the type scale's heading roles, prose in BodyM 14
    /// apart, quotes behind a 3dp rule, lists 8 apart, and tables at a 12dp corner.</summary>
    public MarkdownTheme? Markdown => field ??= new MarkdownTheme
    {
        Heading1 = TypeRole.Heading,
        Heading2 = TypeRole.Title,
        Heading3 = TypeRole.TitleSmall,
        Heading4 = TypeRole.Label,
        Body = TypeRole.BodyM,
        BlockGap = 14,
        CodeLineNumbers = false,
        CodeInverse = false,
        HeadingSpace = 10,
        SubheadingSpace = 6,
        HeadingColor = TextPrimary,
        BodyColor = TextSecondary,
        InlineCodeMaxSize = 13.5f,
        InlineCodeColor = TextPrimary,
        QuoteIndent = Space.S4,
        QuotePaddingY = 2,
        QuoteRuleWidth = 3,
        QuoteRuleColor = BorderStrong,
        ListGap = Space.S2,
        MarkerGap = 10,
        ListIndent = Space.S5,
        MarkerColor = TextMuted,
        TableRadius = 12,
        TableBorderWidth = 1,
        TableBorderColor = Border,
        TableHeaderFill = SurfaceSubtle,
        TableCellPaddingX = 14,
        TableHeaderPaddingY = 10,
        TableCellPaddingY = 11,
    };

    private static SizeLadder BySize(Func<SizeVariant, float> rung) => new()
    {
        Small = rung(SizeVariant.Small),
        Medium = rung(SizeVariant.Medium),
        Large = rung(SizeVariant.Large),
        XLarge = rung(SizeVariant.XLarge),
    };

    private static DensityLadder ByDensity(Func<SizeVariant, Density, float> rung) => new()
    {
        Comfortable = BySize(size => rung(size, Density.Comfortable)),
        Compact = BySize(size => rung(size, Density.Compact)),
    };

    // ---- helpers ----------------------------------------------------------------------------------
    private static ColorToken Token(uint lightRgb, uint darkRgb) => new(Rgb(lightRgb), Rgb(darkRgb));

    private static Color Rgb(uint rgb) =>
        Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static Color Rgba(uint rgb, byte alpha) =>
        Color.FromRgba((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);
}
