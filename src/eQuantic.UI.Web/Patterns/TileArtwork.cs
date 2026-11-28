using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The standalone SVG document a <see cref="DrawingPattern"/>'s tile is (#1937): the artwork on its
/// own grid, at the opacity the pattern asks, answering <c>currentColor</c> with the tint. The
/// runtime's twin is <c>tileArtwork</c> in lowering.ts.
/// <para>
/// The root carries <c>width</c> and <c>height</c> on the artwork's own grid, which gives the browser
/// the intrinsic ratio an <c>auto</c> length reads, and <c>preserveAspectRatio='none'</c>, so a tile
/// given both lengths stretches, as on Photon. A data URL is a document of its own, which reads no
/// variable of the page and no paint server outside it, so its gradients are declared inside it and
/// its <c>color</c> is the tint already resolved.
/// </para>
/// </summary>
internal static class TileArtwork
{
    /// <summary>The tile's document, with <paramref name="tint"/> answering <c>currentColor</c>; the
    /// colour is stated only when a shape inherits it, so artwork painted in its own colours is one
    /// document in every mode.</summary>
    public static RealizedElement Document(DrawingPattern pattern, Color tint)
    {
        var artwork = pattern.Artwork;
        var attributes = new Dictionary<string, string>
        {
            ["xmlns"] = "http://www.w3.org/2000/svg",
            ["viewBox"] = artwork.ViewBox,
            ["width"] = TokenCss.Number(artwork.Width),
            ["height"] = TokenCss.Number(artwork.Height),
            ["preserveAspectRatio"] = "none",
            ["fill"] = "none",
        };
        if (DrawingElements.Inherits(artwork)) attributes["color"] = WebRealizer.CssColor(tint);
        var svg = new RealizedElement("svg") { RawAttributes = attributes };

        var defs = new RealizedElement("defs");
        foreach (var gradient in WebRealizer.Gradients(artwork))
            defs.Children.Add(WebRealizer.GradientElement(gradient.Key, gradient.Value));
        if (defs.Children.Count > 0) svg.Children.Add(defs);

        // The shapes inside ONE group at the opacity, so they composite as one picture and an overlap
        // inside the artwork does not blend twice.
        var shapes = svg;
        if (pattern.Opacity < 1)
        {
            shapes = new RealizedElement("g")
            {
                RawAttributes = new Dictionary<string, string> { ["opacity"] = TokenCss.Number(pattern.Opacity) },
            };
            svg.Children.Add(shapes);
        }
        foreach (var path in DrawingElements.Shapes(artwork)) shapes.Children.Add(path);
        return svg;
    }
}
