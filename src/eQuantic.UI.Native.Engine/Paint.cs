using eQuantic.UI.Primitives;
namespace eQuantic.UI.Native.Engine;

public enum PaintKind : byte
{
    Solid = 0,
    LinearGradient = 1,

    /// <summary>Two-stop ELLIPTICAL radial — the spotlight/glow fill. Reuses the linear gradient's
    /// slots (center in <see cref="Paint.GradientStart"/>, radii in <see cref="Paint.GradientEnd"/>),
    /// so it cost the display list and the uniform block nothing.</summary>
    RadialGradient = 2,
}

/// <summary>
/// How a shape is filled. A flat struct (no heap) so display lists stay arena-friendly.
/// v1 supports solid color and a TWO-STOP linear gradient — the overwhelming majority of UI
/// gradients; multi-stop is a v2 extension (stop pool referenced by index).
/// Gradient colors interpolate in sRGB space (the CSS/Skia default look), then convert to linear
/// for blending — the shader does the same.
/// </summary>
public readonly record struct Paint
{
    public PaintKind Kind { get; init; }

    /// <summary>Solid fill color (also gradient START color for <see cref="PaintKind.LinearGradient"/>).</summary>
    public Color Color { get; init; }

    /// <summary>Gradient end color.</summary>
    public Color EndColor { get; init; }

    /// <summary>Gradient axis, in the shape's LOCAL coordinate space. For
    /// <see cref="PaintKind.RadialGradient"/> these carry the CENTER and the (x, y) RADII instead —
    /// the same four floats, reinterpreted, so no new storage exists on either side of the seam.</summary>
    public Point GradientStart { get; init; }
    public Point GradientEnd { get; init; }

    public static Paint Solid(Color color) => new() { Kind = PaintKind.Solid, Color = color };

    public static Paint Linear(Point start, Point end, Color from, Color to) => new()
    {
        Kind = PaintKind.LinearGradient,
        GradientStart = start,
        GradientEnd = end,
        Color = from,
        EndColor = to,
    };

    /// <summary>
    /// A two-stop ELLIPTICAL radial (the spotlight fill): <paramref name="center"/> and per-axis
    /// <paramref name="radiusX"/>/<paramref name="radiusY"/> in LOCAL space. <paramref name="from"/>
    /// fills the center and <paramref name="to"/> is reached at the ellipse boundary — beyond it the
    /// paint stays at <paramref name="to"/> (CSS's behavior past the last stop).
    /// </summary>
    public static Paint Radial(Point center, float radiusX, float radiusY, Color from, Color to) => new()
    {
        Kind = PaintKind.RadialGradient,
        GradientStart = center,
        GradientEnd = new Point(radiusX, radiusY),
        Color = from,
        EndColor = to,
    };

    /// <summary>
    /// The paint's sRGB color at a LOCAL-space point. Solid paints ignore the point; a linear
    /// gradient projects it onto the axis; a radial takes the normalized ELLIPTICAL distance from
    /// the center. Both clamp to [0,1] and lerp per-channel in sRGB — matching the shader exactly.
    /// </summary>
    public Color ColorAt(Point local)
    {
        if (Kind == PaintKind.Solid) return Color;

        var t = GradientPositionAt(local);
        return new Color(
            Lerp(Color.R, EndColor.R, t),
            Lerp(Color.G, EndColor.G, t),
            Lerp(Color.B, EndColor.B, t),
            Lerp(Color.A, EndColor.A, t));
    }

    /// <summary>
    /// The paint's alpha at a LOCAL point as an UNQUANTIZED 0–1 factor. <see cref="ColorAt"/> rounds
    /// every channel to a byte — fine for the color ramp, but at the TAIL of an alpha ramp that
    /// rounding is the whole signal: an interpolated alpha of 0.4/255 becomes 0 on the CPU while the
    /// shader (which works in float) keeps the sliver. Over high contrast that showed up as a 5/255
    /// divergence in the radial golden. Rasterizers therefore take the color from
    /// <see cref="ColorAt"/> and the alpha from HERE, so the CPU keeps the shader's precision.
    /// </summary>
    public float AlphaFactorAt(Point local)
    {
        if (Kind == PaintKind.Solid) return Color.A / 255f;
        var t = GradientPositionAt(local);
        return (Color.A + (EndColor.A - Color.A) * t) / 255f;
    }

    /// <summary>The gradient parameter (0–1) at a LOCAL point — the axis projection for a linear
    /// paint, the normalized elliptical distance for a radial. Solid paints return 0.</summary>
    private float GradientPositionAt(Point local)
    {
        if (Kind == PaintKind.RadialGradient)
        {
            var dx = GradientEnd.X <= 0 ? 0f : (local.X - GradientStart.X) / GradientEnd.X;
            var dy = GradientEnd.Y <= 0 ? 0f : (local.Y - GradientStart.Y) / GradientEnd.Y;
            return Math.Clamp(MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
        }
        if (Kind == PaintKind.LinearGradient)
        {
            var axis = GradientEnd - GradientStart;
            var lenSq = axis.Dot(axis);
            return lenSq <= 0 ? 0f : Math.Clamp((local - GradientStart).Dot(axis) / lenSq, 0f, 1f);
        }
        return 0f;
    }

    private static byte Lerp(byte a, byte b, float t) => (byte)MathF.Round(a + (b - a) * t);
}
