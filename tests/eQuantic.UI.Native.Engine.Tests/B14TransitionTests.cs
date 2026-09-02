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
        // Half the duration is NOT half the travel: the standard curve front-loads, so at 50% of the
        // time the value is ~88% of the way — exactly where CSS puts it for cubic-bezier(.2,0,0,1).
        // (The smoothstep that stood in here before the curve evaluator existed said 50%.)
        var mid = FillWidth(Bar(800), store, timeMs: 1100);
        mid.Should().BeGreaterThan(300).And.BeLessThan(800, "mid-glide is strictly between the ends");
        mid.Should().BeApproximately(300 + 500 * Curve.Standard.Ease(0.5f), 0.01f,
            "the glide follows the token's own curve, not a stand-in");
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

/// <summary>
/// Spec S6 on Photon: a transition moves under ITS OWN spec — the author's duration, delay and
/// curve, per node — rather than one constant for every animation on the target. This is what lets
/// the same <c>Transition = TransitionSpec.Of(…)</c> mean the same motion here and in a browser.
/// </summary>
public class S6TransitionStoreTests
{
    [Fact]
    public void ATrackFollowsItsSpecsDurationAndCurve()
    {
        var store = new TransitionStore();
        var press = TransitionSpec.Of(StyleChannels.Colors, Motion.Press);   // 100 ms, standard

        store.Resolve("p", 0f, 0, press, reducedMotion: false).Should().Be(0, "mounts at the target");
        store.Resolve("p", 1f, 1000, press, reducedMotion: false).Should().Be(0, "a change starts where it was");
        store.Resolve("p", 1f, 1050, press, reducedMotion: false)
            .Should().BeApproximately(Curve.Standard.Ease(0.5f), 1e-4f, "halfway in TIME, on the curve");
        store.Resolve("p", 1f, 1100, press, reducedMotion: false).Should().Be(1, "100 ms elapsed — settled");
    }

    [Fact]
    public void ADelayHoldsTheOldValue_ThenTheGlideBegins()
    {
        var store = new TransitionStore();
        var spec = new TransitionSpec(StyleChannels.Opacity, DurationMs: 100, DelayMs: 50);

        store.Resolve("d", 0f, 0, spec, false);
        store.Resolve("d", 1f, 1000, spec, false).Should().Be(0);
        store.Resolve("d", 1f, 1040, spec, false).Should().Be(0, "still inside the delay");
        store.Resolve("d", 1f, 1100, spec, false).Should().BeApproximately(Curve.Standard.Ease(0.5f), 1e-4f,
            "50 ms of the 100 ms glide have run once the delay ended");
        store.Resolve("d", 1f, 1150, spec, false).Should().Be(1);
    }

    [Fact]
    public void ReducedMotion_AndANullSpec_BothSnap()
    {
        var store = new TransitionStore();
        var spec = TransitionSpec.All();

        store.Resolve("r", 0f, 0, spec, false);
        store.Resolve("r", 1f, 10, spec, reducedMotion: true).Should().Be(1, "Reduce Motion snaps and forgets");
        store.Resolve("n", 0f, 0, null, false);
        store.Resolve("n", 1f, 10, null, false).Should().Be(1, "no spec is no glide");
    }

    [Fact]
    public void RetargetingMidGlide_ContinuesFromTheShownValue_UnderTheNewSpec()
    {
        var store = new TransitionStore();
        var slow = new TransitionSpec(StyleChannels.Size, DurationMs: 1000);
        var fast = new TransitionSpec(StyleChannels.Size, DurationMs: 100);

        store.Resolve("t", 0f, 0, slow, false);
        store.Resolve("t", 100f, 1000, slow, false);
        var shown = store.Resolve("t", 100f, 1500, slow, false);          // mid-glide toward 100
        shown.Should().BeGreaterThan(0).And.BeLessThan(100);

        // Retarget back to 0 with a different spec: starts from `shown`, and is done 100 ms later.
        store.Resolve("t", 0f, 1500, fast, false).Should().BeApproximately(shown, 1e-3f);
        store.Resolve("t", 0f, 1600, fast, false).Should().Be(0);
    }
}
