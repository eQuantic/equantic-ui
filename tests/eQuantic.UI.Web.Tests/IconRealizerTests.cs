using eQuantic.UI.Web;
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

    [Fact]
    public void Vector_LowersLikeAnIcon_AtAnAuthoredSize()
    {
        // A chart segment or a logo lockup is the same path data an icon carries — the only
        // difference is that the §07 size whitelist does not apply.
        var glyph = new IconGlyph("sunburst", "M10 10 L90 10 L90 90 Z", IconGlyphStyle.Fill, "0 0 100 100");
        var node = WebRealizer.Lower(new Vector(glyph, 300, PhotonTheme.Instance.TextPrimary), PhotonTheme.Instance).Render();

        node.Tag.Should().Be("svg");
        node.Attributes["viewBox"].Should().Be("0 0 100 100");
        node.Attributes["fill"].Should().Be("currentColor");
        node.Attributes["style"].Should().Contain("width: 300px").And.Contain("height: 300px");
        node.Children.Single(c => c.Tag == "path").Attributes["d"].Should().Be("M10 10 L90 10 L90 90 Z");
    }

    /// <summary>
    /// A shape whose box is NOT square — the connector between two nodes of a diagram is the case
    /// that names it. A square box would have squashed it into an icon-shaped nothing, which is why
    /// a generated figure could only ever be a straight line drawn out of Boxes.
    /// </summary>
    [Fact]
    public void Vector_TakesAnAspectOfItsOwn()
    {
        var edge = new IconGlyph("edge", "M0 8 C 46 8, 94 40, 140 40", IconGlyphStyle.Stroke, "0 0 140 48", 1.5f);
        var node = WebRealizer.Lower(new Vector(edge, 140, height: 48), PhotonTheme.Instance).Render();

        node.Attributes["viewBox"].Should().Be("0 0 140 48");
        node.Attributes["style"].Should().Contain("width: 140px").And.Contain("height: 48px");
        // Stroke intent was always honoured — what was missing was the box to draw it in.
        node.Attributes["fill"].Should().Be("none");
        node.Attributes["stroke"].Should().Be("currentColor");
        node.Attributes["stroke-width"].Should().Be("1.5");
    }

    /// <summary>An icon is square by construction, and stays square now that a vector need not be:
    /// the whitelist is about the em-box, and the em-box has one number.</summary>
    [Fact]
    public void AnIconIsStillSquare()
    {
        var glyph = new IconGlyph("dot", "M12 12 L13 13 Z");
        var node = WebRealizer.Lower(new Icon(glyph, IconSize.Md), PhotonTheme.Instance).Render();

        node.Attributes["style"].Should().Contain("width: 24px; height: 24px");
    }
}
