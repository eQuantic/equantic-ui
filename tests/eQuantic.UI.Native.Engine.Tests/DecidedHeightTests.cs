using eQuantic.UI.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A BOX WITH A DECIDED HEIGHT HANDS IT DOWN. The width has always worked this way — a div's child
/// div is full-width, and the engine mirrors it — while the height stopped at the parent, so a 56dp
/// bar holding a Row got a Row as tall as its tallest label and `Cross = Center` centred inside
/// THAT. The toolbar sat against the top of its own bar, three times, in three different screens.
/// <para>
/// It reaches exactly what an auto-sized CONTAINER is. Text, images and icons size themselves and
/// never took the flag; a button, a link and an input hug, because a block stretch stops at an
/// inline-block — the fence the width has always respected.
/// </para>
/// </summary>
public class DecidedHeightTests
{
    private static readonly LayoutContext Ctx =
        new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    private static LayoutNode Layout(VisualNode root) => LayoutEngine.Layout(root, 400, 300, Ctx);

    [Fact]
    public void ARowInsideABarFillsTheBar()
    {
        var row = new Row(gap: 8) { Cross = CrossAlign.Center };
        row.Add(new Text("Title", TypeRole.Label));
        var bar = new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56 }, row);

        var laid = Layout(bar);

        laid.Children[0].Bounds.Height.Should().Be(56,
            "the bar decided a height, so its row takes it — and centring means centring in the bar");
    }

    /// <summary>The label inside is then CENTRED, which is the bug this closes: it used to sit on
    /// the bar's top edge because the row it lived in was only as tall as the label.</summary>
    [Fact]
    public void AndItsContentIsCentredInTheBar()
    {
        var row = new Row(gap: 8) { Cross = CrossAlign.Center };
        row.Add(new Text("Title", TypeRole.Label));
        var laid = Layout(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56 }, row));

        var label = laid.Children[0].Children[0];
        var margin = label.Bounds.Y;
        var below = 56 - (label.Bounds.Y + label.Bounds.Height);
        margin.Should().BeApproximately(below, 1f, "the label sits in the middle of the bar");
    }

    [Fact]
    public void AHuggingBoxStillHugs()
    {
        var row = new Row(gap: 8) { Cross = CrossAlign.Center };
        row.Add(new Text("Title", TypeRole.Label));
        var laid = Layout(new Box(new BoxStyle { Width = SizeValue.Fill }, row));

        laid.Children[0].Bounds.Height.Should().BeLessThan(56,
            "a box that decided nothing hands nothing down");
    }

    /// <summary>A control keeps its own height inside a taller box — the inline-block fence, and
    /// the reason a button in a 56dp bar is still a button and not a 56dp slab.</summary>
    [Fact]
    public void AControlKeepsItsOwnHeight()
    {
        var laid = Layout(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56 },
            new Button("Save")));

        laid.Children[0].Bounds.Height.Should().BeLessThan(56);
    }

    /// <summary>And a child that asked for its own height is never overruled.</summary>
    [Fact]
    public void AnExplicitHeightWins()
    {
        var inner = new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 });
        var laid = Layout(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56 }, inner));

        laid.Children[0].Bounds.Height.Should().Be(20);
    }
}
