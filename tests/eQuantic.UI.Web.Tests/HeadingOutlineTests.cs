using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The document's OUTLINE on the web (design system A9).
/// <para>
/// Before this the framework emitted no heading element anywhere — every title was a span with a
/// type class, so a page was one flat run of text. A screen reader could not jump by heading, and
/// a crawler read no structure. The gap was reported independently by the site's own session,
/// which is how it got found: nothing in the SDK's tests could see it, because a span renders.
/// </para>
/// </summary>
public class HeadingOutlineTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ALevelledTextIsTheRealHeadingElement(int level)
    {
        var tree = Render(new Text("Portfolio", TypeRole.Heading, headingLevel: level));

        Walk(tree).Should().Contain(n => n.Tag == $"h{level}");
    }

    [Fact]
    public void TextWithoutALevelStaysASpan()
    {
        // The default has to be the span, or every label in the library becomes a heading and the
        // outline is noise — which is worse for a screen reader than no outline at all.
        Walk(Render(new Text("Total", TypeRole.BodyL))).Should().NotContain(n => n.Tag.StartsWith('h'));
    }

    [Fact]
    public void TheLevelIsIndependentOfTheTypeScale()
    {
        // A section title can be the second level of the outline and visually smaller than a
        // number above it. Tying the level to the role would make every layout choice semantic.
        var small = Render(new Text("Revenue", TypeRole.LabelSmall, headingLevel: 2));
        var heading = Walk(small).Single(n => n.Tag == "h2");

        heading.Attributes["class"].Should().Contain("eq-type-labelsmall");
    }

    [Fact]
    public void ChoosingALevelCostsNothingOnScreen()
    {
        // The whole reason a designer can afford to mark up the outline: the same text at the same
        // role paints identically whether or not it is a heading. Only the TAG differs.
        var plain = Walk(Render(new Text("Portfolio", TypeRole.Heading))).Single(n => n.Tag == "span");
        var levelled = Walk(Render(new Text("Portfolio", TypeRole.Heading, headingLevel: 1))).Single(n => n.Tag == "h1");

        levelled.Attributes.Should().BeEquivalentTo(plain.Attributes);
    }

    [Fact]
    public void TheBrowsersOwnHeadingRulesAreCancelled()
    {
        // An h1 arrives with a UA margin and font size. The type role already owns both, so the
        // sheet zeroes them — otherwise marking up the outline would move the page.
        var css = PhotonCssGenerator.Generate(Theme);

        css.Should().Contain("h1, h2, h3, h4, h5, h6");
        css.Should().Contain("margin: 0");
        css.Should().Contain("font-size: inherit");
        // The one the other four do not cover: a span is INLINE and a heading is block, which in
        // inline flow is a line break nobody asked for. Flex and grid blockify their children, so
        // this only shows where a Text sits in running text — exactly where it used to be a span.
        css.Should().Contain("display: inline");
    }

    [Fact]
    public void ALevelOutsideTheSixIsRefused_AtEveryDoor()
    {
        var seven = () => new Text("Nope", headingLevel: 7);
        seven.Should().Throw<ArgumentOutOfRangeException>();

        var negative = () => new Text("Nope", headingLevel: -1);
        negative.Should().Throw<ArgumentOutOfRangeException>();

        // The door a parameter check leaves open. An initializer assigns straight to the property,
        // so a guard that only sat on the constructor would let an `h7` through to the realizer.
        var initializer = () => new Text("Nope") { HeadingLevel = 7 };
        initializer.Should().Throw<ArgumentOutOfRangeException>();
    }
}
