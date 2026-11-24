using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Any subtree hears the raw pointer on Photon (#1562), driven through the host as a shell drives it: presses,
/// moves, releases, cancels, scrolls and a secondary click, over a laid-out tree. Each case is one
/// <c>dom/pointer-listener.spec.ts</c> proves for the web, and the hit rule itself runs the shared fixture
/// (<see cref="ListenerHitsFixtureTests"/>).
/// </summary>
public class ListenerHostTests
{
    private sealed class Page(Func<Page, VisualNode> build) : StatefulComponent
    {
        public List<(string Who, PointerEvent Event)> Heard { get; } = [];
        public List<string> Pressed { get; } = [];

        public override VisualNode Build(ComponentContext context) => build(this);

        public Listener Hears(string who, VisualNode child, HitTestBehavior behavior = HitTestBehavior.DeferToChild) =>
            new(child)
            {
                OnPointerDown = e => Heard.Add((who, e)),
                OnPointerMove = e => Heard.Add((who, e)),
                OnPointerUp = e => Heard.Add((who, e)),
                OnPointerCancel = e => Heard.Add((who, e)),
                OnPointerHover = e => Heard.Add((who, e)),
                OnPointerScroll = e => Heard.Add((who, e)),
                Behavior = behavior,
            };
    }

    private static readonly ColorToken Fill = new(Color.FromRgb(0x10, 0x20, 0x30));

    /// <summary>A box that paints: a solid.</summary>
    private static Box Filled(float width, float height) =>
        new(new BoxStyle { Width = width, Height = height, Background = Fill });

    /// <summary>A box that paints nothing: a layout's empty space.</summary>
    private static Box Empty(float width, float height) => new(new BoxStyle { Width = width, Height = height });

    private static (PhotonHost Host, Page Page) Open(Func<Page, VisualNode> build)
    {
        var page = new Page(build);
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 600, 600);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, page);
    }

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var below in Walk(child))
                yield return below;
    }

    private static Rect Listener(PhotonHost host, int index = 0) =>
        Walk(host.LastFrame!.Root).Where(n => n.Source is Listener).ElementAt(index).Bounds;

    [Fact]
    public void APressDraggedOutsideAndReleased_IsFollowedInTheListenersOwnBox()
    {
        var (host, page) = Open(p => p.Hears("L", Filled(200, 100)));
        var box = Listener(host);

        host.PressDown(box.X + 10, box.Y + 6);
        host.PointerMove(box.X + 210, box.Y + 6);
        host.PressUp(box.X + 210, box.Y + 6);

        page.Heard.Select(h => (h.Event.Phase, h.Event.Position, h.Event.Buttons)).Should().Equal(
            (PointerPhase.Down, new Point(10, 6), PointerButtons.Primary),
            (PointerPhase.Move, new Point(210, 6), PointerButtons.Primary),
            (PointerPhase.Up, new Point(210, 6), PointerButtons.None));
        page.Heard.Should().OnlyContain(h => h.Event.Kind == PointerKind.Mouse && h.Event.Pointer == 1);
    }

    [Fact]
    public void AButtonInsideAListener_StillPresses_AndTheListenerHearsIt()
    {
        var (host, page) = Open(p => p.Hears("L", new Pressable(Filled(200, 100), () => p.Pressed.Add("button"))));
        var box = Listener(host);

        host.PressDown(box.X + 20, box.Y + 20);
        host.PressUp(box.X + 20, box.Y + 20);

        page.Pressed.Should().Equal("button");
        page.Heard.Select(h => h.Event.Phase).Should().Equal(PointerPhase.Down, PointerPhase.Up);
    }

    [Theory]
    [InlineData(HitTestBehavior.DeferToChild, false)]
    [InlineData(HitTestBehavior.Opaque, true)]
    [InlineData(HitTestBehavior.Translucent, true)]
    public void TheGapOfAColumn_IsHeardOnlyWhenTheListenerIsNotDeferring(HitTestBehavior behavior, bool heard)
    {
        var (host, page) = Open(p =>
        {
            var column = new Column(gap: 40);
            column.Add(Filled(200, 30));
            column.Add(Filled(200, 30));
            return p.Hears("L", column, behavior);
        });
        var box = Listener(host);

        // In the gap: below the first box (30 tall) and above the second.
        host.PressDown(box.X + 50, box.Y + 50);
        host.PressUp(box.X + 50, box.Y + 50);

        page.Heard.Select(h => h.Event.Phase).Should().Equal(
            heard ? [PointerPhase.Down, PointerPhase.Up] : Array.Empty<PointerPhase>());
    }

    [Fact]
    public void OnTheColumnsText_ADeferringListenerHears()
    {
        var (host, page) = Open(p =>
        {
            var column = new Column(gap: 40);
            column.Add(new Text("first"));
            column.Add(Filled(200, 30));
            return p.Hears("L", column);
        });
        var box = Listener(host);

        host.PressDown(box.X + 2, box.Y + 4);

        page.Heard.Should().ContainSingle().Which.Event.Phase.Should().Be(PointerPhase.Down);
    }

    [Theory]
    [InlineData(HitTestBehavior.Translucent, new[] { "upper", "lower" })]
    [InlineData(HitTestBehavior.Opaque, new[] { "upper" })]
    [InlineData(HitTestBehavior.DeferToChild, new[] { "lower" })]
    public void TwoListenersInAStack_HearAsTheUpperOnesBehaviourSays(HitTestBehavior upper, string[] heard)
    {
        var (host, page) = Open(p =>
        {
            var stack = new Stack();
            stack.Add(p.Hears("lower", Filled(300, 100)));
            stack.Add(p.Hears("upper", Empty(300, 100), upper));
            return stack;
        });
        var box = Listener(host);

        host.PressDown(box.X + 30, box.Y + 30);

        page.Heard.Select(h => h.Who).Should().Equal(heard);
    }

    [Fact]
    public void AMouseHovers_AndAFingerNever()
    {
        var (host, page) = Open(p => p.Hears("L", Filled(200, 100)));
        var box = Listener(host);

        host.PointerMove(box.X + 5, box.Y + 5, PointerKind.Touch, timeMs: 30);
        host.PointerMove(box.X + 7, box.Y + 8, timeMs: 40);

        page.Heard.Should().ContainSingle().Which.Event.Should().Be(new PointerEvent(PointerPhase.Hover,
            new Point(7, 8), PointerKind.Mouse, PointerButtons.None, KeyModifiers.None, 1, Point.Zero, 40));
    }

    [Fact]
    public void APressThatWentDownOutside_HoversNothingItIsDraggedOver()
    {
        var (host, page) = Open(p =>
        {
            var row = new Row(gap: 0);
            row.Add(Filled(100, 100));
            row.Add(p.Hears("L", Filled(200, 100)));
            return row;
        });
        var box = Listener(host);

        host.PressDown(box.X - 50, box.Y + 5);
        host.PointerMove(box.X + 10, box.Y + 5);
        host.PressUp(box.X + 10, box.Y + 5);

        page.Heard.Should().BeEmpty();
    }

    [Fact]
    public void AWheelOverTheListener_IsHeardWithItsDelta()
    {
        var (host, page) = Open(p => p.Hears("L", Filled(200, 100)));
        var box = Listener(host);

        host.ScrollBy(box.X + 5, box.Y + 5, across: 0, down: 48, timeMs: 1234.5);

        page.Heard.Should().ContainSingle().Which.Event.Should().Be(new PointerEvent(PointerPhase.Scroll,
            new Point(5, 5), PointerKind.Mouse, PointerButtons.None, KeyModifiers.None, 1, new Point(0, 48), 1234.5));
    }

    /// <summary>A pointer event carries the time the shell saw it (#1563): two moves 16 ms apart, as the macOS shell
    /// hands them from <c>NSEvent.timestamp</c>, read 16 apart, a press's whole life included.</summary>
    [Fact]
    public void TheShellsTime_ReachesTheListener_SoTwoMoves16MsApartRead16Apart()
    {
        var (host, page) = Open(p => p.Hears("L", Filled(200, 100)));
        var box = Listener(host);
        // A Mac's uptime in milliseconds: past what a float holds to the millisecond.
        const double boot = 864_000_123.25;

        host.PressDown(box.X + 10, box.Y + 6, timeMs: boot);
        host.PointerMove(box.X + 20, box.Y + 6, timeMs: boot + 16);
        host.PointerMove(box.X + 30, box.Y + 6, timeMs: boot + 32);
        host.PressUp(box.X + 30, box.Y + 6, timeMs: boot + 40);
        host.ContextRequest(box.X + 10, box.Y + 6, timeMs: boot + 500);

        page.Heard.Select(h => (h.Event.Phase, h.Event.TimeMs)).Should().Equal(
            (PointerPhase.Down, boot),
            (PointerPhase.Move, boot + 16),
            (PointerPhase.Move, boot + 32),
            (PointerPhase.Up, boot + 40),
            (PointerPhase.Down, boot + 500),
            (PointerPhase.Up, boot + 500));
        (page.Heard[2].Event.TimeMs - page.Heard[1].Event.TimeMs).Should().Be(16);
    }

    /// <summary>A shell that hands no time is timed by the host's own monotonic clock (#1563): it never runs
    /// backwards, and it counts milliseconds.</summary>
    [Fact]
    public void WithNoTimeFromTheShell_TheHostsOwnClockTimesTheEvent()
    {
        var (host, page) = Open(p => p.Hears("L", Filled(200, 100)));
        var box = Listener(host);

        host.PressDown(box.X + 10, box.Y + 6);
        host.PointerMove(box.X + 20, box.Y + 6);
        Thread.Sleep(40);
        host.PointerMove(box.X + 30, box.Y + 6);
        host.PointerCancel();

        var times = page.Heard.Select(h => h.Event.TimeMs).ToList();
        times.Should().HaveCount(4).And.BeInAscendingOrder().And.OnlyContain(time => time >= 0);
        // 40 slept, and at least 15 counted: a timer may wake a tick early on Windows, whose tick is 15.6 ms.
        (times[2] - times[1]).Should().BeGreaterThanOrEqualTo(15, "the clock counts milliseconds, and about 40 of them passed");
    }

    [Fact]
    public void ASecondaryClick_IsHeardAsItsDownAndItsUp()
    {
        var (host, page) = Open(p => p.Hears("L", Filled(200, 100)));
        var box = Listener(host);

        host.ContextRequest(box.X + 10, box.Y + 6);

        page.Heard.Select(h => (h.Event.Phase, h.Event.Position, h.Event.Buttons)).Should().Equal(
            (PointerPhase.Down, new Point(10, 6), PointerButtons.Secondary),
            (PointerPhase.Up, new Point(10, 6), PointerButtons.None));
    }

    [Fact]
    public void AControlClick_HoldsControlOnItsDownAndItsUp()
    {
        var (host, page) = Open(p => new ContextTarget(p.Hears("L", Filled(200, 100)), _ => { }));
        var box = Listener(host);

        host.ContextRequest(box.X + 10, box.Y + 6, buttons: PointerButtons.Primary, modifiers: KeyModifiers.Control);

        page.Heard.Select(h => (h.Event.Phase, h.Event.Buttons, h.Event.Modifiers)).Should().Equal(
            (PointerPhase.Down, PointerButtons.Primary, KeyModifiers.Control),
            (PointerPhase.Up, PointerButtons.None, KeyModifiers.Control));
    }

    [Fact]
    public void AnAnsweredControlClick_HoversNothingUntilItsReleaseIsCancelled()
    {
        var (host, page) = Open(p => new ContextTarget(p.Hears("L", Filled(200, 100)), _ => { }));
        var box = Listener(host);

        host.ContextRequest(box.X + 10, box.Y + 6, buttons: PointerButtons.Primary, modifiers: KeyModifiers.Control);
        host.PointerMove(box.X + 20, box.Y + 6);
        host.PointerCancel();
        host.PointerMove(box.X + 30, box.Y + 6);

        page.Heard.Select(h => (h.Event.Phase, h.Event.Position.X)).Should().Equal(
            (PointerPhase.Down, 10f), (PointerPhase.Up, 10f), (PointerPhase.Hover, 30f));
    }

    [Fact]
    public void ASecondaryClickWhileAPressIsFollowed_KeepsThePressFollowed()
    {
        var (host, page) = Open(p => p.Hears("L", Filled(200, 100)));
        var box = Listener(host);

        host.PressDown(box.X + 10, box.Y + 6);
        host.ContextRequest(box.X + 20, box.Y + 6);
        host.PointerMove(box.X + 30, box.Y + 6);
        host.PressUp(box.X + 30, box.Y + 6);

        page.Heard.Select(h => (h.Event.Phase, h.Event.Buttons)).Should().Equal(
            (PointerPhase.Down, PointerButtons.Primary),
            (PointerPhase.Move, PointerButtons.Primary),
            (PointerPhase.Up, PointerButtons.None));
    }

    [Fact]
    public void ACancelledPress_IsHeardAsACancelWhereItWasLast()
    {
        var (host, page) = Open(p => p.Hears("L", Filled(200, 100)));
        var box = Listener(host);

        host.PressDown(box.X + 10, box.Y + 6, kind: PointerKind.Touch);
        host.PointerMove(box.X + 15, box.Y + 9, PointerKind.Touch);
        host.PointerCancel(PointerKind.Touch);
        host.PointerMove(box.X + 20, box.Y + 9, PointerKind.Touch);

        page.Heard.Select(h => (h.Event.Phase, h.Event.Position, h.Event.Kind)).Should().Equal(
            (PointerPhase.Down, new Point(10, 6), PointerKind.Touch),
            (PointerPhase.Move, new Point(15, 9), PointerKind.Touch),
            (PointerPhase.Cancel, new Point(15, 9), PointerKind.Touch));
    }

    [Fact]
    public void AFrameWithNoListener_RecordsNothingForThem()
    {
        var (host, _) = Open(_ => Filled(200, 100));

        host.LastFrame!.PointerTargets.Should().BeEmpty();
    }
}
