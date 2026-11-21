using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A node that carries a <see cref="GlobalKey"/> keeps its position wherever the tree places it (#797):
/// moved under another parent in one build, the position goes with it, and the stateful components below it
/// keep their instances. A drawer template's counter read "Pressed 0 times" after a phone turned to
/// landscape and back, because each arm of its adaptive shell built the screen anew.
/// <para>
/// Driven through the owner alone, as Photon drives it (one arm held, <c>holdsEveryArm: false</c>), and as
/// the web serves a page (every arm held), where each arm's position is its own.
/// </para>
/// </summary>
public class GlobalKeyTests
{
    private static readonly ComponentContext Context = new(PhotonTheme.Instance);

    private const float Phone = 400f;
    private const float Landscape = 914f;

    private static BuildOwner Owner(bool holdsEveryArm = false) =>
        new((component, context, _) => component.Build(context), holdsEveryArm);

    private static Element Frame(BuildOwner owner, VisualNode root, float width = Phone, bool rebuildAll = true)
    {
        var element = owner.Build(root, Context, width, rebuildAll);
        owner.EndFrame();
        return element;
    }

    /// <summary>A screen that counts, and logs when it mounts and unmounts.</summary>
    private sealed class Screen(List<string> log) : StatefulComponent
    {
        public int Count { get; private set; }

        public void Press() => SetState(() => Count++);

        protected override void OnMount() => log.Add("mounted");

        protected override void OnUnmount() => log.Add("unmounted");

        public override VisualNode Build(ComponentContext context) => new Text($"Pressed {Count} times", TypeRole.BodyM);
    }

    /// <summary>The template's shell: a compact arm that is the screen in a column, and a docked arm that is
    /// a rail beside the screen in a row, the screen built by each arm as the template builds it.</summary>
    private sealed class Shell(List<string> log, GlobalKey? key) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var compact = new Column();
            compact.Add(new Screen(log) { GlobalKey = key });
            var docked = new Row();
            docked.Add(new Text("rail", TypeRole.Label));
            docked.Add(new Screen(log) { GlobalKey = key });
            return new AdaptiveNode(compact, expanded: docked);
        }
    }

    private static Screen ScreenIn(Element root)
    {
        Screen? found = null;
        void Visit(Element element)
        {
            if (element is ComponentElement { Component: Screen screen }) found ??= screen;
            element.VisitChildren(Visit);
        }

        Visit(root);
        return found ?? throw new InvalidOperationException("no screen in the tree");
    }

    private static ComponentElement ScreenPosition(Element root)
    {
        ComponentElement? found = null;
        void Visit(Element element)
        {
            if (element is ComponentElement { Component: Screen } position) found ??= position;
            element.VisitChildren(Visit);
        }

        Visit(root);
        return found ?? throw new InvalidOperationException("no screen in the tree");
    }

    [Fact]
    public void AKeyedScreen_KeepsItsCount_WhenThePhoneTurnsAndTurnsBack()
    {
        var log = new List<string>();
        var key = new GlobalKey("screen");
        var shell = new Shell(log, key);
        var owner = Owner();

        var root = Frame(owner, shell);
        var screen = ScreenIn(root);
        screen.Press();
        screen.Press();
        Frame(owner, shell, rebuildAll: false);

        root = Frame(owner, shell, Landscape);
        ScreenIn(root).Should().BeSameAs(screen, "the screen moved from the column into the row, with its position");
        ScreenIn(root).Count.Should().Be(2);

        // Back to the earlier arm: the column is built before the row leaves, so the screen is taken from
        // the row while it still stands there.
        root = Frame(owner, shell, Phone);
        ScreenIn(root).Should().BeSameAs(screen);
        ScreenIn(root).Count.Should().Be(2);
        log.Should().Equal(["mounted"], "a moved position neither unmounts nor mounts again");
    }

    [Fact]
    public void AScreenWithoutAKey_StartsAgainOnEachArm()
    {
        var log = new List<string>();
        var shell = new Shell(log, key: null);
        var owner = Owner();

        var root = Frame(owner, shell);
        var screen = ScreenIn(root);
        screen.Press();
        Frame(owner, shell, rebuildAll: false);

        root = Frame(owner, shell, Landscape);
        ScreenIn(root).Should().NotBeSameAs(screen, "each arm builds its own screen, which is what a key changes");
        ScreenIn(root).Count.Should().Be(0);
        log.Should().Equal("mounted", "unmounted", "mounted");
    }

    [Fact]
    public void AMovedPosition_StandsWhereItWasTaken()
    {
        var log = new List<string>();
        var shell = new Shell(log, new GlobalKey());
        var owner = Owner();

        var root = Frame(owner, shell);
        var id = ScreenPosition(root).Id;
        root = Frame(owner, shell, Landscape);

        var moved = ScreenPosition(root);
        moved.Id.Should().Be(id, "it is the same position");
        moved.Slot.Should().Be(1, "the screen is the row's second child");
        moved.Parent!.Configuration.Should().BeOfType<Row>();
        moved.Depth.Should().Be(moved.Parent.Depth + 1);
        moved.SlotsBelow(0).Should().Be("0.2.1", "the shell, its expanded arm, the row's second child");
    }

    [Fact]
    public void AKeyedPositionNoNodeTakes_LeavesAtTheFramesEnd()
    {
        var log = new List<string>();
        var key = new GlobalKey();
        var owner = Owner();
        var column = new Column();
        column.Add(new Screen(log) { GlobalKey = key });
        Frame(owner, column);

        Frame(owner, new Column());

        log.Should().Equal("mounted", "unmounted");
    }

    /// <summary>A key moves between two branches of a tree, to a branch the build reaches first: the
    /// position is taken from the branch it leaves before that branch builds.</summary>
    [Fact]
    public void AKeyMovingToAnEarlierBranch_TakesItsPositionAcross()
    {
        var log = new List<string>();
        var key = new GlobalKey();
        var owner = Owner();

        VisualNode Tree(bool first)
        {
            var left = new Column();
            var right = new Column();
            (first ? left : right).Add(new Screen(log) { GlobalKey = key });
            var row = new Row();
            row.Add(left);
            row.Add(right);
            return row;
        }

        var root = Frame(owner, Tree(first: false));
        var screen = ScreenIn(root);
        screen.Press();
        Frame(owner, Tree(first: false), rebuildAll: false);

        root = Frame(owner, Tree(first: true));

        ScreenIn(root).Should().BeSameAs(screen);
        ScreenIn(root).Count.Should().Be(1);
        root.ChildAt(1)!.ChildAt(0).Should().BeNull("the right branch let go of it");
        log.Should().Equal("mounted");
    }

    /// <summary>Where one arm is held, it stands at its own slot, as the web's arms do, and an arm of the
    /// same type as the one shown takes its position across a threshold without a key.</summary>
    [Fact]
    public void WhereOneArmIsHeld_ItStandsAtItsSlot_AndAnArmOfTheSameTypeKeepsThePosition()
    {
        var log = new List<string>();
        var adaptive = new AdaptiveNode(new Screen(log), expanded: new Screen(log));
        var owner = Owner();

        var root = Frame(owner, adaptive);
        var screen = ((ComponentElement)root.ChildAt(0)!).Component;
        ((Screen)screen).Press();
        Frame(owner, adaptive, rebuildAll: false);

        root = Frame(owner, adaptive, Landscape);

        root.ChildAt(0).Should().BeNull("the compact arm is not shown");
        root.ChildAt(2).Should().BeOfType<ComponentElement>().Which.Component.Should().BeSameAs(screen);
        ((Screen)screen).Count.Should().Be(1);
        log.Should().Equal("mounted");
    }

    [Fact]
    public void AKeyPlacedTwiceInOneFrame_StartsOneAgain_AndSaysSo()
    {
        var log = new List<string>();
        var key = new GlobalKey("placed-twice");
        var owner = Owner();
        var heard = new List<string>();
        DevelopmentNotices.Sink = heard.Add;
        try
        {
            var row = new Row();
            row.Add(new Screen(log) { GlobalKey = key });
            row.Add(new Screen(log) { GlobalKey = key });

            var root = Frame(owner, row);

            root.ChildAt(0).Should().NotBeNull("every slot the node places is filled");
            root.ChildAt(1).Should().NotBeNull();
            ((ComponentElement)root.ChildAt(0)!).Component.Should().NotBeSameAs(((ComponentElement)root.ChildAt(1)!).Component);
            heard.Where(notice => notice.Contains("placed-twice", StringComparison.Ordinal)).Should().ContainSingle()
                .Which.Should().Contain("two positions in one frame");
        }
        finally
        {
            DevelopmentNotices.Sink = null;
        }
    }

    /// <summary>Where every arm is held, as a page is served, each arm's position is its own: one key in all
    /// of them is the design, and nothing moves between them.</summary>
    [Fact]
    public void WhereEveryArmIsHeld_EachArmKeepsItsOwnPosition()
    {
        var log = new List<string>();
        var shell = new Shell(log, new GlobalKey());
        var owner = Owner(holdsEveryArm: true);

        var root = Frame(owner, shell);
        var adaptive = root.ChildAt(0)!;
        var compact = adaptive.ChildAt(0)!.ChildAt(0)!;
        var docked = adaptive.ChildAt(2)!.ChildAt(1)!;
        root = Frame(owner, shell, Landscape);

        adaptive.ChildAt(0)!.ChildAt(0).Should().BeSameAs(compact);
        adaptive.ChildAt(2)!.ChildAt(1).Should().BeSameAs(docked);
        log.Should().Equal("mounted", "mounted");
    }

    /// <summary>The value a moved component reads is the one above where it stands now, and a stateful one
    /// hears that what it depends on changed, as Flutter's <c>activate</c> tells it.</summary>
    [Fact]
    public void AMovedComponent_ReadsTheValueAboveItsNewPlace()
    {
        var log = new List<string>();
        var key = new GlobalKey();
        var owner = Owner();

        VisualNode Tree(bool first)
        {
            var reader = new Reader(log) { GlobalKey = key };
            var row = new Row();
            row.Add(new Caption("left", first ? reader : new Text("empty", TypeRole.BodyM)));
            row.Add(new Caption("right", first ? new Text("empty", TypeRole.BodyM) : reader));
            return row;
        }

        var root = Frame(owner, Tree(first: true));
        var reader = ((ComponentElement)root.ChildAt(0)!.ChildAt(0)!).Component;
        root = Frame(owner, Tree(first: false));

        ((ComponentElement)root.ChildAt(1)!.ChildAt(0)!).Component.Should().BeSameAs(reader);
        log.Should().Equal("read left", "changed", "read right");
    }

    /// <summary>A component in a layer under the moved position, whose node the parent keeps (so nothing
    /// rebuilds it by configuration), still hears the value above its new place change, once its layer is
    /// shown: the move marks it, and the layer builds it.</summary>
    [Fact]
    public void AMovedComponentInALayer_KeepsHearingTheValueAboveIt()
    {
        var log = new List<string>();
        var mover = new Mover(log, new GlobalKey());
        var owner = Owner();

        void Show(Element root)
        {
            void Visit(Element element)
            {
                if (element is NodeElement { Configuration: Overlay overlay } holder) holder.ShowDeferred(overlay.Child);
                else element.VisitChildren(Visit);
            }

            Visit(root);
            owner.EndFrame();
        }

        Show(owner.Build(mover, Context, Phone, rebuildAll: true));
        mover.Move();
        Show(owner.Build(mover, Context, Phone, rebuildAll: false));
        mover.Bump();
        Show(owner.Build(mover, Context, Phone, rebuildAll: false));

        // Moved, it hears that what it read changed and reads the value above its new place; then the
        // value changes there, and it hears that too.
        log.Should().Equal("read left0", "changed", "read right0", "changed", "read right1");
    }

    private sealed class Mover(List<string> log, GlobalKey key) : StatefulComponent
    {
        private readonly VisualNode _layer = new Reader(log);
        private bool _right;
        private int _version;

        public void Move() => SetState(() => _right = true);

        public void Bump() => SetState(() => _version++);

        public override VisualNode Build(ComponentContext context)
        {
            var holder = new Holder(_layer) { GlobalKey = key };
            var row = new Row();
            row.Add(new Caption($"left{_version}", _right ? new Text("empty", TypeRole.BodyM) : holder));
            row.Add(new Caption($"right{_version}", _right ? holder : new Text("empty", TypeRole.BodyM)));
            return row;
        }
    }

    private sealed class Holder(VisualNode layer) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Overlay(layer);
    }

    private sealed class Caption(string text, VisualNode child) : InheritedComponent(child)
    {
        public string Text { get; } = text;

        protected override bool UpdateShouldNotify(InheritedComponent old) => ((Caption)old).Text != Text;
    }

    private sealed class Reader(List<string> log) : StatefulComponent
    {
        protected override void OnDependenciesChanged() => log.Add("changed");

        public override VisualNode Build(ComponentContext context)
        {
            log.Add($"read {context.DependOn<Caption>()?.Text}");
            return new Text("reader", TypeRole.BodyM);
        }
    }
}
