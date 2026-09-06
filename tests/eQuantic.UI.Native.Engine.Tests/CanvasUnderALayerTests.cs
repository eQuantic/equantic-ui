using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// THE SHAPE A CONSUMER ACTUALLY WRITES: a Canvas as the first layer of a Stack with chrome painted
/// on top of it — a sunburst under its hub, a bubble field under its labels, a chart under its
/// tooltip. The pointer must reach the CANVAS through the layer above, because the canvas is the
/// only thing that knows what its own pixels mean.
/// </summary>
public class CanvasUnderALayerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static (PhotonHost Host, List<CanvasPointer> Moves, List<CanvasPointer> Presses) Open()
    {
        var moves = new List<CanvasPointer>();
        var presses = new List<CanvasPointer>();
        var stack = new Stack { Width = SizeValue.Fixed(200), Height = SizeValue.Fixed(200) };
        stack.Add(new Canvas(p => p.FillCircle(p.Width / 2, p.Height / 2, 80, Theme.BorderStrong),
            SizeValue.Fill, SizeValue.Fill)
        {
            Label = "Sunburst",
            OnPointerMove = moves.Add,
            OnPointerDown = presses.Add,
        });
        // The hub: painted OVER the middle of the canvas, exactly where the pointer is going.
        stack.Add(new Positioned(new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(60),
            Height = SizeValue.Fixed(60),
            Background = Theme.Surface,
        }, new Text("42", TypeRole.Title, Theme.TextPrimary).Centered()), top: 70, start: 70));

        var host = new PhotonHost(stack, Theme, ThemeMode.Light, 200, 200);
        host.RenderFrame(new DisplayListBuilder());
        return (host, moves, presses);
    }

    [Fact]
    public void ThePointerReachesTheCanvas_ThroughTheLayerPaintedOverIt()
    {
        var (host, moves, presses) = Open();

        // THE PREMISE FIRST: the hub really is painted over the point the pointer is about to visit.
        // Without this the test would pass on an empty stack, proving nothing about layers at all.
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 0);
        builder.Build().Commands.ToArray()
            .Should().Contain(c => c.Kind == DrawCommandKind.FillRRect
                && c.Shape.Rect.Contains(new Point(100, 100))
                && c.Shape.Rect.Width == 60,
                "the hub covers the middle of the canvas");

        // Dead centre: inside the hub's 60x60 box, and inside the canvas underneath it.
        host.PointerMove(100, 100);
        moves.Should().ContainSingle("the canvas is asked wherever the point is inside its box");
        moves[0].X.Should().Be(100);
        moves[0].Y.Should().Be(100);

        host.PressDown(100, 100);
        presses.Should().ContainSingle("a press over the covered middle belongs to the canvas too");
    }

    [Fact]
    public void TheCoordinatesAreTheCanvasesOwn_NotTheWindows()
    {
        var (host, moves, _) = Open();
        host.PointerMove(20, 30);
        moves.Should().ContainSingle();
        moves[0].X.Should().Be(20, "the canvas fills the stack, which starts at the window origin");
        moves[0].Y.Should().Be(30);
    }
}
