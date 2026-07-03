using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine;

/// <summary>
/// A linear-space, PREMULTIPLIED color — the engine's blending representation (rendering happens in
/// linear premultiplied space, mirroring GPU behavior with an sRGB render target). Only rasterizers
/// touch this; the authoring-facing type is the shared <see cref="Primitives.Color"/>.
/// </summary>
public readonly record struct LinearColor(float R, float G, float B, float A)
{
    public static readonly LinearColor Transparent = new(0, 0, 0, 0);

    /// <summary>Source-over: <c>dst' = src + dst · (1 − src.A)</c> (premultiplied).</summary>
    public LinearColor Over(LinearColor dst)
    {
        var inv = 1f - A;
        return new LinearColor(R + dst.R * inv, G + dst.G * inv, B + dst.B * inv, A + dst.A * inv);
    }
}

/// <summary>
/// sRGB ⇄ linear conversions — the piecewise IEC 61966-2-1 curves, the same math GPU hardware applies
/// on sRGB texture reads / render-target writes. Decode uses a 256-entry LUT (hot path).
/// </summary>
public static class ColorSpace
{
    private static readonly float[] SrgbToLinearLut = BuildDecodeLut();

    private static float[] BuildDecodeLut()
    {
        var lut = new float[256];
        for (var i = 0; i < 256; i++)
        {
            var c = i / 255f;
            lut[i] = c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }
        return lut;
    }

    public static float SrgbToLinear(byte value) => SrgbToLinearLut[value];

    public static byte LinearToSrgb(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        var c = value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
        return (byte)MathF.Round(c * 255f);
    }

    /// <summary>Decodes an sRGB color to linear space and premultiplies by its alpha (optionally scaled by coverage).</summary>
    public static LinearColor ToPremultipliedLinear(Color color, float alphaScale = 1f)
    {
        var a = color.A / 255f * alphaScale;
        return new LinearColor(
            SrgbToLinear(color.R) * a,
            SrgbToLinear(color.G) * a,
            SrgbToLinear(color.B) * a,
            a);
    }
}
