using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec B14 on Photon — the value-transition animator: an AnimateChanges flex weight GLIDES over
/// Motion.Base when its target changes (pure function of the frame clock, like loop motion);
/// mounts, reduced motion and untracked values snap. The web twin is `transition: flex-grow`.
/// </summary>
public class B14TransitionTests
{
    private static Row Bar(int filled, bool animate = true)
    {
        var track = new Row(gap: 0) { Width = SizeValue.Fill, Height = 8 };
        track.Add(new Flexible(new Box(new BoxStyle
        {
            Height = 8, Background = new ColorToken(Color.FromRgb(0x00, 0x50, 0xA0)),
        }), filled) { AnimateChanges = animate });
        // B14: the counterweight animates WITH the fill (constant denominator → the ratio glides).
        track.Add(new Spacer(1000 - filled) { AnimateChanges = animate });
        return track;
    }

    private static float FillWidth(VisualNode root, TransitionStore store, float timeMs)
    {
        var builder = new DisplayListBuilder();
        var result = PhotonRealizer.Realize(root, 1000, 100, PhotonTheme.Instance, ThemeMode.Light,
            builder, timeMs: timeMs, transitions: store);
        return result.Root.Children[0].Bounds.Width;
    }

    [Fact]
    public void ValueChange_GlidesOverBase_AndSettles()
    {
        var store = new TransitionStore();

        FillWidth(Bar(300), store, timeMs: 0).Should().Be(300, "mounts land AT the target — no entrance glide");

        // The value changes 300 → 800: the next frames interpolate over Motion.Base (200ms).
        FillWidth(Bar(800), store, timeMs: 1000).Should().Be(300, "the change starts from the shown value");
        var mid = FillWidth(Bar(800), store, timeMs: 1100);
        mid.Should().BeGreaterThan(400).And.BeLessThan(700, "half the duration sits mid-glide (eased)");
        FillWidth(Bar(800), store, timeMs: 1200).Should().Be(800, "Base elapsed — settled at the target");
        FillWidth(Bar(800), store, timeMs: 1300).Should().Be(800, "and stays settled");
    }

    [Fact]
    public void MidFlightRetarget_ContinuesFromTheCurrentValue()
    {
        var store = new TransitionStore();
        FillWidth(Bar(300), store, 0);
        FillWidth(Bar(800), store, 1000);
        var mid = FillWidth(Bar(800), store, 1100);

        // Retarget mid-flight: the new glide starts where the old one visually is.
        var atRetarget = FillWidth(Bar(400), store, 1100);
        atRetarget.Should().BeApproximately(mid, 1f, "no jump on retarget");
    }

    [Fact]
    public void ReducedMotion_AndUnanimatedChanges_Snap()
    {
        var reduced = new TransitionStore();
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(Bar(300), 1000, 100, PhotonTheme.Instance, ThemeMode.Light,
            builder, timeMs: 0, reducedMotion: true, transitions: reduced);
        var result = PhotonRealizer.Realize(Bar(800), 1000, 100, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder(), timeMs: 50, reducedMotion: true, transitions: reduced);
        result.Root.Children[0].Bounds.Width.Should().Be(800, "Reduce Motion snaps");
        result.HasActiveMotion.Should().BeFalse();

        var store = new TransitionStore();
        FillWidth(Bar(300, animate: false), store, 0);
        FillWidth(Bar(800, animate: false), store, 50).Should().Be(800, "AnimateChanges=false snaps (regressions)");
    }

    [Fact]
    public void ActiveTransition_KeepsTheHostScheduling()
    {
        var store = new TransitionStore();
        FillWidth(Bar(300), store, 0);
        var builder = new DisplayListBuilder();
        var mid = PhotonRealizer.Realize(Bar(800), 1000, 100, PhotonTheme.Instance, ThemeMode.Light,
            builder, timeMs: 100, transitions: store);
        mid.HasActiveMotion.Should().BeTrue("a glide is running — the host must keep scheduling frames");

        var settled = PhotonRealizer.Realize(Bar(800), 1000, 100, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder(), timeMs: 400, transitions: store);
        settled.HasActiveMotion.Should().BeFalse("settled — frames stop");
    }
}
