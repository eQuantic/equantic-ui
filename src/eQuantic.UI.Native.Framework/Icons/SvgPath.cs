using eQuantic.UI.Native.Engine;

namespace eQuantic.UI.Native.Framework;

/// <summary>A normalized path step: everything lowers to moves, lines, cubics and closes.</summary>
public enum PathVerb : byte
{
    Move = 0,
    Line = 1,
    /// <summary>Cubic Bézier — C1/C2 are the control points, End the endpoint.</summary>
    Cubic = 2,
    Close = 3,
}

public readonly record struct PathSegment(PathVerb Verb, Point C1 = default, Point C2 = default, Point End = default);

/// <summary>
/// SVG path data in the engine's own point type (W4 icons) — what every platform rasterizer
/// consumes. The PARSING lives in <see cref="Primitives.VectorPath"/>; this is the map across.
/// <para>
/// The full command set (M/L/H/V/C/S/Q/T/A/Z, absolute and relative) lowers to moves, lines and
/// CUBICS: quadratics elevate exactly; arcs convert via the SVG F.6.5 endpoint→center
/// parameterization split into ≤90° cubic segments. Malformed data returns what parsed so far —
/// icons render best-effort, never throw at draw time.
/// </para>
/// </summary>
public static class SvgPath
{
    /// <summary>
    /// Delegates to <see cref="Primitives.VectorPath"/> and maps into the engine's own point type.
    /// The normalizer moved to Primitives when drawings arrived: a `.svg` FILE is read at build
    /// time, where nothing native is loaded, and TWO parsers would have meant two answers about the
    /// same arc.
    /// </summary>
    public static IReadOnlyList<PathSegment> Parse(string data)
    {
        var segments = Primitives.VectorPath.Parse(data);
        var mapped = new PathSegment[segments.Count];
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            mapped[i] = new PathSegment(
                (PathVerb)segment.Verb,
                new Point(segment.C1.X, segment.C1.Y),
                new Point(segment.C2.X, segment.C2.Y),
                new Point(segment.End.X, segment.End.Y));
        }
        return mapped;
    }
}

/// <summary>
/// The platform icon rasterizer (W4): turns an <see cref="Primitives.IconGlyph"/> into an A8
/// coverage raster at device scale — the Texture command draws it tinted, exactly like text.
/// Null (tests, headless) keeps the deterministic disc placeholder.
/// <para>
/// The box has TWO extents because a glyph is not always square: an icon asks for the same number
/// twice, a figure asks for its viewBox's aspect. Each axis scales independently (an
/// <c>&lt;svg&gt;</c> without <c>preserveAspectRatio</c>), so the caller decides whether the shape
/// is fitted or stretched — the rasterizer never guesses on its behalf.
/// </para>
/// </summary>
public interface IIconRasterizer
{
    TextRaster? Rasterize(Primitives.IconGlyph glyph, float widthDp, float heightDp, float scale);
}
