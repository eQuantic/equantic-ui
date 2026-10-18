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
    private static Row Bar(int filled, bool animate = true, string? key = null)
    {
        var track = new Row(gap: 0) { Width = SizeValue.Fill, Height = 8, Key = key };
        track.Add(new Flexible(new Box(new BoxStyle
        {
            Height = 8, Background = new ColorToken(Color.FromRgb(0x00, 0x50, 0xA0)),
        }), filled) { AnimateChanges = animate });
        // B14: the counterweight animates WITH the fill (constant denominator → the ratio glides).
        track.Add(new Spacer(1000 - filled) { AnimateChanges = animate });
        return track;
    }

    /// <summary>A host's frames without the host: the tree it keeps across frames, where the tracks
    /// live, and the clock they glide against.</summary>
    private sealed class Frames
    {
        public BuildOwner Owner { get; } = BuildOwner.Structural();

        public TransitionClock Clock { get; } = new();

        public RealizeResult Draw(VisualNode root, float timeMs, bool reducedMotion = false) =>
            PhotonRealizer.Realize(root, 1000, 100, PhotonTheme.Instance, ThemeMode.Light,
                new DisplayListBuilder(), timeMs: timeMs, reducedMotion: reducedMotion, owner: Owner,
                transitions: Clock);
    }

    private static float FillWidth(VisualNode root, Frames frames, float timeMs) =>
        frames.Draw(root, timeMs).Root.Children[0].Bounds.Width;

    [Fact]
    public void ValueChange_GlidesOverBase_AndSettles()
    {
        var frames = new Frames();

        FillWidth(Bar(300), frames, timeMs: 0).Should().Be(300, "mounts land AT the target — no entrance glide");

        // The value changes 300 → 800: the next frames interpolate over Motion.Base (200ms).
        FillWidth(Bar(800), frames, timeMs: 1000).Should().Be(300, "the change starts from the shown value");
        // Half the duration is NOT half the travel: the standard curve front-loads, so at 50% of the
        // time the value is ~88% of the way — exactly where CSS puts it for cubic-bezier(.2,0,0,1).
        // (The smoothstep that stood in here before the curve evaluator existed said 50%.)
        var mid = FillWidth(Bar(800), frames, timeMs: 1100);
        mid.Should().BeGreaterThan(300).And.BeLessThan(800, "mid-glide is strictly between the ends");
        mid.Should().BeApproximately(300 + 500 * Curve.Standard.Ease(0.5f), 0.01f,
            "the glide follows the token's own curve, not a stand-in");
        FillWidth(Bar(800), frames, timeMs: 1200).Should().Be(800, "Base elapsed — settled at the target");
        FillWidth(Bar(800), frames, timeMs: 1300).Should().Be(800, "and stays settled");
    }

    [Fact]
    public void MidFlightRetarget_ContinuesFromTheCurrentValue()
    {
        var frames = new Frames();
        FillWidth(Bar(300), frames, 0);
        FillWidth(Bar(800), frames, 1000);
        var mid = FillWidth(Bar(800), frames, 1100);

        // Retarget mid-flight: the new glide starts where the old one visually is.
        var atRetarget = FillWidth(Bar(400), frames, 1100);
        atRetarget.Should().BeApproximately(mid, 1f, "no jump on retarget");
    }

    [Fact]
    public void ReducedMotion_AndUnanimatedChanges_Snap()
    {
        var reduced = new Frames();
        reduced.Draw(Bar(300), timeMs: 0, reducedMotion: true);
        var result = reduced.Draw(Bar(800), timeMs: 50, reducedMotion: true);
        result.Root.Children[0].Bounds.Width.Should().Be(800, "Reduce Motion snaps");
        result.HasActiveMotion.Should().BeFalse();

        var frames = new Frames();
        FillWidth(Bar(300, animate: false), frames, 0);
        FillWidth(Bar(800, animate: false), frames, 50).Should().Be(800, "AnimateChanges=false snaps (regressions)");
    }

    [Fact]
    public void ActiveTransition_KeepsTheHostScheduling()
    {
        var frames = new Frames();
        FillWidth(Bar(300), frames, 0);
        var mid = frames.Draw(Bar(800), timeMs: 100);
        mid.HasActiveMotion.Should().BeTrue("a glide is running — the host must keep scheduling frames");

        var settled = frames.Draw(Bar(800), timeMs: 400);
        settled.HasActiveMotion.Should().BeFalse("settled — frames stop");
    }

    /// <summary>
    /// The tracks belong to the position: a bar whose key changed is a new node at the old one's
    /// place, and it mounts at its own weight, where the track the path kept glided it from the old
    /// bar's. A frame realized with no owner of its own starts every node fresh, which is what every
    /// golden draws.
    /// </summary>
    [Fact]
    public void ANodeAtAnothersPlace_MountsAtItsOwnValue()
    {
        var frames = new Frames();
        frames.Draw(new Box(new BoxStyle { Width = SizeValue.Fill }, Bar(300, key: "first")), 0);

        frames.Draw(new Box(new BoxStyle { Width = SizeValue.Fill }, Bar(800, key: "next")), 1000)
            .Root.Children[0].Children[0]
            .Bounds.Width.Should().Be(800, "a new bar mounts at its weight: transitions animate changes, not mounts");
    }
}

