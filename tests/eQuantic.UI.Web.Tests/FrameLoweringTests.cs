using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A FRAME OF A LIVE ELEMENT TREE, lowered by <see cref="MarkupHost.Lower"/>: each component
/// from what its position built, by the instance the position keeps, and each layer shown to the
/// owner before it lowers. The parity fixture proves the first frame is the server's markup; these
/// prove the frames after it, which is where lowering a tree drawn once goes wrong.
/// </summary>
public class FrameLoweringTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static MarkupHost Host() => new(Theme);

    /// <summary>A stateful component a parent builds: the shape that loses its state when a lowering
    /// builds it again, because the parent's build makes a fresh instance every time.</summary>
    private sealed class Counter : StatefulComponent
    {
        private int _count;

        public int Mounts { get; private set; }

        public int Unmounts { get; private set; }

        protected override void OnMount() => Mounts++;

        protected override void OnUnmount() => Unmounts++;

        public override VisualNode Build(ComponentContext context) =>
            new Pressable(new Text($"Count: {_count}", TypeRole.BodyM), () => SetState(() => _count++));
    }

    private sealed class Page(Func<VisualNode> body) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Text("Title", TypeRole.Heading));
            column.Add(body());
            return column;
        }
    }

    private static HtmlNode Frame(MarkupHost host, VisualNode root, bool rebuildAll = false)
    {
        host.Build(root, 1200f, rebuildAll);
        var lowered = host.Lower().Render();
        host.EndFrame();
        return lowered;
    }

    private static void Press(HtmlNode node)
    {
        var press = Clicks(node).First();
        if (press is Action action) action();
        else press.DynamicInvoke();
    }

    private static IEnumerable<Delegate> Clicks(HtmlNode node)
    {
        foreach (var pair in node.Events.Where(e => e.Key is "click" or "onclick")) yield return pair.Value;
        foreach (var child in node.Children)
            foreach (var handler in Clicks(child)) yield return handler;
    }

    private static string Markup(HtmlNode node) => HtmlRenderer.RenderNode(node);

    [Fact]
    public void ANestedStatefulComponent_KeepsItsStateFromOneFrameToTheNext()
    {
        var host = Host();
        var page = new Page(() => new Counter());

        var first = Frame(host, page);
        Markup(first).Should().Contain("Count: 0");

        Press(first);
        host.Owner.HasDirty.Should().BeTrue("the press set state on the instance its position keeps");

        Markup(Frame(host, page)).Should().Contain("Count: 1",
            "the counter is lowered from what its KEPT instance built, not from the fresh one the page's build made");
    }

    [Fact]
    public void TheServersLowering_DrawsTheSameCounterAtItsDefaults()
    {
        // The other half of the test above, and the reason the frame lowering exists: lowering the
        // node builds every component again, so the second drawing reads a fresh counter's zero.
        var page = new Page(() => new Counter());

        Press(WebRealizer.Lower(page, Theme).Render());

        Markup(WebRealizer.Lower(page, Theme).Render()).Should().Contain("Count: 0");
    }

    [Fact]
    public void ALayerIsShownBeforeItLowers_SoWhatItHoldsKeepsItsState()
    {
        var host = Host();
        Counter? kept = null;
        var page = new Page(() => new Overlay(new Counter()));

        var first = Frame(host, page);
        kept = host.Owner.Root is { } root ? KeptCounter(root) : null;
        kept.Should().NotBeNull("an overlay's child builds in the slot the frame showed it in");
        kept!.Mounts.Should().Be(1, "it mounts once, when the frame that first showed it ends");

        Press(first);
        Markup(Frame(host, page)).Should().Contain("Count: 1");
        kept.Mounts.Should().Be(1, "the same instance, still in the tree");
    }

    [Fact]
    public void ALayerTheFrameDoesNotLower_LeavesTheTreeWhenTheFrameEnds()
    {
        var host = Host();
        var open = true;
        var page = new Page(() => new Anchored(new Text("anchor", TypeRole.BodyM), new Counter()) { Open = open });

        Frame(host, page);
        var counter = KeptCounter(host.Owner.Root!)!;
        counter.Mounts.Should().Be(1);

        // Closed, with no motion to keep it mounted: the web lowers no panel for it, so the frame
        // shows nothing in its slot, and the slot is taken out when the frame ends — the rule the
        // element tree states for a layer, and the one Photon follows.
        open = false;
        Frame(host, page, rebuildAll: true);

        counter.Unmounts.Should().Be(1);
    }

    /// <summary>
    /// A DISCOVERY ROUND names the instance a position keeps. The server finds the components to
    /// prefetch through the expansion scope; in a frame of a live tree the instance worth asking is
    /// the kept one, whose fields the next frame reads, and not the fresh one a parent's build made
    /// and the element discarded — the round E4 owes ("a prefetch round that keeps a component's
    /// instance").
    /// </summary>
    [Fact]
    public void ADiscoveryRound_NamesTheKeptInstance_NotTheOneAParentsBuildMade()
    {
        var host = Host();
        var page = new Page(() => new Counter());
        Frame(host, page);
        var kept = KeptCounter(host.Owner.Root!);

        var scope = new ComponentExpansionScope();
        // Every component builds again, so the page hands the position a FRESH counter.
        host.Build(page, 1200f, rebuildAll: true);
        host.Lower(new RenderChannels(scope));
        host.EndFrame();
        scope.Expanded.Values.OfType<Counter>().Should().ContainSingle().Which.Should().BeSameAs(kept);
    }

    [Fact]
    public void AComponentTheFrameDidNotBuild_IsRefused_RatherThanDrawnAtItsDefaults()
    {
        var host = Host();
        var frame = new ElementFrame(host.Build(new Page(() => new Text("only text", TypeRole.BodyM)),
            1200f));

        var stranger = new Counter();
        var lowering = () => frame.At(stranger);

        lowering.Should().Throw<InvalidOperationException>().WithMessage("*did not build*");
    }

    /// <summary>The counter the tree keeps, found by walking positions, never by asking the page.</summary>
    private static Counter? KeptCounter(Element element)
    {
        if (element is ComponentElement { Component: Counter counter }) return counter;
        Counter? found = null;
        element.VisitChildren(child => found ??= KeptCounter(child));
        return found;
    }
}
