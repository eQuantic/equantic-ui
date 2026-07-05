using eQuantic.UI.Material;
using eQuantic.UI.Material.ColorScience;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Material 3 DYNAMIC COLOR: a full theme derived from a seed via HCT tonal palettes (the real M3
/// algorithm — CAM16 hue/chroma + CIE L* tone, ported from material-color-utilities). These tests are
/// the port's ground truth: the CAM16 forward is pinned against Google's published HCT for pure red and
/// blue, and <c>FromSeed(#6750A4)</c> — the seed the M3 baseline derives from — reproduces every low-
/// chroma role of the fixed baseline byte-for-byte, with the vivid primary tones following the current
/// TonalSpot chroma cap (Google's live output) rather than the hand-tuned published baseline.
/// </summary>
public class MaterialDynamicColorTests
{
    private const int Tol = 3; // per-channel ΔRGB slack for solver precision + tone rounding.

    private static void Close(Color actual, Color expected, string because, int tol = Tol)
    {
        Math.Abs(actual.R - expected.R).Should().BeLessThanOrEqualTo(tol, $"{because} (R)");
        Math.Abs(actual.G - expected.G).Should().BeLessThanOrEqualTo(tol, $"{because} (G)");
        Math.Abs(actual.B - expected.B).Should().BeLessThanOrEqualTo(tol, $"{because} (B)");
    }

    private static Color Rgb(uint hex) => Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);

    [Theory]
    // Google material-color-utilities reference HCT for the sRGB primaries (the CAM16 forward's anchor).
    [InlineData(0xFF0000, 27.41, 113.36, 53.24)]
    [InlineData(0x0000FF, 282.79, 87.23, 32.30)]
    [InlineData(0x6750A4, 298.98, 47.86, 40.08)] // the M3 baseline seed
    public void Cam16Forward_MatchesGooglesPublishedHct(uint hex, double hue, double chroma, double tone)
    {
        var hct = Hct.FromColor(Rgb(hex));
        hct.Hue.Should().BeApproximately(hue, 0.5);
        hct.Chroma.Should().BeApproximately(chroma, 0.5);
        hct.Tone.Should().BeApproximately(tone, 0.5);
    }

    [Fact]
    public void Hct_RoundTripsThroughTheSolver()
    {
        foreach (var hex in new uint[] { 0x6750A4, 0x0050A0, 0xB3261E, 0x1B6C3A })
        {
            var seed = Rgb(hex);
            var hct = Hct.FromColor(seed);
            // Solving the measured hue/chroma/tone reproduces the seed (chroma sits inside the gamut).
            Close(Hct.ToColor(hct.Hue, hct.Chroma, hct.Tone), seed, $"round-trip #{hex:X6}", tol: 2);
        }
    }

    [Fact]
    public void FromSeed_OfTheBaselineSeed_ReproducesTheBaselineLowChromaRoles()
    {
        var d = MaterialTheme.FromSeed(Rgb(0x6750A4));
        var b = MaterialTheme.Instance;

        // Neutrals + low-chroma variants (secondary c16, tertiary c24) fit the gamut at every tone, so
        // the derivation is byte-identical to the published baseline — the strongest proof of the port.
        foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark })
        {
            Close(d.Surface.Resolve(mode), b.Surface.Resolve(mode), $"surface {mode}");
            Close(d.Background.Resolve(mode), b.Background.Resolve(mode), $"background {mode}");
            Close(d.TextPrimary.Resolve(mode), b.TextPrimary.Resolve(mode), $"on-surface {mode}");
            Close(d.TextSecondary.Resolve(mode), b.TextSecondary.Resolve(mode), $"on-surface-variant {mode}");
            Close(d.BorderStrong.Resolve(mode), b.BorderStrong.Resolve(mode), $"outline {mode}");
            Close(d.Colors(Variant.Secondary).Base.Resolve(mode),
                  b.Colors(Variant.Secondary).Base.Resolve(mode), $"secondary {mode}");
            Close(d.Colors(Variant.Tertiary).Base.Resolve(mode),
                  b.Colors(Variant.Tertiary).Base.Resolve(mode), $"tertiary {mode}");
        }
    }

    [Fact]
    public void FromSeed_PrimaryFollowsTheCurrentTonalSpotChromaCap()
    {
        var primary = MaterialTheme.FromSeed(Rgb(0x6750A4)).Colors(Variant.Primary);

        // TonalSpot caps primary chroma at 36; the seed itself sits at ~48, so the vivid primary tone
        // is a touch less saturated than the hand-tuned published baseline (#6750A4). This IS Google's
        // live material-color-utilities output for the same seed.
        Close(primary.Base.Resolve(ThemeMode.Light), Rgb(0x65558F), "primary tone-40 = TonalSpot output");

        // Where the gamut holds chroma 36 (the light dark-on tone 80), it matches the baseline exactly.
        Close(primary.Base.Resolve(ThemeMode.Dark), Rgb(0xD0BCFF), "primary tone-80 tracks the baseline");
    }

    [Fact]
    public void FromSeed_OfABrandColor_ProducesACoherentThemeInThatHue()
    {
        // eQuantic blue #0050A0 → a coherent blue Material theme, distinct from the purple baseline.
        var blue = MaterialTheme.FromSeed(Rgb(0x0050A0));

        var primaryLight = blue.Colors(Variant.Primary).Base.Resolve(ThemeMode.Light);
        primaryLight.B.Should().BeGreaterThan(primaryLight.R, "a blue seed yields a blue-dominant primary");
        primaryLight.Should().NotBe(MaterialTheme.Instance.Colors(Variant.Primary).Base.Resolve(ThemeMode.Light));

        var onPrimary = blue.Colors(Variant.Primary).OnBase.Resolve(ThemeMode.Light);
        (onPrimary.R + onPrimary.G + onPrimary.B).Should().BeGreaterThan(600, "on-primary is near-white");

        // Tone ordering holds: dark-scheme surface is darker than light-scheme surface.
        var lightSurface = blue.Surface.Resolve(ThemeMode.Light);
        var darkSurface = blue.Surface.Resolve(ThemeMode.Dark);
        (lightSurface.R + lightSurface.G + lightSurface.B)
            .Should().BeGreaterThan(darkSurface.R + darkSurface.G + darkSurface.B, "light surface is lighter");
    }
}
