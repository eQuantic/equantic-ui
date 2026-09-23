using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The second contrast gate, adopted on 2026-09-23 (Foundations §01, "A second measure: APCA").
///
/// <para>
/// <see cref="DesignTokenTests"/> holds the WCAG 2 ratios. WCAG 2 is a ratio of luminances and
/// reads dark mode generously: Muted text passed it at 5.2:1 while APCA, the lightness-contrast
/// method drafted for WCAG 3, measured Lc 42 for the same pair, well under what it asks for small
/// text. The dark palette was re-solved to clear the floors below, and this keeps it there.
/// </para>
///
/// <para>
/// Floors: 75 for Primary and Secondary text, which carry body copy; 60 for Muted, Link and every
/// label on a fill or a Subtle; 15 for BorderStrong, the boundary of a field. Border is a
/// decorative hairline and has no floor. Text is measured against Background, Surface and
/// SurfaceSubtle, and the lowest of the three is what has to clear.
/// </para>
///
/// <para>
/// The APCA constants are 0.0.98G. The result is compared with a 0.05 tolerance because the palette
/// was solved to land ON some floors, and a last-bit difference in <c>Math.Pow</c> between runtimes
/// must not turn a pass into a failure.
/// </para>
/// </summary>
public class DesignTokenApcaTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private const double Tolerance = 0.05;

    private static readonly Variant[] Filled =
        [Variant.Primary, Variant.Secondary, Variant.Destructive, Variant.Success, Variant.Warning,
         Variant.Info, Variant.Tertiary];

    [Fact]
    public void EveryPairClearsItsApcaFloor()
    {
        var failures = new List<string>();
        foreach (var (mode, pick) in new (string, Func<ColorToken, Color>)[] { ("light", t => t.Light), ("dark", t => t.Dark) })
        {
            var grounds = new[] { ("Background", pick(Theme.Background)), ("Surface", pick(Theme.Surface)),
                ("SurfaceSubtle", pick(Theme.SurfaceSubtle)) };

            void Text(string name, ColorToken token, double floor)
            {
                foreach (var (ground, colour) in grounds)
                    Check($"{name} on {ground}", pick(token), colour, floor);
            }

            void Check(string what, Color foreground, Color background, double floor)
            {
                var lc = Lc(foreground, background);
                if (lc < floor - Tolerance)
                    failures.Add($"{mode} · {what}: Lc {lc:F1}, floor {floor}");
            }

            Text("TextPrimary", Theme.TextPrimary, 75);
            Text("TextSecondary", Theme.TextSecondary, 75);
            Text("TextMuted", Theme.TextMuted, 60);
            Text("LinkColor", Theme.LinkColor, 60);

            foreach (var variant in Filled)
            {
                var colors = Theme.Colors(variant);
                Check($"{variant} OnBase on Base", pick(colors.OnBase), pick(colors.Base), 60);
                Check($"{variant} OnBase on Pressed", pick(colors.OnBase), pick(colors.Pressed), 60);
                Check($"{variant} OnSubtle on Subtle", pick(colors.OnSubtle), pick(colors.Subtle), 60);
            }

            Check("BorderStrong on Surface", pick(Theme.BorderStrong), pick(Theme.Surface), 15);
            Check("BorderStrong on Background", pick(Theme.BorderStrong), pick(Theme.Background), 15);
        }

        failures.Should().BeEmpty(
            "every pair has to clear its APCA floor as well as its WCAG 2 ratio. Re-solve the token the way "
            + "Foundations §01 describes (keep the OKLCH hue, move lightness) and update tokens.json with it:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", failures) + Environment.NewLine);
    }

    /// <summary>
    /// Known pairs, so a mistake in the formula cannot pass by making every value large. Black and
    /// white alone do not pin the TRANSFER curve, since 0 and 1 are the same under any of them:
    /// APCA uses a simple 2.4 exponent where WCAG 2 uses the piecewise sRGB curve, and the grey pair
    /// from the apca-w3 reference is what tells them apart. Under the piecewise curve it reads Lc
    /// 60.02 and -65.46, which would let a dark pair clear a floor it does not.
    /// </summary>
    [Fact]
    public void TheFormulaMatchesTheReferenceValues()
    {
        Lc(Color.Black, Color.White).Should().BeApproximately(106.04, 0.1);
        Lc(Color.White, Color.Black).Should().BeApproximately(107.88, 0.1);
        Lc(Color.FromRgb(0x88, 0x88, 0x88), Color.White).Should().BeApproximately(63.056469930209424, 1e-9);
        Lc(Color.White, Color.FromRgb(0x88, 0x88, 0x88)).Should().BeApproximately(68.54146436644962, 1e-9);
    }

    // ---- APCA 0.0.98G ----------------------------------------------------------------------------

    private static double Luminance(Color c) =>
        0.2126729 * Math.Pow(c.R / 255.0, 2.4)
        + 0.7151522 * Math.Pow(c.G / 255.0, 2.4)
        + 0.0721750 * Math.Pow(c.B / 255.0, 2.4);

    private static double SoftClamp(double y) => y > 0.022 ? y : y + Math.Pow(0.022 - y, 1.414);

    /// <summary>The absolute lightness contrast of text over a background.</summary>
    private static double Lc(Color text, Color background)
    {
        var t = SoftClamp(Luminance(text));
        var b = SoftClamp(Luminance(background));
        if (Math.Abs(b - t) < 0.0005) return 0;

        double output;
        if (b > t)
        {
            var sapc = (Math.Pow(b, 0.56) - Math.Pow(t, 0.57)) * 1.14;
            output = sapc < 0.1 ? 0 : sapc - 0.027;
        }
        else
        {
            var sapc = (Math.Pow(b, 0.65) - Math.Pow(t, 0.62)) * 1.14;
            output = sapc > -0.1 ? 0 : sapc + 0.027;
        }

        return Math.Abs(output * 100);
    }
}
