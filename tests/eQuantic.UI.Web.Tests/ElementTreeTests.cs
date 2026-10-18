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

    private sealed class Logged(string label, List<string> log) : StatefulComponent
    {
        public string Label { get; } = label;
        public int Count { get; private set; }

        public void Press() => SetState(() => Count++);

        protected override void OnMount() => log.Add($"mount:{Label}");
        protected override void OnUnmount() => log.Add($"unmount:{Label}");

        public override VisualNode Build(ComponentContext context) => new Text($"{Label}: {Count}", TypeRole.BodyM);
    }

    /// <summary>Two siblings that say the same key, built fresh by every build of their parent.</summary>
    private sealed class TwinKeys(List<string> log) : StatefulComponent
    {
        public void Rebuild() => SetState(() => { });

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column();
            column.Add(new Logged("first", log) { Key = "same" });
            column.Add(new Logged("second", log) { Key = "same" });
            return column;
        }
    }

    /// <summary>
    /// Two siblings with one key each have a position of their own, which the store did not give
    /// them (it resolved both to one instance), and KEEP it when their parent rebuilds: the second
    /// was made again on every rebuild, its state lost and its hooks run each time (Copilot on #960).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoSiblingsWithOneKey_EachKeepsItsOwnPosition(bool rebuildAll)
    {
        var log = new List<string>();
        var parent = new TwinKeys(log);
        var owner = new BuildOwner();
        var column = Frame(owner, parent).ChildAt(0)!;
        var second = (Logged)((ComponentElement)column.ChildAt(1)!).Component;
        ((ComponentElement)column.ChildAt(0)!).Component.Should().NotBeSameAs(second);

        second.Press();
        parent.Rebuild();
        Frame(owner, parent, rebuildAll);

        ((ComponentElement)column.ChildAt(1)!).Component.Should().BeSameAs(second);
        TextIn(column, 1).Should().Be("second: 1");
        log.Should().Equal("mount:first", "mount:second");
    }

    private static Counter CounterIn(Element column, int index) =>
        (Counter)((ComponentElement)column.ChildAt(index)!).Component;

    private static string TextIn(Element column, int index) =>
        ((Text)column.ChildAt(index)!.ChildAt(0)!.Configuration).Content;

    /// <summary>One instance a parent placed at two positions shows at both, so a <c>SetState</c>
    /// builds both (Copilot on #960: only the last-mounted one built).</summary>
    [Fact]
    public void OneInstanceAtTwoPositions_ASetStateBuildsBoth()
    {
        var shared = new Counter("shared");
        var both = new Column();
        both.Add(shared);
        both.Add(shared);
        var owner = new BuildOwner();
        var root = Frame(owner, both);

        shared.Press();
        Frame(owner, both, rebuildAll: false);

        TextIn(root, 0).Should().Be("shared: 1");
        TextIn(root, 1).Should().Be("shared: 1");
    }

    /// <summary>It stays mounted until the LAST position it holds leaves, whichever leaves first.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OneInstanceAtTwoPositions_UnmountsWhenTheLastLeaves(int leavesFirst)
    {
        var log = new List<string>();
        var shared = new Tracked(log);
        var owner = new BuildOwner();
        Frame(owner, Placed(shared, shared));

        Frame(owner, leavesFirst == 0 ? Placed(new Text("gone", TypeRole.BodyM), shared)
            : Placed(shared, new Text("gone", TypeRole.BodyM)));
        log.Should().Equal(["mounted"], "the other position still shows it");

        Frame(owner, Placed(new Text("gone", TypeRole.BodyM), new Text("gone too", TypeRole.BodyM)));
        log.Should().Equal("mounted", "unmounted");
    }

    private static Column Placed(VisualNode first, VisualNode second)
    {
        var column = new Column();
        column.Add(first);
        column.Add(second);
        return column;
    }

    private sealed class MarksItselfWhenAsked(string label) : StatefulComponent
    {
        public string Label { get; private set; } = label;
        public bool MarkOnNextBuild { get; set; }
        public int Builds { get; private set; }

        public void Touch() => SetState(() => { });

        public override void AdoptConfig(UiComponent next)
        {
            if (next is MarksItselfWhenAsked fresh) Label = fresh.Label;
        }

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            if (MarkOnNextBuild)
            {
                MarkOnNextBuild = false;
                SetState(() => { });
            }

            return new Text(Label, TypeRole.BodyM);
        }
    }

    private sealed class Holding(string label) : StatefulComponent
    {
        private string _label = label;

        public MarksItselfWhenAsked? First { get; private set; }

        public void Relabel(string next) => SetState(() => _label = next);

        public override VisualNode Build(ComponentContext context)
        {
            var child = new MarksItselfWhenAsked(_label);
            First ??= child;
            return child;
        }
    }

    /// <summary>
    /// A marked child its marked parent rebuilds builds ONCE in the frame, even when it marks itself
    /// while building: the mark waits for the next frame, and the child's own entry in the dirty
    /// list is passed over (Copilot on #960: it built a second time in the same frame).
    /// </summary>
    [Fact]
    public void AMarkedChildItsMarkedParentRebuilds_BuildsOnceAFrame()
    {
        var owner = new BuildOwner();
        var parent = new Holding("first");
        Frame(owner, parent);
        var child = parent.First!;

        child.MarkOnNextBuild = true;
        child.Touch();
        parent.Relabel("second");
        Frame(owner, parent, rebuildAll: false);
        child.Builds.Should().Be(2, "the parent's build rebuilt it once, with its new label");
        child.Label.Should().Be("second");

        Frame(owner, parent, rebuildAll: false);
        child.Builds.Should().Be(3, "the mark it made while building is the next frame's");

        Frame(owner, parent, rebuildAll: false);
        child.Builds.Should().Be(3);
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

    // ---- what a target keeps for a node (#975) ---------------------------------------------------

    /// <summary>What a realizer keeps for a node: it says when it is let go.</summary>
    private sealed class Kept(List<string> log, string name) : IDisposable
    {
        public void Dispose() => log.Add(name);
    }

    private static Box Keyed(string key) => new(new BoxStyle()) { Key = key };

    [Fact]
    public void ANodesState_StaysWhileItsPositionDoes()
    {
        var log = new List<string>();
        var owner = BuildOwner.Structural();
        var position = (NodeElement)Frame(owner, Keyed("a"));
        var kept = new Kept(log, "a");
        position.State = kept;

        Frame(owner, Keyed("a")).Should().BeSameAs(position, "the same type and key keep the position");

        position.State.Should().BeSameAs(kept);
        log.Should().BeEmpty();
    }

    [Fact]
    public void ANodesState_IsReleasedOnce_WhenAnotherKeyOrTypeTakesItsPlace()
    {
        var log = new List<string>();
        var owner = BuildOwner.Structural();
        var first = (NodeElement)Frame(owner, Keyed("a"));
        first.State = new Kept(log, "a");

        var second = (NodeElement)Frame(owner, Keyed("b"));
        second.State = new Kept(log, "b");
        Frame(owner, new Text("c", TypeRole.BodyM));
        Frame(owner, new Text("c", TypeRole.BodyM));

        log.Should().Equal("a", "b");
        first.State.Should().BeNull("a position that left keeps nothing");
        second.State.Should().BeNull();
    }

    [Fact]
    public void ASubtreesStates_AreReleasedChildrenFirst()
    {
        var log = new List<string>();
        var owner = BuildOwner.Structural();
        var column = new Column();
        column.Add(new Box(new BoxStyle(), new Box(new BoxStyle())));
        var root = (NodeElement)Frame(owner, column);
        var box = (NodeElement)root.ChildAt(0)!;
        root.State = new Kept(log, "column");
        box.State = new Kept(log, "box");
        ((NodeElement)box.ChildAt(0)!).State = new Kept(log, "inner");

        Frame(owner, new Column());
        log.Should().Equal("inner", "box");

        Frame(owner, new Text("gone", TypeRole.BodyM));
        log.Should().Equal("inner", "box", "column");
    }

    [Fact]
    public void ANodesState_IsReleased_WhenItsLayerIsNotShown_OrTheTreeComesDown()
    {
        var log = new List<string>();
        var owner = new BuildOwner();
        var overlay = new Overlay(new Box(new BoxStyle()));
        var column = new Column();
        column.Add(overlay);
        column.Add(new Box(new BoxStyle()));

        var root = owner.Build(column, Context, WindowSizeClass.Compact, rebuildAll: true);
        var holder = (NodeElement)root.ChildAt(0)!;
        ((NodeElement)holder.ShowDeferred(overlay.Child)).State = new Kept(log, "layer");
        ((NodeElement)root.ChildAt(1)!).State = new Kept(log, "page");
        owner.EndFrame();

        owner.Build(column, Context, WindowSizeClass.Compact, rebuildAll: false);
        owner.EndFrame();
        log.Should().Equal(new[] { "layer" }, "a frame that does not show the layer takes what it held out");

        owner.UnmountAll();
        log.Should().Equal("layer", "page");
    }

    // ---- what a host finds a node by (#976) ------------------------------------------------------

    private static List<Element> Everything(Element root)
    {
        var all = new List<Element> { root };
        for (var slot = 0; slot < 16; slot++)
            if (root.ChildAt(slot) is { } child) all.AddRange(Everything(child));
        return all;
    }

    [Fact]
    public void EveryPosition_HasAnIdOfItsOwn()
    {
        var root = Frame(BuildOwner.Structural(), new Page(["a", "b", "c"]));

        var ids = Everything(root).Select(element => element.Id).ToList();
        ids.Should().HaveCount(8, "the page, its column, three counters and their three texts");
        ids.Should().OnlyHaveUniqueItems().And.NotContain(0, "zero names no position");
    }

    [Fact]
    public void APosition_KeepsItsId_WhileItHoldsItsNode()
    {
        var owner = BuildOwner.Structural();
        var page = new Page(["a", "b"]) { Keyed = true };
        var root = Frame(owner, page);
        var b = root.ChildAt(0)!.ChildAt(1)!;
        var id = b.Id;

        page.Show("b", "a");
        Frame(owner, page);

        root.ChildAt(0)!.ChildAt(0).Should().BeSameAs(b, "the key kept the counter's position as it moved");
        b.Id.Should().Be(id);
    }

    [Fact]
    public void ANodeAtAnothersPlace_TakesANewId()
    {
        var owner = BuildOwner.Structural();
        var first = Frame(owner, Keyed("a"));
        var second = Frame(owner, Keyed("b"));
        var third = Frame(owner, new Text("c", TypeRole.BodyM));

        second.Should().NotBeSameAs(first);
        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems(
            "an id is never handed on, so a host holding the old one finds nothing rather than the newcomer");
    }

    [Fact]
    public void APart_KeepsItsId_WhileItsPositionLives()
    {
        var owner = BuildOwner.Structural();
        var paragraph = (NodeElement)Frame(owner, Keyed("a"));
        var second = paragraph.PartId(1);
        var first = paragraph.PartId(0);

        paragraph.PartId(1).Should().Be(second, "asked again, a part answers what it answered");
        new[] { paragraph.Id, first, second }.Should().OnlyHaveUniqueItems(
            "a part and a position draw from one count");

        Frame(owner, Keyed("a")).Should().BeSameAs(paragraph);
        paragraph.PartId(0).Should().Be(first, "the position lived, so its parts did");

        var next = (NodeElement)Frame(owner, Keyed("b"));
        next.PartId(0).Should().NotBe(first, "a new position's parts are new too");
        next.Invoking(element => element.PartId(-1)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
