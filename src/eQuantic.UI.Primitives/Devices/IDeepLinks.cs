namespace eQuantic.UI.Primitives;

/// <summary>
/// The URLs this app is opened WITH — a licence e-mail's <c>acme://activate?key=…</c>, a link back
/// from a browser sign-in, a colleague pasting a link to one screen of the app.
/// <para>
/// The declaration half already exists: <c>builder.Bundle.UrlScheme("acme")</c> writes the manifest
/// entry that makes the operating system launch this app for that scheme. This is the other half —
/// the app READING what it was launched with. Without it a deep link opens the app at its front
/// door and the URL is silently dropped, which looks to a user exactly like a broken link.
/// </para>
/// <para>
/// Two ways in, because a URL arrives at two very different moments and an app that handles only
/// one is broken half the time:
/// </para>
/// <list type="bullet">
/// <item><see cref="Launch"/> — the URL that STARTED this process, buffered and answered on demand.
/// Reading it twice gives the same answer, and it keeps answering for the life of the app.</item>
/// <item><see cref="Subscribe"/> — every URL from now on, for the app that is already running when
/// the link is clicked. <see cref="Launch"/> is NOT replayed here: a subscriber that also wants it
/// reads it, in the one line the two-callers shape asks for.</item>
/// </list>
/// <para>
/// <para>
/// SUBSCRIBE if you must not miss it. <see cref="Launch"/> is not readable in a constructor on
/// macOS, measured rather than assumed: the launch URL arrives as an AppleEvent, AppleEvents are
/// delivered BY the run loop, and the run loop starts after the first tree is built. A cold launch
/// therefore reaches a subscriber and finds <see cref="Launch"/> still null a moment earlier. The
/// property is for asking later — "what was this app opened with" — and the subscription is for
/// acting on it. An app that does both is right on every platform, which is why the sample does.
/// </para>
/// <para>
/// A URL the app cannot make sense of is still delivered. Deciding what an unknown host or path
/// means is the app's, and a capability that silently swallowed what it did not recognise would be
/// deciding on the app's behalf and leaving no evidence.
/// </para>
/// <para>
/// The callback may arrive on any thread the platform chooses; a component's <c>SetState</c>
/// already marshals through <see cref="IUiDispatcher"/>.
/// </para>
/// <para>
/// PHOTON ONLY, deliberately. On the web a "deep link" is just the address bar, and the router
/// already owns it — offering a second, weaker way to read the same thing would be a capability
/// that exists to be symmetrical rather than to be used.
/// </para>
/// </summary>
public interface IDeepLinks
{
    /// <summary>
    /// The URL this app was launched with, or null when it was started normally. Answers from a
    /// buffer and never blocks, so it is safe to read inside a Build.
    /// </summary>
    Uri? Launch { get; }

    /// <summary>
    /// Starts listening for the URLs that arrive from now on. Disposing stops it; disposing twice
    /// is fine. <see cref="Launch"/> is not replayed — read it if you want it.
    /// </summary>
    IDisposable Subscribe(Action<Uri> onOpened);
}

/// <summary>
/// The BUFFERING and the fan-out, which every platform needs and none of them does differently.
/// <para>
/// A shell feeds it whatever its own mechanism produced — an AppleEvent's direct object on macOS,
/// <c>application:openURL:</c> on iOS, an Intent's data on Android — and this decides the rest:
/// what the launch URL was, who hears about the later ones, and what to do with a string that is
/// not a URL at all.
/// </para>
/// <para>
/// Separated from the platform because the interesting part is not the plumbing. "The FIRST URL is
/// the launch URL and is answered rather than delivered" is a rule with a test; reading a
/// descriptor is a line of interop with none.
/// </para>
/// </summary>
public sealed class DeepLinkRelay : IDeepLinks
{
    private readonly Lock _gate = new();
    private readonly List<Action<Uri>> _listeners = [];
    private Uri? _launch;

    /// <inheritdoc />
    public Uri? Launch
    {
        get { lock (_gate) return _launch; }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<Uri> onOpened)
    {
        ArgumentNullException.ThrowIfNull(onOpened);
        lock (_gate) _listeners.Add(onOpened);
        return new Subscription(this, onOpened);
    }

    /// <summary>
    /// A URL the platform just handed over. Returns whether it was one at all: a string that does
    /// not parse is DROPPED rather than passed on, because <see cref="IDeepLinks"/> promises a
    /// <see cref="Uri"/> and inventing one would be worse than saying no. Anything that parses is
    /// delivered even if the app will not recognise it — that is the app's decision to make.
    /// </summary>
    public bool Offer(string? text)
    {
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var url)) return false;

        Action<Uri>[] listeners;
        lock (_gate)
        {
            // The FIRST one is the launch URL. It is answered by Launch rather than delivered,
            // because on a cold start nothing is subscribed yet — the app that will read it does
            // not exist when this runs.
            _launch ??= url;
            listeners = [.. _listeners];
        }

        // OUTSIDE the lock: a listener that subscribes or disposes from inside its own callback is
        // an ordinary thing for a component to do while it is being torn down.
        foreach (var listener in listeners) listener(url);
        return true;
    }

    private sealed class Subscription(DeepLinkRelay relay, Action<Uri> listener) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (relay._gate) relay._listeners.Remove(listener);
        }
    }
}
