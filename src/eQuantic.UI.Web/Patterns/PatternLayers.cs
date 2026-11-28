using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// What each kind of <see cref="Pattern"/> writes into a box's CSS background (#1937): its layers,
/// as entries of the seven <c>background-*</c> lists. A grid is two layers, a hard-stop gradient for
/// each direction of rule; a tile is one, its image a data URL of the artwork or the raster's
/// <c>url()</c>. The state is the pattern's index in <see cref="BoxStyle.Patterns"/>, which names the
/// custom properties of a layer that varies.
/// </summary>
internal sealed class PatternLayers(IAppTheme theme, Dictionary<string, string> variants)
    : IPatternVisitor<int, IEnumerable<BackgroundLayer>>
{
    private static readonly (ThemeMode Mode, ContrastLevel Contrast)[] Legs =
    [
        (ThemeMode.Light, ContrastLevel.Standard), (ThemeMode.Light, ContrastLevel.More),
        (ThemeMode.Dark, ContrastLevel.Standard), (ThemeMode.Dark, ContrastLevel.More),
    ];

    public IEnumerable<BackgroundLayer> Visit(GridPattern pattern, int index)
    {
        var color = TokenCss.Value(pattern.Color);
        var line = TokenCss.Px(pattern.LineWidth);
        var cell = TokenCss.Px(pattern.Cell);
        foreach (var direction in (string[])["to right", "to bottom"])
            yield return new BackgroundLayer(
                $"linear-gradient({direction}, {color} {line}, transparent {line})",
                Size: $"{cell} {cell}",
                Origin: Area(pattern.Area),
                Clip: Area(pattern.ClipTo),
                Attachment: Attachment(pattern.Attachment));
    }

    public IEnumerable<BackgroundLayer> Visit(DrawingPattern pattern, int index)
    {
        var tint = pattern.Tint ?? theme.TextPrimary;
        // One document per colour: artwork painted in its own colours states none, and is one image.
        var documents = new Dictionary<Color, string>();
        string Image(ThemeMode mode, ContrastLevel contrast)
        {
            var color = DrawingElements.Inherits(pattern.Artwork) ? tint.Resolve(mode, contrast) : default;
            if (!documents.TryGetValue(color, out var url))
                documents[color] = url = DataUrl.Css(DataUrl.Svg(SvgMarkup.Of(TileArtwork.Document(pattern, color))));
            return url;
        }
        yield return Tile(pattern, Varying(index, Image));
    }

    public IEnumerable<BackgroundLayer> Visit(ImagePattern pattern, int index) =>
        [Tile(pattern, Varying(index, (mode, _) =>
            DataUrl.Css(mode == ThemeMode.Dark && pattern.DarkSource is { } dark ? dark : pattern.Source)))];

    private static BackgroundLayer Tile(ArtworkPattern pattern, string image) => new(
        image,
        Size: pattern.Fit switch
        {
            ImageFit.Contain => "contain",
            ImageFit.Cover => "cover",
            ImageFit.Stretch => "100% 100%",
            _ => $"{Length(pattern.Width)} {Length(pattern.Height)}",
        },
        Repeat: pattern.HorizontalRepeat == pattern.VerticalRepeat
            ? Repeat(pattern.HorizontalRepeat)
            : $"{Repeat(pattern.HorizontalRepeat)} {Repeat(pattern.VerticalRepeat)}",
        Position: $"{Position(pattern.Anchor.X, pattern.Offset.X)} {Position(pattern.Anchor.Y, pattern.Offset.Y)}",
        Origin: Area(pattern.Area),
        Clip: Area(pattern.ClipTo),
        Attachment: Attachment(pattern.Attachment));

    /// <summary>
    /// The layer's image, written directly when it is the same in every mode and contrast. When it
    /// varies, each variant is a custom property of the element that is valid only while the theme's
    /// toggles on <c>:root</c> select it (<c>var(--eq-on-dark)</c> is empty in dark and
    /// guaranteed-invalid in light), and the layer reads them through nested fallbacks: the palette's
    /// own decision picks the artwork, live, with no render (<see cref="ThemeStylesheet"/>).
    /// </summary>
    private string Varying(int index, Func<ThemeMode, ContrastLevel, string> image)
    {
        var images = Legs.Select(leg => image(leg.Mode, leg.Contrast)).ToArray();
        var byMode = images[0] != images[2] || images[1] != images[3];
        var byContrast = images[0] != images[1] || images[2] != images[3];
        if (!byMode && !byContrast) return images[0];

        var names = new List<string>(4);
        for (var leg = 0; leg < Legs.Length; leg++)
        {
            var (mode, contrast) = Legs[leg];
            if ((!byMode && mode == ThemeMode.Dark) || (!byContrast && contrast == ContrastLevel.More)) continue;
            var light = mode == ThemeMode.Light;
            var standard = contrast == ContrastLevel.Standard;
            var name = $"--eq-p{index}-{(byMode ? light ? "l" : "d" : "")}{(byContrast ? standard ? "s" : "m" : "")}";
            var toggles = (byMode ? $"var(--eq-on-{(light ? "light" : "dark")}) " : "")
                + (byContrast ? $"var(--eq-on-{(standard ? "std" : "more")}) " : "");
            variants[name] = toggles + images[leg];
            names.Add(name);
        }
        var chain = $"var({names[^1]})";
        for (var i = names.Count - 2; i >= 0; i--) chain = $"var({names[i]}, {chain})";
        return chain;
    }

    private static string Length(TileLength length) =>
        length.IsAuto ? "auto" : length.IsFraction ? TokenCss.Percent(length.Value) : TokenCss.Px(length.Value);

    private static string Position(float anchor, float offset) => offset switch
    {
        0 => TokenCss.Percent(anchor),
        > 0 => $"calc({TokenCss.Percent(anchor)} + {TokenCss.Px(offset)})",
        _ => $"calc({TokenCss.Percent(anchor)} - {TokenCss.Px(-offset)})",
    };

    private static string Repeat(TileRepeat repeat) => repeat switch
    {
        TileRepeat.Once => "no-repeat",
        TileRepeat.Spaced => "space",
        TileRepeat.Fitted => "round",
        _ => "repeat",
    };

    private static string Area(BoxArea area) => area switch
    {
        BoxArea.Border => "border-box",
        BoxArea.Content => "content-box",
        _ => "padding-box",
    };

    private static string Attachment(TileAttachment attachment) =>
        attachment == TileAttachment.Window ? "fixed" : "scroll";
}
