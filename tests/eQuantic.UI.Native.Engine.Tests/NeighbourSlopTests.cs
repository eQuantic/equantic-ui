using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A press inside a pressable's own box is that pressable's before it is a neighbour's slop (#630).
/// Every target grows evenly around its box without moving anything, to the §08 minimum under a
/// finger and to WCAG's floor under a pointer (#430), so a row shorter than the minimum reaches over
/// the rows beside it. The host gave a press to the topmost region that contained it, the row drawn
/// after: under a finger the lower part of every 20dp row ran the row below it. A slop answers where
/// no box beside it is drawn, and in front of the box it stands in.
/// </summary>
public class NeighbourSlopTests
{
    /// <summary>Three rows 20dp tall, one under the other, each a pressable that says it was pressed.</summary>
    private static (PhotonHost Host, List<int> Pressed) Rows(Density density)
    {
        var pressed = new List<int>();
        var column = new Column(gap: 0) { Width = SizeValue.Fixed(200) };
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            column.Add(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }),
                () => pressed.Add(index)));
        }
        var page = new Box(new BoxStyle { Padding = EdgeInsets.All(60) }, column);
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400) { Density = density };
        host.RenderFrame(new DisplayListBuilder());
        return (host, pressed);
    }

    /// <summary>The top, the middle and the bottom of a box, half a point inside it.</summary>
    private static float[] Heights(Rect box) => [box.Top + 0.5f, box.Top + box.Height / 2, box.Bottom - 0.5f];

    [Theory]
    [InlineData(Density.Comfortable)]
    [InlineData(Density.Compact)]
    public void APressAnywhereInARow_IsThatRows(Density density)
    {
        var (host, pressed) = Rows(density);
        var rows = host.LastFrame!.HitRegions;
        rows.Should().HaveCount(3);
        rows[1].Bounds.Height.Should().BeGreaterThan(rows[1].Drawn.Height,
            "the row's target grows past its box, which is what reaches over its neighbours");

        for (var index = 0; index < rows.Count; index++)
        {
            foreach (var y in Heights(rows[index].Drawn))
            {
                pressed.Clear();
                host.PressDown(rows[index].Drawn.X + 10, y);
                host.PressUp(rows[index].Drawn.X + 10, y);
                pressed.Should().Equal([index], $"{y} is inside row {index}'s box, pressed");

                pressed.Clear();
                host.Tap(rows[index].Drawn.X + 10, y);
                pressed.Should().Equal([index], $"{y} is inside row {index}'s box, tapped");
            }
        }
    }

    [Fact]
    public void ARowsSlop_StillTakesAPress_WhereNoBoxIsDrawn()
    {
        var (host, pressed) = Rows(Density.Comfortable);
        var rows = host.LastFrame!.HitRegions;

        host.Tap(rows[0].Drawn.X + 10, rows[0].Drawn.Top - 5);
        host.Tap(rows[2].Drawn.X + 10, rows[2].Drawn.Bottom + 5);

        pressed.Should().Equal([0, 2], "above the first row and under the last, only their slop is there");
    }

    [Fact]
    public void ADisabledRowsSlop_SwallowsNoPressFromTheRowBesideIt()
    {
        var pressed = new List<string>();
        var column = new Column(gap: 0) { Width = SizeValue.Fixed(200) };
        column.Add(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }),
            () => pressed.Add("enabled")));
        column.Add(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }),
            () => pressed.Add("disabled")) { Disabled = true });
        var host = new PhotonHost(new Box(new BoxStyle { Padding = EdgeInsets.All(60) }, column),
            PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        var rows = host.RenderFrame(new DisplayListBuilder()).HitRegions;

        var y = rows[0].Drawn.Bottom - 0.5f;
        host.PressDown(rows[0].Drawn.X + 10, y);
        host.PressUp(rows[0].Drawn.X + 10, y);

        pressed.Should().Equal(["enabled"], "the disabled row's slop reaches there, and its box does not");
        host.CursorAt(rows[0].Drawn.X + 10, y).Should().Be(CursorShape.Pointer, "and the pointer says the same");
    }

    [Fact]
    public void ASmallControlsSlop_InFrontOfTheBoxItStandsIn_TakesThePress()
    {
        // A card that takes presses, holding a 20dp control: a press beside the control, in its slop,
        // lands on the card's box too, and the control is drawn in front of it.
        var pressed = new List<string>();
        var control = new Pressable(new Box(new BoxStyle { Width = 20, Height = 20 }), () => pressed.Add("control"));
        var card = new Pressable(new Box(new BoxStyle { Width = 200, Height = 120, Padding = EdgeInsets.All(50) }, control),
            () => pressed.Add("card"));
        var host = new PhotonHost(new Box(new BoxStyle { Padding = EdgeInsets.All(40) }, card),
            PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        var regions = host.RenderFrame(new DisplayListBuilder()).HitRegions;
        var inner = regions[1].Drawn;

        host.Tap(inner.X - 6, inner.Y + inner.Height / 2);
        host.Tap(regions[0].Drawn.X + 4, regions[0].Drawn.Y + 4);

        pressed.Should().Equal(["control", "card"],
            "beside the control is its target, as a padded target is in Flutter, and the card's corner is the card's");
    }
}
