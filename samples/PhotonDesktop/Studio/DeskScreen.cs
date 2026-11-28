using eQuantic.UI.Primitives;

namespace eQuantic.Studio;

/// <summary>
/// A desk and the textures that sit behind a surface (#1937): the same box paints repeating artwork
/// behind its content, from a vector drawing or an image, and every layer follows the theme.
/// <para>
/// Each section is one thing to look at, in light and in dark, with the theme toggled live and under
/// more contrast: a vector tile repeated at a width, an image tile with its dark source, a tile
/// tinted by a token that differs per mode, a tile attached to the window while its box scrolls, and
/// two layers (one spaced, one fitted) and a grid, each in a bordered, rounded box (#1939).
/// </para>
/// <para>
/// The web dashboard draws the same desk at <c>/desk</c>. Here the image tiles are inline PNGs
/// (<see cref="DeskTiles"/>), the source Photon's image loader decodes, and the window-attached tile
/// lives in a scroller of its own.
/// </para>
/// </summary>
public sealed class DeskScreen : StatelessComponent
{
    // Cream under the light page, a warm charcoal under the dark one: the desk's ground.
    private static readonly ColorToken Desk = new(Color.FromRgb(0xF4, 0xEC, 0xDD), Color.FromRgb(0x24, 0x21, 0x1D));

    // An ink that differs by mode: a deep teal on the light page, an amber on the dark one.
    private static readonly ColorToken Ink = new(Color.FromRgb(0x1F, 0x6F, 0x6B), Color.FromRgb(0xE8, 0xA8, 0x4C));

    // The image tiles, 360 px for a 120 dp tile: crisp at 3x.
    private static readonly string PaperLight = DeskTiles.PaperLight;
    private static readonly string PaperDark = DeskTiles.PaperDark;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;

