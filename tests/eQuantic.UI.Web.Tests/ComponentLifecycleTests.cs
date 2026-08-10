using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The two moments a component owns that a constructor cannot: entering a live tree, and leaving it.
/// <para>
/// A constructor is the wrong place to load stored state or subscribe to a device, because the
/// reconciler builds FRESH instances every pass and keeps the retained one — so a constructor that
/// starts something starts it again for an instance that is then thrown away. The store is what
/// knows which instance stayed, so the store is what delivers the hooks.
/// </para>
/// </summary>
public class ComponentLifecycleTests
{
    private sealed class Tracked : StatefulComponent
    {
        public int Mounts;
        public int Unmounts;

        protected override void OnMount() => Mounts++;
        protected override void OnUnmount() => Unmounts++;

        public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
    }

    [Fact]
    public void AComponentEnteringTheTree_MountsOnce()
    {
        var store = new ComponentInstanceStore();
        var component = new Tracked();

        store.Reconcile("/0", component);
        store.EndPass();

        component.Mounts.Should().Be(1);
    }

    /// <summary>
    /// Mounted when the PASS closes, not inside Reconcile: a hook that ran mid-build would run while
    /// its own parent is still building, so a SetState from it mutates a tree half-way through being
    /// produced.
    /// </summary>
    [Fact]
    public void TheMount_WaitsForThePassToClose()
    {
        var store = new ComponentInstanceStore();
        var component = new Tracked();

        store.Reconcile("/0", component);

        component.Mounts.Should().Be(0, "the tree it belongs to is not finished yet");
    }

    /// <summary>
    /// The whole reason the hook exists: across passes the RETAINED instance is the one that stayed,
    /// and it must not mount again. Mounting per pass would re-run every subscription on every
    /// keystroke that re-rendered the page.
    /// </summary>
    [Fact]
    public void ARetainedInstance_DoesNotMountAgain()
    {
        var store = new ComponentInstanceStore();
        var first = new Tracked();
        store.Reconcile("/0", first);
        store.EndPass();

        // A second pass builds a fresh instance at the same position — the store keeps the first.
        var second = new Tracked();
        var resolved = store.Reconcile("/0", second);
        store.EndPass();

        resolved.Should().BeSameAs(first);
        first.Mounts.Should().Be(1);
        second.Mounts.Should().Be(0, "the discarded instance never entered the tree");
        second.Unmounts.Should().Be(0, "and so it never left it either");
    }

    /// <summary>
    /// A position that leaves the tree unmounts. Without this every mount is a leak — which is why
    /// the pair ships together rather than OnMount alone.
    /// </summary>
    [Fact]
    public void APositionThatLeavesTheTree_Unmounts()
    {
        var store = new ComponentInstanceStore();
        var component = new Tracked();
        store.Reconcile("/0", component);
        store.EndPass();

        // The next pass does not build anything at /0 — the component's position is gone.
        store.EndPass();

        component.Unmounts.Should().Be(1);
    }

    /// <summary>Everything unsubscribes BEFORE anything subscribes: the order that lets two
    /// components share one exclusive resource across a swap.</summary>
    [Fact]
    public void TheOutgoing_UnmountsBeforeTheIncomingMounts()
    {
        var order = new List<string>();
        var store = new ComponentInstanceStore();
        var leaving = new Ordered("leaving", order);
        store.Reconcile("/0", leaving);
        store.EndPass();

        var arriving = new Ordered("arriving", order);
        store.Reconcile("/1", arriving);
        store.EndPass();

        order.Should().Equal("mount:leaving", "unmount:leaving", "mount:arriving");
    }

    private sealed class Ordered(string name, List<string> order) : StatefulComponent
    {
        protected override void OnMount() => order.Add($"mount:{name}");
        protected override void OnUnmount() => order.Add($"unmount:{name}");

        public override VisualNode Build(ComponentContext context) => new Text(name, TypeRole.BodyM);
    }

    /// <summary>A surface closing takes its whole tree with it — the last tree unsubscribes instead
    /// of being collected in silence.</summary>
    [Fact]
    public void TearingTheStoreDown_UnmountsEverythingStillIn()
    {
        var store = new ComponentInstanceStore();
        var a = new Tracked();
        var b = new Tracked();
        store.Reconcile("/0", a);
        store.Reconcile("/1", b);
        store.EndPass();

        store.UnmountAll();

        a.Unmounts.Should().Be(1);
        b.Unmounts.Should().Be(1);
    }
}

