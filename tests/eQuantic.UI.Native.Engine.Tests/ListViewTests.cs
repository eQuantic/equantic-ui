using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The recycling list (plan M3): ten thousand rows must cost what a screenful costs. These tests
/// conduct the whole loop — the window materializes, the frame reports where the scroll really is,
/// the window follows — and pin the property that makes the component worth having: the work is a
/// function of the VIEWPORT, never of the COUNT.
/// </summary>
public class ListViewTests
{
    private const float Extent = 40f;
    private const float Viewport = 400f;

    private static VisualNode Row(int index) =>
        new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fixed(Extent) },
            new Text($"Row {index}", TypeRole.BodyM, PhotonTheme.Instance.TextPrimary, maxLines: 1));

    private static PhotonHost Open(int count)
    {
        var list = new ListView(count, Extent, Row) { Height = SizeValue.Fixed(Viewport) };
        // Smooth scroll GLIDES toward a wheel target over many frames — right in the window,
        // nondeterministic in a test. Direct offsets make each frame a pure function of the input.
        var host = new PhotonHost(list, PhotonTheme.Instance, ThemeMode.Light, 600, Viewport)
        {
            SmoothScroll = false,
        };
        host.RenderFrame(new DisplayListBuilder());
        // Second frame: the realized frame reported the true viewport; the window rebuilds against it.
        host.RenderFrame(new DisplayListBuilder(), 16);
        return host;
    }

    private static IReadOnlyList<string> VisibleRows(PhotonHost host) =>
        host.Semantics().Where(s => s.Role == SemanticRole.StaticText).Select(s => s.Label).ToList();

    // ---- the contract ----------------------------------------------------------------------------

    [Fact]
    public void TenThousandRows_MaterializeAScreenful()
    {
        var host = Open(10_000);

        var rows = VisibleRows(host);

        rows.Should().Contain("Row 0");
        // viewport/extent = 10 visible + overscan below (3) + ceiling slack — nowhere near 10 000.
        rows.Count.Should().BeLessThan(20,
            "the whole point: rows outside the window are two spacers, not ten thousand nodes");
    }

    [Fact]
    public void TheWork_IsAFunctionOfTheViewport_NeverTheCount()
    {
        var builderSmall = new DisplayListBuilder();
        Open(100).RenderFrame(builderSmall, 32);
        var builderHuge = new DisplayListBuilder();
        Open(10_000).RenderFrame(builderHuge, 32);

        builderHuge.Build().Count.Should().Be(builderSmall.Build().Count,
            "a list a hundred times longer must emit exactly the same commands — " +
            "only the spacer LENGTHS differ, and a spacer draws nothing");
    }

    [Fact]
    public void TheScrollbar_SeesTheWholeList()
    {
        var host = Open(10_000);

        var region = host.LastFrame!.ScrollRegions.Single();

        // MaxOffset = content − viewport: the spacers ARE the rest of the list to layout.
        region.MaxOffset.Should().BeApproximately(10_000 * Extent - Viewport, 1f);
    }

    [Fact]
    public void Scrolling_MovesTheWindow()
    {
        var host = Open(10_000);

        // Wheel to the middle of the list; the frame reports the offset, the next builds there.
        host.ScrollBy(300, 200, 5_000 * Extent).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.RenderFrame(new DisplayListBuilder(), 64);

        var rows = VisibleRows(host);
        rows.Should().NotContain("Row 0", "the start of the list left the window five thousand rows ago");
        rows.Should().Contain(label => label.StartsWith("Row 50"),
            "the window follows the scroll to where the reader actually is");
        rows.Count.Should().BeLessThan(25, "the window stays a screenful wherever it sits");
    }

    [Fact]
    public void PixelsScroll_WithoutARebuild_UntilTheWindowMoves()
    {
        var host = Open(10_000);
        host.RenderFrame(new DisplayListBuilder(), 80);
        host.NeedsRender.Should().BeFalse("the list at rest schedules nothing");

        // A nudge smaller than one row: pixels move, the window's indices do not.
        host.ScrollBy(300, 200, Extent / 4);
        host.RenderFrame(new DisplayListBuilder(), 96);

        host.NeedsRender.Should().BeFalse(
            "a scroll that moves no row across the margin repaints, but must not invalidate state");
    }

    [Fact]
    public void AnEmptyList_IsJustAnEmptyScroll()
    {
        var host = Open(0);

        VisibleRows(host).Should().BeEmpty();
        host.LastFrame!.ScrollRegions.Single().MaxOffset.Should().Be(0);
    }
}
