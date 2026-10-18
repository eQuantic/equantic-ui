using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Spec B14 — the native VALUE-TRANSITION animator: when a tracked value CHANGES between frames, it
/// interpolates under the node's own <see cref="TransitionSpec"/> instead of snapping — the Photon twin
/// of the web's <c>transition</c>. Deliberately a FUNCTION OF TIME like LoopMotion: the value is pure
/// in (track, target, timeMs), so frames stay deterministic and tests pin a fixed t. Rules (CSS
/// parity): the FIRST sighting mounts at the target (transitions animate changes, not mounts); a
/// mid-flight retarget starts from the current interpolated value; Reduce Motion and untracked values
/// snap.
/// <para>
/// The tracks live on the node's position (<see cref="NodeStates"/>), so a node drawn at another's
/// place mounts at its own value instead of gliding from the one the other left. This is the host's
/// part: the frame's clock the tracks resolve against, and whether any of them is still moving. A
/// frame realized without one snaps everything.
/// </para>
/// </summary>
public sealed class TransitionClock
{
    private static readonly TransitionSpec Default = new(StyleChannels.Size);

    /// <summary>True when any transition was mid-flight during the current frame — the host keeps
    /// scheduling frames while set. Reset by <see cref="BeginFrame"/>.</summary>
    public bool AnyActive { get; private set; }

    public void BeginFrame() => AnyActive = false;

    /// <summary>The flex-weight form (spec B14): glides over the base motion on the standard curve
    /// when <paramref name="animate"/>, snaps otherwise.</summary>
    public float Resolve(Element? at, TrackKey track, float target, float timeMs, bool animate,
        bool reducedMotion) =>
        Resolve(at, track, target, timeMs, animate ? Default : null, reducedMotion);

    /// <summary>
    /// The value to LAY OUT or PAINT for a track of <paramref name="at"/> this frame. A null
    /// <paramref name="spec"/> snaps and forgets the track; a spec animates changes under ITS
    /// duration, delay and curve — so the box that said <c>Transition = TransitionSpec.Of(Colors,
    /// Motion.Press)</c> moves in 100 ms on the standard curve, and the one beside it that said
    /// nothing snaps, exactly as the same two boxes do in a browser.
    /// </summary>
    public float Resolve(Element? at, TrackKey track, float target, float timeMs, TransitionSpec? spec,
        bool reducedMotion) =>
        Resolve(at, track, 0, target, timeMs, spec, reducedMotion);

    /// <summary>
    /// <paramref name="spec"/> if it animates <paramref name="channel"/>, else null — so a channel
    /// the author left out of <c>Channels</c> SNAPS through the same <c>Resolve</c> call that glides
    /// the ones they put in. One code path per property, and the spec decides.
    /// </summary>
    public static TransitionSpec? Only(TransitionSpec? spec, StyleChannels channel) =>
        spec is { } s && (s.Channels & channel) != 0 ? s : null;

    /// <summary>
    /// A colour glide as four float tracks — red, green, blue and alpha interpolated separately in
    /// sRGB bytes, which is what CSS does for <c>transition: background-color</c>: not perceptually
    /// perfect, and exactly what the web realizer's output does, so the two targets agree.
    /// </summary>
    public Color ResolveColor(Element? at, TrackKey track, Color target, float timeMs, TransitionSpec? spec,
        bool reducedMotion)
    {
        if (spec is null || reducedMotion)
        {
            // Snap AND forget the four tracks, so a later spec starts from the shown colour.
            if (NodeStates.Find<TransitionState>(at) is { } shown)
                for (var component = 0; component < 4; component++) shown.Forget(track, component);
            return target;
        }

        return new Color(
            Byte(Resolve(at, track, 0, target.R, timeMs, spec, false)),
            Byte(Resolve(at, track, 1, target.G, timeMs, spec, false)),
            Byte(Resolve(at, track, 2, target.B, timeMs, spec, false)),
            Byte(Resolve(at, track, 3, target.A, timeMs, spec, false)));
    }

    /// <summary>
    /// How many custom shadows <paramref name="at"/> drew the last frame it was drawn, or null the
    /// first time. A list that grows seeds its new positions at nothing, so they glide in instead of
    /// mounting at their target, and one that shrinks keeps drawing the positions it lost until they
    /// have glided out: CSS pads the shorter of two <c>box-shadow</c> lists with transparent shadows,
    /// and the custom shadows glide the same way (#508).
    /// </summary>
    public int? ShadowsDrawn(Element? at) => NodeStates.Find<TransitionState>(at)?.ShadowsDrawn;

    /// <summary>The count <see cref="ShadowsDrawn"/> answers next frame.</summary>
    public void RememberShadowsDrawn(Element? at, int count)
    {
        if (NodeStates.For<TransitionState>(at) is { } state) state.ShadowsDrawn = count;
    }

    /// <summary>The value a track is gliding TOWARD, or null when it has none — what a member that
    /// left its list fades from.</summary>
    public float? Target(Element? at, TrackKey track) => NodeStates.Find<TransitionState>(at)?.Target(track, 0);

    /// <summary>The colour a <see cref="ResolveColor"/> track is gliding TOWARD, or null when it has
    /// none, rounded as the track rounds what it draws.</summary>
    public Color? TargetColor(Element? at, TrackKey track)
    {
        if (NodeStates.Find<TransitionState>(at) is not { } state || state.Target(track, 0) is not { } r)
            return null;
        return new Color(Byte(r), Byte(state.Target(track, 1) ?? 0), Byte(state.Target(track, 2) ?? 0),
            Byte(state.Target(track, 3) ?? 0));
    }

    private float Resolve(Element? at, TrackKey track, int component, float target, float timeMs,
        TransitionSpec? spec, bool reducedMotion)
    {
        if (spec is not { } motion || reducedMotion || motion.DurationMs <= 0)
        {
            NodeStates.Find<TransitionState>(at)?.Forget(track, component);
            return target;
        }

        if (NodeStates.For<TransitionState>(at) is not { } state) return target;
        var value = state.Resolve(track, component, target, timeMs, motion, out var moving);
        if (moving) AnyActive = true;
        return value;
    }

    private static byte Byte(float channel) => (byte)Math.Clamp(MathF.Round(channel), 0, 255);
}
