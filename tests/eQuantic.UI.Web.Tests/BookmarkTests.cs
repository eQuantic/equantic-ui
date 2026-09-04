using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// An in-page link needs something to arrive at, and the SDK had nothing: a site's whole landing
/// nav and its docs "On this page" rail were dead links, because no node could become the target
/// of <c>#section</c>. `Key` looked like the answer and is not — it is reconciler identity, unique
/// among siblings only.
/// </summary>
public class BookmarkTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static IReadOnlyList<HtmlNode> Render(VisualNode node) =>
        Walk(WebRealizer.Lower(node, Theme).Render()).ToList();

    [Fact]
    public void ABookmarkBecomesTheElementsId()
    {
        var rendered = Render(new Box(new BoxStyle()) { Bookmark = "features" });

        // .Any, not .Should().Contain(predicate): the latter takes an expression tree, and an
        // expression tree cannot declare an `out var`.
        rendered.Any(n => n.Attributes.TryGetValue("id", out var id) && id == "features")
            .Should().BeTrue();
    }

    [Fact]
    public void AnyNodeCanCarryOne_BecauseALinkMayPointAtAnything()
    {
        // Written in the ONE funnel every node passes through, not per kind — a docs rail bookmarks
        // headings, which are Text.
        Render(new Text("Catalog", TypeRole.Heading) { Bookmark = "catalog" })
            .Should().Contain(n => n.Attributes.GetValueOrDefault("id") == "catalog");
        Render(new Row(gap: 0) { Bookmark = "how" })
            .Should().Contain(n => n.Attributes.GetValueOrDefault("id") == "how");
    }

    [Fact]
    public void AKeyIsNotABookmark()
    {
        // The confusion that made the links dead. A Key is unique among SIBLINGS — in a list it is
        // typically "1", "2", "3" — so projecting it onto a screen-wide id would collide at once.
        Render(new Box(new BoxStyle()) { Key = "features" })
            .Should().NotContain(n => n.Attributes.ContainsKey("id"));
    }

    [Fact]
    public void ABookmarkKeepsRoomAboveItself()
    {
        // A browser scrolls the target to the very top, so under a pinned header it arrives hidden:
        // the link works and the page looks broken. The offset is the variable the pinned publishes
        // after being measured, never a number the author writes.
        var bookmarked = Render(new Box(new BoxStyle()) { Bookmark = "features" })
            .First(n => n.Attributes.GetValueOrDefault("id") == "features");

        // WITHOUT the sink the declaration stays inline, which is what this realizer does with
        // every style when nothing is collecting atoms.
        bookmarked.Attributes["style"].Should().Contain("scroll-margin-top")
            .And.Contain("--eq-anchor-offset");
    }

    [Fact]
    public void UnderTheAtomicSink_TheRoomIsAClass_LikeEveryOtherDeclaration()
    {
        // The shape a real SSR run produces: the atomiser turns every declaration into a class and
        // leaves inline only the custom-property tail. The runtime must produce the SAME class or
        // SSR and hydration are one attribute apart on every bookmarked element — which is what
        // the first version of this did, by writing the declaration inline on the client.
        var sink = new StyleSink();
        var html = WebRealizer.Lower(new Box(new BoxStyle()) { Bookmark = "features" },
            Theme, 1f, sink).Render();
        var bookmarked = Walk(html).First(n => n.Attributes.GetValueOrDefault("id") == "features");

        bookmarked.Attributes.Should().ContainKey("class");
        bookmarked.Attributes.GetValueOrDefault("style", "").Should().NotContain("scroll-margin-top",
            "the declaration became a class; only custom properties stay inline");
    }

    [Fact]
    public void APinnedIsMarkedSoItCanBeMeasured()
    {
        // The height of a content-sized bar is not knowable at lowering, so the runtime measures
        // it — and the marker must come from SSR too, or the hydrated DOM differs by an attribute.
        Render(new Pinned(new Box(new BoxStyle())))
            .Should().Contain(n => n.Attributes.ContainsKey("data-eq-pinned"));
    }

    [Fact]
    public void EmptyIsNotABookmark()
    {
        Render(new Box(new BoxStyle()) { Bookmark = "" })
            .Should().NotContain(n => n.Attributes.ContainsKey("id"));
    }
}
