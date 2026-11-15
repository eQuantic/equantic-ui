using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A request for a menu on Photon (#1531), driven through the host as a shell drives it: a secondary click the
/// shell hands over, a finger's hold the host counts, and the menu key or Shift+F10 with the focus inside. Each
/// case is one `dom/context-request.spec.ts` proves for the web.
/// </summary>
public class ContextTargetHostTests
{
    private sealed class Rows : StatefulComponent
    {
        public List<(string Row, ContextRequest Request)> Heard { get; } = [];
        public List<string> Pressed { get; } = [];
        public bool SecondEnabled { get; set; } = true;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 8);
            column.Add(Row(context, "src", enabled: true));
            column.Add(Row(context, "docs", SecondEnabled));
            // A target inside a target: the inner one answers alone.
            column.Add(new ContextTarget(
                new Box(new BoxStyle { Width = 300, Height = 80 },
                    new ContextTarget(new Box(new BoxStyle { Width = 100, Height = 30 }), r => Heard.Add(("inner", r)))),
                r => Heard.Add(("outer", r))));
            return column;
        }

        private VisualNode Row(ComponentContext context, string name, bool enabled) =>
            new ContextTarget(
                new Pressable(new Box(new BoxStyle { Width = 300, Height = 40, Background = context.Theme.Surface }),
                    () => Pressed.Add(name)),
                r => Heard.Add((name, r)))
            { Enabled = enabled };
    }

    private static (PhotonHost Host, Rows Page) Open(Rows? page = null)
    {
        page ??= new Rows();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
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

    private static Rect Targets(PhotonHost host, int index) =>
        Walk(host.LastFrame!.Root).Where(n => n.Source is ContextTarget).ElementAt(index).Bounds;

    [Fact]
    public void ASecondaryClick_IsHeardAtItsPointInTheTarget()
    {
        var (host, page) = Open();
        var src = Targets(host, 0);

        host.ContextRequest(src.X + 10, src.Y + 6).Should().BeTrue();

        page.Heard.Should().ContainSingle().Which.Should().Be(("src", new ContextRequest(new Point(10, 6))));
        page.Pressed.Should().BeEmpty();
    }

    [Fact]
    public void WhereNoTargetIs_TheRequestIsNobodys()
    {
        var (host, page) = Open();

        host.ContextRequest(390, 390).Should().BeFalse("the shell lets the platform's own menu open there");
        page.Heard.Should().BeEmpty();
    }

    [Fact]
    public void ADisabledTarget_AnswersNothing()
    {
        var (host, page) = Open(new Rows { SecondEnabled = false });
        var docs = Targets(host, 1);

        host.ContextRequest(docs.X + 5, docs.Y + 5).Should().BeFalse();
        page.Heard.Should().BeEmpty();
    }

    [Fact]
    public void TheInnermostTarget_AnswersAlone()
    {
        var (host, page) = Open();
        var inner = Targets(host, 3);

        host.ContextRequest(inner.X + 4, inner.Y + 4);

        page.Heard.Select(h => h.Row).Should().Equal("inner");
    }

    [Fact]
    public void TheMenuKeyAndShiftF10_AskTheTargetAroundTheFocus_WithNoPoint()
    {
        var (host, page) = Open();
        host.KeyDown("Tab").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 32);

        host.KeyDown("ContextMenu").Should().BeTrue();
        host.KeyDown("F10", KeyModifiers.Shift).Should().BeTrue();
        host.KeyDown("F10").Should().BeFalse("F10 alone is not a request");

        page.Heard.Should().Equal(("src", new ContextRequest(null)), ("src", new ContextRequest(null)));
    }

    [Fact]
    public void AFingerHeldStill_AsksAtItsPoint_AndThePressItBeganPressesNothing()
    {
        var (host, page) = Open();
        var src = Targets(host, 0);

        host.PressDown(src.X + 20, src.Y + 10, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100);
        host.IsFrameDue(100 + ContextTarget.HoldMs).Should().BeTrue("the hold is due a frame of its own");
        page.Heard.Should().BeEmpty();
        host.RenderFrame(new DisplayListBuilder(), 100 + ContextTarget.HoldMs);
        host.PressUp(src.X + 20, src.Y + 10, PointerKind.Touch);

        page.Heard.Should().ContainSingle().Which.Should().Be(("src", new ContextRequest(new Point(20, 10))));
        page.Pressed.Should().BeEmpty("the menu was the answer to the hold");
    }

    [Fact]
    public void AFingerThatTravels_IsScrolling_AndAsksNothing()
    {
        var (host, page) = Open();
        var src = Targets(host, 0);

        host.PressDown(src.X + 20, src.Y + 10, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100);
        host.PointerMove(src.X + 20, src.Y + 40, PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100 + ContextTarget.HoldMs + 20);

        page.Heard.Should().BeEmpty();
    }

    private sealed class FieldInATarget : StatefulComponent
    {
        public List<ContextRequest> Heard { get; } = [];

        public override VisualNode Build(ComponentContext context) =>
            new ContextTarget(
                new Box(new BoxStyle { Width = 300, Height = 80 },
                    new Box(new BoxStyle { Width = 200, Height = 40 }, new TextEntry("note", _ => { }) { Label = "Note" })),
                r => Heard.Add(r));
    }

    [Fact]
    public void AFieldInsideATarget_KeepsItsOwnMenu()
    {
        var page = new FieldInATarget();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        var field = host.LastFrame!.TextRegions.Single().Bounds;
        var target = Targets(host, 0);

        host.ContextRequest(field.X + 4, field.Y + 4).Should().BeFalse("the field's copy and paste are the field's");
        host.ContextRequest(target.X + 280, target.Y + 70).Should().BeTrue("beside the field the target answers");
        page.Heard.Should().ContainSingle();
    }

    private sealed class CardInAMenu : StatefulComponent
    {
        public List<ContextRequest> Heard { get; } = [];
        public List<SortDrop?> Hovered { get; } = [];

        public override VisualNode Build(ComponentContext context) =>
            new ContextTarget(
                new Sortable(
                    new Column(gap: 8)
                    {
                        new SortableItem(new Box(new BoxStyle { Width = 300, Height = 40, Background = context.Theme.Surface }), "a"),
                        new SortableItem(new Box(new BoxStyle { Width = 300, Height = 40, Background = context.Theme.Surface }), "b"),
                    },
                    "list", _ => { })
                { OnHovered = drop => Hovered.Add(drop) },
                r => Heard.Add(r));
    }

    [Fact]
    public void AHoldThatLiftsASortableItem_IsTheLifts_AndAsksForNoMenu()
    {
        var page = new CardInAMenu();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        var card = Walk(host.LastFrame!.Root).First(n => n.Source is SortableItem).Bounds;

        host.PressDown(card.X + 20, card.Y + 10, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100);
        host.RenderFrame(new DisplayListBuilder(), 100 + SortPlacement.HoldMs);
        host.RenderFrame(new DisplayListBuilder(), 100 + ContextTarget.HoldMs + 50);

        page.Hovered.Should().NotBeEmpty("the item was lifted");
        page.Heard.Should().BeEmpty("the hold was the lift's");
    }

    [Fact]
    public void AMouseHeldStill_IsAPress_NotARequest()
    {
        var (host, page) = Open();
        var src = Targets(host, 0);

        host.PressDown(src.X + 20, src.Y + 10);
        host.RenderFrame(new DisplayListBuilder(), 100);
        host.RenderFrame(new DisplayListBuilder(), 100 + ContextTarget.HoldMs + 20);
        host.PressUp(src.X + 20, src.Y + 10);

        page.Heard.Should().BeEmpty();
        page.Pressed.Should().Equal("src");
    }
}
