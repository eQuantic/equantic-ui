using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A transformed box takes the pointer where it is DRAWN (#513), as CSS hit-tests a transformed
/// element and Flutter's <c>Transform</c> does. Photon drew the box transformed and kept every region
/// it registered at its layout rect: a box laid out at the top and drawn 20 lower was pressed and
/// hovered over the strip it had left, and not over itself. A translation, a scale and a quarter turn
/// keep a box a box; a rotation tilts it, and a point is tested against the tilted shape, not the box
/// around its corners.
/// </summary>
public class TransformedHitTests
{
    private static readonly ColorToken BaseFill = new(Color.FromRgb(0x11, 0x22, 0x33));
    private static readonly ColorToken HoverFill = new(Color.FromRgb(0xAA, 0xBB, 0xCC));

    /// <summary>A 40-square that says when it is pressed and fills on hover, drawn under
    /// <paramref name="transform"/>, laid out <paramref name="inset"/> from the surface's corner.</summary>
    private static (PhotonHost Host, List<string> Pressed) Mount(Transform2D transform, float inset)
    {
        var pressed = new List<string>();
        var square = new Pressable(new Box(new BoxStyle
        {
            Width = 40,
            Height = 40,
            Background = BaseFill,
            Transform = transform,
            Hover = new StyleDiff { Background = HoverFill },
        }), () => pressed.Add("square"));
        var page = new Box(new BoxStyle { Padding = EdgeInsets.All(inset) }, square);
        // A pointer's density, where a 40-square keeps its own box as its target.
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 100, 100) { Density = Density.Compact };
        host.RenderFrame(new DisplayListBuilder());
        return (host, pressed);
    }

    private static bool Hovered(PhotonHost host, float x, float y)
    {
        host.PointerMove(x, y);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        foreach (var command in builder.Build().Commands)
        {
            if (command.Kind == DrawCommandKind.FillRRect && command.Paint.Color == HoverFill.Resolve(ThemeMode.Light))
                return true;
        }
        return false;
    }

    [Fact]
    public void ATranslatedBox_TakesThePointerWhereItIsDrawn_AndNotWhereItWasLaidOut()
    {
        // Laid out at y 0 to 40, drawn at 20 to 60.
        var (host, pressed) = Mount(Transform2D.Translate(0, 20), inset: 0);

        host.Tap(20, 50);
        pressed.Should().Equal(["square"], "y 50 is inside the square as drawn");
        pressed.Clear();
        host.Tap(20, 10);
        pressed.Should().BeEmpty("y 10 is inside the rect it was laid out at, and nothing is drawn there");

        Hovered(host, 20, 50).Should().BeTrue("the pointer over the drawn square hovers it");
        Hovered(host, 20, 10).Should().BeFalse("and over the strip it left, nothing");
    }

    /// <summary>
    /// The case the issue was filed for: a pressable over a box whose HOVER moves it. The box's own
    /// hover region followed it, and the pressable's target was placed with the box's resting
    /// transform, read through the pressable's node, which no hover region carries (found reviewing
    /// the change).
    /// </summary>
    [Fact]
    public void APressableOverABoxItsHoverMoves_TakesThePressWhereTheBoxIsDrawn()
    {
        var pressed = new List<string>();
        var square = new Pressable(new Box(new BoxStyle
        {
            Width = 40,
            Height = 40,
            Background = BaseFill,
            Hover = new StyleDiff { Transform = Transform2D.Translate(0, 20) },
        }), () => pressed.Add("square"));
        var host = new PhotonHost(square, PhotonTheme.Instance, ThemeMode.Light, 100, 100) { Density = Density.Compact };
        host.RenderFrame(new DisplayListBuilder());

        // Over the square at rest, from 0 to 40: the hover draws it from 20 to 60.
        host.PointerMove(20, 30);
        host.RenderFrame(new DisplayListBuilder());
        host.Tap(20, 50);

        pressed.Should().Equal(["square"], "y 50 is inside the square as its hover draws it");
    }

    [Fact]
    public void AScaledBox_TakesThePointerAcrossAllItDraws()
    {
        // Laid out at 30 to 70, drawn twice as large about its centre: 10 to 90.
        var (host, pressed) = Mount(Transform2D.Scale(2), inset: 30);

        host.Tap(15, 50);
        host.Tap(85, 50);

        pressed.Should().Equal(["square", "square"], "both points are inside the square as drawn, outside its layout");
    }

    [Fact]
    public void ARotatedBox_TakesThePointerInItsShape_AndNotInTheCornersOfTheBoxAroundIt()
    {
        // Laid out at 30 to 70 and turned 45° about its centre (50, 50): a diamond whose corners reach
        // 28.3 from the centre along each axis.
        var (host, pressed) = Mount(Transform2D.Rotate(45), inset: 30);

        host.Tap(50, 23);
        pressed.Should().Equal(["square"], "27 above the centre is inside the diamond, above the layout rect");
        pressed.Clear();
        host.Tap(25, 25);
        pressed.Should().BeEmpty("the corner of the box around the diamond is not the diamond");
    }

    /// <summary>
    /// A transform that collapses a box draws nothing, and nothing there takes the pointer (Copilot
    /// on #690). Squashed to no width and turned 45°, the square is a diagonal the renderer skips,
    /// since it cannot invert the transform, and the 28dp box around that diagonal took the presses
    /// and the hover.
    /// </summary>
    [Fact]
    public void ABoxCollapsedOntoALine_TakesNoPointer()
    {
        // Laid out at 30 to 70, squashed flat and turned about its centre: a diagonal through
        // (50, 50) whose box runs from 35.9 to 64.1 on each axis.
        var (host, pressed) = Mount(Transform2D.Scale(0, 1).WithRotate(45), inset: 30);

        host.Tap(50, 50);
        host.Tap(44, 52);
        pressed.Should().BeEmpty("nothing of the square is drawn, on the diagonal or beside it");
        Hovered(host, 44, 52).Should().BeFalse("and nothing is hovered");
    }
}
