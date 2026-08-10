using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The NORMATIVE rule under test (docs/SHARED-COMPONENTS-PLAN.md): the web's embedded CSS follows
/// exactly the mobile design system — every artifact GENERATED from the Primitives tokens, parity
/// TESTED. These tests recompute the expected values from the same tokens the native realizer uses,
/// so a drift between web CSS and Photon is a build failure.
/// </summary>
public class PhotonCssGeneratorTests
{
    private static readonly string Css = PhotonCssGenerator.Generate(PhotonTheme.Instance);

    /// <summary>
    /// EVERY role gets a rule. The emitter used a hand-written list of seven, and the scale later
    /// grew its dense end — so `.eq-type-titlesmall`, `.eq-type-labelsmall` and `.eq-type-overline`
    /// were stamped onto elements with nothing behind them and silently inherited the body's
    /// 16px/400: a fifth too large, in exactly the toolbars and status rails those rungs exist for.
    /// Nothing failed. The type was just wrong, and only a ruler would tell you.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryTypeRole))]
    public void EveryTypeRole_HasAGeneratedRule(TypeRole role)
    {
        var expected = PhotonTheme.Instance.Type(role);

        Css.Should().Contain($".eq-type-{role.ToString().ToLowerInvariant()} {{",
            $"{role} is a role a component can ask for, so it must have a rule");
        // Not merely present — carrying the token's own size, or the class is decoration.
        Css.Should().Contain($"  font-size: {TokenCss.Px(expected.Size)};");
    }

    public static TheoryData<TypeRole> EveryTypeRole()
    {
        var data = new TheoryData<TypeRole>();
        foreach (var role in Enum.GetValues<TypeRole>()) data.Add(role);
        return data;
    }

    /// <summary>
    /// A declared mode has to reach `color-scheme`, because that is the only thing `light-dark()`
    /// reads. When it did not, the page painted what the OS wanted while a toggle reported what the
    /// app had declared — a dark desktop got a dark page offering to "switch to dark".
    /// </summary>
    [Theory]
    [InlineData(ThemeMode.Light, "color-scheme: light;")]
    [InlineData(ThemeMode.Dark, "color-scheme: dark;")]
    public void ADeclaredMode_PinsTheColorScheme(ThemeMode mode, string expected)
    {
        PhotonCssGenerator.Generate(PhotonTheme.Instance, mode).Should().Contain(expected);
    }

    /// <summary>
    /// The themed image pair has to follow the SAME decision the palette follows. It read
    /// `prefers-color-scheme` — the OS, and only the OS — while colours resolve through
    /// `light-dark()`, which reads `color-scheme`. So a declared mode gave a dark palette carrying
    /// the light logo, and the app could not fix it: the theme switch deliberately does not
    /// re-render, so picking the artwork in C# would freeze it at the first paint instead.
    /// </summary>
    [Theory]
    [InlineData(ThemeMode.Dark, ".eq-themed-light { display: none; } .eq-themed-dark { display: block; }")]
    [InlineData(ThemeMode.Light, ".eq-themed-light { display: block; }")]
    public void ADeclaredMode_DecidesTheThemedImagePair(ThemeMode mode, string expected)
    {
        var css = PhotonCssGenerator.Generate(PhotonTheme.Instance, mode);

        css.Should().Contain(expected);
        css.Should().NotContain("@media (prefers-color-scheme: dark) { .eq-themed-light",
            "a declared mode leaves nothing for the OS to disagree with");
    }

    /// <summary>A live toggle outranks a build-time default in every case — the browser's
    /// controller stamps data-theme, and those rules must survive a declared mode.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void TheRuntimeToggle_AlwaysOutranksTheDefault(ThemeMode? mode)
    {
        var css = PhotonCssGenerator.Generate(PhotonTheme.Instance, mode);

        css.Should().Contain(":root[data-theme=\"dark\"] .eq-themed-dark { display: block; }");
        css.Should().Contain(":root[data-theme=\"light\"] .eq-themed-light { display: block; }");
    }

    /// <summary>Unset stays "follow the OS" — the default every app built before this relied on.</summary>
    [Fact]
    public void WithoutADeclaredMode_TheOsDecides()
    {
        var css = PhotonCssGenerator.Generate(PhotonTheme.Instance);

        css.Should().Contain("color-scheme: light dark;");
        css.Should().Contain("@media (prefers-color-scheme: dark) { .eq-themed-light",
            "with nothing declared, the artwork follows the OS like the palette does");
    }

