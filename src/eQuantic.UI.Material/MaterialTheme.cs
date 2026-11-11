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

    /// <summary>
    /// M3's icon button: a fully rounded 40dp container holding a 24dp icon (Icon buttons, Specs), 20dp
    /// at the extra-small 32dp container, and a 1dp outline.
    /// </summary>
    public IconButtonTheme? IconButton { get; } = new()
    {
        IconSize = new SizeLadder { Small = 20, Medium = 24, Large = 24, XLarge = 24 },
        Radius = 999,
        OutlineWidth = 1,
    };

    /// <summary>
    /// M3's text field (Text fields, Specs): a 56dp container with an extra-small (4dp) corner, a 1dp
    /// outline that grows to 2dp while focused, 16dp of padding and of gap to a 24dp icon, and the
    /// supporting text 4dp below. The search field is M3's search bar, 56dp and fully rounded, and a
    /// picker's field is the text field its menu anchors to.
    /// </summary>
    public InputTheme? Input { get; } = new()
    {
        Controls = new ControlsTheme
        {
            Height = new DensityLadder { Comfortable = new SizeLadder { Medium = 56, Large = 56, XLarge = 56 } },
        },
        PaddingX = 16,
        PickerPaddingX = 16,
        Gap = 16,
        PickerGap = 16,
        BorderWidth = 1,
        FocusBorderWidth = 2,
        Radius = 4,
        SearchRadius = 999,
        IconSize = 24,
        CaptionGap = 4,
    };

    /// <summary>
    /// M3's selection controls (Checkbox, Radio button, Switch and Slider, Specs): an 18dp checkbox with
    /// a 2dp border and a 2dp corner, a 20dp radio around a 10dp dot, each in a 48dp row; a 52 by 32
    /// switch whose 24dp handle sits 4dp inside the track; and a slider's 4dp track under a 20dp handle
    /// at elevation 1, with no ring. Material 3 does not shrink them for a dense layout, so the compact
    /// side is the same.
    /// </summary>
    public SelectionControlsTheme? SelectionControls { get; } = new()
    {
        Box = new DensityPair { Comfortable = 18, Compact = 18 },
        RadioSize = new DensityPair { Comfortable = 20, Compact = 20 },
        BorderWidth = 2,
        CheckboxRadius = 2,
        RadioDot = new DensityPair { Comfortable = 10, Compact = 10 },
        RadioRowHeight = 48,
        SwitchWidth = new DensityPair { Comfortable = 52, Compact = 52 },
        SwitchHeight = new DensityPair { Comfortable = 32, Compact = 32 },
        SwitchThumb = new DensityPair { Comfortable = 24, Compact = 24 },
        SwitchInset = 4,
        SliderTrackHeight = 4,
        SliderThumbSize = 20,
        SliderThumbBorder = 0,
        SliderThumbElevation = 1,
    };

    /// <summary>
    /// M3's segmented button (Segmented buttons, Specs): 40dp tall, fully rounded, its label in Label
    /// large (14), 12dp of padding, flat, with no lip around the selected segment and a 1dp outline;
    /// its 18dp icons take the §07 whitelist's next step, 20.
    /// </summary>
    public SegmentedTheme? Segmented { get; } = new()
    {
        Controls = new ControlsTheme
        {
            Height = new DensityLadder { Comfortable = new SizeLadder { Medium = 40 } },
            PaddingX = new DensityLadder { Comfortable = new SizeLadder { Medium = 12 } },
            LabelSize = new DensityLadder { Comfortable = new SizeLadder { Medium = 14 } },
            Radius = new SizeLadder { Small = 999, Medium = 999, Large = 999, XLarge = 999 },
        },
        Inset = 0,
        ThumbElevation = 0,
        BorderWidth = 1,
        IconSize = IconSize.Dense,
    };

    /// <summary>
    /// M3's chip (Chips, Specs): 32dp tall with a small (8dp) corner, 16dp of padding and 8dp between
    /// its icons and its label in Label large; a selected filter chip is filled rather than outlined.
    /// M3 draws the icons at 18dp, which is not on the §07 whitelist an icon is held to, so they take
    /// its next step, 20.
    /// </summary>
    public ChipTheme? Chip { get; } = new()
    {
        Controls = new ControlsTheme
        {
            Height = new DensityLadder { Comfortable = new SizeLadder { Small = 32 } },
        },
        PaddingX = 16,
        Gap = 8,
        LabelSize = 14,
        LabelLineHeight = 20,
        LabelWeight = FontWeight.Medium,
        LabelTracking = 0.1f,
        Radius = 8,
        SelectedBorderWidth = 0,
        IconSize = IconSize.Dense,
        RemoveIconSize = IconSize.Dense,
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
