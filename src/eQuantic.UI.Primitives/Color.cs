namespace eQuantic.UI.Primitives;

/// <summary>
/// An sRGB color with straight (non-premultiplied) alpha — the authoring-facing color value type of
/// the SHARED design language: tokens, typed styles and components hold these. Realizers convert:
/// the web realizer formats CSS values; the Photon engine decodes to linear premultiplied for
/// blending (its color model mirrors GPU behavior with an sRGB render target).
/// </summary>
public readonly record struct Color(byte R, byte G, byte B, byte A)
{
    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);
    public static Color FromRgba(byte r, byte g, byte b, byte a) => new(r, g, b, a);

    /// <summary>This color with alpha scaled by <paramref name="opacity"/> (0..1).</summary>
    public Color WithOpacity(float opacity) =>
        this with { A = (byte)Math.Clamp((int)MathF.Round(A * opacity), 0, 255) };

    /// <summary>Per-channel midpoint with <paramref name="other"/>, alpha included — what
    /// <c>color-mix(in srgb, this 50%, other)</c> lands on, with .5 rounding up. The §10 hover
    /// derivation's primitive.</summary>
    public Color MidpointWith(Color other) => new(
        (byte)((R + other.R + 1) >> 1),
        (byte)((G + other.G + 1) >> 1),
        (byte)((B + other.B + 1) >> 1),
        (byte)((A + other.A + 1) >> 1));

    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0, 255);
    public static readonly Color White = new(255, 255, 255, 255);
}
