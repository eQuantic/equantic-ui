using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Material;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Material Design 3 as a write-once <see cref="IAppTheme"/>: the SAME shared components lower to M3
/// tokens purely by swapping the theme — no component change. Proves the reformulated IAppTheme
/// (color roles + shape-in-theme) carries M3 on the web realizer.
/// </summary>
public class MaterialThemeRealizerTests
{
    private static readonly IAppTheme Material = MaterialTheme.Instance;

    private static HtmlNode Render(VisualNode node, IAppTheme theme) => WebRealizer.Lower(node, theme).Render();

    [Fact]
    public void PrimaryButton_UsesTheM3PrimaryRoleAndShape()
    {
        var node = Render(new Button("Continue"), Material);
        var box = node.Children[0]; // Pressable → Box carries chrome

        // M3 baseline primary #6750A4 (light) / #D0BCFF (dark).
        box.Attributes["style"].Should().Contain(
            $"background-color: {TokenCss.Value(Material.Colors(Variant.Primary).Base)}");
        box.Attributes["style"].Should().Contain("light-dark(#6750a4, #d0bcff)");
        // The Photon Button uses Medium shape → M3 medium is 12dp (Photon's was 10).
        box.Attributes["style"].Should().Contain("border-radius: 12px");
    }

    [Fact]
    public void Card_UsesM3SurfaceAndLargeShape()
    {
        var node = Render(new Card(new Text("Hi")), Material);

        node.Attributes["style"].Should().Contain(
            $"background-color: {TokenCss.Value(Material.Surface)}");
        node.Attributes["style"].Should().Contain("light-dark(#f7f2fa, #1d1b20)");
        // Card uses Large shape → M3 large is 16dp.
        node.Attributes["style"].Should().Contain("border-radius: 16px");
    }

    [Fact]
    public void TertiaryVariant_IsAFirstClassM3Role()
    {
        var node = Render(new Button("Accent", Variant.Tertiary), Material);
        var box = node.Children[0];
        // M3 tertiary #7D5260 / #EFB8C8 — not derived from Info.
        box.Attributes["style"].Should().Contain("light-dark(#7d5260, #efb8c8)");
        Material.Colors(Variant.Tertiary).Base.Should().NotBe(Material.Colors(Variant.Info).Base);
    }

    [Fact]
    public void GeneratedStylesheet_CarriesM3TokensAndShapeScale()
    {
        var css = PhotonCssGenerator.Generate(Material);

        css.Should().Contain("--eq-color-primary-base: light-dark(#6750a4, #d0bcff);");
        css.Should().Contain("--eq-color-tertiary-base: light-dark(#7d5260, #efb8c8);");
        css.Should().Contain("--eq-radius-md: 12px;");   // M3 medium
        css.Should().Contain("--eq-radius-lg: 16px;");   // M3 large
        css.Should().Contain("--eq-radius-xl: 28px;");   // M3 extra-large
    }

    [Fact]
    public void PhotonAndMaterial_AreDistinctThemesThroughTheSameContract()
    {
        var photonButton = Render(new Button("X"), PhotonTheme.Instance).Children[0];
        var materialButton = Render(new Button("X"), Material).Children[0];

        photonButton.Attributes["style"].Should().Contain("border-radius: 10px"); // Photon medium
        materialButton.Attributes["style"].Should().Contain("border-radius: 12px"); // M3 medium
    }
}
