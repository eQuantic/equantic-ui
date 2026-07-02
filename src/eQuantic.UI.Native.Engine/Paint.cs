namespace eQuantic.UI.Native.Engine;

public enum PaintKind : byte
{
    Solid = 0,
    LinearGradient = 1,
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

    /// <summary>Gradient axis, in the shape's LOCAL coordinate space.</summary>
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
    /// The paint's sRGB color at a LOCAL-space point. Solid paints ignore the point; gradients project
    /// it onto the axis, clamp to [0,1], and lerp per-channel in sRGB (matching the shader).
    /// </summary>
    public Color ColorAt(Point local)
    {
        if (Kind == PaintKind.Solid) return Color;

        var axis = GradientEnd - GradientStart;
        var lenSq = axis.Dot(axis);
        var t = lenSq <= 0 ? 0f : Math.Clamp((local - GradientStart).Dot(axis) / lenSq, 0f, 1f);
        return new Color(
            Lerp(Color.R, EndColor.R, t),
            Lerp(Color.G, EndColor.G, t),
            Lerp(Color.B, EndColor.B, t),
            Lerp(Color.A, EndColor.A, t));
    }

    private static byte Lerp(byte a, byte b, float t) => (byte)MathF.Round(a + (b - a) * t);
}
