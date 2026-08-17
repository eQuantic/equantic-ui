namespace eQuantic.UI.Primitives;

/// <summary>
/// TIME, as something a component can subscribe to. Everything else a screen reacts to arrives as an
/// event a person caused: a tap, a key, a scroll, a network reading. A carousel that advances itself,
/// a counter that ticks, a status that polls, a hint that appears after a while: all of them react to
/// nothing but the passage of time, and until this the framework had no way to say so.
/// <para>
/// The subscription is an <see cref="IDisposable"/>, like <see cref="INetworkStatus.Subscribe"/> and
/// for the same reason: a component is torn down and rebuilt constantly, and a timer with no visible
/// end keeps the dead one alive from the timer's side. The pair that ships together is
/// <c>OnMount</c> subscribing and <c>OnUnmount</c> disposing.
/// </para>
/// <para>
/// On a SERVER there is no clock to subscribe to, and that is the correct answer rather than a
/// missing one: nothing fires during server rendering, so the first paint shows the state the
/// component was built with, and the ticking starts when the page hydrates. A component written this
/// way is server-rendered without a special case.
/// </para>
/// <para>
/// FENCE, and the important one: this is a PERIODIC clock, not a frame clock. It is for things that
/// change every so often, measured in hundreds of milliseconds and up. Per-frame animation is a
/// different contract with two prerequisites this does not have — positional state retention through
/// the reconciler (a rebuild per frame otherwise diffs the whole subtree), and a geometry channel in
/// <see cref="StyleChannels"/> (which today can transition colour, opacity, transform, shadow,
/// filters and size, but not a path). Handing an app a 60Hz callback before those exist would be
/// handing it the slowest way to animate and calling it support.
/// </para>
/// </summary>
public interface IClock
{
    /// <summary>
    /// Calls <paramref name="onTick"/> every <paramref name="interval"/>, starting one interval from
    /// now. Disposing the result stops it; disposing twice is fine.
    /// <para>
    /// A tick MISSED while the app was away is dropped rather than replayed: a phone that spent ten
    /// minutes in a pocket comes back to one tick, not to six hundred. What a carousel wants is the
    /// next slide, never the six hundredth.
    /// </para>
    /// <para>
    /// The interval is a <see cref="TimeSpan"/> and not a number of milliseconds like the style
    /// specs, deliberately: those are design tokens, and this is wall-clock time, which .NET already
    /// has a type for.
    /// </para>
    /// </summary>
    IDisposable Every(TimeSpan interval, Action onTick);
}
