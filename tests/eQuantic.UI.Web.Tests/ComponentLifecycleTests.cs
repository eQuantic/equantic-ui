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