/// <summary>
/// A flex takes its layout as CONSTRUCTOR PARAMETERS.
/// <para>
/// The properties are init-only, so before this the only way to set them was an object initializer
/// — which means <c>new</c>, and <c>new</c> is exactly what the declarative surface removes.
/// <c>Row(gap: …)</c> could pass a gap and nothing else, so a row that had to centre or space out
/// its content dropped out of the surface entirely and had to be written the old way.
/// </para>
/// </summary>
public class FlexConstructorTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Render(VisualNode node) =>
        Core.Rendering.HtmlRenderer.RenderNode(WebRealizer.Lower(node, Theme).Render());

    [Fact]
    public void ARowStatesItsAlignment_WithoutAnObjectInitializer()
    {
        var row = new Row(gap: Space.S3, main: MainAlign.SpaceBetween, cross: CrossAlign.Start);

        row.Main.Should().Be(MainAlign.SpaceBetween);
        row.Cross.Should().Be(CrossAlign.Start);
        row.Gap.Should().Be(Space.S3);
    }

    /// <summary>And it reaches the markup — a value the realizer never reads is a value that only
    /// looks set.</summary>
    [Fact]
    public void TheAlignment_ReachesTheRealizedOutput()
    {
        var html = Render(new Row(gap: Space.S3, main: MainAlign.SpaceBetween) { new Text("a"), new Text("b") });

        html.Should().Contain("class=\"eq-", "the flex properties become atomic classes");
        Render(new Row(gap: Space.S3)).Should().NotBe(html, "a different alignment must render differently");
    }

    /// <summary>Each container keeps its OWN cross default — a Row centres, a Column stretches, and
    /// widening the constructor must not quietly hand both the same one.</summary>
    [Fact]
    public void EachContainer_KeepsItsOwnCrossDefault()
    {
        new Row().Cross.Should().Be(CrossAlign.Center);
        new Column().Cross.Should().Be(CrossAlign.Stretch);
    }

    [Fact]
    public void WrappingAndItsRunGap_AreParametersToo()
    {
        var column = new Column(gap: Space.S2, wrap: true, runGap: Space.S4);

        column.Wrap.Should().BeTrue();
        column.RunGap.Should().Be(Space.S4);
    }
}

/// <summary>
/// A border on SOME edges: a rule above a section, an accent bar down the side of a callout, a
/// table cell that shares its neighbour's line.
/// <para>
/// None of those is a Divider — a Divider is a sibling BETWEEN two things, and these belong to the
/// box itself — and the only way to draw one was a Row wrapping a one-dp Box, which is a layout lie
/// about what the design meant.
/// </para>
/// </summary>
public class BorderSidesTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string Css(VisualNode node)
    {
        var sink = new StyleSink();
        WebRealizer.Lower(node, Theme, 1f, sink);
        return sink.Css;
    }

    private static Primitives.Box Ruled(BorderSides sides) => new(new BoxStyle
    {
        BorderWidth = 1,
        BorderColor = Theme.Border,
        BorderSides = sides,
    }, new Text("x", TypeRole.BodyM));

    [Fact]
    public void ATopRule_DrawsOnlyOnTop()
    {
        Css(Ruled(BorderSides.Top)).Should().Contain("border-width:1px 0 0 0");
    }

    /// <summary>Start and End, not left and right — they mirror in a right-to-left reading, and an
    /// accent bar that stays on the left when the text flows the other way is on the wrong side of
    /// it. This is the LTR realization, where Start is the left edge.</summary>
    [Fact]
    public void AnAccentBar_DrawsOnTheStartEdge()
    {
        Css(Ruled(BorderSides.Start)).Should().Contain("border-width:0 0 0 1px");
    }

    /// <summary>A full border keeps the SHORTHAND — one declaration, one atomic class. Everything
    /// written before this renders byte for byte the same.</summary>
    [Fact]
    public void AFullBorder_StaysOneShorthandDeclaration()
    {
        var css = Css(Ruled(BorderSides.All));

        css.Should().Contain("border:1px solid");
        css.Should().NotContain("border-width:");
    }

    [Fact]
    public void TheDefault_IsEveryEdge()
    {
        new BoxStyle().BorderSides.Should().Be(BorderSides.All);
    }

    [Fact]
    public void CombinedSides_DrawOnEachOfThem()
    {
        Css(Ruled(BorderSides.Top | BorderSides.Bottom)).Should().Contain("border-width:1px 0 1px 0");
    }
}
