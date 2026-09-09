using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What two <see cref="SizeKind.Fill"/> siblings do on a stack's main axis, pinned because the
/// enum now documents it and a documented number nobody compiles is the thing this repo keeps
/// getting wrong.
/// <para>
/// Reported from the eQuantic Code IDE, whose docking view had drawn every split this way: the
/// terminal and the file tree were absent from the running window with a green build and green
/// tests. The reporter's model was "the second gets none", which predicts a zero-height strip. It
/// is the opposite — the second is FULL size and outside the parent — and the two predict
/// different symptoms, so the number is worth holding still.
/// </para>
/// </summary>
public class FillSiblingsTests
{
    [Fact]
    public void TwoFillChildren_BothTakeTheWholeExtent_AndTheSecondLandsPastTheEnd()
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill, Height = 300 };
        column.Add(new Box(new BoxStyle { Height = SizeValue.Fill }));
        column.Add(new Box(new BoxStyle { Height = SizeValue.Fill }));

        var root = PhotonRealizer.Realize(column, 400, 300, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder()).Root;

        root.Children.Should().HaveCount(2);
        root.Children[0].Bounds.Y.Should().Be(0);
        root.Children[0].Bounds.Height.Should().Be(300);
        // Not starved — full height, and starting where its parent ends.
        root.Children[1].Bounds.Y.Should().Be(300);
        root.Children[1].Bounds.Height.Should().Be(300);
    }

    [Fact]
    public void FlexibleWithWeights_IsTheSplit_AndTheRatioIsHonoured()
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill, Height = 300 };
        column.Add(new Flexible(new Box(new BoxStyle()), 2));
        column.Add(new Flexible(new Box(new BoxStyle()), 1));

        var root = PhotonRealizer.Realize(column, 400, 300, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder()).Root;

        root.Children[0].Bounds.Height.Should().BeApproximately(200, 0.5f);
        root.Children[1].Bounds.Height.Should().BeApproximately(100, 0.5f);
        (root.Children[0].Bounds.Height + root.Children[1].Bounds.Height)
            .Should().BeApproximately(300, 0.5f, "a split adds up to its parent");
    }
}
