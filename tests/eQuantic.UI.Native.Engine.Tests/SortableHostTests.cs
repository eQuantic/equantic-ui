using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Sortable lists on Photon (#1537), driven through the host the way a shell drives it: a press, moves, frames
/// and a release, or keys. Each case is one the browser's <c>dom/sortable.spec.ts</c> proves for the web, so a
/// board written once moves its cards the same way on both targets.
/// </summary>
public class SortableHostTests
{
    private const float CardHeight = 40;
    private const float Gap = 8;
    private const float ColumnWidth = 150;

    /// <summary>A board of two columns of cards, which applies a drop as an app does.</summary>
    private sealed class Board : StatefulComponent
    {
        public List<string>[] Columns { get; } = [["a1", "a2", "a3"], ["b1", "b2"]];
        public string[] Ids { get; } = ["todo", "doing"];
        public string Group { get; set; } = "board";
        public List<(string List, SortDrop? Drop)> Hovered { get; } = [];
        public List<SortDrop> Dropped { get; } = [];
        public List<string> Pressed { get; } = [];
        public Func<SortDrop, bool>? Guard { get; set; }
        public int Asked { get; set; }
        public float? ColumnHeight { get; set; }
        public bool Nests { get; set; }

        public override VisualNode Build(ComponentContext context)
        {
            var row = new Row(gap: 20);
            for (var c = 0; c < Columns.Length; c++)
            {
                var id = Ids[c];
                var column = new Column(gap: Gap);
                foreach (var card in Columns[c])
                {
                    var name = card;
                    column.Add(new SortableItem(
                        new Pressable(new Box(new BoxStyle
                        {
                            Width = SizeValue.Fill,
                            Height = CardHeight,
                            Background = context.Theme.Surface,
                        }), () => Pressed.Add(name)),
                        card));
                }
                VisualNode body = new Box(new BoxStyle { Width = ColumnWidth, MinHeight = 200 }, column);
                // A column that scrolls is a list whose child is the scroll view, the element the web marks.
                if (ColumnHeight is not null) body = new ScrollView(body);
                var guard = Guard;
                VisualNode list = new Sortable(body, id, Drop)
                {
                    Group = Group,
                    Nests = Nests,
                    OnHovered = drop => Hovered.Add((id, drop)),
                    Accepts = guard is null ? null : drop =>
                    {
                        Asked++;
                        return guard(drop);
                    },
                };
                if (ColumnHeight is { } height)
                    list = new Box(new BoxStyle { Width = ColumnWidth, Height = height }, list);
                row.Add(list);
            }
            return row;
        }

        private void Drop(SortDrop drop)
        {
            Dropped.Add(drop);
            SetState(() =>
            {
                var from = Columns[Array.IndexOf(Ids, drop.From)];
                var to = Columns[Array.IndexOf(Ids, drop.To)];
                from.Remove(drop.Item);
                to.Insert(Math.Min(drop.Index, to.Count), drop.Item);
            });
        }
    }

    private static (PhotonHost Host, Board Board) Open(Board? board = null)
    {
        board ??= new Board();
        var host = new PhotonHost(board, PhotonTheme.Instance, ThemeMode.Light, 400, 600);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, board);
    }

    /// <summary>The centre of the card with <paramref name="id"/>, where the last frame drew it.</summary>
    private static Point CardAt(PhotonHost host, string id)
    {
        var item = Walk(host.LastFrame!.Root).First(n => n.Source is SortableItem { } i && i.Id == id);
        return item.Bounds.Center;
    }

    /// <summary>The box the column with <paramref name="id"/> was drawn in.</summary>
    private static Rect ColumnAt(PhotonHost host, string id) =>
        Walk(host.LastFrame!.Root).First(n => n.Source is Sortable { } s && s.Id == id).Bounds;

    private static IEnumerable<LayoutNode> Walk(LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var below in Walk(child))
                yield return below;
    }

    [Fact]
    public void AMouse_CarriesACardIntoTheOtherColumn_WhichReportsTheDrop()
    {
        var (host, board) = Open();
        var a1 = CardAt(host, "a1");
        var b2 = CardAt(host, "b2");

        host.PressDown(a1.X, a1.Y);
        host.PointerMove(a1.X + 10, a1.Y);
        host.RenderFrame(new DisplayListBuilder(), 32);
        // Over the second column, past the middle of b1 and short of b2's: between them.
        host.PointerMove(b2.X, b2.Y - CardHeight / 2 - 2);
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.PressUp(b2.X, b2.Y - CardHeight / 2 - 2);

        board.Dropped.Should().ContainSingle()
            .Which.Should().Be(new SortDrop("a1", "todo", "doing", 1));
        board.Hovered.Should().Contain(("doing", new SortDrop("a1", "todo", "doing", 1)));
        board.Hovered[^1].Should().Be(("doing", (SortDrop?)null), "the list it lands in hears it left as it drops");
        board.Pressed.Should().BeEmpty("a press that carried a card does not press it");
        host.RenderFrame(new DisplayListBuilder(), 64);
        board.Columns[1].Should().Equal("b1", "a1", "b2");
    }

    [Fact]
    public void AnInListMove_CountsTheListWithoutTheItem()
    {
        var (host, board) = Open();
        var a1 = CardAt(host, "a1");
        var a3 = CardAt(host, "a3");

        host.PressDown(a1.X, a1.Y);
        host.PointerMove(a1.X, a1.Y + 10);
        host.PointerMove(a3.X, a3.Y + CardHeight / 2 + 4);
        host.PressUp(a3.X, a3.Y + CardHeight / 2 + 4);

        board.Dropped.Should().ContainSingle().Which.Should().Be(new SortDrop("a1", "todo", "todo", 2),
            "below the last card of three, the list without the carried one has two");
    }

    [Fact]
    public void APressThatDoesNotTravel_StillPressesTheCard()
    {
        var (host, board) = Open();
        var a2 = CardAt(host, "a2");

        host.PressDown(a2.X, a2.Y);
        host.PointerMove(a2.X + SortPlacement.PickUpTravel - 1, a2.Y);
        host.PressUp(a2.X + SortPlacement.PickUpTravel - 1, a2.Y);

        board.Pressed.Should().Equal("a2");
        board.Dropped.Should().BeEmpty();
        board.Hovered.Should().BeEmpty();
    }

    [Fact]
    public void AFinger_PicksTheCardUpOnlyOnceItHeldStill()
    {
        var (host, board) = Open();
        var a1 = CardAt(host, "a1");

        host.PressDown(a1.X, a1.Y, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100);
        host.IsFrameDue(100 + SortPlacement.HoldMs).Should().BeTrue("the hold is due a frame of its own");
        board.Hovered.Should().BeEmpty("the hold is not long enough yet");
        host.RenderFrame(new DisplayListBuilder(), 100 + SortPlacement.HoldMs);
        board.Hovered.Should().ContainSingle().Which.Should().Be(("todo", new SortDrop("a1", "todo", "todo", 0)));

        var b1 = CardAt(host, "b1");
        // Above the middle of the first card of the other column.
        host.PointerMove(b1.X, b1.Y - CardHeight / 4, PointerKind.Touch);
        host.PressUp(b1.X, b1.Y - CardHeight / 4, PointerKind.Touch);
        board.Dropped.Should().ContainSingle().Which.Should().Be(new SortDrop("a1", "todo", "doing", 0));
    }

    [Fact]
    public void AFingerThatTravelsBeforeItsHold_IsAScroll_AndCarriesNothing()
    {
        var (host, board) = Open();
        var a1 = CardAt(host, "a1");

        host.PressDown(a1.X, a1.Y, kind: PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100);
        host.PointerMove(a1.X, a1.Y + 30, PointerKind.Touch);
        host.RenderFrame(new DisplayListBuilder(), 100 + SortPlacement.HoldMs + 50);
        host.PressUp(a1.X, a1.Y + 30, PointerKind.Touch);

        board.Hovered.Should().BeEmpty();
        board.Dropped.Should().BeEmpty();
    }

    [Fact]
    public void ListsWithoutAGroup_KeepTheirOwnCards()
    {
        var (host, board) = Open(new Board { Group = "" });
        var a1 = CardAt(host, "a1");
        var b1 = CardAt(host, "b1");

        host.PressDown(a1.X, a1.Y);
        host.PointerMove(a1.X + 10, a1.Y);
        host.PointerMove(b1.X, b1.Y);
        host.PressUp(b1.X, b1.Y);

        board.Dropped.Should().BeEmpty("a list with no group sorts only its own");
        board.Hovered.Should().NotContain(h => h.List == "doing");
    }

    [Fact]
    public void TheGuard_IsAskedOncePerPlace_AndARefusedPlaceIsNeitherReportedNorDropped()
    {
        var (host, board) = Open(new Board { Guard = drop => drop.To != "doing" });
        var a1 = CardAt(host, "a1");
        var b1 = CardAt(host, "b1");

        host.PressDown(a1.X, a1.Y);
        host.PointerMove(a1.X + 10, a1.Y);
        var asked = board.Asked;
        host.PointerMove(b1.X, b1.Y + 2);
        host.PointerMove(b1.X + 1, b1.Y + 3);
        host.PointerMove(b1.X + 2, b1.Y + 4);
        host.PressUp(b1.X + 2, b1.Y + 4);

        (board.Asked - asked).Should().Be(1, "three moves over one place ask once");
        board.Hovered.Should().NotContain(h => h.List == "doing");
        board.Dropped.Should().BeEmpty();
    }

    [Fact]
    public void Escape_PutsTheCardBack_AndIsTheCarrysAlone()
    {
        var (host, board) = Open();
        var a1 = CardAt(host, "a1");
        var b1 = CardAt(host, "b1");

        host.PressDown(a1.X, a1.Y);
        host.PointerMove(a1.X + 10, a1.Y);
        host.PointerMove(b1.X, b1.Y);
        host.KeyDown("Escape").Should().BeTrue();
        host.PressUp(b1.X, b1.Y);

        board.Dropped.Should().BeEmpty();
        board.Hovered[^1].Drop.Should().BeNull();
        board.Pressed.Should().BeEmpty();
    }

    [Fact]
    public void TheCarriedCard_IsDimmedInItsPlace_UnderACopyThatFollowsThePointerAndLeans()
    {
        var (host, _) = Open();
        var a1 = CardAt(host, "a1");

        host.PressDown(a1.X, a1.Y);
        host.PointerMove(a1.X + 10, a1.Y);
        host.RenderFrame(new DisplayListBuilder(), 32);
        // Fast to the right: 60 points in one frame's 16ms leans the copy against the travel, all the way.
        host.PointerMove(a1.X + 70, a1.Y);
        host.RenderFrame(new DisplayListBuilder(), 48);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 64);
        var commands = builder.Build().Commands.ToArray();

        commands.Should().Contain(c => c.Kind == DrawCommandKind.BeginLayer
            && Math.Abs(c.StrokeWidth - (float)SortPlacement.LiftedOpacity) < 0.001f, "the card is dimmed in its place");
        var copy = Array.FindLastIndex(commands, c => c.Kind == DrawCommandKind.BeginLayer
            && Math.Abs(c.StrokeWidth - (float)SortPlacement.CopyOpacity) < 0.001f);
        copy.Should().BeGreaterThan(0, "the copy is drawn over the frame");
        var drawn = commands[(copy + 1)..].Where(c => c.Kind == DrawCommandKind.FillRRect).ToArray();
        drawn.Should().NotBeEmpty();
        drawn[0].Transform.M12.Should().BeLessThan(0, "moving right leans the copy back, counter-clockwise");
        drawn[0].Transform.M31.Should().BeGreaterThan(0);
        drawn.Should().Contain(c => c.Shape.Rect.Width > 0);
    }

    [Fact]
    public void TheCopy_StandsUpright_WhenThePersonAskedForLessMotion()
    {
        var (host, _) = Open();
        host.ReducedMotion = true;
        var a1 = CardAt(host, "a1");

        host.PressDown(a1.X, a1.Y);
        host.PointerMove(a1.X + 10, a1.Y);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.PointerMove(a1.X + 70, a1.Y);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 48);
        var commands = builder.Build().Commands.ToArray();

        var copy = Array.FindLastIndex(commands, c => c.Kind == DrawCommandKind.BeginLayer
            && Math.Abs(c.StrokeWidth - (float)SortPlacement.CopyOpacity) < 0.001f);
        commands[(copy + 1)..].Where(c => c.Kind == DrawCommandKind.FillRRect)
            .Should().OnlyContain(c => c.Transform.M12 == 0, "nothing leans");
    }

    [Fact]
    public void TheScrollViewUnderTheCard_ScrollsNearItsEdge_AndThePlaceCountsTheCardsItHides()
    {
        var board = new Board { ColumnHeight = 120 };
        board.Columns[1] = ["b1", "b2", "b3", "b4", "b5", "b6", "b7", "b8"];
        var (host, _) = Open(board);
        var a1 = CardAt(host, "a1");
        var doing = ColumnAt(host, "doing");
        var viewport = Walk(host.LastFrame!.Root).Where(n => n.Source is ScrollView).ElementAt(1).Bounds;

        host.PressDown(a1.X, a1.Y);
        host.PointerMove(a1.X + 10, a1.Y);
        // Near the bottom edge of the second column's scroll view.
        host.PointerMove(doing.Center.X, viewport.Bottom - 4);
        for (var frame = 0; frame < 30; frame++)
            host.RenderFrame(new DisplayListBuilder(), 32 + frame * 16);
        host.PressUp(doing.Center.X, viewport.Bottom - 4);

        board.Dropped.Should().ContainSingle();
        board.Dropped[0].To.Should().Be("doing");
        board.Dropped[0].Index.Should().BeGreaterThanOrEqualTo(6,
            "the column scrolled under the card, and the place counts the cards scrolled out of sight above");
    }

    [Fact]
    public void TheKeyboard_CarriesTheFocusedCard_ThroughItsListAndAcross_AndTheFocusFollowsIt()
    {
        var (host, board) = Open();
        host.KeyDown("Tab").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 32);

        host.KeyDown(" ").Should().BeTrue("Space picks the focused card up");
        board.Hovered[^1].Should().Be(("todo", new SortDrop("a1", "todo", "todo", 0)));
        host.KeyDown("ArrowDown").Should().BeTrue();
        board.Hovered[^1].Should().Be(("todo", new SortDrop("a1", "todo", "todo", 1)));
        host.KeyDown("ArrowRight").Should().BeTrue();
        board.Hovered[^1].Should().Be(("doing", new SortDrop("a1", "todo", "doing", 1)));
        host.KeyDown("Enter").Should().BeTrue();

        board.Dropped.Should().ContainSingle().Which.Should().Be(new SortDrop("a1", "todo", "doing", 1));
        board.Pressed.Should().BeEmpty("the keys that carried the card pressed nothing");
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.RenderFrame(new DisplayListBuilder(), 64);
        board.Columns[1].Should().Equal("b1", "a1", "b2");
        host.KeyDown(" ").Should().BeTrue("the focus followed the card, so Space picks it up again");
        board.Hovered[^1].Should().Be(("doing", new SortDrop("a1", "doing", "doing", 1)));
        host.KeyDown("Escape").Should().BeTrue();
        board.Hovered[^1].Drop.Should().BeNull();
    }

    [Fact]
    public void AGuardThatRefusesAKeysPlace_LeavesTheCardWhereItIs()
    {
        var (host, board) = Open(new Board { Guard = drop => drop.To != "doing" });
        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder(), 32);

        host.KeyDown(" ");
        host.KeyDown("ArrowRight").Should().BeTrue();
        board.Hovered[^1].Should().Be(("todo", new SortDrop("a1", "todo", "todo", 0)));
        host.KeyDown("Tab").Should().BeTrue("Tab puts the card back and moves the focus on");
        board.Hovered[^1].Drop.Should().BeNull();
        board.Dropped.Should().BeEmpty();
    }

    [Fact]
    public void TheKeyboard_StepsThroughThePlacesInsideCards_InAListThatNests()
    {
        var (host, board) = Open(new Board { Nests = true });
        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder(), 32);

        host.KeyDown(" ").Should().BeTrue();
        // Below a1's own place comes the place inside a2, then the one after it (#1533).
        host.KeyDown("ArrowDown").Should().BeTrue();
        board.Hovered[^1].Should().Be(("todo", new SortDrop("a1", "todo", "todo", 0) { Into = "a2" }));
        host.KeyDown("ArrowDown").Should().BeTrue();
        board.Hovered[^1].Should().Be(("todo", new SortDrop("a1", "todo", "todo", 1)));
        host.KeyDown("ArrowUp").Should().BeTrue();
        host.KeyDown("Enter").Should().BeTrue();

        board.Dropped.Should().ContainSingle().Which.Should().Be(new SortDrop("a1", "todo", "todo", 0) { Into = "a2" });
    }

    [Fact]
    public void TheKeyboard_PassesOverAPlaceTheListRefuses_AsAPointerDoes()
    {
        var (host, board) = Open(new Board { Guard = drop => drop.Index != 1 });
        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder(), 32);

        host.KeyDown(" ");
        host.KeyDown("ArrowDown").Should().BeTrue();
        board.Hovered[^1].Should().Be(("todo", new SortDrop("a1", "todo", "todo", 2)),
            "the refused position 2 is passed over (#1533)");
        // At the end, the card keeps the place it holds.
        host.KeyDown("ArrowDown").Should().BeTrue();
        board.Hovered[^1].Should().Be(("todo", new SortDrop("a1", "todo", "todo", 2)));
    }
}
