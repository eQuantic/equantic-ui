namespace eQuantic.UI.Primitives;

/// <summary>Interaction states every control resolves against (spec A12 — Loading freezes width and swallows presses).</summary>
public enum InteractionState : byte
{
    Default = 0,
    Pressed = 1,
    Focused = 2,
    Disabled = 3,
    Loading = 4,
}

/// <summary>
/// A fully-resolved Button appearance — a plain struct on the primitive tree (zero allocation,
/// plan D9). Everything a realizer needs; nothing theme- or state-dependent left. Target-neutral:
/// the web realizer lowers it to DOM + CSS, the native realizer to Photon draw commands.
/// </summary>
public readonly record struct ButtonStyle
{
    // Metrics (spec A12 size table)
    public float Height { get; init; }
    public float PaddingX { get; init; }
    public float Gap { get; init; }
    public float LabelSize { get; init; }
    public FontWeight LabelWeight { get; init; }
    public float IconSize { get; init; }
    public float CornerRadius { get; init; }
    public float MinWidth { get; init; }
    public float HitTarget { get; init; }

    // Colors & chrome
    public Color Fill { get; init; }
    public Color TextColor { get; init; }
    public Color BorderColor { get; init; }
    public float BorderWidth { get; init; }
    public bool UnderlineLabel { get; init; }
    public bool ShowFocusRing { get; init; }
    public Color FocusRingColor { get; init; }
    public Color FocusRingGapColor { get; init; }

    /// <summary>1.0, or the theme's disabled opacity (0.38) — applied as a group over the whole control.</summary>
    public float Opacity { get; init; }
}

/// <summary>
/// Resolves the design system's Button (spec A12 — "the reference implementation of the
/// variant × size × state system; every other control follows its conventions").
/// </summary>
public static class ButtonStyles
{
    /// <summary>Buttons hug their label but never shrink below this (spec A12).</summary>
    public const float MinWidth = 64;

    public static ButtonStyle Resolve(IAppTheme theme, ThemeMode mode, Variant variant, SizeVariant size, InteractionState state)
    {
        var (height, padX, gap, labelSize, iconSize, radius, hit) = Metrics(size);
        var colors = theme.Colors(variant);
        var pressed = state == InteractionState.Pressed;

        // Fill: filled variants swap Base→Pressed (a token swap on the same rrect — no overlay);
        // Outline/Ghost gain a SurfaceSubtle fill only while pressed; Link never fills.
        var fill = colors.Base.Resolve(mode);
        if (pressed) fill = colors.Pressed.Resolve(mode);
        if (variant == Variant.Link) fill = Color.Transparent;

        // Content: OnBase — except Link pressed, which swaps the TEXT to Primary.Pressed (+ underline).
        var text = colors.OnBase.Resolve(mode);
        var underline = false;
        if (variant == Variant.Link && pressed)
        {
            text = theme.Colors(Variant.Primary).Pressed.Resolve(mode);
            underline = true;
        }

        // Border: Outline only — 1dp BorderStrong (uniform width, inside stroke per the fence).
        var borderWidth = variant == Variant.Outline ? 1f : 0f;
        var borderColor = variant == Variant.Outline ? theme.BorderStrong.Resolve(mode) : Color.Transparent;

        // Link is an inline-text control: minimal horizontal padding (spec A12 state table).
        if (variant == Variant.Link) padX = 6;

        return new ButtonStyle
        {
            Height = height,
            PaddingX = padX,
            Gap = gap,
            LabelSize = labelSize,
            LabelWeight = FontWeight.SemiBold,
            IconSize = iconSize,
            CornerRadius = radius,
            MinWidth = MinWidth,
            HitTarget = hit,
            Fill = fill,
            TextColor = text,
            BorderColor = borderColor,
            BorderWidth = borderWidth,
            UnderlineLabel = underline,
            ShowFocusRing = state == InteractionState.Focused,
            FocusRingColor = theme.FocusRing.Resolve(mode),
            FocusRingGapColor = theme.Surface.Resolve(mode),
            Opacity = state == InteractionState.Disabled ? theme.DisabledOpacity : 1f,
        };
    }

    /// <summary>The size table (spec A12): Height · PadX · Gap · Label · Icon · Radius · HitTarget.</summary>
    public static (float Height, float PadX, float Gap, float LabelSize, float IconSize, float Radius, float Hit) Metrics(SizeVariant size) => size switch
    {
        SizeVariant.Small => (32, Space.S3, 6, 13, IconSize.Sm, Radius.Md, Touch.MinTarget),
        SizeVariant.Medium => (40, Space.S4, Space.S2, 15, IconSize.Dense, Radius.Md, Touch.MinTarget),
        SizeVariant.Large => (48, Space.S5, Space.S2, 16, IconSize.Dense, Radius.Md, Touch.MinTarget),
        _ => (56, Space.S6, 10, 17, IconSize.Md, Radius.Lg, 56),
    };
}
