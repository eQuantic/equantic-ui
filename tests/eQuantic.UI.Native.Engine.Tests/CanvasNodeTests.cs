using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// W3: a component draws its own pixels inside a box the layout gives it, and the pointer arrives
/// in the canvas's OWN coordinates — which is what makes polar hit-testing (a sunburst) or
/// per-particle picking (a simulation) the app's arithmetic rather than the engine's.
/// </summary>
public class CanvasNodeTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;
    private static readonly ColorToken Ink = new(new Color(10, 20, 30, 255));

    private static (PhotonHost Host, DrawCommand[] Commands) Render(VisualNode root,
        float width = 200, float height = 120, float timeMs = 0)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, width, height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        return (host, builder.Build().Commands.ToArray());
    }

    [Fact]
    public void TheCanvasFillsTheBoxItIsOffered_AndDrawsInItsOwnCoordinates()
    {
        // Inside a padded box, so the canvas's (0,0) is demonstrably NOT the window's.
        var canvas = new Canvas(painter =>
        {
            painter.Width.Should().Be(160, "the offered box, minus the padding");
            painter.FillRect(0, 0, 10, 10, Ink);
        });
        var (_, commands) = Render(new Box(new BoxStyle
        {
            Width = SizeValue.Fill, Height = SizeValue.Fill, Padding = EdgeInsets.All(20),
        }, canvas));

        var drawn = commands.Last(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == Ink.Light);
        drawn.Shape.Rect.X.Should().Be(20, "canvas (0,0) is the canvas's corner, not the window's");
        drawn.Shape.Rect.Y.Should().Be(20);
        drawn.Shape.Rect.Width.Should().Be(10);
    }

    [Fact]
    public void AFixedSizeIsHonoured()
    {
        float? seen = null;
        var canvas = new Canvas(p => seen = p.Width, width: SizeValue.Fixed(64), height: SizeValue.Fixed(32));
        Render(canvas);
        seen.Should().Be(64);
    }

    [Fact]
    public void EveryShapeReachesTheEngineAsItsOwnCommand()
    {
        // The painter is not an intermediate representation: a FillCircle here IS the engine's.
        var (_, commands) = Render(new Canvas(p =>
        {
            p.FillCircle(50, 50, 20, Ink);
            p.FillAnnularSector(50, 50, 10, 30, 0, MathF.PI / 2, Ink);
            p.StrokeRect(0, 0, 40, 40, Ink, 2);
            p.Line(0, 0, 30, 40, Ink, 3);
        }));

        commands.Should().Contain(c => c.Kind == DrawCommandKind.FillAnnularSector);
        commands.Should().Contain(c => c.Kind == DrawCommandKind.StrokeRRect);
        commands.Count(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == Ink.Light)
            .Should().BeGreaterThanOrEqualTo(2, "the circle and the line are both filled shapes");
    }

    [Fact]
    public void PointerEventsArriveInTheCanvasOwnCoordinates()
    {
        var seen = new List<CanvasPointer>();
        var canvas = new Canvas(_ => { })
        {
            OnPointerDown = seen.Add,
            OnPointerMove = seen.Add,
            OnPointerUp = seen.Add,
        };
        var (host, _) = Render(new Box(new BoxStyle
        {
            Width = SizeValue.Fill, Height = SizeValue.Fill, Padding = EdgeInsets.All(20),
        }, canvas));

        host.PressDown(70, 60);
        host.PointerMove(90, 80);
        host.PressUp(90, 80);

        seen.Should().HaveCount(3);
        seen[0].X.Should().Be(50, "70 in the window is 50 in a canvas that starts at 20");
        seen[0].Y.Should().Be(40);
        seen[0].Pressed.Should().BeTrue();
        seen[1].Pressed.Should().BeTrue("a move during a press is a drag");
        seen[2].Pressed.Should().BeFalse("the release is not pressed any more");
    }

    [Fact]
    public void ADragThatLeavesTheBox_StillBelongsToTheCanvas()
    {
        var moves = new List<CanvasPointer>();
        var ups = 0;
        var canvas = new Canvas(_ => { })
        {
            OnPointerMove = moves.Add,
            OnPointerUp = _ => ups++,
            Width = SizeValue.Fixed(50), Height = SizeValue.Fixed(50),
        };
        var (host, _) = Render(canvas, 200, 200);

        host.PressDown(10, 10);
        host.PointerMove(120, 130);      // well outside the 50×50 box
        host.PressUp(120, 130);

        moves.Should().ContainSingle();
        moves[0].X.Should().Be(120, "a drag that left still has a position, and it is still local");
        ups.Should().Be(1, "the canvas that took the press gets the release");
    }

    [Fact]
    public void LeavingTheCanvasIsAnnouncedOnce()
    {
        var leaves = 0;
        var canvas = new Canvas(_ => { })
        {
            OnPointerMove = _ => { },
            OnPointerLeave = () => leaves++,
            Width = SizeValue.Fixed(50), Height = SizeValue.Fixed(50),
        };
        var (host, _) = Render(canvas, 200, 200);

        host.PointerMove(10, 10);
        host.PointerMove(20, 20);
        leaves.Should().Be(0, "still inside");

        host.PointerMove(150, 150);
        leaves.Should().Be(1, "left the box");
        host.PointerMove(160, 160);
        leaves.Should().Be(1, "and leaving is not announced again");
    }

    [Fact]
    public void PolarHitTestingIsTheAppsArithmetic()
    {
        // The engine hands over a point; the app decides what is under it. This is the sunburst's
        // whole hit test, and the reason the pointer carries these two helpers.
        var pointer = new CanvasPointer(100, 50, Pressed: true, KeyModifiers.None);

        pointer.DistanceFrom(50, 50).Should().BeApproximately(50, 1e-4f);
        pointer.AngleFrom(50, 50).Should().BeApproximately(0, 1e-4f, "due east is zero radians");
        new CanvasPointer(50, 100, true, KeyModifiers.None).AngleFrom(50, 50)
            .Should().BeApproximately(MathF.PI / 2, 1e-4f, "clockwise from three o'clock, like the sector");
    }

    [Fact]
    public void ACanvasWithNoHandlers_TakesNoPointer()
    {
        // Pure ornament must not swallow the press that belongs to what is under it.
        var pressed = false;
        var canvas = new Canvas(_ => { }) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        var stack = new Stack(Alignment.TopStart);
        stack.Add(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }),
            () => pressed = true));
        stack.Add(canvas);

        var (host, _) = Render(stack);
        host.PressDown(50, 50);
        host.PressUp(50, 50);

        pressed.Should().BeTrue("a decorative canvas is not in the way");
    }
}