        // The web dashboard's layout frames this page; here it is the whole window, so it scrolls itself.
        return ScrollView(Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S5),
            Background = theme.Background,
        }, Column(gap: Space.S6, children: [
            Text("The desk behind the sheets", TypeRole.Heading, theme.TextPrimary),
            Text("Each box below paints repeating artwork behind its content. Switch the theme, move the window "
                + "to a display of another scale, scroll: every layer follows.", TypeRole.BodyM, theme.TextSecondary),
            VectorDesk(theme),
            ImageTile(theme),
            TintedTile(theme),
            WindowAttached(theme),
            TwoLayers(theme),
            GridInABorder(theme),
        ])), height: SizeValue.Fill);
    }

    /// <summary>(a) The desk: a vector tile repeated at a width, quiet behind two sheets.</summary>
    private static VisualNode VectorDesk(IAppTheme theme) => Section(theme, "Vector tile, repeated at a width",
        "DrawingPattern(DeskTiles.Tile, 330): 330 dp wide, as tall as the drawing's shape makes it.",
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 300,
            Padding = EdgeInsets.All(Space.S5),
            Background = Desk,
            CornerRadius = new CornerRadii(Radius.Md),
            Patterns = [new DrawingPattern(DeskTiles.Tile, 330) { Opacity = 0.35f }],
        }, Row(gap: Space.S4, children: [Sheet(theme, "Invoice 0412"), Sheet(theme, "Receipt 0089")])));

    /// <summary>(b) An image tile whose dark source is another file.</summary>
    private static VisualNode ImageTile(IAppTheme theme) => Section(theme, "Image tile with a dark source",
        "ImagePattern(DeskTiles.PaperLight, 120) with DarkSource: a teal ink on the light page, an amber one on the dark.",
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 240,
            Padding = EdgeInsets.All(Space.S5),
            Background = Desk,
            CornerRadius = new CornerRadii(Radius.Md),
            Patterns = [new ImagePattern(PaperLight, 120) { DarkSource = PaperDark }],
        }, Sheet(theme, "Paper")));

    /// <summary>(c) A tile whose currentColor shapes take a token that differs between light and dark.</summary>
    private static VisualNode TintedTile(IAppTheme theme) => Section(theme, "Tinted vector tile",
        "DrawingPattern(DeskTiles.Ink, 96, tint: Ink): the currentColor shapes are teal in light and amber in dark.",
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 240,
            Padding = EdgeInsets.All(Space.S5),
            Background = Desk,
            CornerRadius = new CornerRadii(Radius.Md),
            Patterns = [new DrawingPattern(DeskTiles.Ink, 96, tint: Ink) { Opacity = 0.7f }],
        }, Sheet(theme, "Ink")));

    /// <summary>(d) A tile attached to the window, on a box taller than the scroller that holds it.</summary>
    private static VisualNode WindowAttached(IAppTheme theme)
    {
        var rows = new List<VisualNode>();
        for (var i = 1; i <= 14; i++) rows.Add(Sheet(theme, $"Row {i}: the tiles stay still while this scrolls"));

        return Section(theme, "Window-attached tile in a scroller",
            "Attachment = TileAttachment.Window: scroll the frame, the tiles stay where the window put them and the box moves over them.",
            Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                BorderWidth = 1,
                BorderColor = theme.Border,
                CornerRadius = new CornerRadii(Radius.Md),
                Clip = true,
            }, ScrollView(Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Padding = EdgeInsets.All(Space.S4),
                Background = Desk,
                Patterns = [new DrawingPattern(DeskTiles.Tile, 200) { Attachment = TileAttachment.Window, Opacity = 0.5f }],
            }, Column(gap: Space.S3, children: [.. rows])), height: 300)));
    }

    /// <summary>(e) Two layers in a bordered, rounded box: a fitted tile under a spaced one.</summary>
    private static VisualNode TwoLayers(IAppTheme theme) => Section(theme, "Two layers, spaced and fitted",
        "The first layer is Fitted across and down (a whole number of tiles fills the padding area), the second is Spaced "
        + "in the content area and clipped to the padding area. Border 6, corner 28.",
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 280,
            Padding = EdgeInsets.All(Space.S6),
            Background = Desk,
            BorderWidth = 6,
            BorderColor = Ink,
            CornerRadius = new CornerRadii(28),
            Patterns =
            [
                new DrawingPattern(DeskTiles.Tile, 120)
                {
                    HorizontalRepeat = TileRepeat.Fitted,
                    VerticalRepeat = TileRepeat.Fitted,
                    Opacity = 0.3f,
                },
                new DrawingPattern(DeskTiles.Ink, 40, tint: Ink)
                {
                    HorizontalRepeat = TileRepeat.Spaced,
                    VerticalRepeat = TileRepeat.Spaced,
                    Area = BoxArea.Content,
                    ClipTo = BoxArea.Padding,
                },
            ],
        }, Sheet(theme, "Over both layers")));

    /// <summary>(e, #1939) A grid in a bordered, rounded box: placed inside the border, clipped to the corners.</summary>
    private static VisualNode GridInABorder(IAppTheme theme) => Section(theme, "A grid in a bordered, rounded box",
        "GridPattern(24): its rules start inside the border and stop at the rounded corners, on every target.",
        Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = 200,
            Padding = EdgeInsets.All(Space.S5),
            Background = Desk,
            BorderWidth = 6,
            BorderColor = Ink,
            CornerRadius = new CornerRadii(28),
            Patterns = [new GridPattern(24, theme.BorderStrong)],
        }, Sheet(theme, "On the grid")));

    private static VisualNode Section(IAppTheme theme, string title, string note, VisualNode content) =>
        Column(gap: Space.S2, children: [
            Text(title, TypeRole.Title, theme.TextPrimary),
            Text(note, TypeRole.Caption, theme.TextSecondary),
            content,
        ]);

    /// <summary>A sheet of paper lying on the desk.</summary>
    private static VisualNode Sheet(IAppTheme theme, string text) =>
        Box(new BoxStyle
        {
            Padding = EdgeInsets.Symmetric(Space.S4, Space.S3),
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(Radius.Sm),
        }, Text(text, TypeRole.BodyM, theme.TextPrimary, maxLines: 1));
}
