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
        public SortAnnouncements? Words { get; set; }
        public SortAnnouncements? DoingWords { get; set; }
        public string[] Labels { get; set; } = ["", ""];
        public SortableTheme? Style { get; set; }

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
                        }), () => Pressed.Add(name)) { Label = name },
                        card));
                }
                VisualNode body = new Box(new BoxStyle { Width = ColumnWidth, MinHeight = 200 }, column);
                // A column that scrolls is a list whose child is the scroll view, the element the web marks.
                if (ColumnHeight is not null) body = new ScrollView(body);
                var guard = Guard;
                VisualNode list = new Sortable(body, id, Drop, c == 1 && DoingWords is not null ? DoingWords : Words,
                    c == 0 ? Style : null)
                {
                    Group = Group,
                    Nests = Nests,
                    Label = Labels[c],
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

    private static (PhotonHost Host, Board Board) Open(Board? board = null, IAppTheme? theme = null)
    {
        board ??= new Board();
        var host = new PhotonHost(board, theme ?? PhotonTheme.Instance, ThemeMode.Light, 400, 600);
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

    /// <summary>Carries a1 fast to the right and draws the next frame: what it draws, from the dimmed card to the
    /// copy's commands.</summary>
    private static DrawCommand[] CarryFast(PhotonHost host)
    {
        var a1 = CardAt(host, "a1");
        host.PressDown(a1.X, a1.Y);
        host.PointerMove(a1.X + 10, a1.Y);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.PointerMove(a1.X + 70, a1.Y);
        host.RenderFrame(new DisplayListBuilder(), 48);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 64);
        return builder.Build().Commands.ToArray();
    }

    /// <summary>The layer a carry opened at <paramref name="opacity"/>, last first: the copy's is drawn over the frame.</summary>
    private static int LayerAt(DrawCommand[] commands, float opacity) =>
        Array.FindLastIndex(commands, c => c.Kind == DrawCommandKind.BeginLayer && Math.Abs(c.StrokeWidth - opacity) < 0.001f);

    [Fact]
    public void TheLookOfACarriedCard_IsTheThemes_WithTheListsOwnLaidOverIt()
    {
        var theme = PhotonTheme.Instance.With(sortable: new SortableTheme
        {
            LiftedOpacity = 0.25f,
            CopyOpacity = 0.7f,
            CopyElevation = 5,
            MaxLean = 6,
        });
        var (host, _) = Open(new Board { Style = new SortableTheme { CopyOpacity = 0.5f } }, theme);

        var commands = CarryFast(host);

        LayerAt(commands, 0.25f).Should().BeGreaterThan(-1, "the card is dimmed as the theme says");
        LayerAt(commands, 0.7f).Should().Be(-1, "the list's own copy opacity comes first");
        var copy = LayerAt(commands, 0.5f);
        copy.Should().BeGreaterThan(0, "the copy is drawn at the list's own opacity");
        var shadow = commands[(copy + 1)..].First(c => c.Kind == DrawCommandKind.ShadowRRect);
        var level = theme.Elevation(5);
        shadow.StrokeWidth.Should().Be(level.Blur, "the copy's shadow is the theme's for level 5");
        var lean = commands[(copy + 1)..].First(c => c.Kind == DrawCommandKind.FillRRect).Transform.M12;
        lean.Should().BeLessThan(0, "moving right still leans the copy back");
        Math.Abs(lean).Should().BeLessThanOrEqualTo(MathF.Sin(6 * MathF.PI / 180) + 0.001f, "no further than the theme's most");
    }

    [Fact]
    public void ACarriedCard_TakesTheDefaults_WhereNeitherSaysAndTheThemesLevelFourShadow()
    {
        var (host, _) = Open();

        var commands = CarryFast(host);

        LayerAt(commands, (float)SortPlacement.LiftedOpacity).Should().BeGreaterThan(-1);
        var copy = LayerAt(commands, (float)SortPlacement.CopyOpacity);
        copy.Should().BeGreaterThan(0);
        var shadow = commands[(copy + 1)..].First(c => c.Kind == DrawCommandKind.ShadowRRect);
        var level = PhotonTheme.Instance.Elevation(SortPlacement.CopyElevation);
        shadow.StrokeWidth.Should().Be(level.Blur);
        var card = commands[(copy + 1)..].First(c => c.Kind == DrawCommandKind.FillRRect).Shape.Rect;
        shadow.Shape.Rect.Width.Should().BeApproximately(card.Width + 2 * level.Spread, 0.01f,
            "the level's spread draws in, as the theme's shadow does");
    }

    [Fact]
    public void AListWhoseLookLeansNoFurther_KeepsItsCopyUpright()
    {
        var (host, _) = Open(new Board { Style = new SortableTheme { MaxLean = 0 } });

        var commands = CarryFast(host);

        var copy = LayerAt(commands, (float)SortPlacement.CopyOpacity);
        commands[(copy + 1)..].Where(c => c.Kind == DrawCommandKind.FillRRect)
            .Should().OnlyContain(c => c.Transform.M12 == 0, "the list's most is 0");
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

    // ---- what a reader hears (#1564) --------------------------------------------------------------------------

    /// <summary>An app's own words, with every hole a step has, so a test reads each step from them.</summary>
    private static readonly SortAnnouncements OwnWords = new("Use Space.", "Took {0} ({1}/{2}, {3}).",
        "{0} now {1}/{2} in {3}.", "{0} in {1}.", "{0}: not there.", "Left {0} at {1}/{2} in {3}.", "Left {0} in {1}.",
        "{0} back.");

    /// <summary>A key, then a frame a second and a little after the last, past the live region's quiet window, and
    /// what the host has to say since.</summary>
    private static IReadOnlyList<LiveAnnouncement> Step(PhotonHost host, string key, ref float time)
    {
        host.KeyDown(key).Should().BeTrue();
        time += 1100;
        host.RenderFrame(new DisplayListBuilder(), time);
        return host.TakeAnnouncements();
    }

    private static int ListId(PhotonHost host, string id) =>
        Walk(host.LastFrame!.Root).First(n => n.Source is Sortable { } s && s.Id == id).Element!.Id;

    [Fact]
    public void AKeyboardsCarry_SaysEachStep_InTheListsWords_ThroughItsLiveRegion()
    {
        var other = new SortAnnouncements("Other.", "Other.", "Other.", "Other.", "Other.", "Other.", "Other.", "Other.");
        var (host, _) = Open(new Board { Words = OwnWords, DoingWords = other, Labels = ["To do", "Doing"] });
        host.KeyDown("Tab");
        var time = 32f;
        host.RenderFrame(new DisplayListBuilder(), time);
        host.TakeAnnouncements();

        Step(host, " ", ref time).Select(said => said.Text).Should().Equal("Took a1 (1/3, To do).");
        Step(host, "ArrowDown", ref time).Select(said => said.Text).Should().Equal("a1 now 2/3 in To do.");
        // The next list of the group: the carry still speaks through the list it started from, in its words.
        var across = Step(host, "ArrowRight", ref time);
        across.Select(said => said.Text).Should().Equal("a1 now 2/3 in Doing.");
        across[0].Id.Should().Be(ListId(host, "todo"));
        across[0].Urgency.Should().Be(LiveRegionUrgency.Polite);
        Step(host, "Enter", ref time).Select(said => said.Text).Should().Equal("Left a1 at 2/3 in Doing.");

        // Android reads the step from the list's node, a polite live region holding the words.
        var todo = host.Semantics().Single(node => node.Id == ListId(host, "todo"));
        todo.Should().Match<SemanticNode>(node => node.Label == "To do" && node.Live == LiveRegionUrgency.Polite);
        host.LiveRegionText(todo.Id).Should().Be("Left a1 at 2/3 in Doing.");
    }

    [Fact]
    public void AKeyboardsCarry_SaysARefusalThePlacesInsideAndThePutBack()
    {
        var (host, _) = Open(new Board { Words = OwnWords, Nests = true, Guard = drop => drop.To != "doing" });
        host.KeyDown("Tab");
        var time = 32f;
        host.RenderFrame(new DisplayListBuilder(), time);

        Step(host, " ", ref time).Select(said => said.Text).Should().Equal(["Took a1 (1/3, todo)."],
            "a list with no label is named by its id, as on the web");
        Step(host, "ArrowRight", ref time).Select(said => said.Text).Should().Equal("a1: not there.");
        Step(host, "ArrowDown", ref time).Select(said => said.Text).Should().Equal("a1 in a2.");
        Step(host, "Escape", ref time).Select(said => said.Text).Should().Equal("a1 back.");

        host.KeyDown(" ");
        host.KeyDown("ArrowDown");
        Step(host, "Enter", ref time).Select(said => said.Text).Should().Equal("Left a1 in a2.");
    }

    [Fact]
    public void AListGivenNoWords_SaysNothing_AndItsSemanticsAreUnchanged()
    {
        var (host, board) = Open();
        host.KeyDown("Tab");
        var time = 32f;
        host.RenderFrame(new DisplayListBuilder(), time);

        Step(host, " ", ref time).Should().BeEmpty();
        Step(host, "ArrowDown", ref time).Should().BeEmpty();
        Step(host, "Enter", ref time).Should().BeEmpty();
        board.Dropped.Should().ContainSingle();
        host.Semantics().Should().NotContain(node => node.Live != null);
    }

    // ---- a list whose items have depths (#1594) ------------------------------------------------------------------

    /// <summary>The outline <c>sortable.spec.ts</c> carries: src (0), App.cs (1), Program.cs (1), docs (0) and README.md
    /// (0), 40 tall, one depth 16 across, in a list of a group beside a second list holding z.</summary>
    private sealed class Outline : StatefulComponent
    {
        public List<SortDrop?> Hovered { get; } = [];
        public List<SortDrop> Dropped { get; } = [];
        public Func<SortDrop, bool>? Guard { get; init; }

        /// <summary>The id the fourth row goes by, which a test changes while an item is carried over it.</summary>
        private string _docs = "docs";

        public void Rename(string id) => SetState(() => _docs = id);

        private static SortableItem Item(string name, int depth) =>
            new(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = CardHeight }), () => { }) { Label = name },
                name) { Depth = depth };

        public override VisualNode Build(ComponentContext context)
        {
            var files = new Column(gap: 0);
            foreach (var (name, depth) in new[] { ("src", 0), ("App.cs", 1), ("Program.cs", 1), (_docs, 0), ("README.md", 0) })
                files.Add(Item(name, depth));
            var next = new Column(gap: 0);
            next.Add(Item("z", 0));
            var row = new Row(gap: 20);
            row.Add(new Sortable(new Box(new BoxStyle { Width = ColumnWidth }, files), "files", Dropped.Add, OwnWords)
            {
                Group = "outline",
                Nests = true,
                Indent = 16,
                Label = "Files",
                OnHovered = Hovered.Add,
                Accepts = Guard,
            });
            row.Add(new Sortable(new Box(new BoxStyle { Width = ColumnWidth }, next), "next", Dropped.Add, OwnWords)
            {
                Group = "outline",
                Label = "Next",
                OnHovered = Hovered.Add,
            });
            return row;
        }
    }

    private static PhotonHost OpenOutline(Outline outline)
    {
        var host = new PhotonHost(outline, PhotonTheme.Instance, ThemeMode.Light, 400, 600);
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        return host;
    }

    private static Rect ItemBox(PhotonHost host, string id) =>
        Walk(host.LastFrame!.Root).First(n => n.Source is SortableItem { } item && item.Id == id).Bounds;

    /// <summary>A mouse picks README.md up and holds it over docs' top eighth, before docs, <paramref name="across"/>
    /// points toward the end of the cross axis; the place last reported.</summary>
    private static SortDrop? HoldBeforeDocs(PhotonHost host, Outline outline, float across)
    {
        var readme = CardAt(host, "README.md");
        var docs = ItemBox(host, "docs");
        host.PressDown(readme.X, readme.Y);
        host.PointerMove(readme.X, readme.Y - 10);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.PointerMove(readme.X + across, docs.Y + docs.Height / 8);
        host.RenderFrame(new DisplayListBuilder(), 48);
        return outline.Hovered[^1];
    }

    [Fact]
    public void AListWithDepths_KeepsTheItemsOwnDepth_UntilThePointerTravelsAnIndentAcross()
    {
        var outline = new Outline();
        var host = OpenOutline(outline);

        HoldBeforeDocs(host, outline, 0).Should().Be(new SortDrop("README.md", "files", "files", 3) { Depth = 0 });
        var y = ItemBox(host, "docs").Y + ItemBox(host, "docs").Height / 8;
        var x = CardAt(host, "README.md").X;
        host.PointerMove(x + 16, y);
        outline.Hovered[^1].Should().Be(new SortDrop("README.md", "files", "files", 3) { Depth = 1 });
        host.PointerMove(x + 32, y);
        outline.Hovered[^1]!.Depth.Should().Be(2);
        // Four indents across, still over the list (150 wide).
        host.PointerMove(x + 60, y);
        outline.Hovered[^1]!.Depth.Should().Be(2, "no deeper than one past Program.cs");
        host.PointerMove(x + 16, y);
        host.PressUp(x + 16, y);

        outline.Dropped.Should().ContainSingle().Which.Should().Be(new SortDrop("README.md", "files", "files", 3) { Depth = 1 });
    }

    [Fact]
    public void ThePlaceIsAskedAgain_WhenTheItemsAroundItChange_AtTheSameIndex()
    {
        var outline = new Outline();
        var host = OpenOutline(outline);
        var readme = CardAt(host, "README.md");
        var docs = CardAt(host, "docs");
        host.PressDown(readme.X, readme.Y);
        host.PointerMove(readme.X, readme.Y - 10);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.PointerMove(readme.X, docs.Y);
        outline.Hovered[^1]!.Into.Should().Be("docs");

        // The row under the pointer is another item now, at the same index.
        outline.Rename("notes");
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.PointerMove(readme.X, docs.Y + 1);

        outline.Hovered[^1]!.Into.Should().Be("notes", "a place is the items around it, not only its index");
        host.PointerCancel();
    }

    [Fact]
    public void AListWithDepths_GivesARefusedDepthWayToAShallowerOne()
    {
        var outline = new Outline { Guard = drop => drop.Depth != 1 };
        var host = OpenOutline(outline);

        HoldBeforeDocs(host, outline, 16)!.Depth.Should().Be(0);
    }

    [Fact]
    public void AListWithDepths_CarriesTheItemsSubtree_WhichIsNoPlaceOfItsOwn()
    {
        var outline = new Outline();
        var host = OpenOutline(outline);
        var src = CardAt(host, "src");
        var program = CardAt(host, "Program.cs");

        host.PressDown(src.X, src.Y);
        host.PointerMove(src.X, src.Y + 10);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.PointerMove(src.X, program.Y);
        host.PressUp(src.X, program.Y);

        outline.Hovered.Where(drop => drop is not null).Should().OnlyContain(drop => drop!.Index == 0);
        outline.Dropped.Should().ContainSingle().Which.Should().Be(new SortDrop("src", "files", "files", 0) { Depth = 0 });
    }

    [Fact]
    public void AListWithDepths_TheCrossArrowsStepTheDepth_AndAReaderHearsTheLevel()
    {
        var outline = new Outline();
        var host = OpenOutline(outline);
        for (var i = 0; i < 5; i++) host.KeyDown("Tab");
        var time = 32f;
        host.RenderFrame(new DisplayListBuilder(), time);
        host.TakeAnnouncements();

        Step(host, " ", ref time).Select(said => said.Text).Should().Equal("Took README.md (3/3, Files).");
        // After docs, inside docs, before docs.
        Step(host, "ArrowUp", ref time);
        Step(host, "ArrowUp", ref time).Select(said => said.Text).Should().Equal("README.md now 2/3 in Files.");
        Step(host, "ArrowRight", ref time).Select(said => said.Text).Should().Equal("README.md now 3/3 in src.");
        Step(host, "ArrowRight", ref time).Select(said => said.Text).Should().Equal("README.md now 1/1 in Program.cs.");
        Step(host, "ArrowLeft", ref time);
        Step(host, "Enter", ref time).Select(said => said.Text).Should().Equal("Left README.md at 3/3 in src.");

        outline.Dropped.Should().ContainSingle().Which.Should().Be(new SortDrop("README.md", "files", "files", 3) { Depth = 1 });
    }

    [Fact]
    public void AListWithDepths_TheCrossArrowMovesToTheNextList_OnceThePlaceOffersNoOtherDepth()
    {
        var outline = new Outline();
        var host = OpenOutline(outline);
        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder(), 32);

        host.KeyDown(" ").Should().BeTrue();
        host.KeyDown("ArrowRight").Should().BeTrue();

        outline.Hovered[^1]!.To.Should().Be("next");
    }
}
