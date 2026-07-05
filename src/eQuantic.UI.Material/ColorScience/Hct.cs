using eQuantic.UI.Primitives;

namespace eQuantic.UI.Material.ColorScience;

/// <summary>
/// A color in HCT — Google's perceptual space (CAM16 <b>H</b>ue and <b>C</b>hroma, CIE L* <b>T</b>one)
/// that Material 3 dynamic color is built on. Tone maps 1:1 to perceived lightness, so holding hue and
/// chroma while sweeping tone produces a TONAL PALETTE. This is the seam between a seed color and a
/// full theme.
/// </summary>
internal readonly struct Hct
{
    public double Hue { get; }
    public double Chroma { get; }
    public double Tone { get; }

    private Hct(double hue, double chroma, double tone)
    {
        Hue = hue;
        Chroma = chroma;
        Tone = tone;
    }

    /// <summary>The HCT coordinates of an sRGB color (hue/chroma via CAM16, tone via L*).</summary>
    public static Hct FromColor(Color color)
    {
        var (hue, chroma) = Cam16.HueChroma(color);
        var tone = ColorMath.LstarFromY(ColorMath.YFromColor(color));
        return new Hct(hue, chroma, tone);
    }

    /// <summary>The sRGB color for these HCT coordinates (gamut-clamped chroma).</summary>
    public static Color ToColor(double hue, double chroma, double tone) =>
        HctSolver.Solve(hue, chroma, tone);
}