/// <summary>
/// Spec S6 on Photon: a transition moves under ITS OWN spec — the author's duration, delay and
/// curve, per node — rather than one constant for every animation on the target. This is what lets
/// the same <c>Transition = TransitionSpec.Of(…)</c> mean the same motion here and in a browser.
/// The tracks live on a node's position, so each test glides on a position of its own.
/// </summary>
public class S6TransitionClockTests
{
    /// <summary>A node's position, kept by an owner as a host keeps it: where its tracks live.</summary>
    private static Element Position() =>
        BuildOwner.Structural().Build(new Box(new BoxStyle()), new ComponentContext(PhotonTheme.Instance),
            WindowSizeClass.Compact, rebuildAll: true);

    [Fact]
    public void ATrackFollowsItsSpecsDurationAndCurve()
    {
        var clock = new TransitionClock();
        var p = Position();
        var press = TransitionSpec.Of(StyleChannels.Colors, Motion.Press);   // 100 ms, standard

        clock.Resolve(p, GlideChannel.Opacity, 0f, 0, press, reducedMotion: false).Should().Be(0, "mounts at the target");
        clock.Resolve(p, GlideChannel.Opacity, 1f, 1000, press, reducedMotion: false).Should().Be(0, "a change starts where it was");
        clock.Resolve(p, GlideChannel.Opacity, 1f, 1050, press, reducedMotion: false)
            .Should().BeApproximately(Curve.Standard.Ease(0.5f), 1e-4f, "halfway in TIME, on the curve");
        clock.Resolve(p, GlideChannel.Opacity, 1f, 1100, press, reducedMotion: false).Should().Be(1, "100 ms elapsed — settled");
    }

    [Fact]
    public void ADelayHoldsTheOldValue_ThenTheGlideBegins()
    {
        var clock = new TransitionClock();
        var d = Position();
        var spec = new TransitionSpec(StyleChannels.Opacity, DurationMs: 100, DelayMs: 50);

        clock.Resolve(d, GlideChannel.Opacity, 0f, 0, spec, false);
        clock.Resolve(d, GlideChannel.Opacity, 1f, 1000, spec, false).Should().Be(0);
        clock.Resolve(d, GlideChannel.Opacity, 1f, 1040, spec, false).Should().Be(0, "still inside the delay");
        clock.Resolve(d, GlideChannel.Opacity, 1f, 1100, spec, false).Should().BeApproximately(Curve.Standard.Ease(0.5f), 1e-4f,
            "50 ms of the 100 ms glide have run once the delay ended");
        clock.Resolve(d, GlideChannel.Opacity, 1f, 1150, spec, false).Should().Be(1);
    }

    [Fact]
    public void ReducedMotion_AndANullSpec_BothSnap()
    {
        var clock = new TransitionClock();
        var spec = TransitionSpec.All();
        var r = Position();
        var n = Position();

        clock.Resolve(r, GlideChannel.Opacity, 0f, 0, spec, false);
        clock.Resolve(r, GlideChannel.Opacity, 1f, 10, spec, reducedMotion: true).Should().Be(1, "Reduce Motion snaps and forgets");
        clock.Resolve(n, GlideChannel.Opacity, 0f, 0, null, false);
        clock.Resolve(n, GlideChannel.Opacity, 1f, 10, null, false).Should().Be(1, "no spec is no glide");
    }

    [Fact]
    public void RetargetingMidGlide_ContinuesFromTheShownValue_UnderTheNewSpec()
    {
        var clock = new TransitionClock();
        var t = Position();
        var slow = new TransitionSpec(StyleChannels.Size, DurationMs: 1000);
        var fast = new TransitionSpec(StyleChannels.Size, DurationMs: 100);

        clock.Resolve(t, GlideChannel.Width, 0f, 0, slow, false);
        clock.Resolve(t, GlideChannel.Width, 100f, 1000, slow, false);
        var shown = clock.Resolve(t, GlideChannel.Width, 100f, 1500, slow, false);          // mid-glide toward 100
        shown.Should().BeGreaterThan(0).And.BeLessThan(100);

        // Retarget back to 0 with a different spec: starts from `shown`, and is done 100 ms later.
        clock.Resolve(t, GlideChannel.Width, 0f, 1500, fast, false).Should().BeApproximately(shown, 1e-3f);
        clock.Resolve(t, GlideChannel.Width, 0f, 1600, fast, false).Should().Be(0);
    }

    /// <summary>Two channels of one position, and one channel of two, are four tracks: what a track
    /// is found by is what it glides and where.</summary>
    [Fact]
    public void ATrackIsItsChannelAtItsPosition()
    {
        var clock = new TransitionClock();
        var spec = new TransitionSpec(StyleChannels.All, DurationMs: 100);
        var a = Position();
        var b = Position();

        clock.Resolve(a, GlideChannel.Opacity, 0f, 0, spec, false);
        clock.Resolve(a, GlideChannel.Width, 10f, 0, spec, false);
        clock.Resolve(b, GlideChannel.Opacity, 0f, 0, spec, false);

        clock.Resolve(a, GlideChannel.Opacity, 1f, 1000, spec, false);
        clock.Resolve(a, GlideChannel.Opacity, 1f, 1050, spec, false)
            .Should().BeApproximately(Curve.Standard.Ease(0.5f), 1e-4f, "a's opacity glides");
        clock.Resolve(a, GlideChannel.Width, 10f, 1050, spec, false).Should().Be(10, "its width did not change");
        clock.Resolve(b, GlideChannel.Opacity, 0f, 1050, spec, false).Should().Be(0, "b's opacity is b's own");
    }
}
