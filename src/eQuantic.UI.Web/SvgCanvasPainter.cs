using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The web's <see cref="ICanvasPainter"/>: the app's calls become SVG shapes inside the canvas's
/// own viewBox.
/// <para>
/// SVG rather than <c>&lt;canvas&gt;</c>, and the reason is the framework's own: an SVG is markup,
/// so a canvas SSRs to the same pixels it hydrates to, scales with the device pixel ratio for free,
/// and is diffable by the reconciler like everything else. A <c>&lt;canvas&gt;</c> would be a blank
/// rectangle on the server and a second rendering model to keep in step.
/// </para>
/// <para>
/// The vocabulary is the ENGINE's (rounded rects, circles, annular sectors, strokes), so the two
/// targets can draw the same calls: the sector below is an arc path because SVG has no ring
/// primitive, and that is the only place the two differ in spelling rather than in result.
/// </para>
/// </summary>
internal sealed class SvgCanvasPainter(float width, float height) : ICanvasPainter
{
    private readonly List<Core.IComponent> _shapes = [];

    public float Width => width;
    public float Height => height;

    /// <summary>What was drawn, in call order — paint order, exactly as on Photon.</summary>
    public IReadOnlyList<Core.IComponent> Shapes => _shapes;

    // TokenCss.Number, not a format of this file's own: every other number the web target writes
    // goes through it, and a shape rounded to three decimals inside a container rounded to four is
    // a sub-pixel mismatch that shows as a hairline against the viewBox.
    private static string N(float value) => TokenCss.Number(value);

    /// <summary>
    /// The token as CSS, NOT resolved to a mode: `light-dark(...)` lets the browser answer, so a
    /// canvas's colours follow the theme the way every other colour on this target does. Photon
    /// resolves the mode itself because it has no cascade to defer to.
    /// </summary>
    private static string Ink(ColorToken color) => TokenCss.Value(color);

    private static Core.IComponent Shape(string tag, Dictionary<string, string> attributes) =>
        new RealizedElement(tag) { RawAttributes = attributes };

    public void FillRect(float x, float y, float width, float height, ColorToken color, float cornerRadius = 0)
    {
        var attributes = new Dictionary<string, string>
        {
            ["x"] = N(x), ["y"] = N(y), ["width"] = N(width), ["height"] = N(height),
            ["fill"] = Ink(color),
        };
        if (cornerRadius > 0) attributes["rx"] = N(cornerRadius);
        _shapes.Add(Shape("rect", attributes));
    }

    public void StrokeRect(float x, float y, float width, float height, ColorToken color,
        float strokeWidth, float cornerRadius = 0)
    {
        // Inset by half the stroke: SVG centres a stroke on the path, and every border in this
        // framework is drawn INSIDE its bounds. Without this a canvas's outline would sit half a
        // pixel further out than the same rectangle drawn by a Box.
        var inset = strokeWidth / 2;
        var attributes = new Dictionary<string, string>
        {
            ["x"] = N(x + inset), ["y"] = N(y + inset),
            ["width"] = N(MathF.Max(0, width - strokeWidth)),
            ["height"] = N(MathF.Max(0, height - strokeWidth)),
            ["fill"] = "none", ["stroke"] = Ink(color), ["stroke-width"] = N(strokeWidth),
        };
        if (cornerRadius > 0) attributes["rx"] = N(MathF.Max(0, cornerRadius - inset));
        _shapes.Add(Shape("rect", attributes));
    }

    public void FillCircle(float centerX, float centerY, float radius, ColorToken color) =>
        _shapes.Add(Shape("circle", new Dictionary<string, string>
        {
            ["cx"] = N(centerX), ["cy"] = N(centerY), ["r"] = N(radius), ["fill"] = Ink(color),
        }));

    public void FillAnnularSector(float centerX, float centerY, float innerRadius, float outerRadius,
        float startAngle, float endAngle, ColorToken color, float cornerSmoothing = 0)
    {
        // The one shape SVG has no primitive for. Two arcs and two radial lines, which is what the
        // engine's SDF describes analytically — same result, different spelling.
        //
        // The guards and clamps are COPIED FROM THE ENGINE, deliberately and verbatim in effect
        // (DisplayList.FillAnnularSector): a target that quietly drew a reversed sector, or inked a
        // hairline where the band has no width, would be a write-once promise broken in the one
        // place nobody looks — degenerate input.
        if (outerRadius <= 0 || endAngle <= startAngle || innerRadius >= outerRadius) return;
        innerRadius = Math.Clamp(innerRadius, 0, outerRadius);
        endAngle = MathF.Min(endAngle, startAngle + MathF.Tau);
        cornerSmoothing = Math.Clamp(cornerSmoothing, 0, (outerRadius - innerRadius) / 2);

        var sweep = endAngle - startAngle;
        // A full ring cannot be one arc (start and end coincide): SVG needs two halves. The
        // smoothing is NOT forwarded to them — a full ring has no corners on Photon, so rounding
        // the halves would draw a seam that exists on one target only.
        if (sweep >= MathF.Tau - 1e-4f)
        {
            FillAnnularSector(centerX, centerY, innerRadius, outerRadius, startAngle, startAngle + MathF.PI, color);
            FillAnnularSector(centerX, centerY, innerRadius, outerRadius, startAngle + MathF.PI, startAngle + MathF.Tau, color);
            return;
        }

        (float X, float Y) At(float radius, float angle) =>
            (centerX + (radius * MathF.Cos(angle)), centerY + (radius * MathF.Sin(angle)));

        var largeArc = MathF.Abs(sweep) > MathF.PI ? 1 : 0;
        var clockwise = sweep > 0 ? 1 : 0;
        var (ox1, oy1) = At(outerRadius, startAngle);
        var (ox2, oy2) = At(outerRadius, endAngle);
        var (ix2, iy2) = At(innerRadius, endAngle);
        var (ix1, iy1) = At(innerRadius, startAngle);

        var path = innerRadius <= 0
            ? $"M {N(centerX)} {N(centerY)} L {N(ox1)} {N(oy1)} "
                + $"A {N(outerRadius)} {N(outerRadius)} 0 {largeArc} {clockwise} {N(ox2)} {N(oy2)} Z"
            : $"M {N(ox1)} {N(oy1)} "
                + $"A {N(outerRadius)} {N(outerRadius)} 0 {largeArc} {clockwise} {N(ox2)} {N(oy2)} "
                + $"L {N(ix2)} {N(iy2)} "
                + $"A {N(innerRadius)} {N(innerRadius)} 0 {largeArc} {1 - clockwise} {N(ix1)} {N(iy1)} Z";

        var attributes = new Dictionary<string, string> { ["d"] = path, ["fill"] = Ink(color) };
        if (cornerSmoothing > 0)
        {
            // The engine rounds the segment's corners in the SDF; SVG's nearest honest equivalent
            // is a rounded join on a stroke of the same colour, which reads the same at the sizes
            // a sunburst uses. Stated because it is an approximation, not a match.
            attributes["stroke"] = Ink(color);
            attributes["stroke-width"] = N(cornerSmoothing);
            attributes["stroke-linejoin"] = "round";
        }
        _shapes.Add(Shape("path", attributes));
    }

    public void Line(float x1, float y1, float x2, float y2, ColorToken color, float strokeWidth) =>
        _shapes.Add(Shape("line", new Dictionary<string, string>
        {
            ["x1"] = N(x1), ["y1"] = N(y1), ["x2"] = N(x2), ["y2"] = N(y2),
            ["stroke"] = Ink(color), ["stroke-width"] = N(strokeWidth),
        }));
}
