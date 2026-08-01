using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec S5's PROGRAMMABLE hover on Photon — <see cref="Hoverable"/> rides the same pointer
/// pipeline the declarative Style.Hover uses: PointerMove fires OnChanged(true) on entry,
/// OnChanged(false) on exit, PointerLeave counts as an exit, and moving between two hoverables
/// fires leave-before-enter. The web twin attaches mouseenter/mouseleave to the wrapper div.
/// </summary>
public class S5HoverableNativeTests
{
    [Fact]
    public void PointerMove_FiresEnterAndLeave()
    {
        var log = new List<string>();
        var column = new Column(gap: 10);
        column.Add(new Hoverable(
            new Box(new BoxStyle { Width = 60, Height = 30 }),
            entered => log.Add(entered ? "enter" : "leave")));
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 200, 200);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        host.PointerMove(30, 15);   // inside
        host.PointerMove(30, 15);   // still inside — no re-fire
        host.PointerMove(30, 100);  // outside
        host.PointerMove(30, 15);   // back in
        host.PointerLeave();        // window exit counts as leave

        log.Should().Equal("enter", "leave", "enter", "leave");
    }

    [Fact]
    public void MovingBetweenHoverables_FiresLeaveBeforeEnter()
    {
        var log = new List<string>();
        var column = new Column(gap: 10);
        column.Add(new Hoverable(new Box(new BoxStyle { Width = 60, Height = 30 }), e => log.Add($"a:{e}")));
        column.Add(new Hoverable(new Box(new BoxStyle { Width = 60, Height = 30 }), e => log.Add($"b:{e}")));
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 200, 200);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);

        host.PointerMove(30, 15);   // into A (y 0–30)
        host.PointerMove(30, 55);   // into B (y 40–70)

        log.Should().Equal("a:True", "a:False", "b:True");
    }
}
