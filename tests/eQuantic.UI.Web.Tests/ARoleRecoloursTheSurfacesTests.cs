using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A surface's slot colour that one of the theme's roles already gives is left null by the SDK's themes
/// (E6c, #1243, on E6b's rule), so the surface reads the role: a theme laid over <see cref="PhotonTheme"/>
/// that recolours the role recolours the tooltip, the toast, the progress track, the skeleton, the filled
/// card and the sheet's handle. A copy of the role taken into the slot would keep Photon's colour.
/// </summary>
public class ARoleRecoloursTheSurfacesTests
{
    private static readonly ColorToken Ink = new(Color.FromRgb(0x12, 0x34, 0x56));
    private const string InkCss = "#123456";

    public sealed record Case(string Name, string Role, Func<VisualNode> Node)
    {
        public override string ToString() => $"{Name} reads {Role}";
    }

    public static TheoryData<Case> Cases => new()
    {
        new Case("the tooltip's fill", nameof(IAppTheme.TextPrimary), () => new Tooltip(new Box(), "Saved")),
        new Case("the tooltip's words", nameof(IAppTheme.TextInverse), () => new Tooltip(new Box(), "Saved")),
        new Case("the toast's fill", nameof(IAppTheme.TextPrimary), () => new Toast("Saved")),
        new Case("the toast's words", nameof(IAppTheme.TextInverse), () => new Toast("Saved")),
        new Case("the progress track", nameof(IAppTheme.SurfaceSubtle), () => new ProgressBar(0.5f)),
        new Case("the skeleton's fill", nameof(IAppTheme.SurfaceSubtle), () => new Skeleton(SkeletonShape.Line, 120)),
        new Case("the skeleton's glint", nameof(IAppTheme.SurfaceHighlight), () => new Skeleton(SkeletonShape.Line, 120)),
        new Case("the filled card", nameof(IAppTheme.SurfaceSubtle), () => new Card(new Box(), CardKind.Filled)),
        new Case("the elevated card's dark border", nameof(IAppTheme.Border), () => new Card(new Box())),
        new Case("the sheet's handle", nameof(IAppTheme.BorderStrong), () => new BottomSheet(new Box())),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ARecolouredRole_ReachesTheSurface(Case c)
    {
        Lowered(c.Node(), PhotonTheme.Instance).Should().NotContain(InkCss, "Photon draws {0} in its own colour", c.Name);

        Lowered(c.Node(), new RecolouredTheme(PhotonTheme.Instance, c.Role)).Should().Contain(InkCss,
            "{0} reads {1}, so a theme that recolours the role recolours it", c.Name, c.Role);
    }

    private static string Lowered(VisualNode node, IAppTheme theme)
    {
        var sink = new StyleSink();
        var html = HtmlRenderer.RenderNode(WebRealizer.Lower(node, theme, 1f, sink).Render());
        return html + sink.Css;
    }

    /// <summary>The wrapped theme with one role answered in <see cref="Ink"/>, every other member forwarded.</summary>
    private sealed class RecolouredTheme(IAppTheme inner, string role) : IAppTheme
    {
        private ColorToken Role(string name, ColorToken own) => name == role ? Ink : own;

        public ColorToken Background => Role(nameof(Background), inner.Background);
        public ColorToken Surface => Role(nameof(Surface), inner.Surface);
        public ColorToken SurfaceSubtle => Role(nameof(SurfaceSubtle), inner.SurfaceSubtle);
        public ColorToken SurfaceHighlight => Role(nameof(SurfaceHighlight), inner.SurfaceHighlight);
        public ColorToken Border => Role(nameof(Border), inner.Border);
        public ColorToken BorderStrong => Role(nameof(BorderStrong), inner.BorderStrong);
        public ColorToken TextPrimary => Role(nameof(TextPrimary), inner.TextPrimary);
        public ColorToken TextSecondary => Role(nameof(TextSecondary), inner.TextSecondary);
        public ColorToken TextMuted => Role(nameof(TextMuted), inner.TextMuted);
        public ColorToken TextInverse => Role(nameof(TextInverse), inner.TextInverse);
        public ColorToken FocusRing => inner.FocusRing;
        public ColorToken LinkColor => inner.LinkColor;
        public ColorToken Scrim => inner.Scrim;
        public ColorToken? OnSelection => inner.OnSelection;
        public FontFamily? MonoFamily => inner.MonoFamily;
        public FontFamily? TextFamily => inner.TextFamily;
        public SheetTheme? Sheet => inner.Sheet;
        public ControlsTheme? Controls => inner.Controls;
        public ButtonTheme? Button => inner.Button;
        public IconButtonTheme? IconButton => inner.IconButton;
        public InputTheme? Input => inner.Input;
        public SelectionControlsTheme? SelectionControls => inner.SelectionControls;
        public SegmentedTheme? Segmented => inner.Segmented;
        public ChipTheme? Chip => inner.Chip;
        public ModalTheme? Modal => inner.Modal;
        public MenuTheme? Menu => inner.Menu;
        public NoticeTheme? Notice => inner.Notice;
        public ProgressTheme? Progress => inner.Progress;
        public CardTheme? Card => inner.Card;
        public ColorToken Code(CodeTokenKind kind) => inner.Code(kind);
        public float DisabledOpacity => inner.DisabledOpacity;
        public VariantColors Colors(Variant variant) => inner.Colors(variant);
        public TypeStyle Type(TypeRole role) => inner.Type(role);
        public ShadowSpec Elevation(int level) => inner.Elevation(level);
        public float Shape(ShapeScale scale) => inner.Shape(scale);
    }
}