    [Fact]
    public void Root_UsesColorSchemeAndLightDarkPairs()
    {
        Css.Should().Contain("color-scheme: light dark;");
        // Brand anchor: the Primary base pair straight from the token.
        Css.Should().Contain("--eq-color-primary-base: light-dark(#0050a0, #5ca2e8);");
        Css.Should().Contain("--eq-color-background: light-dark(#f5f6f8, #0c0f13);");
    }

    [Theory]
    [InlineData(Variant.Primary)]
    [InlineData(Variant.Secondary)]
    [InlineData(Variant.Destructive)]
    [InlineData(Variant.Success)]
    [InlineData(Variant.Warning)]
    [InlineData(Variant.Info)]
    public void EveryVariant_EmitsItsFiveSubTokens_WithTokenParity(Variant variant)
    {
        var name = variant.ToString().ToLowerInvariant();
        var colors = PhotonTheme.Instance.Colors(variant);

        Css.Should().Contain($"--eq-color-{name}-base: {TokenCss.Value(colors.Base)};");
        Css.Should().Contain($"--eq-color-{name}-on: {TokenCss.Value(colors.OnBase)};");
        Css.Should().Contain($"--eq-color-{name}-pressed: {TokenCss.Value(colors.Pressed)};");
        Css.Should().Contain($"--eq-color-{name}-subtle: {TokenCss.Value(colors.Subtle)};");
        Css.Should().Contain($"--eq-color-{name}-on-subtle: {TokenCss.Value(colors.OnSubtle)};");
    }

    [Fact]
    public void TextTiers_And_Chrome_AreEmitted()
    {
        var theme = PhotonTheme.Instance;
        Css.Should().Contain($"--eq-color-text-primary: {TokenCss.Value(theme.TextPrimary)};");
        Css.Should().Contain($"--eq-color-text-muted: {TokenCss.Value(theme.TextMuted)};");
        Css.Should().Contain($"--eq-color-focus: {TokenCss.Value(theme.FocusRing)};");
        Css.Should().Contain($"--eq-color-scrim: {TokenCss.Value(theme.Scrim)};");
        // Scrim is translucent — the hex must carry alpha.
        TokenCss.Value(theme.Scrim).Should().Contain("#0b0e1266");
    }

    [Fact]
    public void RadiusAndSpacing_MatchTheScales()
    {
        Css.Should().Contain("--eq-radius-md: 10px;");
        Css.Should().Contain("--eq-radius-lg: 14px;");
        Css.Should().Contain("--eq-space-s4: 16px;");
        Css.Should().Contain("--eq-space-s16: 64px;");
    }

    [Theory]
    [InlineData(TypeRole.Display, "font-size: 34px;", "font-weight: 800;")]
    [InlineData(TypeRole.Title, "font-size: 20px;", "font-weight: 600;")]
    [InlineData(TypeRole.BodyM, "font-size: 15px;", "font-weight: 400;")]
    [InlineData(TypeRole.Caption, "font-size: 12px;", "font-weight: 500;")]
    public void TypeRoleClasses_MatchTheScale(TypeRole role, string size, string weight)
    {
        var index = Css.IndexOf($".eq-type-{role.ToString().ToLowerInvariant()} {{", StringComparison.Ordinal);
        index.Should().BeGreaterThan(0);
        var block = Css.Substring(index, Css.IndexOf('}', index) - index);
        block.Should().Contain(size);
        block.Should().Contain(weight);
    }

    [Fact]
    public void Elevation_EmitsShadowClasses_FromTheSpecs()
    {
        Css.Should().Contain(".eq-elevation-0 { box-shadow: none; }");
        var e3 = PhotonTheme.Instance.Elevation(3);
        Css.Should().Contain($".eq-elevation-3 {{ box-shadow: {TokenCss.Shadow(e3)}; }}");
        TokenCss.Shadow(e3).Should().StartWith("0 6px 16px -2px light-dark(");
    }

    [Fact]
    public void Motion_EmitsDurationsAndCurves()
    {
        Css.Should().Contain("--eq-motion-fast: 100ms;");
        Css.Should().Contain("--eq-curve-standard: cubic-bezier(0.2, 0, 0, 1);");
        Css.Should().Contain("--eq-curve-accelerate: cubic-bezier(0.3, 0, 1, 1);");
    }

    [Fact]
    public void Stylesheet_DeclaresItselfGenerated()
    {
        Css.Should().Contain("GENERATED from eQuantic.UI.Primitives design tokens");
    }
}
