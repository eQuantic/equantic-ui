using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Scroll compositor v1: the host routes wheel input to the topmost viewport, offsets clamp to the
/// measured extent, and a Sticky child PINS at the viewport start while the content scrolls past —
/// the native twin of CSS position:sticky.
/// </summary>
public class ScrollCompositorTests
{
    private static PhotonHost Host(out ScrollView scroll)
    {
        // Content: sticky header (h24) at y=100, inside a 500-tall column; viewport 200.
        var content = new Column(gap: 0) { Width = SizeValue.Fill };
        content.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 100 }));
        content.Add(new Sticky(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 24 })));
        content.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 376 }));
        scroll = new ScrollView(content) { Width = SizeValue.Fill, Height = 200 };
        // The MECHANICS, not the glide: these pin where a wheel puts the content and where the
        // extent stops it. The gliding is its own feature, with its own tests.
        return new PhotonHost(scroll, PhotonTheme.Instance, ThemeMode.Light, 300, 200)
        {
            SmoothScroll = false,
        };
    }

    private static LayoutNode StickyNode(RealizeResult frame)
    {
        // scroll → content column → children[1] is the Sticky wrapper.
        return frame.Root.Children[0].Children[1];
    }

    [Fact]
    public void ScrollBy_MovesTheContent_AndClampsAtTheExtent()
    {
        var host = Host(out _);
        var first = host.RenderFrame(new DisplayListBuilder());
        first.ScrollRegions.Should().HaveCount(1);
        first.Root.Children[0].Bounds.Y.Should().Be(0);

        host.ScrollBy(150, 100, 120).Should().BeTrue();
        var scrolled = host.RenderFrame(new DisplayListBuilder());
        scrolled.Root.Children[0].Bounds.Y.Should().Be(-120, "the content offsets by the wheel delta");

        host.ScrollBy(150, 100, 10_000).Should().BeTrue();
        var clamped = host.RenderFrame(new DisplayListBuilder());
        clamped.Root.Children[0].Bounds.Y.Should().Be(-(500 - 200), "offset clamps to content - viewport");

        host.ScrollBy(150, 100, 50).Should().BeFalse("already at the end — nothing changes");
        host.ScrollBy(999, 999, 50).Should().BeFalse("outside every scroll region");
    }

    [Fact]
    public void Sticky_PinsAtTheViewportStart_WhileContentScrollsPast()
    {
        var host = Host(out _);

        var rest = host.RenderFrame(new DisplayListBuilder());
        StickyNode(rest).Bounds.Y.Should().Be(100, "in flow before any scrolling");

        host.ScrollBy(150, 100, 60);   // header still visible (100 - 60 = 40 from the top)
        var partial = host.RenderFrame(new DisplayListBuilder());
        StickyNode(partial).Bounds.Y.Should().Be(40, "still in flow (absolute: 100 - offset 60)");

        host.ScrollBy(150, 100, 120);  // offset 180 > y0 100 → pinned
        var pinned = host.RenderFrame(new DisplayListBuilder());
        StickyNode(pinned).Bounds.Y.Should().Be(0, "pinned at the viewport start (absolute coords)");
    }
}
