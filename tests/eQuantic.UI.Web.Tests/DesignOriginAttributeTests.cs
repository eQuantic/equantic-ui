using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// <c>VisualNode.Origin</c> reaching the DOM as <c>data-eq-origin</c> — the web half of the identity
/// a visual editor selects on.
/// <para>
/// The attribute name and the "only when set" rule are pinned on BOTH producers: this file for the
/// C# realizer (SSR) and <c>lowering.spec.ts</c> for the TypeScript one (client). They render the
/// same tree on either side of hydration, and a single attribute of disagreement is a diverged
/// subtree — which is why the twin test says so in its own words rather than trusting this one.
/// </para>
/// </summary>
public class DesignOriginAttributeTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in Walk(child)) yield return descendant;
        }
    }

    private static string? OriginOf(HtmlNode node) =>
        node.Attributes.TryGetValue("data-eq-origin", out var value) ? value : null;

    [Fact]
    public void ANodeWithAnOriginCarriesItAsADataAttribute()
    {
        const string origin = "/src/Screens/Payments.cs|41:8|41:52";
        var tree = Render(new Box(new BoxStyle { Padding = EdgeInsets.All(Space.S2) })
        {
            Origin = origin,
        });

        Walk(tree).Select(OriginOf).Should().Contain(origin);
    }

    /// <summary>
    /// Production is every build where nothing set an origin, and there the DOM must be exactly what
    /// it was before this existed: no attribute, not an empty one. An empty <c>data-eq-origin=""</c>
    /// would still be an attribute the other producer might not write.
    /// </summary>
    [Fact]
    public void WithoutAnOrigin_NothingIsEmitted()
    {
        var tree = Render(new Column(gap: Space.S2)
        {
            new Text("hello", TypeRole.BodyM, Theme.TextPrimary),
        });

        Walk(tree).Select(OriginOf).Should().AllSatisfy(value => value.Should().BeNull());
    }

    /// <summary>
    /// Each node answers for itself. A child does not inherit its parent's origin — selecting the
    /// row would otherwise resolve to the screen that contains it.
    /// </summary>
    [Fact]
    public void OriginsArePerNode_NotInherited()
    {
        var column = new Column(gap: Space.S2) { Origin = "/src/Screen.cs|10:0|10:20" };
        column.Add(new Text("child", TypeRole.BodyM, Theme.TextPrimary) { Origin = "/src/Screen.cs|11:4|11:40" });

        Walk(Render(column)).Select(OriginOf).Where(value => value is not null)
            .Should().BeEquivalentTo(["/src/Screen.cs|10:0|10:20", "/src/Screen.cs|11:4|11:40"]);
    }
}
