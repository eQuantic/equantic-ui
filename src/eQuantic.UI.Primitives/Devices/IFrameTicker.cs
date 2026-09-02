namespace eQuantic.UI.Primitives;

/// <summary>One frame's moment: the clock the frame was built against, and how long since the last.</summary>
/// <param name="TimeMs">The host's frame clock — the same value every transition and loop motion in
/// that frame sampled, so a subscriber and the styles around it agree on what "now" is.</param>
/// <param name="DeltaMs">Elapsed since the previous frame this subscriber saw; 0 on its first.</param>
public readonly record struct FrameTick(float TimeMs, float DeltaMs);

/// <summary>
/// The FRAME clock — a callback before every frame is built, for things that move every frame.
/// <para>
/// <see cref="IClock"/> is deliberately periodic (hundreds of milliseconds and up), and its own
/// fence says why per-frame animation was not offered through it: without positional state
/// retention and a geometry channel, a 60 Hz callback would be the slowest way to animate. Both
/// prerequisites exist now — the reconciler retains by path, and <c>BoxStyle.Transition</c> glides
/// every style channel on every target — so most motion needs NO ticker at all: change the style,
/// and it glides. This is for what a style cannot express: physics-driven art, a simulation, a
/// chart whose geometry is recomputed every frame from data that never stops.
/// </para>
/// <para>
/// A subscription KEEPS FRAMES FLOWING: while anyone is subscribed the host renders continuously
/// instead of idling, which is the point and also the cost — dispose when the motion stops, and
/// pair <c>OnMount</c> subscribing with <c>OnUnmount</c> disposing, like <see cref="IClock"/>. The
/// callback runs on the UI thread, before the frame builds, so <c>SetState</c> inside it is inline
/// and the new state is what this very frame draws.
/// </para>
/// <para>
/// On a SERVER there are no frames: subscribing succeeds and never fires, and the first paint shows
/// the state the component was built with — the same answer <see cref="IClock"/> gives, for the
/// same reason.
/// </para>
/// </summary>
public interface IFrameTicker
{
    /// <summary>Calls <paramref name="onFrame"/> before every frame until the result is disposed.
    /// Disposing twice is fine; disposing from inside the callback is fine and takes effect next frame.</summary>
    IDisposable OnFrame(Action<FrameTick> onFrame);
}
