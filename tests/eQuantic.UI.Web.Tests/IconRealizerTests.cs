using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>Spec A10 web lowering: inline SVG, registry path, currentColor tint via the token.</summary>
public class IconRealizerTests
{
    [Fact]
    public void Icon_LowersToAnInlineSvg_WithTheRegistryPath()
    {
        var theme = PhotonTheme.Instance;
        var node = WebRealizer.Lower(new Icon(Icons.Check, 20, theme.TextSecondary, "Done"), theme).Render();

        node.Tag.Should().Be("svg");
        node.Attributes["viewBox"].Should().Be("0 0 24 24");
        node.Attributes["fill"].Should().Be("currentColor");
        node.Attributes["aria-label"].Should().Be("Done");
        node.Attributes["style"].Should().Contain("width: 20px");
        node.Attributes["style"].Should().Contain($"color: {TokenCss.Value(theme.TextSecondary)}");
        node.Children[0].Tag.Should().Be("path");
        node.Children[0].Attributes["d"].Should().Be(IconRegistry.Path(Icons.Check));
    }

    [Fact]
    public void Icon_OffWhitelistSize_Throws()
    {
        var act = () => new Icon(Icons.Close, 18);
        act.Should().Throw<ArgumentOutOfRangeException>("arbitrary sizes are a spec error (§07 whitelist)");
    }

    [Fact]
    public void DecorativeIcon_IsAriaHidden()
    {
        WebRealizer.Lower(new Icon(Icons.Search), PhotonTheme.Instance).Render()
            .Attributes["aria-hidden"].Should().Be("true");
    }

    [Fact]
    public void PackGlyph_Stroke_LowersWithTheOutlineAttributes()
    {
        var glyph = new IconGlyph("camera", "M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9", IconGlyphStyle.Stroke);
        var node = WebRealizer.Lower(new Icon(glyph, IconSize.Md), PhotonTheme.Instance).Render();

        node.Tag.Should().Be("svg");
        node.Attributes["viewBox"].Should().Be("0 0 24 24");
        node.Attributes["fill"].Should().Be("none");
        node.Attributes["stroke"].Should().Be("currentColor");
        node.Attributes["stroke-width"].Should().Be("2");
        node.Attributes["stroke-linecap"].Should().Be("round");
        node.Attributes["stroke-linejoin"].Should().Be("round");
        node.Children[0].Attributes["d"].Should().Be(glyph.Path);
    }

    [Fact]
    public void PackGlyph_ForeignGrid_CarriesItsViewBox()
    {
        var glyph = new IconGlyph("bolt", "M0 0h448v512H0z", IconGlyphStyle.Fill, "0 0 448 512");
        var node = WebRealizer.Lower(new Icon(glyph, IconSize.Sm), PhotonTheme.Instance).Render();

        node.Attributes["viewBox"].Should().Be("0 0 448 512");
        node.Attributes["fill"].Should().Be("currentColor");
        node.Attributes["style"].Should().Contain("width: 16px; height: 16px");
    }
}
