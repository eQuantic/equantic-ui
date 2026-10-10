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

    /// <summary>
    /// A row its scroll view clipped away whole takes no press from the last row shown (Copilot on
    /// #690). Its slop reaches back over the viewport's edge, and its drawn box, clipped, is a line of
    /// no height on that edge, which lay inside the last row's box by the rule that lets a slop in
    /// front win: a press low in the last row ran the row under it, which nobody could see.
    /// </summary>
    [Fact]
    public void ARowClippedAwayWhole_TakesNoPressFromTheLastRowShown()
    {
        var pressed = new List<int>();
        var column = new Column(gap: 0) { Width = SizeValue.Fixed(200) };
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            column.Add(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }),
                () => pressed.Add(index)));
        }
        // A viewport 100dp tall: rows 0 to 4 fill it, and row 5 starts on its bottom edge.
        var page = new Box(new BoxStyle { Padding = EdgeInsets.All(60) },
            new ScrollView(column) { Width = SizeValue.Fixed(200), Height = SizeValue.Fixed(100) });
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400) { Density = Density.Comfortable };
        var rows = host.RenderFrame(new DisplayListBuilder()).HitRegions;
        rows.Should().HaveCount(6, "row 5's slop still reaches into the viewport, so it is registered");
        rows[5].Drawn.Height.Should().Be(0, "and nothing of its box is on screen");

        host.Tap(rows[4].Drawn.X + 10, rows[4].Drawn.Bottom - 5);

        pressed.Should().Equal([4], "the press is inside the last row shown, and nothing of row 5 is drawn there");
    }

    /// <summary>
    /// Turned together, a narrow row under a wide one stands beside it, not inside it (Copilot's
    /// second round on #690). The box around the turned narrow row nests in the box around the turned
    /// wide one, and a press near the wide row's foot, inside the narrow row's slop, ran the narrow row.
    /// </summary>
    [Fact]
    public void TwoRowsTurnedTogether_ARowsSlopTakesNoPressFromTheRowBesideIt()
    {
        var pressed = new List<string>();
        var column = new Column(gap: 0, cross: CrossAlign.Center);
        column.Add(new Pressable(new Box(new BoxStyle { Width = 200, Height = 20 }), () => pressed.Add("wide")));
        column.Add(new Pressable(new Box(new BoxStyle { Width = 20, Height = 20 }), () => pressed.Add("narrow")));
        // Laid out at 150, 150: the wide row from 150 to 350 across and 150 to 170 down, the narrow one
        // from 240 to 260 across and 170 to 190 down, both turned 45° about the box's centre, (250, 170).
        var turned = new Box(new BoxStyle { Width = 200, Height = 40, Transform = Transform2D.Rotate(45) }, column);
        var host = new PhotonHost(new Box(new BoxStyle { Padding = EdgeInsets.All(150) }, turned),
            PhotonTheme.Instance, ThemeMode.Light, 500, 400) { Density = Density.Comfortable };
        host.RenderFrame(new DisplayListBuilder());

        // 2dp above the wide row's foot, at its middle: inside the wide row as drawn, and inside the
        // target the narrow row keeps beyond its box.
        var at = Matrix2D.Rotation(MathF.PI / 4).Transform(new Point(0, -2));
        host.Tap(250 + at.X, 170 + at.Y);

        pressed.Should().Equal(["wide"], "the press is on the wide row as drawn, and the narrow row is drawn beside it");
    }

    /// <summary>A card 200 by 100 in a view that clips at its right edge, holding a 40 by 20 button
    /// placed 180 in, which runs past the card and is drawn only up to the clip; the subtree is moved by
    /// <paramref name="moved"/> when one is given. Taps 185 in and 35 down, in the button's slop and on
    /// the card's box, and answers what took the tap.</summary>
    private static List<string> TapBesideAClippedButton(Transform2D? moved)
    {
        var pressed = new List<string>();
        var stack = new Stack();
        stack.Add(new Positioned(new Pressable(new Box(new BoxStyle { Width = 40, Height = 20 }),
            () => pressed.Add("button")), top: 10, start: 180));
        var card = new Pressable(new Box(new BoxStyle { Width = 200, Height = 100 }, stack), () => pressed.Add("card"));
        var view = new Box(new BoxStyle { Width = 200, Height = 100, Clip = true }, card);
        VisualNode subtree = moved is { } transform ? new Box(new BoxStyle { Transform = transform }, view) : view;
        var host = new PhotonHost(new Box(new BoxStyle { Padding = EdgeInsets.All(100) }, subtree),
            PhotonTheme.Instance, ThemeMode.Light, 500, 400) { Density = Density.Comfortable };
        host.RenderFrame(new DisplayListBuilder());

        var shift = moved is { } by ? new Point(by.TranslateX, by.TranslateY) : new Point(0, 0);
        host.Tap(100 + 185 + shift.X, 100 + 35 + shift.Y);
        return pressed;
    }

    /// <summary>
    /// A control that runs past its card and is clipped at the card's edge stands inside the card as
    /// drawn, moved or not (Copilot's third round on #690). Moved, the two boxes were compared by their
    /// own corners, unclipped: the button reached past the card, and the card took the tap its slop
    /// takes unmoved.
    /// </summary>
    [Fact]
    public void AClippedButtonInACard_TakesTheTapBesideIt_MovedOrNot()
    {
        TapBesideAClippedButton(null).Should().Equal(["button"],
            "beside the button is its target, and what is drawn of it lies inside the card");
        TapBesideAClippedButton(Transform2D.Translate(100, 100)).Should().Equal(["button"],
            "and moving the whole subtree changes nothing about it");
    }
}
