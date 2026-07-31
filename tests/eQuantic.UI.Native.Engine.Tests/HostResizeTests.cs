using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>W5 resize: the host adopts a new viewport WITHOUT being recreated — the next frame
/// lays out against the new size and interaction state machinery survives intact.</summary>
public class HostResizeTests
{
    [Fact]
    public void Resize_AdoptsTheViewport_AndKeepsInteractionAlive()
    {
        var presses = 0;
        var root = new Column(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        root.Add(new Button("Go", onPressed: () => presses++) { Expand = true });

        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 360, 400);
        var before = host.RenderFrame(new DisplayListBuilder());
        before.Root.Bounds.Width.Should().Be(360);
        var narrow = before.HitRegions[0].Bounds.Width;

        host.Resize(600, 300);
        host.NeedsRender.Should().BeTrue("a new viewport must repaint");
        host.Width.Should().Be(600);

        var after = host.RenderFrame(new DisplayListBuilder());
        after.Root.Bounds.Width.Should().Be(600, "layout adopts the new viewport");
        after.Root.Bounds.Height.Should().Be(300);
        after.HitRegions[0].Bounds.Width.Should().BeGreaterThan(narrow, "the Fill button re-lays out wider");

        host.PressDown(after.HitRegions[0].Bounds.Center.X, after.HitRegions[0].Bounds.Center.Y);
        host.PressUp(after.HitRegions[0].Bounds.Center.X, after.HitRegions[0].Bounds.Center.Y);
        presses.Should().Be(1, "the SAME host keeps dispatching after resize");
    }

    [Fact]
    public void Resize_ToTheSameSize_IsANoop()
    {
        var host = new PhotonHost(new Box(), PhotonTheme.Instance, ThemeMode.Light, 360, 400);
        host.RenderFrame(new DisplayListBuilder());
        host.NeedsRender.Should().BeFalse();
        host.Resize(360, 400);
        host.NeedsRender.Should().BeFalse("same size must not schedule a frame");
    }
}
