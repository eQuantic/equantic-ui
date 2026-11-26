using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A <see cref="LayoutBuilder"/> on Photon (#1555): its builder runs in the layout pass, as Flutter's
/// does, handed the room its box has, a box that fills on both axes. Driven through a real
/// <see cref="PhotonHost"/>, frame by frame, so the build in layout runs against the owner the host
/// keeps, with its components, their state and the slot the frame shows.
/// </summary>
public class LayoutBuilderTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static PhotonHost Open(VisualNode root, float width = 400, float height = 300)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, width, height);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    private static LayoutNode Frame(PhotonHost host, float timeMs = 16) =>
        host.RenderFrame(new DisplayListBuilder(), timeMs).Root;

    /// <summary>Every text the frame laid out, in the order it reached them.</summary>
    private static List<string> Texts(LayoutNode node)
    {
        var found = new List<string>();
        Walk(node);
        return found;

        void Walk(LayoutNode at)
        {
            if (at.Source is Text text) found.Add(text.Content);
            foreach (var child in at.Children) Walk(child);
        }
    }

    private static LayoutNode Find(LayoutNode node, Func<VisualNode, bool> match)
    {
        if (match(node.Source)) return node;
        foreach (var child in node.Children)
            if (Find(child, match) is { } found) return found;
        return null!;
    }

    private static LayoutBuilder Telling(List<BoxConstraints> told, Func<BoxConstraints, VisualNode>? child = null) =>
        new(room =>
        {
            told.Add(room);
            return child?.Invoke(room) ?? new Text("built", TypeRole.BodyM);
        });

    [Fact]
    public void ABuilderInABoxOf300By200_IsToldThatRoom()
    {
        var told = new List<BoxConstraints>();
        Open(new Box(new BoxStyle { Width = 300, Height = 200 }, Telling(told)));

        told.Should().NotBeEmpty();
        told[^1].Should().Be(new BoxConstraints(0, 300, 0, 200));
    }

    /// <summary>A width its parent decides at zero is a room of zero, not an unbounded one: the browser
    /// reads the same box the same way (layout-builder.spec.ts).</summary>
    [Fact]
    public void ABuilderInABoxDecidedAtZero_IsToldZero()
    {
        var told = new List<BoxConstraints>();
        Open(new Box(new BoxStyle { Width = 0, Height = 200 }, Telling(told)));

        told[^1].Should().Be(new BoxConstraints(0, 0, 0, 200));
    }

    /// <summary>The box the builder takes its room as fills both axes, so the node is the room it was
    /// told, whatever it built inside it: a text far smaller than the box changes nothing.</summary>
    [Fact]
    public void TheBuilderTakesTheRoomItIsTold_WhateverItBuilt()
    {
        var told = new List<BoxConstraints>();
        var root = Frame(Open(new Box(new BoxStyle { Width = 300, Height = 200 }, Telling(told))));

        var builder = Find(root, node => node is LayoutBuilder);
        builder.Bounds.Width.Should().Be(300);
        builder.Bounds.Height.Should().Be(200);
    }

    [Fact]
    public void ABuilderInAColumnSizedByWhatItHolds_IsToldAnUnboundedHeight()
    {
        var told = new List<BoxConstraints>();
        var column = new Column();
        column.Add(Telling(told));
        Open(column, width: 400, height: 300);

        told[^1].MaxWidth.Should().Be(400, "the column decides its width, the window's");
        told[^1].HasBoundedHeight.Should().BeFalse("the column takes its height from what it holds");
        told[^1].MinWidth.Should().Be(0);
        told[^1].MinHeight.Should().Be(0);
    }

    [Fact]
    public void ABuilderInARow_IsToldTheRoomTheRowLeavesIt()
    {
        var told = new List<BoxConstraints>();
        var row = new Row { Width = SizeValue.Fill };
        row.Add(new Box(new BoxStyle { Width = 100, Height = 20 }));
        row.Add(Telling(told));
        Open(new Box(new BoxStyle { Width = 400, Height = 300 }, row));

        told[^1].MaxWidth.Should().Be(300, "the builder fills what the fixed box leaves of the row");
    }

    [Fact]
    public void ABuilderInsideABuilder_IsToldWhatTheOuterChildGivesIt()
    {
        var inner = new List<BoxConstraints>();
        var outer = new LayoutBuilder(room =>
            new Box(new BoxStyle { Width = room.MaxWidth / 2, Height = 100 }, Telling(inner)));
        Open(new Box(new BoxStyle { Width = 400, Height = 300 }, outer));

        inner[^1].Should().Be(new BoxConstraints(0, 200, 0, 100));
    }

    /// <summary>A frame that lays the same node out in the same room shows the child it built and runs
    /// nothing: the builder is not a paint callback.</summary>
    [Fact]
    public void TheSameRoom_RunsTheBuilderOnce_HoweverManyFramesItIsDrawnIn()
    {
        var told = new List<BoxConstraints>();
        var host = Open(new Box(new BoxStyle { Width = 300, Height = 200 }, Telling(told)));

        for (var frame = 1; frame <= 5; frame++) Frame(host, frame * 16f);

        told.Should().HaveCount(1);
    }

    private abstract class Mounted(string name, List<string> log) : StatefulComponent
    {
        protected override void OnMount() => log.Add($"{name} mounted");
        protected override void OnUnmount() => log.Add($"{name} unmounted");
        public override VisualNode Build(ComponentContext context) => new Text(name, TypeRole.BodyM);
    }

    // Two types, so the narrower room's child is another component and not the wide one adopting a new
    // configuration at the same position.
    private sealed class Wide(List<string> log) : Mounted("wide", log);

    private sealed class Narrow(List<string> log) : Mounted("narrow", log);

    [Fact]
    public void ANarrowerRoom_BuildsAnotherChild_AndTheOldOnesComponentsLeave()
    {
        var log = new List<string>();
        var builder = new LayoutBuilder(room => room.MaxWidth >= 480
            ? new Wide(log)
            : new Narrow(log));
        var host = Open(builder, width: 600, height: 300);
        log.Should().Equal("wide mounted");

        host.Resize(360, 300);
        var root = Frame(host);

        Texts(root).Should().Equal("narrow");
        log.Should().Equal("wide mounted", "wide unmounted", "narrow mounted");
    }

    private sealed class Holder(Func<int, VisualNode> child) : StatefulComponent
    {
        private int _count;

        public void Bump() => SetState(() => _count++);

        public override VisualNode Build(ComponentContext context) => child(_count);
    }

    /// <summary>The builder is the closure of the component that holds it, so when that component builds
    /// again the node is a new one, and the builder runs again with what the component now holds.</summary>
    [Fact]
    public void TheComponentThatHoldsTheBuilderBuildingAgain_RunsTheBuilderAgain()
    {
        var told = new List<BoxConstraints>();
        var holder = new Holder(count => new Box(new BoxStyle { Width = 300, Height = 200 },
            Telling(told, _ => new Text($"count {count}", TypeRole.BodyM))));
        var host = Open(holder);

        holder.Bump();
        var root = Frame(host);

        told.Should().HaveCount(2);
        Texts(root).Should().Equal("count 1");
    }

    private sealed class Counter : StatefulComponent
    {
        public static Counter? Last;
        private int _count;

        public Counter() => Last = this;

        public void Bump() => SetState(() => _count++);

        public override VisualNode Build(ComponentContext context) => new Text($"counter {_count}", TypeRole.BodyM);
    }

    /// <summary>A component the builder built keeps its position across frames, and a SetState inside it
    /// builds it in the next frame, when the slot is shown, without running the builder.</summary>
    [Fact]
    public void AComponentUnderTheBuilder_KeepsItsStateAndBuildsWhenItChanges()
    {
        var told = new List<BoxConstraints>();
        var host = Open(new Box(new BoxStyle { Width = 300, Height = 200 },
            Telling(told, _ => new Counter())));
        var kept = Counter.Last!;

        kept.Bump();
        var root = Frame(host);

        Texts(root).Should().Equal("counter 1");
        told.Should().HaveCount(1, "the room and the node did not change");
    }

    /// <summary>
    /// A parent that measures the builder twice in a frame: in a row, a box with a minimum width hugs, so
    /// the builder is first told an unbounded width, and the box, settling at its minimum, measures it again
    /// at 200. Photon lays out in every frame, so without a memory of the pair it would build both children
    /// in every frame and take the settled one's components out and put them back in each time. Each room
    /// is built once, and from the second frame on the child the first frame settled on stays.
    /// </summary>
    [Fact]
    public void ABuilderMeasuredTwiceAFrame_BuildsEachRoomOnce_AndKeepsTheChildItSettledOn()
    {
        var log = new List<string>();
        var told = new List<BoxConstraints>();
        var row = new Row();
        row.Add(new eQuantic.UI.Components.ConstrainedBox(new BoxConstraints(minWidth: 200),
            new LayoutBuilder(room =>
            {
                told.Add(room);
                return room.MaxWidth >= 480 ? new Wide(log) : new Narrow(log);
            })));
        var host = Open(new Box(new BoxStyle { Width = 600, Height = 300 }, row), width: 800);
        var afterFirstFrame = log.ToList();

        LayoutNode root = null!;
        for (var frame = 1; frame <= 3; frame++) root = Frame(host, frame * 16f);

        told.Select(room => room.MaxWidth).Should().Equal(float.PositiveInfinity, 200);
        log.Should().Equal(afterFirstFrame, "the settled child's components stay mounted");
        log.Should().EndWith("narrow mounted");
        Texts(root).Should().Equal("narrow");
    }

    /// <summary>A held value applied: in a column 600 wide whose children stretch, a box loose at 320 by
    /// 480 is its maximum wide, the CSS rule a Box follows, and its child is laid out within it.</summary>
    [Fact]
    public void AConstrainedBoxInAWideSlot_IsItsMaximumWide()
    {
        var column = new Column { Width = SizeValue.Fill };
        column.Add(new eQuantic.UI.Components.ConstrainedBox(BoxConstraints.Loose(new Size(320, 480)),
            new Box(new BoxStyle { Width = SizeValue.Fill, Height = 40 })));
        var root = Frame(Open(new Box(new BoxStyle { Width = 600, Height = 300 }, column), width: 800));

        var fill = Find(root, node => node is Box { Style.Height: { Kind: SizeKind.Fixed, Value: 40 } });
        fill.Bounds.Width.Should().Be(320);
    }

    /// <summary>
    /// The dashboard's /layout page, laid out on Photon at the window the e2e suite reads it in a browser
    /// (tests/e2e/tests/layout-builder.spec.ts): the fixed cards are told the same room on both targets,
    /// and the filling one an unbounded height. The tree is the page's own cards, written again here
    /// because this suite does not reference the sample.
    /// </summary>
    [Fact]
    public void TheLayoutPagesCards_AreToldTheRoomTheBrowserTellsThem()
    {
        var told = new Dictionary<string, BoxConstraints>();
        VisualNode Card(string name, BoxStyle style) => new Box(style, new LayoutBuilder(room =>
        {
            told[name] = room;
            return new Text(name, TypeRole.Label);
        }));

        var page = new Column(gap: 24);
        page.Add(Card("fixed", new BoxStyle { Width = 600, Height = 200 }));
        page.Add(Card("narrow", new BoxStyle { Width = 360, Height = 200 }));
        page.Add(Card("fill", new BoxStyle { Width = SizeValue.Fill }));
        Open(page, width: 1280, height: 900);

        told["fixed"].Should().Be(new BoxConstraints(0, 600, 0, 200));
        told["narrow"].Should().Be(new BoxConstraints(0, 360, 0, 200));
        told["fill"].HasBoundedHeight.Should().BeFalse("the card sizes its height from what it holds");
        told["fill"].MaxWidth.Should().Be(1280, "the card fills the window's width, which no chrome narrows here");

        // The e2e suite's narrower window: the filling card is told less than 480, so its parts stack.
        Open(page, width: 420, height: 900);
        told["fill"].MaxWidth.Should().Be(420);
        told["fill"].HasBoundedHeight.Should().BeFalse();
    }
}
