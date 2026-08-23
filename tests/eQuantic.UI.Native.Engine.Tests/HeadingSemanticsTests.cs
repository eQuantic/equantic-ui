using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The document's OUTLINE on the native side (design system A9).
/// <para>
/// The same fact the web emits as <c>h1</c>–<c>h6</c> reaches the platform as a TRAIT on static
/// text, which is what VoiceOver's rotor and TalkBack's heading swipe walk. Without it a long
/// native page can only be read end to end.
/// </para>
/// </summary>
public class HeadingSemanticsTests
{
    private static IReadOnlyList<SemanticNode> SemanticsOf(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics();
    }

    [Fact]
    public void ALevelledTextCarriesItsLevelIntoTheTree()
    {
        var column = new Column(gap: Space.S2);
        column.Add(new Text("Portfolio", TypeRole.Heading, headingLevel: 1));
        column.Add(new Text("Holdings", TypeRole.Title, headingLevel: 2));
        column.Add(new Text("Total value", TypeRole.BodyM));

        var nodes = SemanticsOf(column).Where(n => n.Role == SemanticRole.StaticText).ToList();

        nodes.Single(n => n.Label == "Portfolio").HeadingLevel.Should().Be(1);
        nodes.Single(n => n.Label == "Holdings").HeadingLevel.Should().Be(2);
        // Ordinary text is not a heading, or the rotor fills with every label on the screen.
        nodes.Single(n => n.Label == "Total value").HeadingLevel.Should().Be(0);
    }

    [Fact]
    public void ItIsATraitOnStaticText_NotARoleOfItsOwn()
    {
        // A heading is still text: it reads as text, it is copied as text, and only the NAVIGATION
        // changes. Making it a role would have cost it everything StaticText already answers.
        var nodes = SemanticsOf(new Text("Portfolio", TypeRole.Heading, headingLevel: 1));

        nodes.Should().ContainSingle(n => n.Label == "Portfolio")
            .Which.Role.Should().Be(SemanticRole.StaticText);
    }
}
