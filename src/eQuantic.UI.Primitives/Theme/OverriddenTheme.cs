namespace eQuantic.UI.Primitives;

/// <summary>
/// The theme <see cref="AppThemeExtensions.With"/> answers: the slots it was given, and every other member of the
/// theme it wraps, forwarded. Every member is declared here, the interface's defaults included, so the
/// wrapped theme answers each one as it would alone; <c>OverriddenThemeTests</c> fails on a member of
/// <see cref="IAppTheme"/> this class leaves to the interface.
/// </summary>
internal sealed class OverriddenTheme(IAppTheme inner, ControlsTheme? controls, ButtonTheme? button) : IAppTheme
{
    public ColorToken Background => inner.Background;
    public ColorToken Surface => inner.Surface;
    public ColorToken SurfaceSubtle => inner.SurfaceSubtle;
    public ColorToken SurfaceHighlight => inner.SurfaceHighlight;
    public ColorToken Border => inner.Border;
    public ColorToken BorderStrong => inner.BorderStrong;
    public ColorToken TextPrimary => inner.TextPrimary;
    public ColorToken TextSecondary => inner.TextSecondary;
    public ColorToken TextMuted => inner.TextMuted;
    public ColorToken TextInverse => inner.TextInverse;
    public ColorToken FocusRing => inner.FocusRing;
    public ColorToken LinkColor => inner.LinkColor;
    public ColorToken Scrim => inner.Scrim;
    public ColorToken Selection => inner.Selection;
    public ColorToken? OnSelection => inner.OnSelection;
    public ColorToken Placeholder => inner.Placeholder;
    public ColorToken ScrollbarThumb => inner.ScrollbarThumb;
    public ColorToken ScrollbarTrack => inner.ScrollbarTrack;
    public VariantColors Colors(Variant variant) => inner.Colors(variant);
    public TypeStyle Type(TypeRole role) => inner.Type(role);
    public FontFamily? MonoFamily => inner.MonoFamily;
    public FontFamily? TextFamily => inner.TextFamily;
    public ShadowSpec Elevation(int level) => inner.Elevation(level);
    public float Shape(ShapeScale scale) => inner.Shape(scale);
    public float DisabledOpacity => inner.DisabledOpacity;
    public DataPalette Data => inner.Data;
    public SheetTheme? Sheet => inner.Sheet;
    public ControlsTheme? Controls => controls ?? inner.Controls;
    public ButtonTheme? Button => button ?? inner.Button;
    public ColorToken Code(CodeTokenKind kind) => inner.Code(kind);
}
