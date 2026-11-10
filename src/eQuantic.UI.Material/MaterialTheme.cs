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
    /// tracking 0.1), 24dp of padding, an 18dp icon and an 8dp gap at the medium size it specifies, a 1dp
    /// outline, and a text button's 12dp padding for the link variant.
    /// </summary>
    public ButtonTheme? Button { get; } = new()
    {
        Controls = new ControlsTheme
        {
            PaddingX = new DensityLadder { Comfortable = new SizeLadder { Medium = 24 } },
            LabelSize = new DensityLadder { Comfortable = new SizeLadder { Medium = 14 } },
            Gap = new SizeLadder { Medium = 8 },
            Icon = new SizeLadder { Medium = 18 },
            Radius = new SizeLadder { Small = 999, Medium = 999, Large = 999, XLarge = 999 },
        },
        LabelWeight = FontWeight.Medium,
        LabelTracking = 0.1f,
        OutlineWidth = 1,
        LinkPaddingX = 12,
    };

    // The slots below hold only what M3 specifies for the component; a member it says nothing about
    // stays null, so the component keeps its own value (a toast's status dot, a skeleton, the cookie
    // consent). The ones that carry a colour read this theme's scheme, so they are built per instance.

    /// <summary>
    /// M3's dialog (Dialogs, Specs): extra-large corners, elevation level 3, 24dp of padding, 16dp from
    /// the headline to the supporting text, 24dp to the actions and 8dp between them, at most 560dp
    /// wide. Its bottom sheet: extra-large top corners, level 1, a 32 by 4dp drag handle in
    /// on-surface-variant at 40%. Its modal navigation drawer: level 1, 12dp of padding.
    /// </summary>
    public SurfaceTheme? Surfaces => field ??= new()
    {
        DialogMaxWidth = 560,
        DialogShape = ShapeScale.ExtraLarge,
        DialogElevation = 3,
        DialogPadding = EdgeInsets.All(24),
        DialogGap = 24,
        DialogTextGap = 16,
        DialogActionGap = 8,
        BottomSheetShape = ShapeScale.ExtraLarge,
        BottomSheetElevation = 1,
        HandleWidth = 32,
        HandleHeight = 4,
        HandleColor = _colors.TextSecondary.WithOpacity(0.4f),
        DrawerElevation = 1,
        DrawerPadding = EdgeInsets.All(12),
    };

    /// <summary>
    /// M3's menu (Menus, Specs): extra-small corners, no outline, elevation level 2, at least 112dp wide,
    /// 8dp above and below its items, 12dp at an item's sides and between its icon and its words. Its
    /// plain tooltip: extra-small corners, 8 by 4dp of padding, inverse-on-surface on inverse-surface.
    /// </summary>
    public MenuTheme? Menu => field ??= new()
    {
        PanelShape = ShapeScale.ExtraSmall,
        PanelBorderWidth = 0,
        PanelElevation = 2,
        MenuMinWidth = 112,
        MenuPadding = EdgeInsets.Symmetric(0, 8),
        ItemPadding = EdgeInsets.Symmetric(12, 0),
        ItemGap = 12,
        TooltipShape = ShapeScale.ExtraSmall,
        TooltipPadding = EdgeInsets.Symmetric(8, 4),
        TooltipFill = _colors.InverseSurface,
        TooltipText = _colors.TextInverse,
    };

    /// <summary>
    /// M3's snackbar (Snackbar, Specs): inverse-on-surface on inverse-surface, extra-small corners,
    /// elevation level 3, 16dp at the sides and 14dp above and below. Its banner's words: title small
    /// over body medium (14 over 20, medium then regular), 16dp after the leading icon.
    /// </summary>
    public NoticeTheme? Notice => field ??= new()
    {
        ToastFill = _colors.InverseSurface,
        ToastText = _colors.TextInverse,
        ToastShape = ShapeScale.ExtraSmall,
        ToastElevation = 3,
        ToastPadding = EdgeInsets.Symmetric(16, 14),
        BannerIconGap = 16,
        BannerTextSize = 14,
        BannerLineHeight = 20,
        BannerTitleWeight = FontWeight.Medium,
        BannerBodyWeight = FontWeight.Regular,
    };

    /// <summary>
    /// M3's linear progress indicator (Progress indicators, Specs): a 4dp track, 8dp when it is the
    /// thick one, fully rounded, on secondary-container.
    /// </summary>
    public ProgressTheme? Progress => field ??= new()
    {
        BarHeight = 4,
        ProminentBarHeight = 8,
        BarShape = ShapeScale.Full,
        TrackColor = _colors.Secondary.Subtle,
    };

    /// <summary>
    /// M3's cards (Cards, Specs): medium corners, a 1dp outline-variant border on the outlined one,
    /// elevation level 1 and no border on the elevated one in either mode, surface-container-highest
    /// under the filled one.
    /// </summary>
    public CardTheme? Card => field ??= new()
    {
        Shape = ShapeScale.Medium,
        BorderWidth = 1,
        Elevation = 1,
        FilledColor = _colors.SurfaceContainerHighest,
        ElevatedBorderColor = new ColorToken(Color.Transparent),
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
