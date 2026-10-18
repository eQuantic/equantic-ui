using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What a node keeps between frames belongs to its position (#975): a scroll view's offset, a drag,
/// what an <see cref="InView"/> last reported, a transition's tracks and a <see cref="Presence"/>'s
/// entrance live on the node's element and leave the tree with it, so a node drawn at the place of
/// another starts with its own. They were kept by layout path, which a page that replaces another, or
/// a key that changes on a single-child slot, spells the same way; each test here but the one that
/// keeps its position fails on a tree that keeps them so, and they read only the drawn frame, so they
/// run on both.
/// </summary>
public class NodeStateTests
{
    private const float Width = 300;
    private const float Height = 200;

    // ---- the trees -------------------------------------------------------------------------------

    /// <summary>A root that shows one page and then the other, as an app that navigates does: the
    /// two pages are components of two types, so each builds what it shows at the same place.</summary>
    private sealed class Swap : StatefulComponent
    {
        private readonly UiComponent _first;
        private readonly UiComponent _second;
        private bool _showsSecond;

        public Swap(UiComponent first, UiComponent second)
        {
            _first = first;
            _second = second;
        }

        public void Next() => SetState(() => _showsSecond = true);

        public override VisualNode Build(ComponentContext context) => _showsSecond ? _second : _first;
    }

    private sealed class FirstPage(VisualNode body) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => body;
    }

    private sealed class SecondPage(VisualNode body) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => body;
    }

    /// <summary>A root that builds its tree with a key it can change, on a slot that holds one child:
    /// the key decides whether the node there is the one before.</summary>
    private sealed class Keyed(Func<string, VisualNode> build) : StatefulComponent
    {
        private string _key = "a";

        public void Rekey(string key) => SetState(() => _key = key);

        public override VisualNode Build(ComponentContext context) => build(_key);
    }

    /// <summary>Forty rows in a scroll view that fills the window: 800 of content in 200.</summary>
    private static ScrollView List(string? key = null)
    {
        var rows = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < 40; i++) rows.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }));
        return new ScrollView(rows) { Height = SizeValue.Fill, Key = key };
    }

    private static PhotonHost Open(UiComponent root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, Width, Height) { SmoothScroll = false };
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    /// <summary>The first laid-out node, depth first, whose source is a <typeparamref name="T"/>.</summary>
    private static LayoutNode? TryFind<T>(LayoutNode node) where T : VisualNode
    {
        if (node.Source is T) return node;
        foreach (var child in node)
            if (TryFind<T>(child) is { } found) return found;
        return null;
    }

    private static LayoutNode Find<T>(RealizeResult frame) where T : VisualNode =>
        TryFind<T>(frame.Root) ?? throw new InvalidOperationException($"The frame draws no {typeof(T).Name}.");

    /// <summary>How far the frame's scroll view is scrolled, read from where it drew its content.</summary>
    private static float ScrolledBy(RealizeResult frame)
    {
        var view = Find<ScrollView>(frame);
        return view.Bounds.Y - view.Children[0].Bounds.Y;
    }

    // ---- a scroll view's offset ------------------------------------------------------------------

    [Fact]
    public void APageThatReplacesAnother_StartsItsScrollViewAtItsOwnOffset()
    {
        var swap = new Swap(new FirstPage(List()), new SecondPage(List()));
        var host = Open(swap);
        host.ScrollBy(Width / 2, Height / 2, 120).Should().BeTrue();
        ScrolledBy(host.RenderFrame(new DisplayListBuilder())).Should().Be(120, "the wheel scrolled the first page");

        swap.Next();

        ScrolledBy(host.RenderFrame(new DisplayListBuilder()))
            .Should().Be(0, "the second page's list is a node of its own, at its own offset");
    }

    [Fact]
    public void AKeyThatChanges_GivesAScrollViewItsOwnOffset()
    {
        var keyed = new Keyed(key => new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }, List(key)));
        var host = Open(keyed);
        host.ScrollBy(Width / 2, Height / 2, 120).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());

        keyed.Rekey("b");

        ScrolledBy(host.RenderFrame(new DisplayListBuilder()))
            .Should().Be(0, "a key that changes makes a new node, as it does in Flutter");
    }

    [Fact]
    public void ANodeThatKeepsItsPosition_KeepsItsOffset()
    {
        var keyed = new Keyed(key => new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }, List(key)));
        var host = Open(keyed);
        host.ScrollBy(Width / 2, Height / 2, 120).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());

        keyed.Rekey("a");

        ScrolledBy(host.RenderFrame(new DisplayListBuilder()))
            .Should().Be(120, "a rebuild that hands the position the same type and key keeps its state");
    }

    /// <summary>
    /// A view that leaves while it glides stops there: its state is released with its position and the
    /// clock drops it, so the host stops asking for frames. And a gesture still held on it, a fling on
    /// release, finds no state to move and makes none.
    /// </summary>
    [Fact]
    public void AViewThatLeavesWhileItGlides_StopsAskingForFrames()
    {
        var owner = BuildOwner.Structural();
        var context = new ComponentContext(PhotonTheme.Instance);
        var view = owner.Build(new Box(new BoxStyle(), List()), context, WindowSizeClass.Compact, rebuildAll: true)
            .ChildAt(0)!;
        var clock = new ScrollClock();
        clock.ScrollBy(view, 300, 600).Should().BeTrue();
        clock.Advance(0);
        clock.Advance(16).Should().BeTrue("the wheel's target is still ahead");

        owner.Build(new Text("gone", TypeRole.BodyM), context, WindowSizeClass.Compact, rebuildAll: true);

        clock.Advance(32).Should().BeFalse("the view left the tree, and its glide with it");
        clock.Fling(view, 2, 600).Should().BeFalse("a position that left keeps nothing to move");
        clock.Advance(48).Should().BeFalse();
    }

    // ---- a drag ----------------------------------------------------------------------------------

    [Fact]
    public void ADraggableReplacedWhileItGlidesBack_IsDrawnAtItsRest()
    {
        var keyed = new Keyed(key => new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill },
            new Draggable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 60 }))
            {
                Axis = DragAxis.Horizontal,
                Min = -96,
                Max = 96,
                Key = key,
            }));
        var host = Open(keyed);
        host.PressDown(Width / 2, 30);
        host.PointerMove(Width / 2 + 60, 30);
        Find<Draggable>(host.RenderFrame(new DisplayListBuilder(), 10)).DragOffset
            .Should().Be(60, "the finger holds the surface 60 along");
        host.PressUp(Width / 2 + 60, 30);

        // Released at 10, it glides back to its rest over Motion.Base: halfway there at 110.
        keyed.Rekey("b");

        Find<Draggable>(host.RenderFrame(new DisplayListBuilder(), 60)).DragOffset
            .Should().Be(0, "the new surface was never dragged, so nothing glides it");
    }

    // ---- an InView's last report -----------------------------------------------------------------

    [Fact]
    public void AnInViewAtAnothersPlace_ReportsItsFirstSight()
    {
        var first = new List<bool>();
        var second = new List<bool>();
        var swap = new Swap(
            new FirstPage(new InView(new Box(new BoxStyle { Width = 100, Height = 50 }), first.Add)),
            new SecondPage(new InView(new Box(new BoxStyle { Width = 100, Height = 50 }), second.Add)));
        var host = Open(swap);
        first.Should().Equal(true);

        swap.Next();
        host.RenderFrame(new DisplayListBuilder());

        second.Should().Equal(new[] { true }, "the second page's InView never reported, so its first sight is news");
    }

    // ---- a transition's tracks -------------------------------------------------------------------

    private static Box Sized(float width) => new(new BoxStyle
    {
        Width = width,
        Height = 40,
        Transition = TransitionSpec.Of(StyleChannels.Size, Motion.State),
    });

    [Fact]
    public void ABoxAtAnothersPlace_MountsAtItsOwnWidth()
    {
        var swap = new Swap(new FirstPage(Sized(100)), new SecondPage(Sized(300)));
        var host = Open(swap);
        Find<Box>(host.RenderFrame(new DisplayListBuilder(), 1000)).Bounds.Width.Should().Be(100);

        swap.Next();

        Find<Box>(host.RenderFrame(new DisplayListBuilder(), 1050)).Bounds.Width
            .Should().Be(300, "a transition animates a change, and a new box has none: it mounts at its value");
    }

    // ---- a presence ------------------------------------------------------------------------------

    private static readonly Color FirstColor = Color.FromRgb(0xC0, 0x20, 0x20);
    private static readonly Color SecondColor = Color.FromRgb(0x20, 0x20, 0xC0);

    private static Presence Shown(Color color) =>
        new(new Box(new BoxStyle { Width = 100, Height = 50, Background = new ColorToken(color) }));

    [Fact]
    public void APresenceReplacedAtItsPlace_LeavesWhileTheNewOneEnters()
    {
        var swap = new Swap(new FirstPage(Shown(FirstColor)), new SecondPage(Shown(SecondColor)));
        var host = Open(swap);
        host.RenderFrame(new DisplayListBuilder(), 1000);

        swap.Next();
        var builder = new DisplayListBuilder();
        var frame = host.RenderFrame(builder, 1000);

        var commands = builder.Build().Commands.ToArray();
        commands.Should().Contain(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == FirstColor,
            "the first presence left, and its last paint is replayed as its exit");
        Find<Presence>(frame).Presence.Should().Be(0, "the second presence enters from the start");
    }

    /// <summary>
    /// A layer that closes shows its presence's exit in the frame it closed in. Its slot leaves the
    /// tree when that frame ends, so the frame has to end before the exits are drawn, or the exit
    /// would wait a frame and the layer would blink out first. The store noticed the departure at the
    /// same point, so this one passes on a tree that keeps state by path too: it guards the order.
    /// </summary>
    [Fact]
    public void AnOverlayThatCloses_ShowsItsPresencesExitInTheFrameItClosed()
    {
        var open = new Overlay(Shown(FirstColor)) { Motion = TransitionSpec.Of(StyleChannels.Opacity, Motion.State) };
        var closed = new Overlay(Shown(FirstColor))
        {
            Motion = TransitionSpec.Of(StyleChannels.Opacity, Motion.State),
            Open = false,
        };
        var keyed = new Keyed(key => key == "a" ? open : closed);
        var host = Open(keyed);
        host.RenderFrame(new DisplayListBuilder(), 1000);

        keyed.Rekey("closed");
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 1000);

        builder.Build().Commands.ToArray()
            .Should().Contain(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == FirstColor,
                "the frame that closes the layer starts the exit, at full opacity");
    }

    /// <summary>Whether the first box drawn in <paramref name="under"/>'s colour is drawn before the
    /// first in <paramref name="over"/>'s: under it, where the two overlap.</summary>
    private static bool DrawnUnder(DisplayListBuilder builder, Color under, Color over)
    {
        var commands = builder.Build().Commands.ToArray();
        var first = Array.FindIndex(commands, c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == under);
        var second = Array.FindIndex(commands, c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == over);
        first.Should().BeGreaterThanOrEqualTo(0, "the frame draws the first presence's exit");
        second.Should().BeGreaterThanOrEqualTo(0, "the frame draws the second presence's exit");
        return first < second;
    }

    /// <summary>
    /// Presences that leave in one frame fade in the order they were painted, so the one that was on
    /// top fades on top. A list's surplus members leave last first, so the order their positions leave
    /// in drew the top one underneath.
    /// </summary>
    [Fact]
    public void PresencesThatLeaveTogether_FadeInTheOrderTheyWerePainted()
    {
        var keyed = new Keyed(key =>
        {
            var stack = new Stack();
            if (key != "a") return stack;
            stack.Add(Shown(FirstColor));
            stack.Add(Shown(SecondColor));
            return stack;
        });
        var host = Open(keyed);
        host.RenderFrame(new DisplayListBuilder(), 1000);

        keyed.Rekey("gone");
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 1000);

        DrawnUnder(builder, FirstColor, SecondColor)
            .Should().BeTrue("the second presence was painted over the first, and fades over it");
    }

    /// <summary>
    /// The same for layers that close in one frame: the owner sweeps the layers it did not show last
    /// first, and the one opened later stays on top while both fade.
    /// </summary>
    [Fact]
    public void LayersThatCloseTogether_FadeInTheOrderTheyWerePainted()
    {
        var motion = TransitionSpec.Of(StyleChannels.Opacity, Motion.State);
        var keyed = new Keyed(key =>
        {
            var page = new Stack();
            page.Add(new Overlay(Shown(FirstColor)) { Motion = motion, Open = key == "a" });
            page.Add(new Overlay(Shown(SecondColor)) { Motion = motion, Open = key == "a" });
            return page;
        });
        var host = Open(keyed);
        host.RenderFrame(new DisplayListBuilder(), 1000);

        keyed.Rekey("closed");
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 1000);

        DrawnUnder(builder, FirstColor, SecondColor)
            .Should().BeTrue("the second layer was painted over the first, and its presence fades over the first's");
    }
}
