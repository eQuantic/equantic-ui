using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A11: a labelled graphic announces as an IMAGE — on every target.
/// <para>
/// The semantics walk had a case for a labelled <c>Icon</c> and none for <c>Image</c>, so a photo
/// with alt text emitted NOTHING on Photon and was invisible to VoiceOver and TalkBack, while the web
/// had carried <c>&lt;img alt&gt;</c> all along. The write-once promise is that the same tree says
/// the same thing on both, and this was one node saying it on one.
/// </para>
/// </summary>
public class GraphicSemanticsTests
{
    private static IReadOnlyList<SemanticNode> SemanticsOf(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics();
    }

    [Fact]
    public void AnImageWithAltTextAnnounces()
    {
        var nodes = SemanticsOf(new Image("logo.png", 120, 40, alt: "Company logo"));

        nodes.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Role = SemanticRole.Image, Label = "Company logo" },
                options => options.ExcludingMissingMembers());
    }

    /// <summary>An empty alt is HTML's own way of saying decorative, and it means the same here: a
    /// spacer image that announced itself would be noise on every screen it sits on.</summary>
    [Fact]
    public void AnImageWithoutAltStaysSilent()
    {
        SemanticsOf(new Image("texture.png", 120, 40)).Should().BeEmpty();
    }

    /// <summary>The two graphics answer alike — the point of fixing the missing case rather than
    /// special-casing photos.</summary>
    [Fact]
    public void AnIconAndAnImageAnswerTheSameWay()
    {
        var column = new Column(gap: 0)
        {
            new Image("logo.png", 120, 40, alt: "Company logo"),
            new Icon(Icons.Heart, IconSize.Md, label: "Favourite"),
        };

        var nodes = SemanticsOf(column);

        nodes.Should().HaveCount(2);
        nodes.Should().OnlyContain(node => node.Role == SemanticRole.Image);
        nodes.Select(node => node.Label).Should().Equal("Company logo", "Favourite");
    }
}
