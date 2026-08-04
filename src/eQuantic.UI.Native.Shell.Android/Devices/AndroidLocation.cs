using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using eQuantic.UI.Primitives;
using AndroidLocationListener = Android.Locations.ILocationListener;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The framework's LocationManager — not Play Services' fused provider, for the same reason the
/// biometrics use the framework prompt: no dependency an app carries for a capability it may never
/// use, and one code path that exists on every Android, Google-blessed or not.
/// </summary>
internal sealed class AndroidLocation : ILocation
{
    private const string Fine = global::Android.Manifest.Permission.AccessFineLocation;
    private const string Coarse = global::Android.Manifest.Permission.AccessCoarseLocation;

    private static LocationManager? Manager =>
        PhotonActivity.Current?.GetSystemService(Context.LocationService) as LocationManager;

    public bool IsAvailable
    {
        get
        {
            if (Manager is not { } manager) return false;
            return Build.VERSION.SdkInt >= BuildVersionCodes.P
                ? manager.IsLocationEnabled
                : manager.IsProviderEnabled(LocationManager.GpsProvider)
                    || manager.IsProviderEnabled(LocationManager.NetworkProvider);
        }
    }

    public PermissionState Permission
    {
        get
        {
            if (PhotonActivity.Current is not { } activity) return PermissionState.NotDetermined;
            if (activity.CheckSelfPermission(Fine) == global::Android.Content.PM.Permission.Granted
                || activity.CheckSelfPermission(Coarse) == global::Android.Content.PM.Permission.Granted)
                return PermissionState.Granted;
            // Android cannot distinguish "never asked" from "denied once" without asking; the
            // rationale API needs an ask on record. NotDetermined is the honest default — asking
            // is allowed, and a permanent denial surfaces as the prompt not appearing.
            return PermissionState.NotDetermined;
        }
    }

    public async ValueTask<GeoLocation?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (PhotonActivity.Current is not { } activity || Manager is not { } manager) return null;

        if (Permission != PermissionState.Granted
            && !await activity.RequestPermissionsAsync([Fine, Coarse], cancellationToken))
            return null;

        var completion = new TaskCompletionSource<GeoLocation?>();
        var listener = new OneShot(completion);
        manager.RequestSingleUpdate(Criteria(), listener, Looper.MainLooper);

        var winner = await Task.WhenAny(completion.Task,
            Task.Delay(TimeSpan.FromSeconds(15), cancellationToken));
        if (winner != completion.Task)
        {
            manager.RemoveUpdates(listener);
            return null;
        }
        return completion.Task.Result;
    }

    public IDisposable Subscribe(Action<GeoLocation> onChanged)
    {
        if (Manager is not { } manager || Permission != PermissionState.Granted)
            return EmptySubscription.Instance;
        var listener = new Stream(onChanged);
        // ~10 s / 10 m: the delivery-tracking cadence, not the navigation one.
        manager.RequestLocationUpdates(10_000, 10, Criteria(), listener, Looper.MainLooper);
        return new Subscription(manager, listener);
    }

    private static Criteria Criteria() => new() { Accuracy = Accuracy.Fine };

    private static GeoLocation Read(global::Android.Locations.Location location) =>
        new(location.Latitude, location.Longitude, location.HasAccuracy ? location.Accuracy : 0);

    private sealed class OneShot(TaskCompletionSource<GeoLocation?> completion)
        : Java.Lang.Object, AndroidLocationListener
    {
        public void OnLocationChanged(global::Android.Locations.Location location) =>
            completion.TrySetResult(Read(location));

        public void OnProviderDisabled(string provider) => completion.TrySetResult(null);
        public void OnProviderEnabled(string provider) { }
        public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
    }

    private sealed class Stream(Action<GeoLocation> onChanged) : Java.Lang.Object, AndroidLocationListener
    {
        public void OnLocationChanged(global::Android.Locations.Location location) =>
            onChanged(Read(location));

        public void OnProviderDisabled(string provider) { }
        public void OnProviderEnabled(string provider) { }
        public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
    }

    private sealed class Subscription(LocationManager manager, AndroidLocationListener listener) : IDisposable
    {
        private AndroidLocationListener? _listener = listener;

        public void Dispose()
        {
            if (_listener is null) return;
            manager.RemoveUpdates(_listener);
            _listener = null;
        }
    }

    private sealed class EmptySubscription : IDisposable
    {
        public static readonly EmptySubscription Instance = new();
        public void Dispose() { }
    }
}
