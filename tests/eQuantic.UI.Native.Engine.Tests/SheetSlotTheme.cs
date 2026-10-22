using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>A theme that answers a spreadsheet slot, and is <paramref name="inner"/> in everything else.</summary>
internal sealed class SheetSlotTheme(IAppTheme inner, SheetTheme sheet) : IAppTheme
{
    public SheetTheme? Sheet => sheet;

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
    public float DisabledOpacity => inner.DisabledOpacity;
    public VariantColors Colors(Variant variant) => inner.Colors(variant);
    public TypeStyle Type(TypeRole role) => inner.Type(role);
    public ShadowSpec Elevation(int level) => inner.Elevation(level);
    public float Shape(ShapeScale scale) => inner.Shape(scale);
}
