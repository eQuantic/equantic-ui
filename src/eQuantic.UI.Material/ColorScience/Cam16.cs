using eQuantic.UI.Primitives;

namespace eQuantic.UI.Material.ColorScience;

/// <summary>
/// CAM16 viewing conditions (material-color-utilities <c>ViewingConditions</c>) — the DEFAULT set HCT
/// is computed under. A faithful transcription of <c>ViewingConditions.make</c> with the library's
/// default parameters (D65 white, background L* 50, average surround).
/// </summary>
internal sealed class ViewingConditions
{
    public double N { get; }
    public double Aw { get; }
    public double Nbb { get; }
    public double Ncb { get; }
    public double C { get; }
    public double Nc { get; }
    public double Fl { get; }
    public double Z { get; }
    public double[] RgbD { get; }

    public static readonly ViewingConditions Default = Make();

    private ViewingConditions(double n, double aw, double nbb, double ncb, double c, double nc,
        double fl, double z, double[] rgbD)
    {
        N = n; Aw = aw; Nbb = nbb; Ncb = ncb; C = c; Nc = nc; Fl = fl; Z = z; RgbD = rgbD;
    }

    private static ViewingConditions Make()
    {
        var whitePoint = ColorMath.WhitePointD65;
        var adaptingLuminance = 200.0 / Math.PI * ColorMath.YFromLstar(50.0) / 100.0;
        const double backgroundLstar = 50.0;
        const double surround = 2.0;

        var rW = whitePoint[0] * 0.401288 + whitePoint[1] * 0.650173 + whitePoint[2] * -0.051461;
        var gW = whitePoint[0] * -0.250268 + whitePoint[1] * 1.204414 + whitePoint[2] * 0.045854;
        var bW = whitePoint[0] * -0.002079 + whitePoint[1] * 0.048952 + whitePoint[2] * 0.953127;

        var f = 0.8 + surround / 10.0;
        var c = f >= 0.9
            ? Lerp(0.59, 0.69, (f - 0.9) * 10.0)
            : Lerp(0.525, 0.59, (f - 0.8) * 10.0);
        var d = f * (1.0 - 1.0 / 3.6 * Math.Exp((-adaptingLuminance - 42.0) / 92.0));
        d = Math.Clamp(d, 0.0, 1.0);
        var nc = f;
        var rgbD = new[]
        {
            d * (100.0 / rW) + 1.0 - d,
            d * (100.0 / gW) + 1.0 - d,
            d * (100.0 / bW) + 1.0 - d,
        };

        var k = 1.0 / (5.0 * adaptingLuminance + 1.0);
        var k4 = k * k * k * k;
        var k4F = 1.0 - k4;
        var fl = k4 * adaptingLuminance + 0.1 * k4F * k4F * Math.Cbrt(5.0 * adaptingLuminance);
        var n = ColorMath.YFromLstar(backgroundLstar) / whitePoint[1];
        var z = 1.48 + Math.Sqrt(n);
        var nbb = 0.725 / Math.Pow(n, 0.2);
        var ncb = nbb;

        var rgbAFactors = new[]
        {
            Math.Pow(fl * rgbD[0] * rW / 100.0, 0.42),
            Math.Pow(fl * rgbD[1] * gW / 100.0, 0.42),
            Math.Pow(fl * rgbD[2] * bW / 100.0, 0.42),
        };
        var rgbA = new[]
        {
            400.0 * rgbAFactors[0] / (rgbAFactors[0] + 27.13),
            400.0 * rgbAFactors[1] / (rgbAFactors[1] + 27.13),
            400.0 * rgbAFactors[2] / (rgbAFactors[2] + 27.13),
        };
        var aw = (2.0 * rgbA[0] + rgbA[1] + 0.05 * rgbA[2]) * nbb;

        return new ViewingConditions(n, aw, nbb, ncb, c, nc, fl, z, rgbD);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

/// <summary>
/// CAM16 forward transform (material-color-utilities <c>Cam16.fromInt</c>) — enough to extract a
/// color's HUE and CHROMA under the default viewing conditions. The inverse (HCT tone → color) lives
/// in <see cref="HctSolver"/>.
/// </summary>
internal static class Cam16
{
    public static (double Hue, double Chroma) HueChroma(Color color)
    {
        var xyz = ColorMath.XyzFromColor(color);
        var vc = ViewingConditions.Default;

        var rC = 0.401288 * xyz[0] + 0.650173 * xyz[1] - 0.051461 * xyz[2];
        var gC = -0.250268 * xyz[0] + 1.204414 * xyz[1] + 0.045854 * xyz[2];
        var bC = -0.002079 * xyz[0] + 0.048952 * xyz[1] + 0.953127 * xyz[2];

        var rD = vc.RgbD[0] * rC;
        var gD = vc.RgbD[1] * gC;
        var bD = vc.RgbD[2] * bC;

        var rAF = Math.Pow(vc.Fl * Math.Abs(rD) / 100.0, 0.42);
        var gAF = Math.Pow(vc.Fl * Math.Abs(gD) / 100.0, 0.42);
        var bAF = Math.Pow(vc.Fl * Math.Abs(bD) / 100.0, 0.42);
        var rA = ColorMath.Signum(rD) * 400.0 * rAF / (rAF + 27.13);
        var gA = ColorMath.Signum(gD) * 400.0 * gAF / (gAF + 27.13);
        var bA = ColorMath.Signum(bD) * 400.0 * bAF / (bAF + 27.13);

        var a = (11.0 * rA - 12.0 * gA + bA) / 11.0;
        var b = (rA + gA - 2.0 * bA) / 9.0;
        var u = (20.0 * rA + 20.0 * gA + 21.0 * bA) / 20.0;
        var p2 = (40.0 * rA + 20.0 * gA + bA) / 20.0;

        var atanDegrees = Math.Atan2(b, a) * 180.0 / Math.PI;
        var hue = ColorMath.SanitizeDegrees(atanDegrees);
        var hueRadians = hue * Math.PI / 180.0;

        var ac = p2 * vc.Nbb;
        var j = 100.0 * Math.Pow(ac / vc.Aw, vc.C * vc.Z);

        var huePrime = hue < 20.14 ? hue + 360.0 : hue;
        var eHue = 0.25 * (Math.Cos(huePrime * Math.PI / 180.0 + 2.0) + 3.8);
        var p1 = 50000.0 / 13.0 * eHue * vc.Nc * vc.Ncb;
        var t = p1 * Math.Sqrt(a * a + b * b) / (u + 0.305);
        var alpha = Math.Pow(t, 0.9) * Math.Pow(1.64 - Math.Pow(0.29, vc.N), 0.73);
        var chroma = alpha * Math.Sqrt(j / 100.0);

        return (hue, chroma);
    }
}
