using eQuantic.Console;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// An app's own gesture recognizer on Photon (#1563), driven through the host as a shell drives it: presses, moves,
/// releases and frames, with a finger and with a mouse, over a laid-out tree. The recognizer of the long-press
/// cases is the dashboard's own, compiled here from the sample's file (<c>HoldThenDrag.cs</c>), so the class a
/// person runs in the browser is the class this suite runs. The arena is Flutter's whole on Photon: a scroll view's
/// pan, a press and a drag are members beside the recognizer, and the first to accept takes the pointer.
/// </summary>
public class RawGestureDetectorHostTests
{
    private const float CardHeight = 120;
    private const int Cards = 6;

    /// <summary>A recognizer that writes down everything it hears, and asks for a deadline when told to.</summary>
    private class Probe : GestureRecognizer
    {
        public List<string> Heard { get; } = [];
        public List<PointerEvent> Events { get; } = [];
        public double? Deadline { get; set; }

        /// <summary>A deadline it asks for on its first move, rather than at the down.</summary>
        public double? DeadlineOnMove { get; set; }

        protected override void AddAllowedPointer(PointerEvent down)
        {
            Heard.Add("down");
            Events.Add(down);
            if (Deadline is { } wait) StartDeadline(wait);
        }

        protected override void HandleEvent(PointerEvent ev)
        {
            Heard.Add(ev.Phase.ToString().ToLowerInvariant());
            Events.Add(ev);
            if (ev.Phase == PointerPhase.Move && DeadlineOnMove is { } wait)
            {
                DeadlineOnMove = null;
                StartDeadline(wait);
            }
        }

        public override void AcceptGesture(int pointer) => Heard.Add("accept");
        public override void RejectGesture(int pointer) => Heard.Add("reject");
        public override void DidExceedDeadline(int pointer) => Heard.Add("deadline");
    }

    private sealed class ProbeA : Probe;

    /// <summary>A probe that wants only the primary button, as the sample's recognizer does.</summary>
    private sealed class PrimaryOnly : Probe
    {
        public override bool IsPointerAllowed(PointerEvent down) => (down.Buttons & PointerButtons.Primary) != 0;
    }

    private sealed class ProbeB : Probe;

    /// <summary>The dashboard's gestures screen, as small as the cases need: a scrolling column of cards, each in
    /// a detector of the sample's <see cref="HoldThenDrag"/>, each holding a button.</summary>
    private sealed class Screen : StatefulComponent
    {
        public List<HoldThenDrag> Made { get; } = [];
        public List<string> Held { get; } = [];
        public List<Point> Moved { get; } = [];
        public List<Point> Released { get; } = [];
        public List<string> Pressed { get; } = [];
        public int Builds { get; private set; }

        public void Rebuild() => SetState(() => { });

        public override VisualNode Build(ComponentContext context)
        {
            var build = ++Builds;
            var column = new Column(gap: 8);
            for (var i = 0; i < Cards; i++)
            {
                var card = i;
                column.Add(new RawGestureDetector(
                    new Box(new BoxStyle { Width = SizeValue.Fill, Height = CardHeight, Background = context.Theme.Surface },
                        new Pressable(new Box(new BoxStyle { Width = 160, Height = 48, Background = Fill }),
                            () => Pressed.Add($"card {card}"))),
                    [
                        new GestureRecognizerFactoryWithHandlers<HoldThenDrag>(() =>
                        {
                            var made = new HoldThenDrag();
                            Made.Add(made);
                            return made;
                        }, drag =>
                        {
                            drag.OnHeld = () => Held.Add($"card {card}, build {build}");
                            drag.OnMoved = offset => Moved.Add(offset);
                            drag.OnReleased = offset => Released.Add(offset);
                        }),
                    ]));
            }
            return new Box(new BoxStyle { Width = 300, Height = 300 }, new ScrollView(column));
        }
    }

    /// <summary>A page whose detector holds what <paramref name="gestures"/> says, around what
    /// <paramref name="child"/> builds.</summary>
    private sealed class Page(Func<Page, VisualNode> child, Func<Page, IReadOnlyList<GestureRecognizerFactory>> gestures)
        : StatefulComponent
    {
        public bool Second { get; private set; }
        public List<string> Pressed { get; } = [];
        public List<string> Dragged { get; } = [];

        public void UseTheSecond() => SetState(() => Second = true);

        public override VisualNode Build(ComponentContext context) =>
            new RawGestureDetector(child(this), gestures(this));
    }

