using System.Reflection;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A Photon host keeps its tree in a <see cref="BuildOwner"/> across frames (docs/ELEMENT-TREE-PLAN.md,
/// E1): what a position holds survives the frames it is drawn in, a layer builds when it is shown, and
/// the layout measures what was built, with each stateful child's own state.
/// </summary>
public class ElementHostTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly TransitionSpec Fade = new(StyleChannels.Opacity, 200);

    private static PhotonHost Open(VisualNode root, float width = 400, float height = 300)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, width, height);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    private sealed class Leaf(string name, List<string> log) : StatefulComponent
    {
        protected override void OnMount() => log.Add($"{name} mounted");
        protected override void OnUnmount() => log.Add($"{name} unmounted");
        public override VisualNode Build(ComponentContext context) => new Text(name, TypeRole.Caption);
    }

    private sealed class Root(List<string> log) : StatefulComponent
    {
        private int _count;

        public void Bump() => SetState(() => _count++);

        protected override void OnMount() => log.Add("root mounted");

        public override VisualNode Build(ComponentContext context) => new Text($"count {_count}", TypeRole.BodyM);
    }

    /// <summary>
    /// #935, measured the way it was found: the listeners on what a stateful root's SetState reaches.
    /// The instance store reported the root, the same instance every frame, as ENTERING retention every
    /// frame, and the host subscribed again each time: 101 handlers after 100 frames. The host now
    /// listens to its owner once, and an instance has nothing anyone subscribes to.
    /// </summary>
    [Fact]
    public void AStatefulRoot_IsListenedToOnce_HoweverManyFramesItDraws()
    {
        var log = new List<string>();
        var root = new Root(log);
        var host = Open(root);
        for (var frame = 1; frame <= 100; frame++)
            host.RenderFrame(new DisplayListBuilder(), frame * 16f);

        log.Should().Equal("root mounted");
        Listeners(host).Should().Be(1);
        host.NeedsRender.Should().BeFalse("nothing moves and nothing changed");

        root.Bump();

        host.NeedsRender.Should().BeTrue("the root's SetState asked for the frame that shows it");
    }

    private static int Listeners(PhotonHost host)
    {
        const BindingFlags Field = BindingFlags.Instance | BindingFlags.NonPublic;
        var owner = (BuildOwner)typeof(PhotonHost).GetField("_owner", Field)!.GetValue(host)!;
        var requested = (Delegate?)typeof(BuildOwner).GetField(nameof(BuildOwner.BuildRequested), Field)!
            .GetValue(owner);
        return requested?.GetInvocationList().Length ?? 0;
    }

    private sealed class Menu(List<string> log) : StatefulComponent
    {
        private bool _open;

        public void Toggle() => SetState(() => _open = !_open);

        public override VisualNode Build(ComponentContext context) =>
            new Anchored(new Text("anchor", TypeRole.BodyM), new Leaf("panel", log)) { Open = _open };
    }

    /// <summary>A panel's component lives while the panel is shown: once mounted however many frames
    /// it stays open, and gone when it closes, as it was while the panel was built in measure.</summary>
    [Fact]
    public void AnAnchoredPanel_MountsWhenItOpens_AndUnmountsWhenItCloses()
    {
        var log = new List<string>();
        var menu = new Menu(log);
        var host = Open(menu);
        log.Should().BeEmpty("a closed panel is built nowhere");

        menu.Toggle();
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.RenderFrame(new DisplayListBuilder(), 32);
        log.Should().Equal("panel mounted");

        menu.Toggle();
        host.RenderFrame(new DisplayListBuilder(), 48);
        log.Should().Equal("panel mounted", "panel unmounted");
    }

    private sealed class Dialog(List<string> log) : StatefulComponent
    {
        private bool _open = true;

        public void Close() => SetState(() => _open = false);

        public override VisualNode Build(ComponentContext context) =>
            new Overlay(new Leaf("dialog", log)) { Open = _open, Motion = Fade, Label = "Dialog" };
    }

    /// <summary>A layer closed with motion is realized nowhere (#252), and what it held leaves the
    /// tree with it.</summary>
    [Fact]
    public void AnOverlayClosedWithMotion_UnmountsWhatItHeld()
    {
        var log = new List<string>();
        var dialog = new Dialog(log);
        var host = Open(dialog);
        log.Should().Equal("dialog mounted");

        dialog.Close();
        host.RenderFrame(new DisplayListBuilder(), 16);

        log.Should().Equal("dialog mounted", "dialog unmounted");
    }

    private sealed class Grower : StatefulComponent
    {
        private int _letters = 1;

        public void Grow(int by) => SetState(() => _letters += by);

        public override VisualNode Build(ComponentContext context) =>
            new Text(new string('W', _letters), TypeRole.BodyM, maxLines: 1);
    }

    /// <summary>The same two growers every build, so the test can grow the one a position holds.</summary>
    private sealed class Sheet(Grower narrow, Grower wide) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var grid = new Grid([GridTrack.Auto]);
            grid.Add(narrow);
            grid.Add(wide);
            return grid;
        }
    }

    /// <summary>
    /// A column that hugs its content is as wide as its widest cell DRAWS. The probe that sized it
    /// used to build every candidate at one shared path, so two stateful cells of one type were
    /// measured with a single instance's state, and the grown one was squeezed into the width of the
    /// other.
    /// </summary>
    [Fact]
    public void AGridColumnThatHugsItsContent_IsAsWideAsItsWidestCellDraws()
    {
        var (narrow, wide) = (new Grower(), new Grower());
        var host = Open(new Sheet(narrow, wide), width: 1200);

        wide.Grow(9);
        host.RenderFrame(new DisplayListBuilder(), 16);

        var drawn = Find(host.LastFrame!.Root, node => node.Source is Text { Content: "WWWWWWWWWW" });
        var alone = LayoutEngine.Layout(new Text("WWWWWWWWWW", TypeRole.BodyM, maxLines: 1), 1200, 300,
            new LayoutContext(Theme, ApproximateTextMeasurer.Instance));
        drawn.Bounds.Width.Should().BeApproximately(alone.Bounds.Width, 0.5f);
    }

    private sealed class Counted : StatefulComponent
    {
        public int Builds { get; private set; }

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            return new Text("x", TypeRole.BodyM);
        }
    }

    /// <summary>
    /// A box whose minimum width bites measures its child twice (the reflow); the child built twice
    /// with it while Build ran inside measure. It builds once now, before the layout, and the layout
    /// measures that one subtree twice.
    /// </summary>
    [Fact]
    public void ABoxThatMeasuresItsChildTwice_BuildsItOnce()
    {
        var counted = new Counted();

        Open(new Primitives.Box(new BoxStyle { MinWidth = 300 }, counted));

        counted.Builds.Should().Be(1);
    }

    private sealed class Tick : StatefulComponent
    {
        public int Builds { get; private set; }

        public void Touch() => SetState(() => { });

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            return new Text("tick", TypeRole.BodyM);
        }
    }

    /// <summary>A screen with a loop running and two components that build nothing of it.</summary>
    private static Column Moving(Tick first, Tick second)
    {
        var page = new Column();
        page.Add(new LoopMotion(new Primitives.Box(new BoxStyle
        {
            Width = SizeValue.Fixed(120),
            Height = SizeValue.Fixed(8),
        }), LoopEffect.SlideX, -0.35f, 1.05f, 1200));
        page.Add(first);
        page.Add(second);
        return page;
    }

    /// <summary>
    /// The frames a loop asks for move a strip and change nothing a build reads, so they build
    /// nothing: every one of them rebuilt every component of the screen while Build ran in measure.
    /// </summary>
    [Fact]
    public void FramesDrawnOnlyForMotion_BuildNothing()
    {
        var (first, second) = (new Tick(), new Tick());
        var host = Open(Moving(first, second));

        for (var frame = 1; frame <= 30; frame++)
        {
            host.NeedsRender.Should().BeTrue("the loop keeps frames coming");
            host.RenderFrame(new DisplayListBuilder(), frame * 16f);
        }

        first.Builds.Should().Be(1);
        second.Builds.Should().Be(1);
    }

    [Fact]
    public void ASetState_BuildsItsComponentAlone()
    {
        var (first, second) = (new Tick(), new Tick());
        var host = Open(Moving(first, second));

        second.Touch();
        host.RenderFrame(new DisplayListBuilder(), 16);

        first.Builds.Should().Be(1);
        second.Builds.Should().Be(2);
    }

    /// <summary>What the host is told from outside the tree rebuilds all of it: the models input
    /// reaches are read by builds that no SetState marked.</summary>
    [Fact]
    public void AFrameAfterAnInvalidation_RebuildsEveryComponent()
    {
        var (first, second) = (new Tick(), new Tick());
        var host = Open(Moving(first, second));

        host.Invalidate();
        host.RenderFrame(new DisplayListBuilder(), 16);

        first.Builds.Should().Be(2);
        second.Builds.Should().Be(2);
    }

    private sealed class Windowed : StatefulComponent
    {
        private float _viewport;

        public int Builds { get; private set; }

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            return new ScrollView(new Text($"viewport {_viewport}", TypeRole.BodyM))
            {
                Height = SizeValue.Fixed(120),
                OnViewportChanged = viewport =>
                {
                    if (MathF.Abs(viewport - _viewport) < 0.5f) return;
                    SetState(() => _viewport = viewport);
                },
            };
        }
    }

    /// <summary>
    /// A scroller reports its viewport from the frame that drew it, and a list that windows its rows
    /// answers with a SetState. That request arrives after the build, and the frame's end used to
    /// write it over with "nothing moves", so the first window waited for the next input.
    /// </summary>
    [Fact]
    public void ASetStateFromAFramesOwnCallback_GetsTheNextFrame()
    {
        var windowed = new Windowed();
        var host = Open(windowed);

        host.NeedsRender.Should().BeTrue("the viewport arrived after the build that needs it");
        host.RenderFrame(new DisplayListBuilder(), 16);

        windowed.Builds.Should().Be(2);
        host.NeedsRender.Should().BeFalse("the viewport it built with is the one reported");
    }

    private static LayoutNode Find(LayoutNode node, Func<LayoutNode, bool> match) =>
        TryFind(node, match) ?? throw new InvalidOperationException("no laid-out node matched");

    private static LayoutNode? TryFind(LayoutNode node, Func<LayoutNode, bool> match)
    {
        if (match(node)) return node;
        foreach (var child in node)
        {
            if (TryFind(child, match) is { } found) return found;
        }

        return null;
    }
}
