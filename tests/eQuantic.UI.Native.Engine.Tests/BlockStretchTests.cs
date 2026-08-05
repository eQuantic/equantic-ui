using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// CSS block semantics in the box model, and the inline-block boundary that keeps it honest:
/// a box whose width is determined stretches an auto-sized CONTAINER child across it (a div's
/// child div is full-width without asking) — but a button, a link, an input hug there, because
/// they are inline-block. A FLEX stretch (align-items: stretch) reaches through anything,
/// buttons included — that distinction is <see cref="StretchKind"/>. Height never block-stretches:
/// pages grow downward.
/// <para>
/// Found in the Studio's "Bounded values" card: a Slider inside a hugging Column lost its track
/// (the halves are weights over leftover, and there was no leftover to weigh), and a Stepper's
/// reading sat left inside its MinWidth cell because the cell never handed its width down.
/// </para>
/// </summary>
public class BlockStretchTests
{
    private static LayoutNode Find(LayoutNode node, Func<LayoutNode, bool> match)
    {
        if (match(node)) return node;
        foreach (var child in node.Children)
        {
            var found = Find(child, match);
            if (found is not null) return found;
        }
        return null!;
    }

    private static LayoutNode Realize(VisualNode root, float width = 400, float height = 200) =>
        PhotonRealizer.Realize(root, width, height, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder()).Root;

    [Fact]
    public void ABoxStretchesAnAutoContainerChild_LikeADiv()
    {
        var inner = new Row(gap: 0) { Main = MainAlign.Center };
        inner.Add(new Text("mid", TypeRole.BodyM));
        var column = new Column(gap: 0);
        column.Add(inner);
        var box = new Box(new BoxStyle { Width = 300 }, column);

        var root = Realize(box);
        var text = Find(root, n => n.Source is Text);
        var centre = text.Bounds.X + text.Bounds.Width / 2;
        centre.Should().BeApproximately(150, 1, "a block container hands its width to a block child");
    }

    [Fact]
    public void AButtonInsideABoxStaysAtItsOwnSize_InlineBlock()
    {
        var box = new Box(new BoxStyle { Width = 300 }, new Button("Save", onPressed: () => { }));

        var root = Realize(box);
        var press = Find(root, n => n.Source is Pressable);
        press.Bounds.Width.Should().BeLessThan(150,
            "a button is inline-block — a BLOCK container does not stretch it (a flex column does)");
    }

    [Fact]
    public void ASliderInAShrinkToFitContainer_KeepsAVisibleTrack()
    {
        // The Studio bug: inside a hugging container the track halves had no leftover to share and
        // the line vanished, leaving only the thumb. The control owns an intrinsic minimum for
        // exactly the reason <input type=range> is ~129px in the UA stylesheet.
        var hugging = new Row(gap: 0) { Cross = CrossAlign.Start };
        hugging.Add(new Slider(0.5f, _ => { }));

        var root = Realize(hugging);
        var slider = Find(root, n => n.Source is Box { Style.MinWidth: >= 120 });
        slider.Bounds.Width.Should().BeGreaterThanOrEqualTo(120);
        // The halves flex over the reflowed width, so the filled half has real extent.
        var half = Find(slider, n => n.Source is Flexible);
        half.Bounds.Width.Should().BeGreaterThan(10, "the track is the readout; a thumb alone is not a slider");
    }

    [Fact]
    public void AStepperReading_CentresInsideItsMinWidthCell()
    {
        var hugging = new Row(gap: 0) { Cross = CrossAlign.Start };
        hugging.Add(new Stepper(3, _ => { }) { Min = 1, Max = 9, Label = "Quantity" });

        var root = Realize(hugging);
        var cell = Find(root, n => n.Source is Box { Style.MinWidth: > 0 } box && box.Child is Row);
        var digit = Find(cell, n => n.Source is Text);
        var centre = digit.Bounds.X + digit.Bounds.Width / 2;
        var cellCentre = cell.Bounds.X + cell.Bounds.Width / 2;
        centre.Should().BeApproximately(cellCentre, 1.5f,
            "the clamped width is a decided size — the reading centres in it, not against its left edge");
    }
}
