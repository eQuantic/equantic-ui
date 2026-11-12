namespace eQuantic.UI.Primitives;

/// <summary>
/// The theme <see cref="AppThemeExtensions.With"/> answers: the slots it was given laid over the wrapped theme's, and every other member of the
/// theme it wraps, forwarded. Every member is declared here, the interface's defaults included, so the
/// wrapped theme answers each one as it would alone; <c>OverriddenThemeTests</c> fails on a member of
/// <see cref="IAppTheme"/> this class leaves to the interface.
/// </summary>
internal sealed class OverriddenTheme(IAppTheme inner, ControlsTheme? controls, ButtonTheme? button,
    IconButtonTheme? iconButton, InputTheme? input, SelectionControlsTheme? selectionControls, SegmentedTheme? segmented,
    ChipTheme? chip, ModalTheme? modal, MenuTheme? menu, NoticeTheme? notice, ProgressTheme? progress, CardTheme? card) : IAppTheme
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
    // A slot it was given is laid over the wrapped theme's, member by member, once.
    public ControlsTheme? Controls => field ??= ControlsTheme.Merge(inner.Controls, controls);
    public ButtonTheme? Button => field ??= ButtonTheme.Merge(inner.Button, button);
    public ModalTheme? Modal => field ??= ModalTheme.Merge(inner.Modal, modal);
    public MenuTheme? Menu => field ??= MenuTheme.Merge(inner.Menu, menu);
    public NoticeTheme? Notice => field ??= NoticeTheme.Merge(inner.Notice, notice);
    public ProgressTheme? Progress => field ??= ProgressTheme.Merge(inner.Progress, progress);
    public CardTheme? Card => field ??= CardTheme.Merge(inner.Card, card);
    public ColorToken Code(CodeTokenKind kind) => inner.Code(kind);
    public IconButtonTheme? IconButton => field ??= IconButtonTheme.Merge(inner.IconButton, iconButton);
    public InputTheme? Input => field ??= InputTheme.Merge(inner.Input, input);
    public SelectionControlsTheme? SelectionControls => field ??= SelectionControlsTheme.Merge(inner.SelectionControls, selectionControls);
    public SegmentedTheme? Segmented => field ??= SegmentedTheme.Merge(inner.Segmented, segmented);
    public ChipTheme? Chip => field ??= ChipTheme.Merge(inner.Chip, chip);
}
