using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>
/// What a device capability is DURING SERVER RENDERING: absent.
/// <para>
/// A page that takes a camera, a photo library or the app's storage through its constructor has to
/// be constructible on the server, or it cannot be server-rendered at all — and then the one page
/// that does something is the one page a crawler never sees, and the visitor waits for JavaScript
/// to be told the page exists. Without these the page did not render, it failed: no constructor
/// could be satisfied, and the request ended in a 500.
/// </para>
/// <para>
/// ABSENT, deliberately, and not simulated. Every one of these reports itself unavailable and hands
/// back nothing, which is the truth — there is no camera in a datacenter, and the visitor's
/// localStorage is on the visitor's machine. A server-side fake would be worse than a failure: the
/// page would render one thing, the browser would hydrate another, and the mismatch would be
/// blamed on the reconciler.
/// </para>
/// <para>
/// So a page's Build is written the way it has to be written anyway — check availability, draw the
/// fallback — and the SSR pass takes that branch. The client boot registers the real ones, and the
/// first client render replaces the fallback with whatever the browser can actually do.
/// </para>
/// <para>
/// Registered with TryAdd, so an app that has a genuine server-side answer for one of these — a
/// storage backed by the user's session, say — registers it and wins.
/// </para>
/// </summary>
internal static class AbsentCapabilities
{
    /// <summary>A store that holds nothing and says so: reads answer null, writes go nowhere.
    /// It cannot do otherwise — the storage this stands for lives on the visitor's machine, and a
    /// server-side dictionary pretending to be it would disagree with the real one the moment the
    /// page hydrated.</summary>
    internal sealed class Storage : IAppStorage, ISecretStore
    {
        public string? Get(string key) => null;

        public void Set(string key, string value)
        {
        }

        public void Remove(string key)
        {
        }
    }

    internal sealed class Clipboard : ITextClipboard
    {
        public string? Read() => null;

        public void Write(string text)
        {
        }
    }

    internal sealed class PhotoLibrary : IPhotoLibrary
    {
        public bool IsAvailable => false;

        public ValueTask<PermissionState> GetPermissionAsync(CancellationToken cancellationToken = default) =>
            new(PermissionState.Denied);

        public ValueTask<ImageData?> PickImageAsync(CancellationToken cancellationToken = default) =>
            new((ImageData?)null);

        public ValueTask<IReadOnlyList<ImageData>> PickImagesAsync(int limit = 0,
            CancellationToken cancellationToken = default) =>
            new((IReadOnlyList<ImageData>)Array.Empty<ImageData>());
    }

    internal sealed class Camera : ICamera
    {
        public bool IsAvailable => false;

        public PermissionState Permission => PermissionState.Denied;

        public ValueTask<ICameraSession?> StartPreviewAsync(CancellationToken cancellationToken = default) =>
            new((ICameraSession?)null);
    }

    internal sealed class Location : ILocation
    {
        public bool IsAvailable => false;

        public PermissionState Permission => PermissionState.Denied;

        public ValueTask<GeoLocation?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
            new((GeoLocation?)null);

        public IDisposable Subscribe(Action<GeoLocation> onChanged) => NoSubscription.Instance;
    }

    internal sealed class MotionSensor : IMotionSensor
    {
        public bool IsAvailable => false;

        public IDisposable Subscribe(Action<MotionReading> onReading) => NoSubscription.Instance;
    }

    internal sealed class Biometrics : IBiometrics
    {
        public bool IsAvailable => false;

        public ValueTask<BiometricResult> AuthenticateAsync(string reason,
            CancellationToken cancellationToken = default) =>
            new(BiometricResult.Unavailable);
    }

    /// <summary>
    /// The one that does NOT report absence — and the exception is the point. "Offline" here would
    /// mean the SSR pass renders the offline banner into the markup every crawler and every first
    /// paint sees, for visitors who are plainly online, since they just fetched the page. The
    /// server cannot observe the visitor's connection, so it says what the request already proved.
    /// </summary>
    internal sealed class NetworkStatus : INetworkStatus
    {
        public NetworkState Current => new(Online: true, NetworkKind.Other);

        public IDisposable Subscribe(Action<NetworkState> onChanged) => NoSubscription.Instance;
    }

    /// <summary>Nothing will ever be delivered, so unsubscribing has nothing to undo.</summary>
    private sealed class NoSubscription : IDisposable
    {
        internal static readonly NoSubscription Instance = new();

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A clock that never ticks, which is the truth rather than a gap: a server renders ONE frame
    /// and sends it, so there is no later for a periodic callback to arrive in. What the visitor
    /// gets is the first paint the component was built with, and the ticking starts when the page
    /// hydrates in their browser.
    /// </summary>
    internal sealed class Clock : IClock
    {
        public IDisposable Every(TimeSpan interval, Action onTick) => NoSubscription.Instance;
    }

    /// <summary>No frames on a server: subscribing succeeds and never fires, so the first paint is
    /// the state the component was built with — the same answer the periodic clock above gives, for
    /// the same reason.</summary>
    internal sealed class FrameTicker : IFrameTicker
    {
        public IDisposable OnFrame(Action<FrameTick> onFrame) => NoSubscription.Instance;
    }

    /// <summary>Analytics during server rendering: a no-op, and not an apology — analytics
    /// describes what a USER did, and SSR is not a user. A page that tracks on the server would
    /// count its own crawlers.</summary>
    internal sealed class Analytics : IAnalytics
    {
        public void Track(string eventName, Dictionary<string, object?>? data = null)
        {
        }
    }
}
