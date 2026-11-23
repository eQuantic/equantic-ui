using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The arm Photon shows keeps what its components hold, through the host that draws it (#797, #1783): the
/// element tree holds the arm at its own slot, and the layout measures and paints the arm with the element
/// at that slot. Measured with the element at another slot, a stateful arm was built anew every frame and
/// drew its defaults.
/// </summary>
public class AdaptiveArmStateTests
{
    /// <summary>A counter whose state shows as a bar that grows 24 dp per press.</summary>
    private sealed class Counter : StatefulComponent
    {
        private int _count;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new Box(new BoxStyle
            {
                Width = 24 + _count * 24,
                Height = 10,
                CornerRadius = new CornerRadii(Radius.Full),
            }));
            column.Add(new Button("Press", onPressed: () => SetState(() => _count++)));
            return column;
        }
    }

    private static float BarWidth(LayoutNode node)
    {
        if (node.Source is Box { Style.CornerRadius.TopLeft: Radius.Full }) return node.Bounds.Width;
        foreach (var child in node.Children)
        {
            var found = BarWidth(child);
            if (found > 0) return found;
        }

        return 0;
    }

    private static RealizeResult Press(PhotonHost host)
    {
        var frame = host.RenderFrame(new DisplayListBuilder());
        var hit = frame.HitRegions.Single().Bounds;
        host.Tap(hit.Center.X, hit.Center.Y).Should().BeTrue();
        return host.RenderFrame(new DisplayListBuilder());
    }

    [Fact]
    public void TheExpandedArm_KeepsItsState_FromFrameToFrame()
    {
        var host = new PhotonHost(new AdaptiveNode(new Text("narrow", TypeRole.BodyM), expanded: new Counter()),
            PhotonTheme.Instance, ThemeMode.Light, 1000, 400);

        var frame = Press(host);

        BarWidth(frame.Root).Should().Be(48, "the press the expanded arm's counter heard is drawn");
    }

    /// <summary>An arm a line lays out in the node's place (a column's child), measured with the element at
    /// the arm's slot as the node's own measurement is.</summary>
    [Fact]
    public void TheExpandedArm_InALine_KeepsItsState_FromFrameToFrame()
    {
        var column = new Column(gap: Space.S2);
        column.Add(new Text("header", TypeRole.Label));
        column.Add(new AdaptiveNode(new Text("narrow", TypeRole.BodyM), expanded: new Counter()));
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 1000, 400);

        var frame = Press(host);

        BarWidth(frame.Root).Should().Be(48);
    }

    /// <summary>The drawer template's shape, through a host the window resizes: a keyed screen alone on a
    /// phone, beside a rail on a wide window.</summary>
    private sealed class Shell : StatelessComponent
    {
        public GlobalKey Screen { get; } = new("screen");

        public override VisualNode Build(ComponentContext context)
        {
            var compact = new Column();
            compact.Add(new Counter { GlobalKey = Screen });
            var docked = new Row(gap: Space.S3);
            docked.Add(new Text("rail", TypeRole.Label));
            docked.Add(new Counter { GlobalKey = Screen });
            return new AdaptiveNode(compact, expanded: docked);
        }
    }

    [Fact]
    public void AKeyedScreen_KeepsItsState_WhenTheWindowIsResizedAcrossTheThreshold()
    {
        var host = new PhotonHost(new Shell(), PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        Press(host);

        host.Resize(1000, 400);
        BarWidth(host.RenderFrame(new DisplayListBuilder()).Root).Should().Be(48, "the screen crossed into the row");

        host.Resize(400, 400);
        BarWidth(host.RenderFrame(new DisplayListBuilder()).Root).Should().Be(48, "and back into the column");
    }
}
