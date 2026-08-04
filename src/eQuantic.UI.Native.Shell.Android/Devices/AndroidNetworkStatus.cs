using Android.Content;
using Android.Net;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// ConnectivityManager, through the NetworkCallback registered for the default network — the modern
/// path (the broadcast receiver it replaces has been deprecated since Android 7).
/// <para>
/// `Current` reads the active network DIRECTLY rather than caching the callback's last word: the
/// callback only speaks when something changes, and a capability constructed after boot would
/// otherwise answer Offline until the first change — on a phone that never leaves wifi, forever.
/// </para>
/// </summary>
internal sealed class AndroidNetworkStatus : INetworkStatus
{
    private readonly Lock _gate = new();
    private readonly List<Subscription> _subscriptions = new();
    private bool _watching;

    private static ConnectivityManager? Manager =>
        PhotonActivity.Current?.GetSystemService(Context.ConnectivityService) as ConnectivityManager;

    public NetworkState Current
    {
        get
        {
            if (Manager is not { } manager) return NetworkState.Offline;
            var network = manager.ActiveNetwork;
            if (network is null) return NetworkState.Offline;
            var capabilities = manager.GetNetworkCapabilities(network);
            if (capabilities is null || !capabilities.HasCapability(NetCapability.Internet))
                return NetworkState.Offline;

            var kind = capabilities.HasTransport(TransportType.Wifi) ? NetworkKind.Wifi
                : capabilities.HasTransport(TransportType.Cellular) ? NetworkKind.Cellular
                : capabilities.HasTransport(TransportType.Ethernet) ? NetworkKind.Wired
                : NetworkKind.Other;
            return new NetworkState(true, kind);
        }
    }

    public IDisposable Subscribe(Action<NetworkState> onChanged)
    {
        Watch();
        var subscription = new Subscription(this, onChanged);
        lock (_gate) _subscriptions.Add(subscription);
        return subscription;
    }

    private void Watch()
    {
        lock (_gate)
        {
            if (_watching || Manager is not { } manager) return;
            _watching = true;
            manager.RegisterDefaultNetworkCallback(new Callback(this));
        }
    }

    private void Changed()
    {
        var state = Current;
        Subscription[] listeners;
        lock (_gate) listeners = _subscriptions.ToArray();
        foreach (var listener in listeners) listener.Notify(state);
    }

    private sealed class Callback(AndroidNetworkStatus owner) : ConnectivityManager.NetworkCallback
    {
        public override void OnAvailable(Network network) => owner.Changed();
        public override void OnLost(Network network) => owner.Changed();
        public override void OnCapabilitiesChanged(Network network, NetworkCapabilities capabilities) =>
            owner.Changed();
    }

    private sealed class Subscription(AndroidNetworkStatus owner, Action<NetworkState> onChanged) : IDisposable
    {
        private Action<NetworkState>? _onChanged = onChanged;

        public void Notify(NetworkState state) => _onChanged?.Invoke(state);

        public void Dispose()
        {
            _onChanged = null;
            lock (owner._gate) owner._subscriptions.Remove(this);
        }
    }
}
