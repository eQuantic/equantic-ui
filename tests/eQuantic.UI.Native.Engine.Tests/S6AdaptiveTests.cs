using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Spec S6 on Photon: an AdaptiveNode IS its resolved variant — the window size class
/// derives from the viewport width, and a resize across a threshold swaps the subtree.</summary>
public class S6AdaptiveTests
{
    private static Box Marker(byte r) => new(new BoxStyle
    {
        Width = 40, Height = 12, Background = new ColorToken(Color.FromRgb(r, 0, 0)),
    });

    [Theory]
    [InlineData(400f, 0x10)]   // compact
    [InlineData(700f, 0x20)]   // medium
    [InlineData(1000f, 0x30)]  // expanded
    public void AdaptiveNode_LaysOutTheVariantForTheWindowClass(float width, byte expected)
    {
        var adaptive = new AdaptiveNode(Marker(0x10), Marker(0x20), Marker(0x30));
        var host = new PhotonHost(adaptive, PhotonTheme.Instance, ThemeMode.Light, width, 200);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        var fills = new List<byte>();
        foreach (var command in builder.Build().Commands)
            if (command.Kind == DrawCommandKind.FillRRect)
                fills.Add(command.Paint.Color.R);

        fills.Should().Contain(expected).And.NotContain((byte)(expected == 0x10 ? 0x20 : 0x10),
            "only the matching variant measures and paints");
    }

    [Fact]
    public void MissingVariants_FallBackTowardCompact()
    {
        var adaptive = new AdaptiveNode(Marker(0x10), medium: null, expanded: Marker(0x30));
        WindowSizeClasses.FromWidth(700).Should().Be(WindowSizeClass.Medium);
        adaptive.Resolve(WindowSizeClass.Medium).Should().BeSameAs(adaptive.Compact,
            "no Medium variant → Compact serves the medium range");
        adaptive.Resolve(WindowSizeClass.Expanded).Should().BeSameAs(adaptive.Expanded);
    }

    /// <summary>
    /// The native half of #670. The arm IS the node on Photon, so the column lays out the arm: a
    /// Spacer arm is a Spacer to the column, rigid space on its main axis. Before, the column
    /// asked the AdaptiveNode whether it was a Spacer, measured it as an ordinary child, and the
    /// Spacer inside measured as nothing, which is what a Spacer outside a flex container is.
    /// </summary>
    [Theory]
    [InlineData(400f, 24f)]   // compact
    [InlineData(1200f, 64f)]  // expanded
    public void ASpacerArm_KeepsItsSpaceInAColumn(float width, float gap)
    {
        var column = new Column();
        column.Add(new Box(new BoxStyle { Width = 40, Height = 10 }));
        column.Add(new AdaptiveNode(Spacer.Fixed(24), medium: null, expanded: Spacer.Fixed(64)));
        column.Add(new Box(new BoxStyle { Width = 40, Height = 10 }));

        var frame = PhotonRealizer.Realize(column, width, 400, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder());

        frame.Root.Children[^1].Bounds.Y.Should().Be(10 + gap, "the arm's space sits between the two boxes");
    }

    /// <summary>
    /// The native half of #671: a Positioned arm is placed by its Stack at its offsets, and takes no
    /// part in the stack's size, exactly as a Positioned child does. Before, the stack asked the
    /// AdaptiveNode whether it was positioned, and laid the arm out at the stack's alignment.
    /// </summary>
    [Fact]
    public void APositionedArm_IsAnchoredInItsStack()
    {
        var stack = new Stack();
        stack.Add(new Box(new BoxStyle { Width = 400, Height = 300 }));
        stack.Add(new AdaptiveNode(new Box(), medium: null,
            expanded: new Positioned(new Box(new BoxStyle { Width = 32, Height = 32 }), top: 0, end: 0)));

        var frame = PhotonRealizer.Realize(stack, 1200, 800, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder());

        frame.Root.Bounds.Width.Should().Be(400);
        frame.Root.Children[1].Bounds.X.Should().Be(368, "400 wide, 32 across, pinned to the end");
        frame.Root.Children[1].Bounds.Y.Should().Be(0);
    }

    /// <summary>
    /// The same rule for the rest of what a parent reads off a direct child: the arm's own
    /// align-self places it in its line, and the arm's own span in its grid. Before, the row and
    /// the grid read both off the AdaptiveNode.
    /// </summary>
    [Fact]
    public void AnArm_AlignsItselfInItsLine_AndSpansItsGrid()
    {
        var row = new Row(cross: CrossAlign.Start);
        row.Add(new Box(new BoxStyle { Width = 40, Height = 100 }));
        row.Add(new AdaptiveNode(Marker(0x10), medium: null,
            expanded: new Box(new BoxStyle { Width = 40, Height = 10 }) { AlignSelf = CrossAlign.End }));
        var grid = new Grid([GridTrack.Fixed(100), GridTrack.Fixed(100)]);
        grid.Add(new AdaptiveNode(Marker(0x10), medium: null,
            expanded: new Box(new BoxStyle { Width = SizeValue.Fill, Height = 10 }) { GridSpan = 2 }));
        grid.Add(new Box(new BoxStyle { Width = 40, Height = 10 }));

        LayoutNode Lay(VisualNode root) => PhotonRealizer.Realize(root, 1200, 800, PhotonTheme.Instance,
            ThemeMode.Light, new DisplayListBuilder()).Root;

        Lay(row).Children[1].Bounds.Y.Should().Be(90, "the arm aligns to the end of a 100dp line");
        var cells = Lay(grid).Children;
        cells[0].Bounds.Width.Should().Be(200, "the arm spans both columns");
        cells[1].Bounds.Y.Should().Be(10, "so the next child starts the next row");
    }
}
