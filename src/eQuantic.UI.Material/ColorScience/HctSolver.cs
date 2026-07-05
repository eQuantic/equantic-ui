using eQuantic.UI.Primitives;

namespace eQuantic.UI.Material.ColorScience;

/// <summary>
/// The HCT inverse: given a perceptual (hue, chroma, tone) request, find the sRGB color that realizes
/// it. Ported from material-color-utilities <c>HctSolver.findResultByJ</c> — the accurate CAM16-based
/// solver used for in-gamut requests. When a chroma is unattainable at the requested hue+tone, the
/// gamut caps it: we binary-search the chroma down to the maximum the display can show (the same
/// semantics as the library's boundary solver, without its 255-plane precomputed table).
/// </summary>
internal static class HctSolver
{
    // material-color-utilities LINRGB_FROM_SCALED_DISCOUNT.
    private static readonly double[][] LinrgbFromScaledDiscount =
    {
        new[] { 1373.2198709594231, -1100.4251190754821, -7.278681089101213 },
        new[] { -271.815969077903, 559.6580465940733, -32.46047482791194 },
        new[] { 1.9622899599665666, -57.173814538844006, 308.7233197812385 },
    };

    private static readonly double[] YFromLinrgb = { 0.2126, 0.7152, 0.0722 };

    /// <summary>The color for (hue, chroma, tone). Reduces chroma to fit the gamut when necessary.</summary>
    public static Color Solve(double hueDegrees, double chroma, double tone)
    {
        if (chroma < 1.0 || Math.Round(tone) <= 0.0 || Math.Round(tone) >= 100.0)
            return ColorMath.GrayFromLstar(tone);

        var hue = ColorMath.SanitizeDegrees(hueDegrees);
        var hueRadians = hue * Math.PI / 180.0;
        var y = ColorMath.YFromLstar(tone);

        // Exact answer at the requested chroma, if in gamut.
        if (FindResultByJ(hueRadians, chroma, y) is { } exact)
            return exact;

        // Unattainable: binary-search the largest realizable chroma at this hue+tone.
        double lo = 0.0, hi = chroma;
        var best = ColorMath.GrayFromLstar(tone);
        for (var i = 0; i < 12; i++)
        {
            var mid = (lo + hi) / 2.0;
            if (FindResultByJ(hueRadians, mid, y) is { } candidate)
            {
                best = candidate;
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        return best;
    }

    private static Color? FindResultByJ(double hueRadians, double chroma, double y)
    {
        var j = Math.Sqrt(y) * 11.0;
        var vc = ViewingConditions.Default;
        var tInnerCoeff = 1.0 / Math.Pow(1.64 - Math.Pow(0.29, vc.N), 0.73);
        var eHue = 0.25 * (Math.Cos(hueRadians + 2.0) + 3.8);
        var p1 = eHue * (50000.0 / 13.0) * vc.Nc * vc.Ncb;
        var hSin = Math.Sin(hueRadians);
        var hCos = Math.Cos(hueRadians);

        for (var round = 0; round < 5; round++)
        {
            var jNormalized = j / 100.0;
            var alpha = chroma == 0.0 || j == 0.0 ? 0.0 : chroma / Math.Sqrt(jNormalized);
            var t = Math.Pow(alpha * tInnerCoeff, 1.0 / 0.9);
            var ac = vc.Aw * Math.Pow(jNormalized, 1.0 / vc.C / vc.Z);
            var p2 = ac / vc.Nbb;
            var gamma = 23.0 * (p2 + 0.305) * t / (23.0 * p1 + 11.0 * t * hCos + 108.0 * t * hSin);
            var a = gamma * hCos;
            var b = gamma * hSin;
            var rA = (460.0 * p2 + 451.0 * a + 288.0 * b) / 1403.0;
            var gA = (460.0 * p2 - 891.0 * a - 261.0 * b) / 1403.0;
            var bA = (460.0 * p2 - 220.0 * a - 6300.0 * b) / 1403.0;

            var rCScaled = InverseChromaticAdaptation(rA);
            var gCScaled = InverseChromaticAdaptation(gA);
            var bCScaled = InverseChromaticAdaptation(bA);
            var linrgb = new[]
            {
                LinrgbFromScaledDiscount[0][0] * rCScaled + LinrgbFromScaledDiscount[0][1] * gCScaled + LinrgbFromScaledDiscount[0][2] * bCScaled,
                LinrgbFromScaledDiscount[1][0] * rCScaled + LinrgbFromScaledDiscount[1][1] * gCScaled + LinrgbFromScaledDiscount[1][2] * bCScaled,
                LinrgbFromScaledDiscount[2][0] * rCScaled + LinrgbFromScaledDiscount[2][1] * gCScaled + LinrgbFromScaledDiscount[2][2] * bCScaled,
            };

            if (linrgb[0] < 0 || linrgb[1] < 0 || linrgb[2] < 0)
                return null;

            var fnj = YFromLinrgb[0] * linrgb[0] + YFromLinrgb[1] * linrgb[1] + YFromLinrgb[2] * linrgb[2];
            if (fnj <= 0)
                return null;

            if (round == 4 || Math.Abs(fnj - y) < 0.002)
            {
                if (linrgb[0] > 100.01 || linrgb[1] > 100.01 || linrgb[2] > 100.01)
                    return null;
                return ColorMath.ColorFromLinrgb(linrgb);
            }

            j -= (fnj - y) * j / (2.0 * fnj);
        }
        return null;
    }

    private static double InverseChromaticAdaptation(double adapted)
    {
        var adaptedAbs = Math.Abs(adapted);
        var @base = Math.Max(0.0, 27.13 * adaptedAbs / (400.0 - adaptedAbs));
        return ColorMath.Signum(adapted) * Math.Pow(@base, 1.0 / 0.42);
    }
}
