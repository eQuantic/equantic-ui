using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Gestures v1 — the pointer pipeline: PointerMove resolves the topmost hover-reactive Box under
/// the pointer (paint order), the next frame applies its Hover diff, and PointerLeave restores.
/// This is the native half of spec S5's declarative hover — the same tree the web serves as a
/// zero-JS :hover rule.
/// </summary>
public class PointerHoverTests
{
    private static readonly ColorToken BaseFill = new(Color.FromRgb(0x11, 0x22, 0x33));
    private static readonly ColorToken HoverFill = new(Color.FromRgb(0xAA, 0xBB, 0xCC));

    private static Box HoverBox(float w = 60, float h = 30) => new(new BoxStyle
    {
        Width = w, Height = h, Background = BaseFill,
        Hover = new StyleDiff { Background = HoverFill },
    });

    private static bool FrameHasHoverFill(PhotonHost host)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        foreach (var command in builder.Build().Commands)
            if (command.Kind == DrawCommandKind.FillRRect
                && command.Paint.Color == HoverFill.Resolve(ThemeMode.Light))
                return true;
        return false;
    }

    [Fact]
    public void PointerMove_HoversTheBoxUnderThePointer_AndLeaveRestores()
    {
        var column = new Column(gap: 10) { Padding = EdgeInsets.All(0) };
        column.Add(HoverBox());               // y 0–30
        column.Add(new Box(new BoxStyle { Width = 60, Height = 30, Background = BaseFill })); // inert
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 200, 200);

        FrameHasHoverFill(host).Should().BeFalse("nothing is hovered at rest");

        host.PointerMove(30, 15);             // inside the hover box
        host.NeedsRender.Should().BeTrue("the hover target changed");
        FrameHasHoverFill(host).Should().BeTrue("the next frame applies the Hover diff");

        host.PointerMove(30, 45);             // over the INERT box — no hover region there
        FrameHasHoverFill(host).Should().BeFalse("moving off clears the hover");

        host.PointerMove(30, 15);
        host.PointerLeave();
        FrameHasHoverFill(host).Should().BeFalse("PointerLeave restores the base fill");
    }

    [Fact]
    public void PointerMove_PicksTheTopmostRegion_InPaintOrder()
    {
        var stack = new Stack();
        stack.Add(HoverBox(80, 80));                                     // bottom
        stack.Add(new Positioned(HoverBox(40, 40), top: 0, start: 0));   // top, overlapping
        var host = new PhotonHost(stack, PhotonTheme.Instance, ThemeMode.Light, 200, 200);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        host.PointerMove(20, 20);              // inside BOTH — topmost (last painted) must win
        host.Hovered.Should().BeSameAs(((Positioned)stack.Children[1]).Child);
    }
}
