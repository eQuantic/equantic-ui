using System.Globalization;
using System.Text;

namespace eQuantic.UI.Primitives;

/// <summary>Which colour-vision deficiency a check is simulated under. <see cref="None"/> is
/// unsimulated (full-colour) vision.</summary>
public enum Cvd : byte
{
    None = 0,
    /// <summary>Red-blind.</summary>
    Protan = 1,
    /// <summary>Green-blind.</summary>
    Deutan = 2,
    /// <summary>Blue-blind — reported, never gated on (it is rare and the two above are the standard).</summary>
    Tritan = 3,
}

/// <summary>The three answers a check gives. <see cref="Relief"/> is legal only with a second
/// channel doing the same work — direct labels, gaps, texture, or the table view.</summary>
public enum AuditVerdict : byte
{
    Pass = 0,
    Relief = 1,
    Fail = 2,
}

/// <summary>One check of an audit: its name, its verdict, and the measurement that decided it.</summary>
public readonly record struct AuditLine(string Check, AuditVerdict Verdict, string Detail);

/// <summary>
/// What an audit found. <see cref="Ok"/> is "no hard failure" — a <see cref="AuditVerdict.Relief"/>
/// line still passes, and still obligates the second channel it names. The worst measured distances
/// are exposed as numbers so a test can pin them rather than parse a sentence.
/// </summary>
public sealed record AuditReport(
    IReadOnlyList<AuditLine> Lines,
    double WorstCvdDeltaE,
    double WorstNormalDeltaE,
    IReadOnlyList<Color> BelowContrast)
{
    public bool Ok => Lines.All(line => line.Verdict != AuditVerdict.Fail);

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var line in Lines)
            sb.Append('[').Append(line.Verdict.ToString().ToUpperInvariant().PadRight(6)).Append("] ")
              .Append(line.Check.PadRight(22)).Append(' ').AppendLine(line.Detail);
        return sb.ToString();
    }
}

/// <summary>
/// The colour checks a chart palette must pass, as code — never eyeballed. A categorical palette
/// (series identity) is held to: an OKLCH lightness band per mode, a chroma floor (below it a hue
/// reads as gray and stops doing identity work), separation under simulated protanopia and
/// deuteranopia (Machado, Oliveira and Fernandes 2009 at full severity — the thresholds are
/// calibrated to that model, so the model is part of the standard), a normal-vision floor so
/// full-colour readers can tell neighbours apart too, and contrast against the chart surface. A
/// one-hue ramp (ordered categories) is held to its own checks instead: monotone lightness, visible
/// steps, a light end that still clears the surface, one hue.
/// <para>
/// Distances are Euclidean in OKLab, ×100. The default pairlist is ADJACENT slots — only neighbours
/// touch in a stack, a bar group or a line legend, and assignment never skips. Scatter, bubble,
/// choropleth and small multiples put any two marks side by side and take
/// <c>allPairs: true</c>, which is a strictly harder test and caps how many series those forms can
/// carry.
/// </para>
/// </summary>
public static class PaletteAudit
{
    private const double LightBandLow = 0.43, LightBandHigh = 0.77;
    private const double DarkBandLow = 0.48, DarkBandHigh = 0.67;
    private const double ChromaFloor = 0.10;
    private const double CvdTarget = 8.0, CvdFloor = 6.0;
    private const double NormalFloor = 15.0;
    private const double ContrastMin = 3.0;
    private const double OrdinalMinDeltaL = 0.06;
    private const double OrdinalLightEndFloor = 2.0;
    private const double OneHueSpread = 40.0;

    /// <summary>The chart surface the method validates against by mode, when a theme gives none.</summary>
    public static Color DefaultSurface(ThemeMode mode) =>
        mode == ThemeMode.Dark ? Color.FromRgb(0x1a, 0x1a, 0x19) : Color.FromRgb(0xfc, 0xfc, 0xfb);