/// <summary>
/// The same degenerate inputs the web pins, asserted on the ENGINE — so the two targets are pinned
/// against one rule rather than against each other's implementations.
/// </summary>
public class CanvasDegenerateSectorTests
{
    private static readonly ColorToken Ink = new(new Color(10, 20, 30, 255));

    private static int Sectors(Action<ICanvasPainter> draw)
    {
        var host = new PhotonHost(new Primitives.Canvas(draw), PhotonTheme.Instance, ThemeMode.Light, 200, 200);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 0);
        return builder.Build().Commands.ToArray().Count(c => c.Kind == DrawCommandKind.FillAnnularSector);
    }

    [Theory]
    [InlineData(0f, 0f, 0f, 1f)]
    [InlineData(5f, 20f, 1f, 1f)]
    [InlineData(5f, 20f, 1f, 0.5f)]
    [InlineData(20f, 20f, 0f, 1f)]
    [InlineData(30f, 20f, 0f, 1f)]
    public void DegenerateSectorsDrawNothing(float inner, float outer, float start, float end)
    {
        Sectors(p => p.FillAnnularSector(50, 50, inner, outer, start, end, Ink)).Should().Be(0);
    }

    [Fact]
    public void AZeroInnerRadiusIsAPieSlice_AndDraws()
    {
        Sectors(p => p.FillAnnularSector(50, 50, 0, 20, 0, 1, Ink)).Should().Be(1);
    }

    [Fact]
    public void AFullRingIsOneCommand_WhereTheWebNeedsTwo()
    {
        // The one place the targets differ in SPELLING: SVG has no ring primitive and an arc whose
        // ends coincide draws nothing, so the web splits. The engine has no such trouble.
        Sectors(p => p.FillAnnularSector(50, 50, 10, 20, 0, MathF.Tau, Ink)).Should().Be(1);
    }
}

public class CanvasLeaveRepaintTests
{
    [Fact]
    public void LeavingACanvasForEmptySpace_AsksForAFrame()
    {
        // A canvas that clears its hover highlight on leave must be repainted to show it gone —
        // and the leave-to-nothing path fell past the branch that asked, so the highlight stayed
        // until something else happened to need a frame.
        var canvas = new Primitives.Canvas(_ => { })
        {
            OnPointerMove = _ => { },
            OnPointerLeave = () => { },
            Width = SizeValue.Fixed(50),
            Height = SizeValue.Fixed(50),
        };
        var host = new PhotonHost(canvas, PhotonTheme.Instance, ThemeMode.Light, 200, 200);
        host.RenderFrame(new DisplayListBuilder(), 0);

        host.PointerMove(10, 10);
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.NeedsRender.Should().BeFalse("settled while inside");

        host.PointerMove(150, 150);
        host.NeedsRender.Should().BeTrue("the leave owes a frame");
    }
}
