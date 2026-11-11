using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
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

    /// <summary>
    /// A node's OWN threshold switches its arm at the same width on every target (#735, #1465): the element
    /// tree holds the arm <see cref="AdaptiveNode.ResolveWidth"/> answers for the window's width, Photon lays
    /// that arm out and paints it, and the web's gates show it. Photon and the element tree resolved by the
    /// spec's size classes, so the dashboard's payments page (<c>ExpandedFrom = 1024</c>) drew its grid on
    /// Photon at 900 dp where the web stacked it, and an arm the web showed was one no element held. The
    /// rows of #735: a threshold above the spec's 840, and one below it, at fractional dp.
    /// </summary>
    [Theory]
    [InlineData(1024f, 900f)]
    [InlineData(980f, 840f)]
    [InlineData(980f, 979f)]
    [InlineData(980f, 980f)]
    [InlineData(703.704f, 704f)]
    [InlineData(703.704f, 839f)]
    [InlineData(703.704f, 840f)]
    public void ANodesOwnThreshold_PicksTheSameArm_InTheElementTree_OnPhoton_AndInTheWebsGates(float expandedFrom,
        float width)
    {
        var expanded = width >= expandedFrom;
        AdaptiveNode Node() => new(Marker(0x10), medium: null, expanded: Marker(0x30)) { ExpandedFrom = expandedFrom };

        var node = Node();
        var root = ComponentBoundary.StructuralOwner().Build(node, new ComponentContext(PhotonTheme.Instance), width,
            rebuildAll: true);
        VisualNode? held = null;
        root.VisitChildren(child => held = child.Configuration);
        held.Should().BeSameAs(expanded ? node.Expanded : node.Compact, "the element tree holds the arm for the width");

        var host = new PhotonHost(Node(), PhotonTheme.Instance, ThemeMode.Light, width, 200);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        var fills = new List<byte>();
        foreach (var command in builder.Build().Commands)
            if (command.Kind == DrawCommandKind.FillRRect) fills.Add(command.Paint.Color.R);
        fills.Should().Contain(expanded ? (byte)0x30 : (byte)0x10, "Photon paints the arm for the width")
            .And.NotContain(expanded ? (byte)0x10 : (byte)0x30);

        var sink = new StyleSink();
        WebRealizer.Lower(Node(), PhotonTheme.Instance, 1f, sink);
        sink.HidesAt(AdaptiveGates.ExpandedFrom(expandedFrom), width).Should().Be(!expanded, "the web's gate for Expanded");
        sink.HidesAt(AdaptiveGates.CompactUntil(expandedFrom), width).Should().Be(expanded, "the web's gate for Compact");
    }

    /// <summary>The middle arm at a node's own <c>MediumFrom</c>, held to the same rule on every target (#1465):
    /// a three-arm node that turns medium at 720 dp and expanded at 1100.</summary>
    [Theory]
    [InlineData(650f, 0x10)]
    [InlineData(720f, 0x20)]
    [InlineData(1099f, 0x20)]
    [InlineData(1100f, 0x30)]
    public void ANodesOwnMediumThreshold_PicksTheSameArm_InTheElementTree_OnPhoton_AndInTheWebsGates(float width,
        byte arm)
    {
        AdaptiveNode Node() => new(Marker(0x10), Marker(0x20), Marker(0x30)) { MediumFrom = 720, ExpandedFrom = 1100 };

        var node = Node();
        var root = ComponentBoundary.StructuralOwner().Build(node, new ComponentContext(PhotonTheme.Instance), width,
            rebuildAll: true);
        VisualNode? held = null;
        root.VisitChildren(child => held = child.Configuration);
        held.Should().BeSameAs(arm switch { 0x10 => node.Compact, 0x20 => node.Medium, _ => node.Expanded });

        var host = new PhotonHost(Node(), PhotonTheme.Instance, ThemeMode.Light, width, 200);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        var fills = new List<byte>();
        foreach (var command in builder.Build().Commands)
            if (command.Kind == DrawCommandKind.FillRRect) fills.Add(command.Paint.Color.R);
        fills.Should().Contain(arm).And.OnlyContain(fill => fill == arm || (fill != 0x10 && fill != 0x20 && fill != 0x30));

        var sink = new StyleSink();
        WebRealizer.Lower(Node(), PhotonTheme.Instance, 1f, sink);
        sink.HidesAt(AdaptiveGates.CompactUntil(720), width).Should().Be(arm != 0x10);
        sink.HidesAt(AdaptiveGates.MediumFrom(720, 1100), width).Should().Be(arm != 0x20);
        sink.HidesAt(AdaptiveGates.ExpandedFrom(1100), width).Should().Be(arm != 0x30);
    }

    [Fact]
    public void MissingVariants_FallBackTowardCompact()
    {
        var adaptive = new AdaptiveNode(Marker(0x10), medium: null, expanded: Marker(0x30));
        adaptive.ResolveWidth(700).Should().BeSameAs(adaptive.Compact,
            "no Medium variant → Compact serves the medium range");
        adaptive.ResolveWidth(1000).Should().BeSameAs(adaptive.Expanded);
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
    /// the grid read both off the AdaptiveNode. The node here carries a placement of its own that
    /// no arm asks for, and it is never read: at a compact width the compact arm, which asks for
    /// neither, stays unaligned and unspanned.
    /// </summary>
    [Fact]
    public void AnArm_AlignsItselfInItsLine_AndSpansItsGrid()
    {
        var row = new Row(cross: CrossAlign.Start);
        row.Add(new Box(new BoxStyle { Width = 40, Height = 100 }));
        row.Add(new AdaptiveNode(Marker(0x10), medium: null,
            expanded: new Box(new BoxStyle { Width = 40, Height = 10 }) { AlignSelf = CrossAlign.End })
        {
            AlignSelf = CrossAlign.Center,
        });
        var grid = new Grid([GridTrack.Fixed(100), GridTrack.Fixed(100)]);
        grid.Add(new AdaptiveNode(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 12 }), medium: null,
            expanded: new Box(new BoxStyle { Width = SizeValue.Fill, Height = 10 }) { GridSpan = 2 })
        {
            GridSpan = 2,
        });
        grid.Add(new Box(new BoxStyle { Width = 40, Height = 10 }));

        LayoutNode Lay(VisualNode root, float width) => PhotonRealizer.Realize(root, width, 800,
            PhotonTheme.Instance, ThemeMode.Light, new DisplayListBuilder()).Root;

        Lay(row, 1200).Children[1].Bounds.Y.Should().Be(90,
            "the arm aligns to the end of a 100dp line, not to the node's center");
        var cells = Lay(grid, 1200).Children;
        cells[0].Bounds.Width.Should().Be(200, "the arm spans both columns");
        cells[1].Bounds.Y.Should().Be(10, "so the next child starts the next row");

        Lay(row, 400).Children[1].Bounds.Y.Should().Be(0,
            "the compact arm asks for no alignment, so it sits at the line's start, not at the node's center");
        var compact = Lay(grid, 400).Children;
        compact[0].Bounds.Width.Should().Be(100,
            "the compact arm asks for no span, so it fills one column, not the node's two");
        compact[1].Bounds.Y.Should().Be(0, "and the next child shares its row");
    }
}
