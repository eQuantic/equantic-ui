using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A component is what it builds. So a node only its parent can place, a Spacer, a Flexible or an
/// AdaptiveNode, is placed by that parent whether it is written there or a component builds it, as
/// the web lowers both alike. Photon asked those questions of the component, which answers none of
/// them: a Spacer it built took no space in a column (#736), and an arm it built was not placed by
/// its line or its grid (#738).
/// </summary>
public class ContainerSeesThroughAComponentTests
{
    private sealed class Builds(VisualNode built) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => built;
    }

    private static Box Tall(float height) => new(new BoxStyle { Width = 40, Height = height });

    private static Framework.LayoutNode Laid(VisualNode root, float width = 400) =>
        PhotonRealizer.Realize(root, width, 300, PhotonTheme.Instance, ThemeMode.Light, new DisplayListBuilder()).Root;

    /// <summary>The issue's numbers: a box 10 tall, a Spacer of 24, and the second box at 34.</summary>
    [Fact]
    public void ASpacerAComponentBuilds_TakesTheSpaceAWrittenOneTakes()
    {
        var written = new Column(gap: 0);
        written.Add(Tall(10));
        written.Add(Spacer.Fixed(24));
        written.Add(Tall(10));

        var built = new Column(gap: 0);
        built.Add(Tall(10));
        built.Add(new Builds(Spacer.Fixed(24)));
        built.Add(Tall(10));

        Laid(written).Children[1].Bounds.Y.Should().Be(34, "the column as written");
        var node = Laid(built);
        node.Children[^1].Bounds.Y.Should().Be(34, "and the same Spacer out of a component");
    }

    /// <summary>Through two components too: what counts is what the build ends in.</summary>
    [Fact]
    public void ASpacerTwoComponentsBuild_TakesItsSpace()
    {
        var column = new Column(gap: 0);
        column.Add(Tall(10));
        column.Add(new Builds(new Builds(Spacer.Fixed(24))));
        column.Add(Tall(10));

        Laid(column).Children[^1].Bounds.Y.Should().Be(34);
    }

    /// <summary>A weighted Flexible out of a component shares the line as a written one does.</summary>
    [Fact]
    public void AFlexibleAComponentBuilds_TakesItsShare()
    {
        var row = new Row(gap: 0) { Width = 400, Height = 20 };
        row.Add(new Builds(new Flexible(new Box(new BoxStyle { Width = SizeValue.Fill }), flex: 3)));
        row.Add(new Flexible(new Box(new BoxStyle { Width = SizeValue.Fill }), flex: 1));

        var node = Laid(row);

        node.Children[0].Bounds.Width.Should().Be(300);
        node.Children[1].Bounds.Width.Should().Be(100);
    }

    /// <summary>An arm's own <c>AlignSelf</c> places it in the line, through the component.</summary>
    [Fact]
    public void AnArmAComponentBuilds_IsAlignedByItsLine()
    {
        var row = new Row(gap: 0, cross: CrossAlign.Start) { Width = 400, Height = 100 };
        row.Add(new Builds(new AdaptiveNode(new Box(new BoxStyle { Width = 40, Height = 20 }) { AlignSelf = CrossAlign.End })));

        Laid(row).Children[0].Bounds.Y.Should().Be(80);
    }

    /// <summary>An arm's <c>GridSpan</c> takes its tracks, through the component.</summary>
    [Fact]
    public void AnArmAComponentBuilds_SpansItsGridTracks()
    {
        var grid = new Grid([GridTrack.Fixed(100), GridTrack.Fixed(100), GridTrack.Fixed(100)]);
        grid.Add(new Builds(new AdaptiveNode(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }) { GridSpan = 2 })));
        grid.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }));

        var node = Laid(grid);

        node.Children[0].Bounds.Width.Should().Be(200);
        node.Children[1].Bounds.X.Should().Be(200);
    }

    /// <summary>What a component builds that is no contract with its parent stays inside the
    /// component's own node, as it always has.</summary>
    [Fact]
    public void AnOrdinaryChild_KeepsTheComponentsNode()
    {
        var column = new Column(gap: 0);
        var component = new Builds(Tall(10));
        column.Add(component);

        Laid(column).Children[0].Source.Should().BeSameAs(component);
    }

    /// <summary>A stateful Flexible's content keeps its instance from frame to frame: the element is
    /// the one the build holds, so the state stays where it was.</summary>
    [Fact]
    public void AStatefulChildOfAFlexibleAComponentBuilds_KeepsItsInstance()
    {
        var counter = new Counter();
        var row = new Row(gap: 0) { Width = 400, Height = 20 };
        row.Add(new Builds(new Flexible(counter, flex: 1)));
        var host = new PhotonHost(row, PhotonTheme.Instance, ThemeMode.Light, 400, 100);

        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder());

        counter.Builds.Should().BeGreaterThan(0, "the child was built and measured through the Flexible");
    }

    private sealed class Counter : StatefulComponent
    {
        public int Builds { get; private set; }

        public override VisualNode Build(ComponentContext context)
        {
            Builds++;
            return new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 });
        }
    }
}