    // Machado, Oliveira & Fernandes (2009), severity 1.0, applied to LINEAR sRGB.
    private static readonly double[,] Protan =
    {
        { 0.152286, 1.052583, -0.204868 },
        { 0.114503, 0.786281, 0.099216 },
        { -0.003882, -0.048116, 1.051998 },
    };

    private static readonly double[,] Deutan =
    {
        { 0.367322, 0.860646, -0.227968 },
        { 0.280085, 0.672501, 0.047413 },
        { -0.011820, 0.042940, 0.968881 },
    };

    private static readonly double[,] Tritan =
    {
        { 1.255528, -0.076749, -0.178779 },
        { -0.078411, 0.930809, 0.147602 },
        { 0.004733, 0.691367, 0.303900 },
    };

    /// <summary>
    /// The categorical checks over <paramref name="palette"/> in slot order, against
    /// <paramref name="surface"/> (the mode's default when null).
    /// </summary>
    public static AuditReport Categorical(IReadOnlyList<Color> palette, ThemeMode mode, Color? surface = null,
        bool allPairs = false)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var ground = surface ?? DefaultSurface(mode);
        var (low, high) = mode == ThemeMode.Dark ? (DarkBandLow, DarkBandHigh) : (LightBandLow, LightBandHigh);
        var lines = new List<AuditLine>(5);

        var offBand = palette.Where(c => { var l = Oklch(c).L; return l < low || l > high; }).ToList();
        lines.Add(new AuditLine("Lightness band", offBand.Count == 0 ? AuditVerdict.Pass : AuditVerdict.Fail,
            offBand.Count == 0
                ? $"all {palette.Count} inside L {F(low)}–{F(high)}"
                : "outside band: " + string.Join(", ", offBand.Select(c => $"{Hex(c)} L {F(Oklch(c).L)}"))));

        var lowChroma = palette.Where(c => Oklch(c).C < ChromaFloor).ToList();
        lines.Add(new AuditLine("Chroma floor", lowChroma.Count == 0 ? AuditVerdict.Pass : AuditVerdict.Fail,
            lowChroma.Count == 0
                ? $"all {palette.Count} >= {F(ChromaFloor)}"
                : "below floor (reads gray): " + string.Join(", ", lowChroma.Select(c => $"{Hex(c)} C {F(Oklch(c).C)}"))));

        var pairs = Pairs(palette.Count, allPairs);
        var label = allPairs ? "all-pairs" : "adjacent";
        var worstCvd = double.PositiveInfinity;
        var worstCvdText = "n/a";
        foreach (var kind in new[] { Cvd.Protan, Cvd.Deutan })
        {
            foreach (var (i, j) in pairs)
            {
                var d = DeltaE(palette[i], palette[j], kind);
                if (d < worstCvd)
                {
                    worstCvd = d;
                    worstCvdText = $"worst {label} {Hex(palette[j])}↔{Hex(palette[i])} ΔE {F1(d)} ({kind.ToString().ToLowerInvariant()})";
                }
            }
        }

        var tritan = pairs.Count == 0 ? 99 : pairs.Min(p => DeltaE(palette[p.i], palette[p.j], Cvd.Tritan));
        if (pairs.Count == 0) worstCvd = 99;
        var cvdVerdict = worstCvd >= CvdTarget ? AuditVerdict.Pass : worstCvd >= CvdFloor ? AuditVerdict.Relief : AuditVerdict.Fail;
        lines.Add(new AuditLine("CVD separation", cvdVerdict,
            pairs.Count == 0 ? "n/a" : $"{worstCvdText} · tritan {F1(tritan)}"));

        var worstNormal = double.PositiveInfinity;
        var worstNormalText = "n/a";
        foreach (var (i, j) in pairs)
        {
            var d = DeltaE(palette[i], palette[j]);
            if (d < worstNormal)
            {
                worstNormal = d;
                worstNormalText = $"worst {label} {Hex(palette[j])}↔{Hex(palette[i])} ΔE {F1(d)} (normal)";
            }
        }

