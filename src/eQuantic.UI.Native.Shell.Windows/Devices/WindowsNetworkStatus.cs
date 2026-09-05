using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The network as Windows reports it: the CONNECTIVITY HINT (Windows 10 2004+) for whether anything
/// is reachable — the same signal the taskbar's globe icon draws from — and .NET's own
/// <see cref="NetworkInterface"/> for what carries the traffic, which the hint does not say.
/// <para>
/// The monitor starts LAZILY, on the first read or subscription, and then stays up for the process:
/// a capability taken through a constructor is constructed even by pages that never look at it, and
/// registering a callback for those would be paying for the question nobody asked.
/// </para>
/// <para>
/// The callback arrives on a THREAD-POOL thread, which is the contract every live capability here
/// states: the realization hands the platform's change to the app as it arrives, and
/// <c>SetState</c> marshals the consequence into the next frame.
/// </para>
/// </summary>
public sealed unsafe class WindowsNetworkStatus : INetworkStatus, IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NL_NETWORK_CONNECTIVITY_HINT
    {
        public int ConnectivityLevel;
        public int ConnectivityCost;
        public byte ApproachingDataLimit;
        public byte OverDataLimit;
        public byte Roaming;
    }

    // NL_NETWORK_CONNECTIVITY_LEVEL_HINT: Unknown 0, None 1, LocalAccess 2, InternetAccess 3,
    // ConstrainedInternetAccess 4, Hidden 5.
    private const int LevelNone = 1;
    private const int LevelUnknown = 0;

    [DllImport("iphlpapi.dll")]
    private static extern int GetNetworkConnectivityHint(NL_NETWORK_CONNECTIVITY_HINT* hint);

    [DllImport("iphlpapi.dll")]
    private static extern int NotifyNetworkConnectivityHintChange(IntPtr callback, IntPtr context,
        [MarshalAs(UnmanagedType.U1)] bool initialNotification, IntPtr* handle);

    [DllImport("iphlpapi.dll")]
    private static extern int CancelMibChangeNotify2(IntPtr handle);

    private static readonly Lock Gate = new();
    private static readonly List<WindowsNetworkStatus> Listening = [];
    private static IntPtr _notification;

    private readonly List<Subscription> _subscriptions = [];
    private NetworkState _current = NetworkState.Offline;
    private bool _started;

    public NetworkState Current
    {
        get
        {
            EnsureStarted();
            lock (Gate) return _current;
        }
    }

    public IDisposable Subscribe(Action<NetworkState> onChanged)
    {
        ArgumentNullException.ThrowIfNull(onChanged);
        EnsureStarted();
        var subscription = new Subscription(this, onChanged);
        lock (Gate) _subscriptions.Add(subscription);
        return subscription;
    }

    private void EnsureStarted()
    {
        lock (Gate)
        {
            if (_started) return;
            _started = true;
            _current = Read();
            Listening.Add(this);
            if (_notification == IntPtr.Zero)
            {
                IntPtr handle;
                var registered = NotifyNetworkConnectivityHintChange(
                    (IntPtr)(delegate* unmanaged<IntPtr, NL_NETWORK_CONNECTIVITY_HINT, void>)&OnHintChanged,
                    IntPtr.Zero, false, &handle);
                if (registered == 0) _notification = handle;
            }
        }
    }

    [UnmanagedCallersOnly]
    private static void OnHintChanged(IntPtr context, NL_NETWORK_CONNECTIVITY_HINT hint)
    {
        WindowsNetworkStatus[] listening;
        lock (Gate) listening = [.. Listening];
        var state = new NetworkState(IsOnline(hint.ConnectivityLevel), KindNow());
        foreach (var monitor in listening) monitor.Publish(state);
    }

    private void Publish(NetworkState state)
    {
        Subscription[] subscribers;
        lock (Gate)
        {
            if (_current == state) return;
            _current = state;
            subscribers = [.. _subscriptions];
        }
        foreach (var subscriber in subscribers) subscriber.OnChanged(state);
    }

    private static NetworkState Read()
    {
        NL_NETWORK_CONNECTIVITY_HINT hint;
        var level = GetNetworkConnectivityHint(&hint) == 0 ? hint.ConnectivityLevel : LevelUnknown;
        var online = IsOnline(level);
        return new NetworkState(online, online ? KindNow() : NetworkKind.None);
    }

    /// <summary>Local access counts as online: the contract is "can anything be reached", not "is
    /// the internet up" — the Mac's NWPathMonitor answers the same question.</summary>
    private static bool IsOnline(int level) => level is not (LevelNone or LevelUnknown);

    /// <summary>What carries the traffic: the first interface that is up and has a route out.</summary>
    internal static NetworkKind KindNow()
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                if (adapter.GetIPProperties().GatewayAddresses.Count == 0) continue;
                return adapter.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Wireless80211 => NetworkKind.Wifi,
                    NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => NetworkKind.Cellular,
                    NetworkInterfaceType.Ethernet or NetworkInterfaceType.Ethernet3Megabit
                        or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx
                        or NetworkInterfaceType.GigabitEthernet => NetworkKind.Wired,
                    _ => NetworkKind.Other,
                };
            }
        }
        catch (NetworkInformationException)
        {
        }
        return NetworkKind.Other;
    }

    public void Dispose()
    {
        lock (Gate)
        {
            Listening.Remove(this);
            _subscriptions.Clear();
            if (Listening.Count == 0 && _notification != IntPtr.Zero)
            {
                CancelMibChangeNotify2(_notification);
                _notification = IntPtr.Zero;
            }
        }
    }

    private sealed class Subscription(WindowsNetworkStatus owner, Action<NetworkState> onChanged) : IDisposable
    {
        public readonly Action<NetworkState> OnChanged = onChanged;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (Gate) owner._subscriptions.Remove(this);
        }
    }
}
