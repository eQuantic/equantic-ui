using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What two <see cref="SizeKind.Fill"/> siblings do on a stack's main axis, pinned because the
/// enum documents it and a documented number nobody compiles is the thing this repo keeps getting
/// wrong.
/// <para>
/// Reported from the eQuantic Code IDE, whose docking view had drawn every split with the second
/// pane full size and past the end of its column: the terminal and the file tree were absent from
/// the running window with a green build and green tests. That was Photon's single-line column,
/// which took nothing back from an overflow (#747), where a browser and a Photon row share it: 150 |
/// 150 in Chrome 152, on both axes, for the declarations the web realizer writes.
/// </para>
/// </summary>
public class FillSiblingsTests
{
    [Fact]
    public void TwoFillChildrenInAColumn_ShareItsExtent()
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill, Height = 300 };
        column.Add(new Box(new BoxStyle { Height = SizeValue.Fill }));
        column.Add(new Box(new BoxStyle { Height = SizeValue.Fill }));

        var root = PhotonRealizer.Realize(column, 400, 300, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder()).Root;

        root.Children.Should().HaveCount(2);
        root.Children[0].Bounds.Y.Should().Be(0);
        root.Children[0].Bounds.Height.Should().Be(150);
        root.Children[1].Bounds.Y.Should().Be(150);
        root.Children[1].Bounds.Height.Should().Be(150);
    }

    [Fact]
    public void TwoFillChildrenInARow_ShareItsExtent()
    {
        var row = new Row(gap: 0) { Width = 300, Height = 40 };
        row.Add(new Box(new BoxStyle { Width = SizeValue.Fill }));
        row.Add(new Box(new BoxStyle { Width = SizeValue.Fill }));

        var root = PhotonRealizer.Realize(row, 400, 300, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder()).Root;

        root.Children[0].Bounds.Width.Should().Be(150);
        root.Children[1].Bounds.X.Should().Be(150);
        root.Children[1].Bounds.Width.Should().Be(150);
    }

    /// <summary>A Fill below a header keeps what the header leaves, where it used to take the whole
    /// column and run the header's height past its end.</summary>
    [Fact]
    public void AFillChild_KeepsWhatAFixedSiblingLeaves()
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill, Height = 300 };
        column.Add(new Box(new BoxStyle { Height = 100 }));
        column.Add(new Box(new BoxStyle { Height = SizeValue.Fill }));

        var root = PhotonRealizer.Realize(column, 400, 300, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder()).Root;

        root.Children[0].Bounds.Height.Should().Be(100);
        root.Children[1].Bounds.Y.Should().Be(100);
        root.Children[1].Bounds.Height.Should().Be(200);
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
