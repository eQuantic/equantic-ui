using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The element tree (docs/ELEMENT-TREE-PLAN.md, 3.1 and 3.2), driven through its owner alone, with no
/// host and no layout: a position keeps its stateful instance while the same type and key stand at
/// it, a frame builds what was marked and nothing else, ancestors first, and the build is bounded.
/// </summary>
public class ElementTreeTests
{
    private static readonly ComponentContext Context = new(PhotonTheme.Instance);

    private static Element Frame(BuildOwner owner, VisualNode root, bool rebuildAll = true)
    {
        var element = owner.Build(root, Context, WindowSizeClass.Compact, rebuildAll);
        owner.EndFrame();
        return element;
    }

    /// <summary>A label that is CONFIGURATION and a count that is STATE: the two halves
    /// <see cref="UiComponent.AdoptConfig"/> keeps apart.</summary>
    private sealed class Counter(string label, List<string>? builds = null) : StatefulComponent
    {
        public string Label { get; private set; } = label;
        public int Count { get; private set; }
        public int Builds { get; private set; }

        public void Press() => SetState(() => Count++);

        public override void AdoptConfig(UiComponent next)
        {
            if (next is Counter counter) Label = counter.Label;
        }

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            builds?.Add(Label);
            return new Text($"{Label}: {Count}", TypeRole.BodyM);
        }
    }

    /// <summary>A page that holds counters, rebuilt by its own state.</summary>
    private sealed class Page(string[] labels, List<string>? builds = null) : StatefulComponent
    {
        private string[] _labels = labels;

        public int Builds { get; private set; }
        public bool Keyed { get; init; }

        public void Show(params string[] labels) => SetState(() => _labels = labels);

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            builds?.Add("page");
            var column = new Column();
            foreach (var label in _labels)
                column.Add(Keyed ? new Counter(label, builds) { Key = label } : new Counter(label, builds));
            return column;
        }
    }

    private static Counter CounterAt(Element root, int index) =>
        (Counter)((ComponentElement)root.ChildAt(0)!.ChildAt(index)!).Component;

    [Fact]
    public void AParentRebuilds_TheChildKeepsItsStateAndTakesItsNewLabel()
    {
        var owner = new BuildOwner();
        var page = new Page(["first"]);
        var root = Frame(owner, page);
        var counter = CounterAt(root, 0);
        counter.Press();
        counter.Press();
        counter.Press();

        page.Show("second");
        Frame(owner, page, rebuildAll: false);

        CounterAt(root, 0).Should().BeSameAs(counter, "the position kept the instance it mounted");
        counter.Count.Should().Be(3);
        counter.Label.Should().Be("second", "the fresh instance handed over its configuration");
        ((Text)root.ChildAt(0)!.ChildAt(0)!.ChildAt(0)!.Configuration).Content.Should().Be("second: 3");
    }

    [Fact]
    public void KeyedRowsSwap_EachKeepsItsOwnState()
    {
        var owner = new BuildOwner();
        var page = new Page(["a", "b"]) { Keyed = true };
        var root = Frame(owner, page);
        var a = CounterAt(root, 0);
        var b = CounterAt(root, 1);
        a.Press();

        page.Show("b", "a");
        Frame(owner, page, rebuildAll: false);

        CounterAt(root, 0).Should().BeSameAs(b);
        CounterAt(root, 1).Should().BeSameAs(a, "a keyed row is matched by its key wherever it moved");
        a.Count.Should().Be(1);
        b.Count.Should().Be(0);
    }

    /// <summary>The unkeyed rule, which is the index's: the state stays where it was, whatever moved.</summary>
    [Fact]
    public void UnkeyedRowsSwap_TheStateStaysAtItsIndex()
    {
        var owner = new BuildOwner();
        var page = new Page(["a", "b"]);
        var root = Frame(owner, page);
        CounterAt(root, 0).Press();

        page.Show("b", "a");
        Frame(owner, page, rebuildAll: false);

        CounterAt(root, 0).Count.Should().Be(1);
        CounterAt(root, 0).Label.Should().Be("b");
    }

    private sealed class Swapping : StatefulComponent
    {
        private bool _other;

        public void Toggle() => SetState(() => _other = !_other);

        public override VisualNode Build(ComponentContext context) =>
            _other ? new Text("another type", TypeRole.BodyM) : new Counter("counter");
    }

    [Fact]
    public void ATypeChangesAtAPosition_TheStateStartsAgain()
    {
        var owner = new BuildOwner();
        var swapping = new Swapping();
        var root = Frame(owner, swapping);
        var first = (Counter)((ComponentElement)root.ChildAt(0)!).Component;
        first.Press();

        swapping.Toggle();
        Frame(owner, swapping, rebuildAll: false);
        swapping.Toggle();
        Frame(owner, swapping, rebuildAll: false);

        var second = (Counter)((ComponentElement)root.ChildAt(0)!).Component;
        second.Should().NotBeSameAs(first);
        second.Count.Should().Be(0);
    }

    [Fact]
    public void TwoMarkedPositions_TheAncestorBuildsFirst_AndEachBuildsOnce()
    {
        var builds = new List<string>();
        var owner = new BuildOwner();
        var page = new Page(["row"], builds);
        var root = Frame(owner, page);
        var counter = CounterAt(root, 0);
        builds.Clear();

        // Marked child-first, so the order the frame builds in is the owner's, not the marking's.
        counter.Press();
        page.Show("row");
        Frame(owner, page, rebuildAll: false);

        builds.Should().Equal("page", "row");
    }

    [Fact]
    public void AFrameNothingMarked_BuildsNothing()
    {
        var owner = new BuildOwner();
        var page = new Page(["a", "b"]);
        var root = Frame(owner, page);
        var (a, b) = (CounterAt(root, 0), CounterAt(root, 1));

        Frame(owner, page, rebuildAll: false);

        page.Builds.Should().Be(1);
        a.Builds.Should().Be(1);
        b.Builds.Should().Be(1);
    }

    [Fact]
    public void OneComponentChanges_OnlyItBuilds()
    {
        var owner = new BuildOwner();
        var page = new Page(["a", "b"]);
        var root = Frame(owner, page);
        var (a, b) = (CounterAt(root, 0), CounterAt(root, 1));

        b.Press();
        Frame(owner, page, rebuildAll: false);

        page.Builds.Should().Be(1);
        a.Builds.Should().Be(1);
        b.Builds.Should().Be(2);
    }

    [Fact]
    public void RebuildingAll_BuildsEveryComponentOnce()
    {
        var owner = new BuildOwner();
        var page = new Page(["a", "b"]);
        var root = Frame(owner, page);
        var (a, b) = (CounterAt(root, 0), CounterAt(root, 1));

        Frame(owner, page, rebuildAll: true);

        page.Builds.Should().Be(2);
        CounterAt(root, 0).Should().BeSameAs(a);
        a.Builds.Should().Be(2);
        b.Builds.Should().Be(2);
    }

    [Fact]
    public void ASetState_AsksForOneFrame()
    {
        var owner = new BuildOwner();
        var requests = 0;
        owner.BuildRequested += () => requests++;
        var root = Frame(owner, new Page(["a"]));

        CounterAt(root, 0).Press();
        CounterAt(root, 0).Press();

        requests.Should().Be(1, "a position already waiting to build does not ask again");
        owner.HasDirty.Should().BeTrue();
    }

    private sealed class SettingWhileBuilding : StatefulComponent
    {
        private bool _settled;

        public int Builds { get; private set; }

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            if (!_settled) SetState(() => _settled = true);
            return new Text(_settled ? "settled" : "settling", TypeRole.BodyM);
        }
    }

    /// <summary>A build that marks itself is built again in the NEXT frame: otherwise a component
    /// that always sets its state while building would never let a frame end.</summary>
    [Fact]
    public void ASetStateWhileBuilding_WaitsForTheNextFrame()
    {
        var owner = new BuildOwner();
        var requests = 0;
        owner.BuildRequested += () => requests++;
        var component = new SettingWhileBuilding();

        Frame(owner, component);
        component.Builds.Should().Be(1);
        requests.Should().Be(1, "the frame's end asked for the next one");

        Frame(owner, component, rebuildAll: false);
        component.Builds.Should().Be(2);
    }

    private sealed class BuildsItself : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new BuildsItself();
    }

    /// <summary>
    /// The boundary's bound, read from the tree: a component that builds itself is contained at the
    /// sixty-fifth nesting, as the walk that expanded it in measure contained it, instead of
    /// overflowing the stack.
    /// </summary>
    [Fact]
    public void AComponentThatBuildsItself_IsContainedAtTheBound()
    {
        ComponentBoundary.ClearContained();
        var root = Frame(BuildOwner.Structural(), new BuildsItself());

        var components = 0;
        Element position = root;
        while (position is ComponentElement)
        {
            components++;
            position = position.ChildAt(0)!;
        }

        components.Should().Be(65);
        position.Configuration.Should().BeOfType<Box>("what takes its place is the boundary's surface");
        ComponentBoundary.Contained.Should().Contain(nameof(BuildsItself));
        ComponentBoundary.ClearContained();
    }

    private sealed class Tracked(List<string> log) : StatefulComponent
    {
        protected override void OnMount() => log.Add("mounted");
        protected override void OnUnmount() => log.Add("unmounted");
        public override VisualNode Build(ComponentContext context) => new Text("tracked", TypeRole.BodyM);
    }

    /// <summary>An overlay's child is built when the realizer shows its layer, and leaves the tree
    /// when a frame ends without showing it.</summary>
    [Fact]
    public void ADeferredSlot_BuildsWhenShown_AndLeavesWhenNot()
    {
        var log = new List<string>();
        var owner = new BuildOwner();
        var overlay = new Overlay(new Tracked(log));
        var column = new Column();
        column.Add(overlay);

        var root = owner.Build(column, Context, WindowSizeClass.Compact, rebuildAll: true);
        var holder = (NodeElement)root.ChildAt(0)!;
        holder.ChildAt(0).Should().BeNull("the build phase leaves the slot to the realizer");
        holder.ShowDeferred(overlay.Child);
        owner.EndFrame();
        log.Should().Equal("mounted");

        owner.Build(column, Context, WindowSizeClass.Compact, rebuildAll: false);
        owner.EndFrame();

        log.Should().Equal("mounted", "unmounted");
        holder.ChildAt(0).Should().BeNull();
    }

    /// <summary>The store gave two siblings with one key ONE instance between them.</summary>
    [Fact]
    public void TwoSiblingsWithOneKey_EachHasItsOwnPosition()
    {
        var column = new Column();
        column.Add(new Counter("first") { Key = "same" });
        column.Add(new Counter("second") { Key = "same" });

        var root = Frame(BuildOwner.Structural(), column);

        ((ComponentElement)root.ChildAt(0)!).Component
            .Should().NotBeSameAs(((ComponentElement)root.ChildAt(1)!).Component);
    }

    /// <summary>One instance a parent placed at two positions is mounted at the last, and the other
    /// leaving does not end it there: only the position it is mounted at unmounts it.</summary>
    [Fact]
    public void OneInstanceAtTwoPositions_IsNotEndedByTheOneThatLeaves()
    {
        var log = new List<string>();
        var shared = new Tracked(log);
        var owner = new BuildOwner();
        var both = new Column();
        both.Add(shared);
        both.Add(shared);
        Frame(owner, both);

        var one = new Column();
        one.Add(new Text("gone", TypeRole.BodyM));
        one.Add(shared);
        Frame(owner, one);

        log.Should().Equal("mounted");
    }

    [Fact]
    public void AStructuralTree_AsksForNoFrame()
    {
        var owner = BuildOwner.Structural();
        var requests = 0;
        owner.BuildRequested += () => requests++;
        var root = Frame(owner, new Page(["a"]));

        CounterAt(root, 0).Press();

        requests.Should().Be(0, "no host draws a structural tree, so its instances hold no position");
        owner.HasDirty.Should().BeFalse();
    }

    private sealed class ReadsInFlow(List<bool> seen) : StatefulComponent
    {
        public void Touch() => SetState(() => { });

        public override VisualNode Build(ComponentContext context)
        {
            seen.Add(context.InFlow);
            return new Text("x", TypeRole.BodyM);
        }
    }

    /// <summary>
    /// The intent an InFlow arms reaches a component that builds ALONE, outside its parent's walk:
    /// its position knows what is above it.
    /// </summary>
    [Fact]
    public void AComponentUnderInFlow_BuildsInFlow_EvenWhenItBuildsAlone()
    {
        var inside = new List<bool>();
        var outside = new List<bool>();
        var reader = new ReadsInFlow(inside);
        var column = new Column();
        column.Add(new InFlow(reader));
        column.Add(new ReadsInFlow(outside));
        var owner = new BuildOwner();
        var root = Frame(owner, column);

        ((ReadsInFlow)((ComponentElement)root.ChildAt(0)!.ChildAt(0)!).Component).Touch();
        Frame(owner, column, rebuildAll: false);

        inside.Should().Equal(true, true);
        outside.Should().Equal(false);
        InFlow.Current.Should().BeFalse("the intent is put back after the build");
    }
}
