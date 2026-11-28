using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The elements a <see cref="VectorDrawing"/>'s shapes are drawn with in SVG, one <c>&lt;path&gt;</c>
/// per shape with the paint the artwork chose: shared by the inline <see cref="Drawing"/> and the
/// tile a <see cref="DrawingPattern"/> repeats (#1937), so the two cannot paint one artwork two ways.
/// The runtime's twin is <c>drawingShapes</c> in lowering.ts.
/// <para>
/// The shapes the artwork left as <c>currentColor</c> are written as <c>currentColor</c>, the word and
/// not a resolved value, so the colour of whatever holds them answers it: the wrapper's CSS colour
/// inline, the document's <c>color</c> in a tile.
/// </para>
/// </summary>
internal static class DrawingElements
{
    /// <summary>One element per shape, in the artwork's order: the picture alone, with no input.</summary>
    public static List<RealizedElement> Shapes(VectorDrawing artwork)
    {
        var paths = new List<RealizedElement>(artwork.Shapes.Count);
        foreach (var shape in artwork.Shapes)
        {
            var attributes = new Dictionary<string, string> { ["d"] = shape.Path };
            attributes["fill"] = WebRealizer.PaintCss(shape.Fill);
            // `currentColor` has no alpha of its own, so an inherited paint drawn at a fraction
            // says so beside it — a Solid one carries its alpha in the colour already.
            if (shape.Fill.Kind == VectorPaintKind.Inherit && shape.Fill.Alpha < 1)
                attributes["fill-opacity"] = TokenCss.Number(shape.Fill.Alpha);
            if (shape.Stroke.Paints)
            {
                attributes["stroke"] = WebRealizer.PaintCss(shape.Stroke);
                if (shape.Stroke.Kind == VectorPaintKind.Inherit && shape.Stroke.Alpha < 1)
                    attributes["stroke-opacity"] = TokenCss.Number(shape.Stroke.Alpha);
                attributes["stroke-width"] = TokenCss.Number(shape.StrokeWidth);
                attributes["stroke-linecap"] = "round";
                attributes["stroke-linejoin"] = "round";
            }
            if (shape.EvenOdd) attributes["fill-rule"] = "evenodd";
            if (shape.Opacity < 1) attributes["opacity"] = TokenCss.Number(shape.Opacity);
            paths.Add(new RealizedElement("path") { RawAttributes = attributes });
        }
        return paths;
    }

    /// <summary>Whether a shape of the artwork is painted in the colour that holds it, so a tile's
    /// document has a colour to state.</summary>
    public static bool Inherits(VectorDrawing artwork) =>
        artwork.Shapes.Any(shape => shape.Fill.Kind == VectorPaintKind.Inherit
            || shape.Stroke.Kind == VectorPaintKind.Inherit);
}
