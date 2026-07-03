using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec-fidelity tests for the Photon design tokens (docs/design/Photon-Design-System.dc.html).
/// Values are transcriptions — these tests pin them against the document AND recompute the WCAG
/// claims ("ratios verified computationally", §01) so a palette edit that breaks accessibility
/// fails the build, not a review.
/// </summary>
public class DesignTokenTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    // ---- WCAG 2.x contrast math (relative luminance over sRGB) ----------------------------------
    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static double Contrast(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    public static IEnumerable<object[]> FilledVariants() =>
        new[] { Variant.Primary, Variant.Secondary, Variant.Destructive, Variant.Success, Variant.Warning, Variant.Info }
            .SelectMany(v => new[] { new object[] { v, ThemeMode.Light }, new object[] { v, ThemeMode.Dark } });

    [Theory]
    [MemberData(nameof(FilledVariants))]
    public void OnBase_AgainstBase_MeetsAA(Variant variant, ThemeMode mode)
    {
        var colors = Theme.Colors(variant);
        Contrast(colors.OnBase.Resolve(mode), colors.Base.Resolve(mode)).Should().BeGreaterThanOrEqualTo(4.5,
            $"§01 claims every OnBase/Base pair clears AA ({variant}, {mode})");
        // Pressed keeps the same OnBase content — it must stay readable on the pressed fill too.
        Contrast(colors.OnBase.Resolve(mode), colors.Pressed.Resolve(mode)).Should().BeGreaterThanOrEqualTo(4.5);
    }

    [Theory]
    [MemberData(nameof(FilledVariants))]
    public void OnSubtle_AgainstSubtle_MeetsAA(Variant variant, ThemeMode mode)
    {
        var colors = Theme.Colors(variant);
        Contrast(colors.OnSubtle.Resolve(mode), colors.Subtle.Resolve(mode)).Should().BeGreaterThanOrEqualTo(4.5);
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void TextTiers_AgainstSurface_MeetTheirClaims(ThemeMode mode)
    {
        var surface = Theme.Surface.Resolve(mode);
        Contrast(Theme.TextPrimary.Resolve(mode), surface).Should().BeGreaterThanOrEqualTo(7.0, "§01 claims AAA");
        Contrast(Theme.TextSecondary.Resolve(mode), surface).Should().BeGreaterThanOrEqualTo(7.0, "§01 claims AAA");
        Contrast(Theme.TextMuted.Resolve(mode), surface).Should().BeGreaterThanOrEqualTo(4.5, "§01 claims AA");
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void Link_AgainstBackground_MeetsAA(ThemeMode mode)
    {
        Contrast(Theme.LinkColor.Resolve(mode), Theme.Background.Resolve(mode))
            .Should().BeGreaterThanOrEqualTo(4.5, "§01 audits Link as text on Background");
    }

    // ---- Value pins (transcription guards) --------------------------------------------------------
    [Fact]
    public void BrandAnchors_ArePinned()
    {
        // eQuantic blue drives Primary; light Primary IS the brand hex.
        Theme.Colors(Variant.Primary).Base.Light.Should().Be(Color.FromRgb(0x00, 0x50, 0xA0));
        Theme.Colors(Variant.Primary).Base.Dark.Should().Be(Color.FromRgb(0x5C, 0xA2, 0xE8));
        // Success anchors eQuantic green, darkened in light mode to clear AA (§01).
        Theme.Colors(Variant.Success).Base.Light.Should().Be(Color.FromRgb(0x3B, 0x7A, 0x22));
        Theme.Colors(Variant.Success).Base.Dark.Should().Be(Color.FromRgb(0x85, 0xC0, 0x5E));
    }

    [Fact]
    public void Secondary_SubtleAndOnSubtle_EqualBasePair()
    {
        var colors = Theme.Colors(Variant.Secondary);
        colors.Subtle.Should().Be(colors.Base, "§01: Secondary is already subtle");
        colors.OnSubtle.Should().Be(colors.OnBase);
    }

    [Fact]
    public void DerivedVariants_AreTransparentWithSurfaceSubtlePressed()
    {
        foreach (var variant in new[] { Variant.Outline, Variant.Ghost })
        {
            var colors = Theme.Colors(variant);
            colors.Base.Light.A.Should().Be(0);
            colors.OnBase.Should().Be(Theme.TextPrimary);
            colors.Pressed.Should().Be(Theme.SurfaceSubtle, "§01: derived pressed = SurfaceSubtle");
        }
        Theme.Colors(Variant.Link).OnBase.Should().Be(Theme.LinkColor);
        Theme.Colors(Variant.Link).Pressed.Light.A.Should().Be(0, "Link never fills — pressed swaps the text");
    }

    [Fact]
    public void SpacingScale_IsThe4dpLadder()
    {
        new[] { Space.S1, Space.S2, Space.S3, Space.S4, Space.S5, Space.S6, Space.S8, Space.S10, Space.S12, Space.S16 }
            .Should().Equal(4, 8, 12, 16, 20, 24, 32, 40, 48, 64);
    }

    [Fact]
    public void RadiusScale_MatchesSpec()
    {
        new[] { Radius.Xs, Radius.Sm, Radius.Md, Radius.Lg, Radius.Xl }.Should().Equal(4, 6, 10, 14, 20);
        Radius.Full.Should().BeGreaterThan(100, "Full relies on the engine's min(w,h)/2 clamp");
    }

    [Theory]
    [InlineData(TypeRole.Display, 34, 40, FontWeight.ExtraBold, 1.15f)]
    [InlineData(TypeRole.Heading, 28, 34, FontWeight.Bold, 1.15f)]
    [InlineData(TypeRole.Title, 20, 26, FontWeight.SemiBold, 1.25f)]
    [InlineData(TypeRole.BodyL, 17, 24, FontWeight.Regular, 1.3f)]
    [InlineData(TypeRole.BodyM, 15, 20, FontWeight.Regular, 1.3f)]
    [InlineData(TypeRole.Label, 13, 16, FontWeight.SemiBold, 1.3f)]
    [InlineData(TypeRole.Caption, 12, 16, FontWeight.Medium, 1.3f)]
    public void TypeScale_MatchesSpec(TypeRole role, float size, float line, FontWeight weight, float maxScale)
    {
        var style = Theme.Type(role);
        style.Size.Should().Be(size);
        style.LineHeight.Should().Be(line);
        style.Weight.Should().Be(weight);
        style.MaxScale.Should().Be(maxScale);
    }

    [Fact]
    public void DynamicType_ClampsAndSnaps()
    {
        // Reading roles scale fully to ×1.3: 17 × 1.3 = 22.1 → snaps to 22 (0.5 step).
        Theme.Type(TypeRole.BodyL).ScaledSize(1.3f).Should().Be(22f);
        // Beyond the tier the clamp holds.
        Theme.Type(TypeRole.BodyL).ScaledSize(2f).Should().Be(22f);
        // Display clamps at ×1.15: 34 × 1.15 = 39.1 → 39 ("39 (×1.15 cap)" in the table).
        Theme.Type(TypeRole.Display).ScaledSize(1.3f).Should().Be(39f);
        // Title clamps at ×1.25: 20 × 1.25 = 25.
        Theme.Type(TypeRole.Title).ScaledSize(1.3f).Should().Be(25f);
    }

    [Theory]
    [InlineData(1, 1, 3, 0, 26, 115)]    // E1: 10% light, 45% dark
    [InlineData(2, 2, 8, 0, 31, 122)]    // E2: 12% / 48%
    [InlineData(3, 6, 16, -2, 41, 133)]  // E3: 16% / 52%
    [InlineData(4, 12, 28, -4, 51, 143)] // E4: 20% / 56%
    [InlineData(5, 20, 44, -6, 66, 158)] // E5: 26% / 62%
    public void Elevation_MatchesSpec(int level, float offsetY, float blur, float spread, byte lightAlpha, byte darkAlpha)
    {
        var shadow = Theme.Elevation(level);
        shadow.OffsetY.Should().Be(offsetY);
        shadow.Blur.Should().Be(blur);
        shadow.Spread.Should().Be(spread);
        shadow.Color.Light.A.Should().Be(lightAlpha);
        shadow.Color.Dark.A.Should().Be(darkAlpha);
        shadow.Color.Dark.R.Should().Be(0, "dark shadows are pure black");
    }

    [Fact]
    public void ElevationZero_IsNone()
    {
        Theme.Elevation(0).IsNone.Should().BeTrue("E0 is 'none + border'");
    }

    [Fact]
    public void Motion_DurationsAndExitRule()
    {
        Motion.FastMs.Should().Be(100);
        Motion.BaseMs.Should().Be(200);
        Motion.SlowMs.Should().Be(300);
        Motion.ExitFor(Motion.SlowMs).Should().Be(200, "exit = ⅔ of enter");
        Curve.Standard.Should().Be(new Curve(0.2f, 0, 0, 1));
        SpringSpec.Default.Should().Be(new SpringSpec(380, 34, 1));
    }
}
