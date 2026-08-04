using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A finger scrolls. It sounds too obvious to test — which is exactly why nobody had: only the
/// macOS wheel ever reached the scroll store, so on a phone every list simply did not move, and
/// none of the goldens or the press sweep could tell (a still list renders perfectly).
/// </summary>
public class PanToScrollTests
{
    private sealed class LongList : Primitives.StatefulComponent
    {
        public readonly List<string> Fired = [];

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0) { Width = SizeValue.Fill };
            for (var i = 0; i < 40; i++)
            {
                var index = i;
                column.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 40 },
                    new Pressable(new Text($"row {index}", TypeRole.BodyM),
                        () => Fired.Add($"row {index}"))));
            }
            return new ScrollView(column) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        }
    }

    private static (LongList Page, PhotonHost Host) Mount(bool smooth = true)
    {
        var page = new LongList();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 300, 200)
        {
            SmoothScroll = smooth,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 0);
        return (page, host);
    }

    private static float ContentTop(PhotonHost host, float timeMs) =>
        host.RenderFrame(new DisplayListBuilder(), timeMs).Root.Children[0].Children[0].Bounds.Y;

    [Fact]
    public void AFingerDraggingUp_RevealsWhatIsBelow()
    {
        var (_, host) = Mount(smooth: false);

        host.PressDown(150, 150);
        host.PointerMove(150, 100);
        host.PointerMove(150, 50);
        host.PressUp(150, 50);

        ContentTop(host, 16).Should().Be(-100, "the content moves opposite the finger");
    }

    [Fact]
    public void ATapInsideAList_IsStillATap()
    {
        var (page, host) = Mount(smooth: false);

        // x=20 is ON the row's label, which is what the Pressable wraps.
        host.PressDown(20, 100);
        host.PointerMove(20, 98);       // no hand is perfectly still
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.PressUp(20, 98);

        page.Fired.Should().NotBeEmpty("two pixels is a tap, not a scroll");
        ContentTop(host, 32).Should().Be(0);
    }

    [Fact]
    public void OnceItSCROLLS_ThePressIsOff()
    {
        var (page, host) = Mount(smooth: false);

        host.PressDown(20, 150);
        host.PointerMove(20, 80);       // well past the slop
        host.PressUp(20, 80);

        page.Fired.Should().BeEmpty("a scroll is not a tap, however it started");
    }

    [Fact]
    public void ASidewaysSwipe_LeavesAVerticalListAlone()
    {
        var (_, host) = Mount(smooth: false);

        host.PressDown(150, 150);
        host.PointerMove(40, 150);
        host.PressUp(40, 150);

        ContentTop(host, 16).Should().Be(0);
    }

    [Fact]
    public void AFlick_CarriesOnAfterTheFingerLifts()
    {
        var (_, host) = Mount();

        host.PressDown(150, 180);
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.PointerMove(150, 100);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.PointerMove(150, 40);
        host.PressUp(150, 40);

        var atRelease = ContentTop(host, 48);
        for (var t = 64f; t < 4000; t += 16) ContentTop(host, t);

        ContentTop(host, 4016).Should().BeLessThan(atRelease, "the flick kept going");
    }

    [Fact]
    public void ItNeverGoesPastTheEnd()
    {
        var (_, host) = Mount(smooth: false);

        host.PressDown(150, 190);
        host.PointerMove(150, -5000);
        host.PressUp(150, -5000);

        // 40 rows of 40 in a 200-tall viewport: 1600 - 200 = 1400 of travel, and not one dp more.
        ContentTop(host, 16).Should().Be(-1400);
    }
}
