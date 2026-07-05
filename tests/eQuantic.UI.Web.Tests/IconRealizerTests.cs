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
}