        if (pairs.Count == 0) worstNormal = 99;
        lines.Add(new AuditLine("Normal-vision floor", worstNormal >= NormalFloor ? AuditVerdict.Pass : AuditVerdict.Fail,
            pairs.Count == 0
                ? "n/a"
                : worstNormalText + (worstNormal >= NormalFloor
                    ? ""
                    : $" — below {F0(NormalFloor)}, hard to tell apart even with full colour vision")));

        var belowContrast = palette.Where(c => Contrast(c, ground) < ContrastMin).ToList();
        lines.Add(new AuditLine("Contrast vs surface", belowContrast.Count == 0 ? AuditVerdict.Pass : AuditVerdict.Relief,
            belowContrast.Count == 0
                ? $"all {palette.Count} >= {F1(ContrastMin)}:1"
                : $"below {F1(ContrastMin)}:1 — relief required (visible labels or table view): "
                  + string.Join(", ", belowContrast.Select(c => $"{Hex(c)} {F2(Contrast(c, ground))}:1"))));

        return new AuditReport(lines, worstCvd, worstNormal, belowContrast);
    }

    /// <summary>
    /// The checks for a ONE-HUE ramp used on ordered categories (funnel stages, tiers, buckets):
    /// the categorical checks fail a correct ramp by design (it spans the band, light steps drop
    /// below the chroma floor), so a ramp is held to reading AS a ramp instead.
    /// </summary>
    public static AuditReport Ordinal(IReadOnlyList<Color> ramp, ThemeMode mode, Color? surface = null)
    {
        ArgumentNullException.ThrowIfNull(ramp);
        var ground = surface ?? DefaultSurface(mode);
        var lines = new List<AuditLine>(4);
        var ls = ramp.Select(c => Oklch(c).L).ToArray();

        var ascending = true;
        var descending = true;
        for (var i = 1; i < ls.Length; i++)
        {
            if (ls[i] < ls[i - 1]) ascending = false;
            if (ls[i] > ls[i - 1]) descending = false;
        }

        var monotone = ls.Length < 2 || ascending || descending;
        lines.Add(new AuditLine("Lightness monotone", monotone ? AuditVerdict.Pass : AuditVerdict.Fail,
            monotone ? "steps read light→dark" : "out of order — L " + string.Join(", ", ls.Select(F))));

        var thin = new List<string>();
        for (var i = 1; i < ls.Length; i++)
        {
            var gap = Math.Abs(ls[i] - ls[i - 1]);
            if (gap < OrdinalMinDeltaL) thin.Add($"{Hex(ramp[i - 1])}↔{Hex(ramp[i])} ΔL {F(gap)}");
        }

        lines.Add(new AuditLine("Adjacent ΔL", thin.Count == 0 ? AuditVerdict.Pass : AuditVerdict.Fail,
            thin.Count == 0 ? $"all gaps >= {F(OrdinalMinDeltaL)}" : "steps too close: " + string.Join(", ", thin)));

        // The step nearest the surface must still clear it: the lightest on a light surface, the
        // darkest on a dark one.
        var nearest = mode == ThemeMode.Dark
            ? ramp.MinBy(c => Oklch(c).L)
            : ramp.MaxBy(c => Oklch(c).L);
        var contrast = Contrast(nearest, ground);
        lines.Add(new AuditLine("Light-end contrast", contrast >= OrdinalLightEndFloor ? AuditVerdict.Pass : AuditVerdict.Fail,
            $"{Hex(nearest)} at {F2(contrast)}:1 vs surface" + (contrast >= OrdinalLightEndFloor ? "" : $" — below {F1(OrdinalLightEndFloor)}:1 floor")));

        var hues = ramp.Select(Hue).ToArray();
        var spread = hues.Length == 0 ? 0 : hues.Max() - hues.Min();
        if (spread > 180) spread = 360 - spread;
        lines.Add(new AuditLine("Single hue", spread <= OneHueSpread ? AuditVerdict.Pass : AuditVerdict.Fail,
            $"hue spread {F0(spread)}°" + (spread <= OneHueSpread ? "" : " — >40°, not a one-hue ramp")));

        return new AuditReport(lines, 99, 99, []);
    }

    /// <summary>WCAG 2 contrast ratio between two colours (alpha ignored: both are taken as painted).</summary>
    public static double Contrast(Color a, Color b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>Perceptual distance in OKLab, ×100 — under <paramref name="kind"/> when it is a
    /// deficiency, so both colours are seen as that reader sees them.</summary>
    public static double DeltaE(Color a, Color b, Cvd kind = Cvd.None)
    {
        var (l1, a1, b1) = Oklab(kind == Cvd.None ? Linear(a) : Simulate(Linear(a), kind));
        var (l2, a2, b2) = Oklab(kind == Cvd.None ? Linear(b) : Simulate(Linear(b), kind));
        return 100 * Math.Sqrt(Sq(l1 - l2) + Sq(a1 - a2) + Sq(b1 - b2));
    }

    /// <summary>OKLCH lightness and chroma (and the hue, in degrees).</summary>
    public static (double L, double C, double H) Oklch(Color color)
    {
        var (l, a, b) = Oklab(Linear(color));
        return (l, Math.Sqrt(Sq(a) + Sq(b)), Hue(color));
    }

    /// <summary>OKLCH hue in degrees, 0–360.</summary>
    public static double Hue(Color color)
    {
        var (_, a, b) = Oklab(Linear(color));
        return ((Math.Atan2(b, a) * 180 / Math.PI) % 360 + 360) % 360;
    }

    /// <summary>The colour as a reader with <paramref name="kind"/> sees it.</summary>
    public static Color Simulate(Color color, Cvd kind)
    {
        if (kind == Cvd.None) return color;
        var (r, g, b) = Simulate(Linear(color), kind);
        return Color.FromRgba(Byte(Delinearize(r)), Byte(Delinearize(g)), Byte(Delinearize(b)), color.A);
    }

    private static (double R, double G, double B) Simulate((double R, double G, double B) lin, Cvd kind)
    {
        var m = kind switch
        {
            Cvd.Protan => Protan,
            Cvd.Deutan => Deutan,
            Cvd.Tritan => Tritan,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a deficiency"),
        };
        return (
            Clamp01(m[0, 0] * lin.R + m[0, 1] * lin.G + m[0, 2] * lin.B),
            Clamp01(m[1, 0] * lin.R + m[1, 1] * lin.G + m[1, 2] * lin.B),
            Clamp01(m[2, 0] * lin.R + m[2, 1] * lin.G + m[2, 2] * lin.B));
    }

    private static (double L, double A, double B) Oklab((double R, double G, double B) lin)
    {
        var l = Math.Cbrt(0.4122214708 * lin.R + 0.5363325363 * lin.G + 0.0514459929 * lin.B);
        var m = Math.Cbrt(0.2119034982 * lin.R + 0.6806995451 * lin.G + 0.1073969566 * lin.B);
        var s = Math.Cbrt(0.0883024619 * lin.R + 0.2817188376 * lin.G + 0.6299787005 * lin.B);
        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static (double R, double G, double B) Linear(Color c) =>
        (Linearize(c.R / 255.0), Linearize(c.G / 255.0), Linearize(c.B / 255.0));

    private static double Linearize(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double Delinearize(double c)
    {
        c = Clamp01(c);
        return c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
    }

    private static double RelativeLuminance(Color c)
    {
        var (r, g, b) = Linear(c);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static List<(int i, int j)> Pairs(int n, bool all)
    {
        var pairs = new List<(int, int)>();
        if (all)
        {
            for (var i = 0; i < n; i++)
                for (var j = i + 1; j < n; j++)
                    pairs.Add((i, j));
        }
        else
        {
            for (var i = 0; i + 1 < n; i++) pairs.Add((i, i + 1));
        }

        return pairs;
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    private static byte Byte(double unit) => (byte)Math.Round(unit * 255);
    private static double Sq(double v) => v * v;
    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
    private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
}
