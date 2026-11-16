using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// WHERE each component that has a pressed fill paints it on Photon, and in what colour (#616).
/// <para>
/// The handoff names the place. A button, an icon button, a chip, a list row and the search
/// field's clear button fill their whole surface; a tab fills its cell ("Pressed: SurfaceSubtle fill
/// on the tab cell"); the navigation bar and the rail show their pill ("Press-down shows the pill"),
/// on every destination but the selected one, whose pill already shows.
/// </para>
/// <para>
/// Until #616 a pressed fill reached Photon through <c>PendingFill</c>, which the FIRST box drawn
/// under the pressed control took, wherever that box was. A tab's first box is the 3dp strip under
/// its label, so a pressed tab painted a sliver the handoff never asked for, while the web filled
/// the cell. One mechanism, the fill on the box the handoff names, puts the same place on both
/// targets; the web half is pinned in <c>PressedFillPlacementRealizerTests</c>.
/// </para>
/// </summary>
public class PressedFillPlacementTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly VariantColors Primary = Theme.Colors(Variant.Primary);

    /// <summary>Where the fill belongs: the control's own bounds, or a pill of a known size.</summary>
    public enum Place { Control, Pill }

    public sealed record Case(string Name, Func<VisualNode> Build, ColorToken Fill, Place Where,
        float PillWidth = 0, float PillHeight = 0)
    {
        public override string ToString() => Name;
    }

    private static readonly NavItem[] Items =
        [new NavItem(Icons.CheckCircle, "Home"), new NavItem(Icons.Mail, "Cards"), new NavItem(Icons.Person, "Profile")];

    public static TheoryData<Case> Cases => new()
    {
        new Case("button", () => new Button("Pay", onPressed: () => { }), Primary.Pressed, Place.Control),
        new Case("filled icon button", () => new IconButton(new Icon(Icons.Search), "Search",
            IconButtonKind.Filled, onPressed: () => { }), Primary.Pressed, Place.Control),
        new Case("selected filter chip", () => new Chip("Income", ChipKind.Filter, selected: true,
            onPressed: () => { }), Primary.Pressed.WithOpacity(0.24f), Place.Control),
        new Case("unselected filter chip", () => new Chip("Expenses", ChipKind.Filter,
            onPressed: () => { }), Theme.SurfaceSubtle, Place.Control),
        new Case("list item", () => new ListItem("Wallet", onPressed: () => { }), Theme.SurfaceSubtle, Place.Control),
        new Case("search field's clear button", () => new SearchField("query", _ => { }), Theme.Border, Place.Control),
        new Case("tab", () => new Tabs(["Overview", "Activity", "Settings"], 1, _ => { }), Theme.SurfaceSubtle, Place.Control),
        new Case("navigation bar item", () => new BottomNavigation(Items, 0, _ => { }), Primary.Subtle.WithOpacity(0.4f),
            Place.Pill, 56, 26),
        new Case("navigation rail destination", () => new NavigationRail(Items, 0, _ => { }), Primary.Subtle.WithOpacity(0.4f),
            Place.Pill, 52, 30),
    };

    private static (DisplayList Frame, LayoutNode Root) Draw(VisualNode node)
    {
        var builder = new DisplayListBuilder();
        var result = PhotonRealizer.Realize(node, 400, 400, Theme, ThemeMode.Light, ContrastLevel.Standard, builder);
        return (builder.Build(), result.Root);
    }

    private static List<Rect> Painted(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light, ContrastLevel.Standard);
        var rects = new List<Rect>();
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.FillRRect && command.Paint.Color == color)
                rects.Add(command.Shape.Rect);
        return rects;
    }

    private static List<Rect> Controls(LayoutNode node, List<Rect>? found = null)
    {
        found ??= [];
        if (node.Source is Pressable { Disabled: false }) found.Add(node.Bounds);
        foreach (var child in node.Children) Controls(child, found);
        return found;
    }

    private static bool Same(Rect a, Rect b) =>
        MathF.Abs(a.X - b.X) < 0.5f && MathF.Abs(a.Y - b.Y) < 0.5f
        && MathF.Abs(a.Width - b.Width) < 0.5f && MathF.Abs(a.Height - b.Height) < 0.5f;

    [Theory]
    [MemberData(nameof(Cases))]
    public void APressedComponent_FillsWhereTheHandoffSays(Case c)
    {
        var (rest, root) = Draw(c.Build());
        var (pressed, _) = Draw(new Simulated(SimulatedState.Pressed, c.Build()));
        var controls = Controls(root);
        controls.Should().NotBeEmpty("the component is pressable");

        var fills = Painted(pressed, c.Fill);
        fills.Should().NotBeEmpty($"a pressed {c.Name} paints its pressed fill");

        if (c.Where == Place.Control)
        {
            // Every control is filled whole: the fill covers the control's own bounds, not a part of it.
            foreach (var control in controls)
                fills.Should().Contain(r => Same(r, control),
                    $"a pressed {c.Name} fills its whole surface ({control.Width}x{control.Height}); "
                    + $"it painted {string.Join(", ", fills.Select(r => $"{r.Width}x{r.Height}"))}");
        }
        else
        {
            // Every item but the selected one shows its pill, and the fill is the pill, never the
            // whole item. The selected one's pill already shows, and keeps its own colour.
            fills.Count.Should().Be(controls.Count - 1, $"each unselected {c.Name} shows its pill");
            Painted(pressed, Theme.Colors(Variant.Primary).Subtle).Should().Contain(
                r => MathF.Abs(r.Width - c.PillWidth) < 0.5f && MathF.Abs(r.Height - c.PillHeight) < 0.5f,
                $"the selected {c.Name} keeps its pill while pressed");
            fills.Should().OnlyContain(r => MathF.Abs(r.Width - c.PillWidth) < 0.5f && MathF.Abs(r.Height - c.PillHeight) < 0.5f,
                $"a pressed {c.Name} fills its {c.PillWidth}x{c.PillHeight} pill; "
                + $"it painted {string.Join(", ", fills.Select(r => $"{r.Width}x{r.Height}"))}");
        }

        // And the fill is the press's: at rest none of those rects is painted in it.
        foreach (var fill in fills)
            Painted(rest, c.Fill).Should().NotContain(r => Same(r, fill), $"a {c.Name} at rest is not pressed");
    }

    /// <summary>A HELD press paints the same place as a pictured one: the host tracks the control the
    /// pointer went down on, and its fill lands where the simulated press put it.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void AHeldPress_FillsTheSamePlaceAsAPicturedOne(Case c)
    {
        var (pictured, root) = Draw(new Simulated(SimulatedState.Pressed, c.Build()));
        // The last control: an unselected one wherever there is a selection.
        var target = Controls(root)[^1];
        var expected = Painted(pictured, c.Fill)
            .Where(r => target.Contains(new Point(r.X + r.Width / 2, r.Y + r.Height / 2))).ToList();

        var host = new PhotonHost(c.Build(), Theme, ThemeMode.Light, 400, 400);
        Frame(host);
        host.PressDown(target.X + target.Width / 2, target.Y + target.Height / 2);
        var held = Painted(Frame(host), c.Fill)
            .Where(r => target.Contains(new Point(r.X + r.Width / 2, r.Y + r.Height / 2))).ToList();

        held.Should().ContainSingle($"the held {c.Name} paints one pressed fill");
        Same(held[0], expected.Single()).Should().BeTrue(
            $"held, the {c.Name} fills {held[0].Width}x{held[0].Height}, pictured {expected.Single().Width}x{expected.Single().Height}");
    }

    /// <summary>A selected row's fill IS its state, and a press does not wash it away.</summary>
    [Fact]
    public void APressedSelectedRow_KeepsItsSelection()
    {
        var (pressed, _) = Draw(new Simulated(SimulatedState.Pressed,
            new ListItem("Wallet", onPressed: () => { }) { Selected = true }));

        Painted(pressed, Primary.Subtle).Should().NotBeEmpty("the selected row keeps its fill pressed");
        Painted(pressed, Theme.SurfaceSubtle).Should().BeEmpty("and is not washed to the press's");
    }

    private static List<DrawCommand> Commands(DisplayList list, DrawCommandKind kind, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light, ContrastLevel.Standard);
        var found = new List<DrawCommand>();
        foreach (var command in list.Commands)
            if (command.Kind == kind && command.Paint.Color == color) found.Add(command);
        return found;
    }

    /// <summary>The ink of every text and glyph Photon laid out, in order. Read from the layout tree:
    /// a glyph's raster needs the platform's rasterizer, which only the Mac's suite has.</summary>
    private static List<ColorToken?> Inks(LayoutNode node, List<ColorToken?>? found = null)
    {
        found ??= [];
        switch (node.Source)
        {
            case Text text: found.Add(text.Color); break;
            case Icon icon: found.Add(icon.Color); break;
            case Vector vector: found.Add(vector.Color); break;
        }
        foreach (var child in node.Children) Inks(child, found);
        return found;
    }

    /// <summary>B8's frame: an unselected filter chip rests OUTLINED (a 1dp BorderStrong edge, no
    /// fill, TextPrimary text), so its SurfaceSubtle press is a change the eye can see.</summary>
    [Fact]
    public void AnUnselectedFilterChip_RestsOutlined()
    {
        var (rest, root) = Draw(new Chip("Expenses", ChipKind.Filter, onPressed: () => { }));

        Painted(rest, Theme.SurfaceSubtle).Should().BeEmpty("an outlined chip has no fill at rest");
        Commands(rest, DrawCommandKind.StrokeRRect, Theme.BorderStrong).Should().ContainSingle()
            .Which.StrokeWidth.Should().Be(1f, "its edge is a 1dp BorderStrong line");
        Inks(root).Should().Equal([Theme.TextPrimary], "its label is TextPrimary");
    }

    /// <summary>B8: "outlined/quiet chips = SurfaceSubtle". Hovered, the outlined chip fills its whole
    /// surface with it, as an outline Button does.</summary>
    [Fact]
    public void AnUnselectedFilterChip_HoversOnSurfaceSubtle()
    {
        var (hovered, root) = Draw(new Simulated(SimulatedState.Hovered,
            new Chip("Expenses", ChipKind.Filter, onPressed: () => { })));

        Painted(hovered, Theme.SurfaceSubtle).Should().ContainSingle()
            .Which.Should().Match<Rect>(r => Same(r, Controls(root).Single()), "the hover fills the chip whole");
    }

    /// <summary>B8's frame ("rio-trip.pdf"): the Input chip rests on SurfaceSubtle with a TextPrimary
    /// label, and its ✕ a tier below, TextMuted.</summary>
    [Fact]
    public void AnInputChip_LabelIsPrimary_AndItsCloseIsMuted()
    {
        var (rest, root) = Draw(new Chip("rio-trip.pdf", ChipKind.Input, onRemove: () => { }));

        Painted(rest, Theme.SurfaceSubtle).Should().ContainSingle("the Input chip rests on SurfaceSubtle");
        Inks(root).Should().Equal([Theme.TextPrimary, Theme.TextMuted], "its label is TextPrimary, its ✕ TextMuted");
    }

    private static DisplayList Frame(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return builder.Build();
    }
}