    private static readonly ColorToken Fill = new(Color.FromRgb(0x10, 0x20, 0x30));

    private static Box Filled(float width, float height) =>
        new(new BoxStyle { Width = width, Height = height, Background = Fill });

    private static PhotonHost Open(StatefulComponent root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 600, 600) { SmoothScroll = false };
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        return host;
    }

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var below in Walk(child))
                yield return below;
    }

    /// <summary>The centre of the first card's button, where the last frame drew it.</summary>
    private static Point Button(PhotonHost host) =>
        Walk(host.LastFrame!.Root).First(n => n.Source is Pressable).Bounds.Center;

    /// <summary>A point of the first card below its button.</summary>
    private static Point CardBeside(PhotonHost host)
    {
        var card = Walk(host.LastFrame!.Root).First(n => n.Source is RawGestureDetector).Bounds;
        var button = Walk(host.LastFrame!.Root).First(n => n.Source is Pressable).Bounds;
        card.Contains(new Point(card.X + 10, card.Y + 100)).Should().BeTrue();
        button.Contains(new Point(card.X + 10, card.Y + 100)).Should().BeFalse();
        return new Point(card.X + 10, card.Y + 100);
    }

    private static Rect Detector(PhotonHost host) =>
        Walk(host.LastFrame!.Root).First(n => n.Source is RawGestureDetector).Bounds;

    private static float Offset(PhotonHost host) =>
        host.ScrollOffsetOf(host.LastFrame!.ScrollRegions.Single(r => r.MaxOffset > 0).Element);

    [Theory]
    [InlineData(PointerKind.Touch)]
    [InlineData(PointerKind.Mouse)]
    public void AHoldThenADrag_InsideAScrollView_MovesTheCard_AndLeavesTheOffsetWhereItWas(PointerKind kind)
    {
        var screen = new Screen();
        var host = Open(screen);
        var at = Button(host);
        var before = Offset(host);

        host.PressDown(at.X, at.Y, kind: kind);
        host.RenderFrame(new DisplayListBuilder(), 100);
        host.RenderFrame(new DisplayListBuilder(), 400);
        host.RenderFrame(new DisplayListBuilder(), 700);
        screen.Held.Should().ContainSingle("the hold lasted past ContextTarget.HoldMs without a move");
        host.PointerMove(at.X + 20, at.Y + 30, kind);
        host.PointerMove(at.X + 60, at.Y + 80, kind);
        host.PressUp(at.X + 60, at.Y + 80, kind);
        host.RenderFrame(new DisplayListBuilder(), 716);

        screen.Moved.Should().Equal(new Point(20, 30), new Point(60, 80));
        screen.Released.Should().Equal(new Point(60, 80));
        Offset(host).Should().Be(before, "the recognizer took the pointer from the scroll view's pan");
        screen.Pressed.Should().BeEmpty("the hold took the button's press");
    }

    [Theory]
    [InlineData(PointerKind.Touch)]
    [InlineData(PointerKind.Mouse)]
    public void ASwipeAtOnce_ScrollsTheList_AndTheRecognizerHearsItsRejection(PointerKind kind)
    {
        var screen = new Screen();
        var host = Open(screen);
        var at = CardBeside(host);
        var down = new PointerEvent(PointerPhase.Down, Point.Zero, kind, PointerButtons.Primary, KeyModifiers.None, 1,
            Point.Zero, 0);

        host.PressDown(at.X, at.Y, kind: kind);
        host.RenderFrame(new DisplayListBuilder(), 50);
        var recognizer = screen.Made.First();
        recognizer.IsPointerAllowed(down).Should().BeFalse("it follows the pointer that went down on its card");
        host.PointerMove(at.X, at.Y - 40, kind);
        host.PointerMove(at.X, at.Y - 80, kind);

        recognizer.IsPointerAllowed(down).Should().BeTrue("it lost the pointer, and follows none any more");
        Offset(host).Should().Be(80, "the pan accepted past the slop and the content follows the finger");
        host.RenderFrame(new DisplayListBuilder(), 700);
        host.PressUp(at.X, at.Y - 80, kind);

        screen.Held.Should().BeEmpty("a recognizer that lost its pointer hears its deadline no more");
        screen.Moved.Should().BeEmpty();
        screen.Released.Should().BeEmpty();
    }

    [Theory]
    [InlineData(PointerKind.Touch)]
    [InlineData(PointerKind.Mouse)]
    public void ATap_PressesTheButton(PointerKind kind)
    {
        var screen = new Screen();
        var host = Open(screen);
        var at = Button(host);

        host.PressDown(at.X, at.Y, kind: kind);
        host.RenderFrame(new DisplayListBuilder(), 80);
        host.PressUp(at.X, at.Y, kind);
        host.RenderFrame(new DisplayListBuilder(), 700);

        screen.Pressed.Should().Equal("card 0");
        screen.Held.Should().BeEmpty();
        screen.Released.Should().BeEmpty();
    }

    [Theory]
    [InlineData(PointerKind.Touch)]
    [InlineData(PointerKind.Mouse)]
    public void AtTheUp_TheButtonInsideTheDetectorWinsTheSweep_AndTheRecognizerIsRejected(PointerKind kind)
    {
        var probe = new Probe();
        var page = new Page(p => new Pressable(Filled(200, 100), () => p.Pressed.Add("button")),
            _ => [new GestureRecognizerFactoryWithHandlers<Probe>(() => probe, _ => { })]);
        var host = Open(page);
        var box = Detector(host);

        host.PressDown(box.X + 20, box.Y + 20, kind: kind);
        host.PressUp(box.X + 20, box.Y + 20, kind);

        page.Pressed.Should().Equal("button");
        probe.Heard.Should().Equal("down", "up", "reject");
    }

    [Theory]
    [InlineData(PointerKind.Touch)]
    [InlineData(PointerKind.Mouse)]
    public void ADraggableThatArmsFirst_TakesThePointer_AndTheRecognizersDeadlineNeverRuns(PointerKind kind)
    {
        var probe = new Probe { Deadline = 500 };
        var page = new Page(p => new Draggable(Filled(200, 100), travel => p.Dragged.Add($"released {travel}"))
            {
                Axis = DragAxis.Horizontal,
                Min = -200,
                Max = 200,
                OnMoved = travel => p.Dragged.Add($"moved {travel}"),
            },
            _ => [new GestureRecognizerFactoryWithHandlers<Probe>(() => probe, _ => { })]);
        var host = Open(page);
        var box = Detector(host);

        host.PressDown(box.X + 20, box.Y + 20, kind: kind);
        host.RenderFrame(new DisplayListBuilder(), 50);
        host.PointerMove(box.X + 40, box.Y + 20, kind);
        host.PointerMove(box.X + 60, box.Y + 20, kind);
        host.RenderFrame(new DisplayListBuilder(), 700);
        host.PressUp(box.X + 60, box.Y + 20, kind);

        page.Dragged.Should().Equal("moved 20", "moved 40", "released 40");
        probe.Heard.Should().Equal("down", "move", "reject");
    }

    [Theory]
    [InlineData(PointerKind.Touch)]
    [InlineData(PointerKind.Mouse)]
    public void APanThatTravelsPastTheSlopFirst_TakesThePointer_FromARecognizerThatWaits(PointerKind kind)
    {
        var probe = new Probe { Deadline = 500 };
        var page = new Page(_ =>
            {
                var column = new Column(gap: 0);
                for (var i = 0; i < 6; i++) column.Add(Filled(200, 100));
                return new Box(new BoxStyle { Width = 200, Height = 200 }, new ScrollView(column));
            },
            _ => [new GestureRecognizerFactoryWithHandlers<Probe>(() => probe, _ => { })]);
        var host = Open(page);
        var box = Detector(host);

        host.PressDown(box.X + 50, box.Y + 150, kind: kind);
        host.RenderFrame(new DisplayListBuilder(), 50);
        host.PointerMove(box.X + 50, box.Y + 100, kind);
        host.RenderFrame(new DisplayListBuilder(), 700);
        host.PressUp(box.X + 50, box.Y + 100, kind);

        Offset(host).Should().Be(50);
        probe.Heard.Should().Equal("down", "move", "reject");
    }

    [Fact]
    public void APressAroundADetectorAroundADraggable_ReadsInHitOrder_TheDraggableFirstAndThePressLast()
    {
        var probe = new Probe();
        var page = new Page(p => new Draggable(Filled(200, 100), travel => p.Dragged.Add($"released {travel}"))
            {
                Axis = DragAxis.Horizontal,
                Min = -200,
                Max = 200,
            },
            _ => [new GestureRecognizerFactoryWithHandlers<Probe>(() => probe, _ => { })]);
        var pressed = new List<string>();
        var host = Open(new Switch(() => new Pressable(page, () => pressed.Add("press"))));
        var box = Detector(host);

        host.PressDown(box.X + 20, box.Y + 20, kind: PointerKind.Touch);
        host.PressUp(box.X + 20, box.Y + 20, PointerKind.Touch);

        // [Draggable, recognizer, Pressable]: at the up the sweep gives the tap to the innermost, the draggable, which
        // takes nothing from the press around it, and the recognizer between them hears its rejection.
        probe.Heard.Should().Equal("down", "up", "reject");
        pressed.Should().Equal("press");
    }

    [Fact]
    public void ARecognizerThatNeverAsks_LeavesTheButtonItsPress()
    {
        var probe = new Probe { Deadline = 500 };
        var page = new Page(p => new Pressable(Filled(200, 100), () => p.Pressed.Add("button")),
            _ => [new GestureRecognizerFactoryWithHandlers<Probe>(() => probe, _ => { })]);
        var host = Open(page);
        var box = Detector(host);

        host.PressDown(box.X + 20, box.Y + 20, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 50);
        host.RenderFrame(new DisplayListBuilder(), 600);
        probe.Heard.Should().Equal("down", "deadline");
        host.PressUp(box.X + 20, box.Y + 20, PointerKind.Touch);

        // The probe never asked for the pointer: at the up the sweep gives it to the first member, the button.
        page.Pressed.Should().Equal("button");
    }

    [Fact]
    public void AHeldButton_PressesNothing_OnceTheSamplesRecognizerTookIt()
    {
        var screen = new Screen();
        var host = Open(screen);
        var at = Button(host);

        host.PressDown(at.X, at.Y, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100);
        host.RenderFrame(new DisplayListBuilder(), 800);
        host.PressUp(at.X, at.Y, PointerKind.Touch);

        screen.Held.Should().ContainSingle();
        screen.Released.Should().Equal(Point.Zero);
        screen.Pressed.Should().BeEmpty();
    }

    [Fact]
    public void ARecognizer_FollowsItsPointerPastItsDetectorsEdge_InTheDetectorsBox()
    {
        var probe = new Probe();
        var page = new Page(_ => Filled(200, 100), _ => [new GestureRecognizerFactoryWithHandlers<Probe>(() => probe, _ => { })]);
        var host = Open(page);
        var box = Detector(host);

        host.PressDown(box.X + 10, box.Y + 6, timeMs: 1000);
        host.PointerMove(box.X + 210, box.Y + 6, timeMs: 1016);
        host.PressUp(box.X + 210, box.Y + 6, timeMs: 1032);

        probe.Events.Select(e => (e.Phase, e.Position, e.Buttons, e.TimeMs)).Should().Equal(
            (PointerPhase.Down, new Point(10, 6), PointerButtons.Primary, 1000d),
            (PointerPhase.Move, new Point(210, 6), PointerButtons.Primary, 1016d),
            (PointerPhase.Up, new Point(210, 6), PointerButtons.None, 1032d));
        probe.Heard.Should().Equal("down", "accept", "move", "up");
    }

    [Fact]
    public void ARebuildDuringALongPress_KeepsTheRecognizer_AndItRunsTheLatestCallbacks()
    {
        var made = 0;
        var held = new List<int>();
        var builds = 0;
        var page = new Page(_ => Filled(200, 100), _ =>
        {
            var build = ++builds;
            return
            [
                new GestureRecognizerFactoryWithHandlers<HoldThenDrag>(() =>
                {
                    made++;
                    return new HoldThenDrag();
                }, drag => drag.OnHeld = () => held.Add(build)),
            ];
        });
        var host = Open(page);
        var box = Detector(host);

        host.PressDown(box.X + 20, box.Y + 20, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100);
        for (var i = 0; i < 3; i++)
        {
            page.UseTheSecond();
            host.RenderFrame(new DisplayListBuilder(), 200 + i * 100);
        }
        var last = builds;
        host.RenderFrame(new DisplayListBuilder(), 700);

        made.Should().Be(1);
        held.Should().Equal(last);
    }

    [Fact]
    public void APlaceWhoseFactoryChangesType_GetsANewRecognizer_AndTheOldOneHearsNothingMore()
    {
        var first = new ProbeA();
        var second = new ProbeB();
        var page = new Page(_ => Filled(200, 100), p => p.Second
            ? [new GestureRecognizerFactoryWithHandlers<ProbeB>(() => second, _ => { })]
            : [new GestureRecognizerFactoryWithHandlers<ProbeA>(() => first, _ => { })]);
        var host = Open(page);
        var box = Detector(host);

        host.PressDown(box.X + 20, box.Y + 20);
        host.PressUp(box.X + 20, box.Y + 20);
        page.UseTheSecond();
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.PressDown(box.X + 20, box.Y + 20);
        host.PressUp(box.X + 20, box.Y + 20);

        first.Heard.Should().Equal("down", "accept", "up");
        second.Heard.Should().Equal("down", "accept", "up");
    }

    [Fact]
    public void ASecondaryClick_IsOfferedToEveryRecognizer_AndOneThatWantsOnlyThePrimaryButtonNeverJoins()
    {
        var any = new Probe();
        var primary = new PrimaryOnly();
        var page = new Page(_ => Filled(200, 100), _ =>
        [
            new GestureRecognizerFactoryWithHandlers<Probe>(() => any, _ => { }),
            new GestureRecognizerFactoryWithHandlers<PrimaryOnly>(() => primary, _ => { }),
        ]);
        var host = Open(page);
        var box = Detector(host);

        host.ContextRequest(box.X + 20, box.Y + 10, timeMs: 500);

        any.Events.Select(e => (e.Phase, e.Position, e.Buttons)).Should().Equal(
            (PointerPhase.Down, new Point(20, 10), PointerButtons.Secondary),
            (PointerPhase.Up, new Point(20, 10), PointerButtons.None));
        any.Heard.Should().Equal("down", "accept", "up");
        primary.Heard.Should().BeEmpty();
    }

    [Fact]
    public void AnIdleShell_IsDueAFrame_WhenTheRecognizersDeadlinePasses()
    {
        var probe = new Probe { Deadline = 500 };
        var page = new Page(_ => Filled(200, 100), _ => [new GestureRecognizerFactoryWithHandlers<Probe>(() => probe, _ => { })]);
        var host = Open(page);
        var box = Detector(host);

        host.PressDown(box.X + 20, box.Y + 20, kind: PointerKind.Touch);
        host.NeedsRender.Should().BeTrue("the deadline counts from the first frame that sees the press");
        host.RenderFrame(new DisplayListBuilder(), 100);

        host.IsFrameDue(599).Should().BeFalse();
        host.IsFrameDue(600).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 600);
        probe.Heard.Should().Equal("down", "accept", "deadline");
    }

    [Fact]
    public void ADeadlineAskedForOnAMove_AsksAnIdleShellForTheFrameItCountsFrom_AndPassesOnTheFramesThatFollow()
    {
        var probe = new Probe { DeadlineOnMove = 300 };
        var page = new Page(_ => Filled(200, 100), _ => [new GestureRecognizerFactoryWithHandlers<Probe>(() => probe, _ => { })]);
        var host = Open(page);
        var box = Detector(host);

        host.PressDown(box.X + 20, box.Y + 20, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100);
        host.NeedsRender.Should().BeFalse("nothing is due once the press was drawn");

        host.PointerMove(box.X + 22, box.Y + 20, PointerKind.Touch);
        host.NeedsRender.Should().BeTrue("the deadline the move asked for counts from the first frame after it");
        host.RenderFrame(new DisplayListBuilder(), 200);

        host.IsFrameDue(499).Should().BeFalse();
        host.IsFrameDue(500).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 500);
        probe.Heard.Should().Equal("down", "accept", "move", "deadline");
        host.PressUp(box.X + 22, box.Y + 20, PointerKind.Touch);
    }

    [Fact]
    public void ADetectorThatLeavesTheTree_DropsItsRecognizer()
    {
        var probe = new Probe { Deadline = 500 };
        var page = new Page(_ => Filled(200, 100), _ => [new GestureRecognizerFactoryWithHandlers<Probe>(() => probe, _ => { })]);
        var gone = false;
        var root = new Switch(() => gone ? Filled(200, 100) : page);
        var host = Open(root);
        var box = Detector(host);

        host.PressDown(box.X + 20, box.Y + 20, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100);
        root.Flip(() => gone = true);
        host.RenderFrame(new DisplayListBuilder(), 200);
        host.RenderFrame(new DisplayListBuilder(), 700);

        probe.Heard.Should().Equal("down", "accept");
    }

    /// <summary>A root that shows what <paramref name="show"/> answers, built again when flipped.</summary>
    private sealed class Switch(Func<VisualNode> show) : StatefulComponent
    {
        public void Flip(Action change) => SetState(change);

        public override VisualNode Build(ComponentContext context) => show();
    }
}
