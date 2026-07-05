using eQuantic.UI.Primitives;

namespace eQuantic.UI.Material.ColorScience;

/// <summary>
/// Color math primitives ported from Google's material-color-utilities (ColorUtils / MathUtils) —
/// the exact sRGB ↔ linear ↔ XYZ ↔ L* conversions the HCT color space is built on. Values are the
/// canonical constants of that library; this is a faithful transcription, not an approximation.
/// </summary>
internal static class ColorMath
{
    public static readonly double[] WhitePointD65 = { 95.047, 100.0, 108.883 };

    // sRGB → XYZ (D65), rows applied to linearized RGB in [0,100].
    private static readonly double[][] SrgbToXyz =
    {
        new[] { 0.41233895, 0.35762064, 0.18051042 },
        new[] { 0.2126, 0.7152, 0.0722 },
        new[] { 0.01932141, 0.11916382, 0.95034478 },
    };

    /// <summary>sRGB 8-bit channel → linear light in [0,100].</summary>
    public static double Linearized(int rgb8)
    {
        var normalized = rgb8 / 255.0;
        return normalized <= 0.040449936
            ? normalized / 12.92 * 100.0
            : Math.Pow((normalized + 0.055) / 1.055, 2.4) * 100.0;
    }

    /// <summary>Linear light in [0,100] → sRGB 8-bit channel (clamped).</summary>
    public static int Delinearized(double rgbLin)
    {
        var normalized = rgbLin / 100.0;
        var delinearized = normalized <= 0.0031308
            ? normalized * 12.92
            : 1.055 * Math.Pow(normalized, 1.0 / 2.4) - 0.055;
        return Math.Clamp((int)Math.Round(delinearized * 255.0), 0, 255);
    }

    /// <summary>CIE L* (perceptual lightness, 0..100) → Y tristimulus (0..100). L* IS the HCT tone.</summary>
    public static double YFromLstar(double lstar) => 100.0 * LabInvf((lstar + 16.0) / 116.0);

    /// <summary>Y tristimulus (0..100) → CIE L* (0..100).</summary>
    public static double LstarFromY(double y) => LabF(y / 100.0) * 116.0 - 16.0;

    public static Color ColorFromLinrgb(double[] linrgb) => Color.FromRgb(
        (byte)Delinearized(linrgb[0]), (byte)Delinearized(linrgb[1]), (byte)Delinearized(linrgb[2]));

    /// <summary>The Y component of a color's XYZ (needed for tone = L*).</summary>
    public static double YFromColor(Color c)
    {
        var r = Linearized(c.R);
        var g = Linearized(c.G);
        var b = Linearized(c.B);
        return SrgbToXyz[1][0] * r + SrgbToXyz[1][1] * g + SrgbToXyz[1][2] * b;
    }

    public static double[] XyzFromColor(Color c)
    {
        var r = Linearized(c.R);
        var g = Linearized(c.G);
        var b = Linearized(c.B);
        return new[]
        {
            SrgbToXyz[0][0] * r + SrgbToXyz[0][1] * g + SrgbToXyz[0][2] * b,
            SrgbToXyz[1][0] * r + SrgbToXyz[1][1] * g + SrgbToXyz[1][2] * b,
            SrgbToXyz[2][0] * r + SrgbToXyz[2][1] * g + SrgbToXyz[2][2] * b,
        };
    }

    /// <summary>A pure-gray color at the given L* (used when chroma is unattainable / ≈0).</summary>
    public static Color GrayFromLstar(double lstar)
    {
        var y = YFromLstar(Math.Clamp(lstar, 0.0, 100.0));
        var component = Delinearized(y);
        return Color.FromRgb((byte)component, (byte)component, (byte)component);
    }

    public static double SanitizeDegrees(double degrees)
    {
        degrees %= 360.0;
        if (degrees < 0) degrees += 360.0;
        return degrees;
    }

    public static double SanitizeRadians(double angle) => (angle + Math.PI * 8) % (Math.PI * 2);

    public static double Signum(double v) => v < 0 ? -1 : v > 0 ? 1 : 0;

    private static double LabF(double t)
    {
        const double e = 216.0 / 24389.0;
        const double kappa = 24389.0 / 27.0;
        return t > e ? Math.Cbrt(t) : (kappa * t + 16.0) / 116.0;
    }

    private static double LabInvf(double ft)
    {
        const double e = 216.0 / 24389.0;
        const double kappa = 24389.0 / 27.0;
        var ft3 = ft * ft * ft;
        return ft3 > e ? ft3 : (116.0 * ft - 16.0) / kappa;
    }
}
