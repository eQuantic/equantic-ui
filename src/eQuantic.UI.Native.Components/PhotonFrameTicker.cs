using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Photon's <see cref="IFrameTicker"/>: subscribers are called at the top of every frame, on the
/// render thread, before the tree is built — so a <c>SetState</c> inside the callback is inline and
/// this very frame draws the new state.
/// <para>
/// STATIC, like <see cref="PhotonDispatcher"/> and for the same reason: which loop draws is a fact
/// about the process. The host fires it and asks <see cref="HasSubscribers"/> to know whether to
/// keep frames flowing — a subscribed ticker IS active motion, and an unsubscribed one costs the
/// idle loop nothing.
/// </para>
/// </summary>
public sealed class PhotonFrameTicker : IFrameTicker
{
    public static readonly PhotonFrameTicker Shared = new();

    private sealed class Subscription(PhotonFrameTicker owner, Action<FrameTick> onFrame) : IDisposable
    {
        public readonly Action<FrameTick> OnFrame = onFrame;
        public float? LastMs;
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;   // disposing twice is fine
            owner.Remove(this);
        }
    }

    private readonly object _gate = new();
    private readonly List<Subscription> _subscribers = [];

    /// <summary>Whether anyone wants frames — the host keeps rendering while this is true.</summary>
    public bool HasSubscribers
    {
        get { lock (_gate) return _subscribers.Count > 0; }
    }

    public IDisposable OnFrame(Action<FrameTick> onFrame)
    {
        var subscription = new Subscription(this, onFrame);
        lock (_gate) _subscribers.Add(subscription);
        return subscription;
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate) _subscribers.Remove(subscription);
    }

    /// <summary>
    /// Delivers the frame to everyone subscribed WHEN IT STARTED — a snapshot, so a callback that
    /// disposes itself or subscribes something new changes the next frame, never this one.
    /// </summary>
    public void Fire(float timeMs)
    {
        Subscription[] snapshot;
        lock (_gate)
        {
            if (_subscribers.Count == 0) return;
            snapshot = _subscribers.ToArray();
        }
        foreach (var subscription in snapshot)
        {
            var delta = subscription.LastMs is { } last ? timeMs - last : 0f;
            subscription.LastMs = timeMs;
            subscription.OnFrame(new FrameTick(timeMs, delta));
        }
    }
}
