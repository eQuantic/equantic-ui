using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The list-detail pane. The first catalog component that is itself ADAPTIVE, so what is pinned here
/// is the rule: which panes exist at which width, and what the compact half does with a back
/// affordance that must not appear beside a detail the reader can already see.
/// </summary>
public class ListDetailTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static IEnumerable<HtmlNode> Walk(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Walk(child))
                yield return descendant;
    }

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static IReadOnlyList<string> TextsIn(HtmlNode node) =>
        Walk(node)
            .Select(candidate => candidate.TextContent)
            .Where(text => text is { Length: > 0 })
            .Select(text => text!)
            .ToList();

    private static VisualNode Inbox() => new ScrollView(new Column(gap: 0)
    {
        new ListItem("First message", "Someone") { OnPressed = () => { } },
        new ListItem("Second message", "Someone else") { OnPressed = () => { } },
    });

    /// <summary>
    /// BOTH panes exist in the markup, gated by media queries rather than chosen at runtime: that is
    /// what makes the right one already on screen at first paint, before any JavaScript runs.
    /// </summary>
    [Fact]
    public void TheWebEmitsBothShapes()
    {
        var html = Render(new ListDetail(Inbox(), new Text("The message body"))
        {
            Title = "Message detail",
            ListTitle = "Inbox",
            OnBack = () => { },
        });

        var texts = TextsIn(html);
        // The detail's title appears in BOTH variants' bars — the compact one beside the back
        // affordance and the wide one over the right pane.
        texts.Count(text => text == "Message detail").Should().Be(2,
            "the compact tree and the wide tree are both emitted");
        // The list only exists in the wide tree here: compact shows one pane, and something is chosen.
        texts.Count(text => text == "First message").Should().Be(1);
        texts.Should().Contain("Inbox", "the list pane keeps its own bar beside the detail");
        // The variants are GATED, not branched: the atomizer mints one media class per range, so the
        // browser picks the shape with no JavaScript and no measurement.
        var sink = new StyleSink();
        var element = WebRealizer.Lower(
            new ListDetail(Inbox(), new Text("The message body")) { Title = "Message detail" },
            Theme, 1f, sink);

        element.Children.Should().HaveCount(2);
        ((HtmlElement)element.Children[0]).ClassName.Should()
            .StartWith(AdaptiveGates.CompactUntil(WindowSizeClasses.ExpandedMinDp));
        ((HtmlElement)element.Children[1]).ClassName.Should()
            .StartWith(AdaptiveGates.ExpandedFrom(WindowSizeClasses.ExpandedMinDp));
        sink.Css.Should().Contain("@media (min-width: 840px)");
    }

    /// <summary>
    /// The app hands over ONE list node and both shapes need it, so the same instance lands in two
    /// trees — the thing the vocabulary says not to do. It holds because lowering READS a node and
    /// builds fresh elements per position: each variant gets its own subtree, with its own paths.
    /// Pinned because the day it stops holding, one of the two shapes silently renders nothing.
    /// </summary>
    [Fact]
    public void OneListNodeServesBothShapes()
    {
        var html = Render(new ListDetail(Inbox()) { ListTitle = "Inbox" });

        var texts = TextsIn(html);
        texts.Count(text => text == "First message").Should().Be(2,
            "nothing is chosen, so the list is the compact screen AND the wide left pane");
        texts.Count(text => text == "Inbox").Should().Be(2);
    }

    /// <summary>Nothing chosen on a phone means the LIST is the screen: no detail bar, and no back
    /// affordance to a place the reader never left.</summary>
    [Fact]
    public void WithNothingChosen_TheCompactShapeIsTheListAlone()
    {
        var compact = ((AdaptiveNode)new ListDetail(Inbox())
        {
            Title = "Message detail",
            ListTitle = "Inbox",
            OnBack = () => { },
        }.BuildContained(Context())).Compact;

        var texts = TextsIn(Render(compact));
        texts.Should().Contain("Inbox");
        texts.Should().NotContain("Message detail", "no detail is open, so its bar has no business here");
    }

    /// <summary>And with something chosen, the detail REPLACES it — one pane at a time is the whole
    /// difference between a phone and a tablet here.</summary>
    [Fact]
    public void WithSomethingChosen_TheCompactShapeIsTheDetail()
    {
        var compact = ((AdaptiveNode)new ListDetail(Inbox(), new Text("The message body"))
        {
            Title = "Message detail",
            ListTitle = "Inbox",
            OnBack = () => { },
        }.BuildContained(Context())).Compact;

        var texts = TextsIn(Render(compact));
        texts.Should().Contain("The message body");
        texts.Should().NotContain("Second message", "the list is not on screen beside it");
    }

    /// <summary>
    /// The back affordance belongs to the COMPACT pane only. Wide, both panes are already visible, so
    /// a control that returns you to something you can see is how an adaptive layout reads as broken.
    /// </summary>
    [Fact]
    public void TheWideShapeHasNoBackAffordance()
    {
        var shape = (AdaptiveNode)new ListDetail(Inbox(), new Text("Body"), onBack: () => { })
        {
            Title = "Message detail",
        }.BuildContained(Context());

        static int BackButtons(HtmlNode html) =>
            Walk(html).Count(node => node.Attributes.GetValueOrDefault("aria-label", "") == SdkStrings.Back);

        BackButtons(Render(shape.Compact)).Should().Be(1);
        BackButtons(Render(shape.Expanded!)).Should().Be(0);
    }

    /// <summary>A wide window with nothing chosen still shows a DESIGNED screen. The alternative —
    /// choosing the first row on the app's behalf — would fire a handler for a choice nobody made.
    /// </summary>
    [Fact]
    public void AWideWindowWithNothingChosen_ShowsTheEmptyState()
    {
        var own = (AdaptiveNode)new ListDetail(Inbox())
        {
            Placeholder = new Text("Pick a message"),
        }.BuildContained(Context());
        var none = (AdaptiveNode)new ListDetail(Inbox()).BuildContained(Context());

        TextsIn(Render(own.Expanded!)).Should().Contain("Pick a message");
        TextsIn(Render(none.Expanded!)).Should().Contain(SdkStrings.NothingSelected,
            "an app that supplied no placeholder still gets a screen, from the SDK's own strings");
    }

    /// <summary>The threshold is the app's to move — a 360dp list beside a detail needs room, and a
    /// design that wants the split earlier or later says so.</summary>
    [Fact]
    public void TheThresholdIsTheAppsToMove()
    {
        var shape = (AdaptiveNode)new ListDetail(Inbox()) { TwoPaneFrom = 1024 }
            .BuildContained(Context());

        shape.ExpandedFrom.Should().Be(1024);
        shape.ResolveWidth(900).Should().BeSameAs(shape.Compact);
        shape.ResolveWidth(1100).Should().BeSameAs(shape.Expanded);
    }

    private static ComponentContext Context() => new(Theme);
}
