using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A wheel notch that TELEPORTS the content reads as a redraw rather than as movement, and the eye
/// loses its place. The offset glides to where the wheel sent it — subtly, and without ever
/// arriving anywhere the wheel did not ask for.
/// </summary>
public class SmoothScrollTests
{
    private sealed class LongPage : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0) { Width = SizeValue.Fill };
            for (var i = 0; i < 40; i++)
                column.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 40 }));
            return new ScrollView(column) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        }
    }

    private static PhotonHost Mount(bool smooth = true)
    {
        var host = new PhotonHost(new LongPage(), PhotonTheme.Instance, ThemeMode.Light, 300, 200)
        {
            SmoothScroll = smooth,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 0);
        return host;
    }

    private static float ContentTop(PhotonHost host, float timeMs) =>
        host.RenderFrame(new DisplayListBuilder(), timeMs).Root.Children[0].Children[0].Bounds.Y;

    [Fact]
    public void AWheelNotch_GlidesInsteadOfJumping()
    {
        var host = Mount();
        host.ScrollBy(150, 100, 200);

        var afterOneFrame = ContentTop(host, 16);
        afterOneFrame.Should().BeLessThan(0, "it started moving");
        afterOneFrame.Should().BeGreaterThan(-200, "…but did not arrive in one frame");
    }

    [Fact]
    public void ItArrivesExactlyWhereTheWheelSentIt()
    {
        var host = Mount();
        host.ScrollBy(150, 100, 200);

        for (var t = 16f; t < 4000; t += 16) ContentTop(host, t);
        ContentTop(host, 4016).Should().Be(-200, "a glide is a path, not a destination of its own");
    }

    [Fact]
    public void NotchesThatARRIVEMidGlide_Accumulate()
    {
        var host = Mount();
        host.ScrollBy(150, 100, 100);
        ContentTop(host, 16);
        host.ScrollBy(150, 100, 100);   // a second notch before the first settled

        for (var t = 32f; t < 4000; t += 16) ContentTop(host, t);
        ContentTop(host, 4016).Should().Be(-200, "both notches counted, neither restarted the other");
    }

    [Fact]
    public void TurnedOff_ItLandsAtOnce()
    {
        var host = Mount(smooth: false);
        host.ScrollBy(150, 100, 200);
        ContentTop(host, 16).Should().Be(-200);
    }

    [Fact]
    public void ReduceMotion_TurnsItOffWhateverTheAppAsked()
    {
        var host = Mount();
        host.ReducedMotion = true;
        host.ScrollBy(150, 100, 200);
        ContentTop(host, 16).Should().Be(-200, "smooth scrolling IS movement");
    }

    [Fact]
    public void AStillPage_AsksForNoFrames()
    {
        var host = Mount();
        host.ScrollBy(150, 100, 200);
        for (var t = 16f; t < 4000; t += 16) ContentTop(host, t);

        host.RenderFrame(new DisplayListBuilder(), 4016);
        host.NeedsRender.Should().BeFalse("nothing is moving any more");
    }
}
