using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Goldens for the write-once core: ABSTRACT node trees (Box/Row/Column/Text/Pressable + tokens) laid
/// out by the C# flex engine and lowered by the native realizer. These trees are exactly what shared
/// components will produce — the same trees the web realizer must render equivalently (text renders
/// as placeholder bars until the W4 text stack lands; regenerating then is by design).
/// </summary>
public class AbstractNodeGoldenTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static void Run(string name, ThemeMode mode, int w, int h, VisualNode root)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(w, h);
        var builder = new DisplayListBuilder();
        builder.Clear(Theme.Background.Resolve(mode));
        PhotonRealizer.Realize(root, w, h, Theme, mode, builder);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, name);
    }

    private static Box Swatch(ColorToken color, SizeValue width, float height = 28) =>
        new(new BoxStyle { Width = width, Height = height, Background = color, CornerRadius = new CornerRadii(Radius.Sm) });

    [Fact]
    public void FlexGallery()
    {
        var primary = Theme.Colors(Variant.Primary);
        var mint = Theme.Colors(Variant.Success);

        var root = new Column(gap: Space.S3)
        {
            Padding = EdgeInsets.All(Space.S4),
            Width = SizeValue.Fill,
        };
        // fixed + Flexible(1) + Flexible(2)
        var flexRow = new Row(gap: Space.S2) { Width = SizeValue.Fill };
        flexRow.Add(Swatch(primary.Base, 56));
        flexRow.Add(new Flexible(Swatch(primary.Subtle, SizeValue.Fill), flex: 1));
        flexRow.Add(new Flexible(Swatch(mint.Subtle, SizeValue.Fill), flex: 2));
        root.Add(flexRow);
        // SpaceBetween
        var between = new Row(gap: 0) { Width = SizeValue.Fill, Main = MainAlign.SpaceBetween };
        between.Add(Swatch(mint.Base, 48));
        between.Add(Swatch(mint.Base, 48));
        between.Add(Swatch(mint.Base, 48));
        root.Add(between);
        // Spacer push-apart
        var spacerRow = new Row(gap: 0) { Width = SizeValue.Fill };
        spacerRow.Add(Swatch(primary.Base, 72));
        spacerRow.Add(new Spacer());
        spacerRow.Add(Swatch(primary.Pressed, 72));
        root.Add(spacerRow);
        // Column stretch (full-width child by default)
        root.Add(Swatch(Theme.SurfaceSubtle, SizeValue.Fill, height: 20));

        Run("abstract-flex-gallery", ThemeMode.Light, 320, 190, root);
    }

    private static VisualNode BuildCard()
    {
        var primary = Theme.Colors(Variant.Primary);

        var identity = new Column(gap: Space.S1);
        identity.Add(new Text("Ana Beatriz Nogueira", TypeRole.BodyM));
        identity.Add(new Text("Premium account · since 2021", TypeRole.Caption, Theme.TextMuted));

        var header = new Row(gap: Space.S3);
        header.Add(new Box(new BoxStyle
        {
            Width = 40, Height = 40,
            Background = primary.Subtle,
            CornerRadius = new CornerRadii(Radius.Full),
            BorderColor = primary.Base, BorderWidth = 2,
        }));
        header.Add(new Flexible(identity));

        var actions = new Row(gap: Space.S2);
        actions.Add(new Pressable(
            new Box(new BoxStyle
            {
                Width = 108, Height = 36,
                Background = primary.Base,
                CornerRadius = new CornerRadii(Radius.Md),
                Padding = EdgeInsets.Symmetric(Space.S3, 10),
            }, new Text("Transferir", TypeRole.Label, primary.OnBase, maxLines: 1)),
            onPressed: null) { Label = "Transferir" });
        actions.Add(new Box(new BoxStyle
        {
            Width = 96, Height = 36,
            CornerRadius = new CornerRadii(Radius.Md),
            BorderColor = Theme.BorderStrong, BorderWidth = 1,
            Padding = EdgeInsets.Symmetric(Space.S3, 10),
        }, new Text("Extrato", TypeRole.Label, Theme.TextPrimary, maxLines: 1)));

        var card = new Column(gap: Space.S3)
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
            Background = Theme.Surface,
            CornerRadius = new CornerRadii(Radius.Lg),
        };
        card.Add(new Text("Portfolio overview", TypeRole.Title));
        card.Add(header);
        card.Add(actions);

        return new Box(new BoxStyle { Padding = EdgeInsets.All(Space.S4), Width = SizeValue.Fill }, card);
    }

    [Fact]
    public void AbstractCard_Light() => Run("abstract-card-light", ThemeMode.Light, 340, 200, BuildCard());

    [Fact]
    public void AbstractCard_Dark() => Run("abstract-card-dark", ThemeMode.Dark, 340, 200, BuildCard());
}
