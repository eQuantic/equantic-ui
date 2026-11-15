using eQuantic.UI.Material;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The web's half of contrast levels (#340): the theme's colour variables are declared again under
/// <c>@media (prefers-contrast: more)</c> with their More pairs, and nothing else changes. Every atomic
/// class reads a theme colour through its variable, so the page steps up without a class of its own.
/// </summary>
public class ContrastLevelStylesheetTests
{
    private static readonly PhotonTheme Photon = PhotonTheme.Instance;

    /// <summary>The block under the contrast query, as written.</summary>
    private static string MoreBlock(string css)
    {
        var start = css.IndexOf("@media (prefers-contrast: more) {", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "a theme with a More palette declares it under the contrast query");
        var end = css.IndexOf("\n}", start, StringComparison.Ordinal);
        return css[start..end];
    }

    [Fact]
    public void ATokenThatStepsUp_IsDeclaredAgainWithItsMorePair()
    {
        var more = MoreBlock(ThemeStylesheet.Generate(Photon));

        more.Should().Contain($"--eq-color-text-muted: {TokenCss.ValueAt(Photon.TextMuted, ContrastLevel.More)};");
        more.Should().Contain("--eq-color-text-muted: light-dark(#475261, #c5d1df);", "the handoff's More pair for Muted");
        more.Should().Contain("--eq-color-border-strong: light-dark(#6d7178, #768190);");
        more.Should().Contain("--eq-color-primary-base: light-dark(#0050a0, #aed5fe);");
        // The hover is the midpoint of the More fills, so a filled control's hover steps up with it.
        more.Should().Contain($"--eq-color-primary-hover: {TokenCss.ValueAt(Photon.Colors(Variant.Primary).Hover, ContrastLevel.More)};");
    }

    [Fact]
    public void ATokenWithoutAMorePair_IsNotDeclaredAgain()
    {
        var more = MoreBlock(ThemeStylesheet.Generate(Photon));

        more.Should().NotContain("--eq-color-text-primary:", "the handoff leaves Primary text where it is");
        more.Should().NotContain("--eq-color-background:");
        more.Should().NotContain("--eq-color-link:", "Link keeps its pair at More");
    }

    /// <summary>The level is the system's, so it composes with a mode the app declared: the block is
    /// the same whichever mode pins <c>color-scheme</c>, and <c>light-dark()</c> picks the leg.</summary>
    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void ADeclaredMode_KeepsTheContrastBlock(ThemeMode mode) =>
        MoreBlock(ThemeStylesheet.Generate(Photon, mode)).Should().Be(MoreBlock(ThemeStylesheet.Generate(Photon)));

    [Fact]
    public void AThemeWithoutAMorePalette_WritesNoContrastBlock()
    {
        var material = MaterialTheme.FromSeed(Color.FromRgb(0x67, 0x50, 0xA4));
        ThemeStylesheet.Generate(material).Should().NotContain("prefers-contrast",
            "a theme that never stated a More pair paints its Standard one at More, so there is nothing to switch");
    }

    /// <summary>
    /// The atomizer sees a colour as its Standard CSS, so two theme roles with the same Standard pair
    /// share one variable, the first registered. Two such roles with different More pairs would hand
    /// the second role the first one's More colour, so the shipped palette must never hold one.
    /// </summary>
    [Fact]
    public void RolesThatShareAStandardPair_ShareTheirMorePair()
    {
        var roles = new List<(string Name, ColorToken Token)>
        {
            ("background", Photon.Background), ("surface", Photon.Surface), ("surface-subtle", Photon.SurfaceSubtle),
            ("surface-highlight", Photon.SurfaceHighlight), ("border", Photon.Border), ("border-strong", Photon.BorderStrong),
            ("text-primary", Photon.TextPrimary), ("text-secondary", Photon.TextSecondary), ("text-muted", Photon.TextMuted),
            ("text-inverse", Photon.TextInverse), ("focus", Photon.FocusRing), ("link", Photon.LinkColor), ("scrim", Photon.Scrim),
        };
        foreach (var variant in new[] { Variant.Primary, Variant.Secondary, Variant.Destructive, Variant.Success,
                     Variant.Warning, Variant.Info, Variant.Tertiary })
        {
            var colors = Photon.Colors(variant);
            var name = variant.ToString().ToLowerInvariant();
            roles.AddRange([($"{name}-base", colors.Base), ($"{name}-on", colors.OnBase), ($"{name}-pressed", colors.Pressed),
                ($"{name}-subtle", colors.Subtle), ($"{name}-on-subtle", colors.OnSubtle), ($"{name}-hover", colors.Hover)]);
        }

        var clashes = roles
            .GroupBy(role => TokenCss.Value(role.Token))
            .Where(group => group.Select(role => TokenCss.ValueAt(role.Token, ContrastLevel.More)).Distinct().Count() > 1)
            .Select(group => string.Join(", ", group.Select(role => role.Name)))
            .ToList();

        clashes.Should().BeEmpty("roles that read one variable must step up to one colour");
    }
}
