namespace eQuantic.UI.Primitives;

/// <summary>
/// The app's hand on ANALYTICS — a service, not a node, exactly like
/// <see cref="ICultureController"/>: a page tracks an event by asking for this the way it asks for
/// a camera, and never learns which collector answered. On the web the realization pushes into the
/// tag manager's <c>dataLayer</c> when an installer wired one (<c>UseGtm</c>); with no installer it
/// is a silent no-op, and on the server it always is — analytics describes what a USER did, and
/// SSR is not a user.
/// <para>
/// The contract is deliberately the dataLayer's own shape: an event NAME plus a flat bag of
/// values. Names are the vocabulary the app shares with whoever reads the analytics
/// (<c>sign_up</c>, <c>begin_checkout</c>); the framework never invents or prefixes any.
/// </para>
/// <para>
/// A DICTIONARY rather than an anonymous object, because this call crosses to the browser through
/// eqc and a dictionary is the shape that survives the crossing. Values should be strings,
/// numbers and booleans — what a tag manager can actually route.
/// </para>
/// </summary>
public interface IAnalytics
{
    /// <summary>Records that something happened, with whatever detail it deserves. Fire-and-forget
    /// by design: analytics must never make a page wait, and a failed delivery is the collector's
    /// problem, not the page's.</summary>
    void Track(string eventName, Dictionary<string, object?>? data = null);
}
