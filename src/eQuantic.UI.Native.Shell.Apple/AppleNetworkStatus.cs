using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// Network.framework's NWPathMonitor — the same framework and the same calls on the Mac and the
/// iPhone, which is exactly the kind of difference an app should never see.
/// <para>
/// The update handler is a hand-built <see cref="ObjCBlock"/>, and this class is the first direct
/// beneficiary of that block declaring itself GLOBAL: the monitor OWNS its handler — copies it on
/// install, releases it on cancel — precisely the lifetime that crashed the biometric prompt until
/// the block stopped claiming to live on the stack.
/// </para>
/// <para>
/// The monitor starts LAZILY, on the first read or subscription, and then stays up for the process:
/// a capability taken through a constructor is constructed even by pages that never look at it, and
/// spinning a monitor up for those would be paying for the question nobody asked.
/// </para>
/// </summary>
public sealed class AppleNetworkStatus : INetworkStatus
{
    // The framework binary itself — probed with dlopen/dlsym on this machine before being written
    // down; /usr/lib/swift/libnetwork.dylib, the plausible-looking guess, does not exist.
    private const string NetworkLib = "/System/Library/Frameworks/Network.framework/Network";

    private readonly Lock _gate = new();
    private readonly List<Subscription> _subscriptions = new();
    private NetworkState _current = NetworkState.Offline;
    private bool _started;

    // Kept for the lifetime of the monitor: the native side holds the block, the block holds the
    // delegate, and the GC must not collect what the framework will still call.
    private ObjCBlock? _handler;
    private PathUpdate? _callback;
    private IntPtr _monitor;

    private delegate void PathUpdate(IntPtr block, IntPtr path);

    public NetworkState Current
    {
        get
        {
            EnsureStarted();
            lock (_gate) return _current;
        }
    }

    public IDisposable Subscribe(Action<NetworkState> onChanged)
    {
        EnsureStarted();
        var subscription = new Subscription(this, onChanged);
        lock (_gate) _subscriptions.Add(subscription);
        return subscription;
    }

    private void EnsureStarted()
    {
        lock (_gate)
        {
            if (_started) return;
            _started = true;

            _monitor = nw_path_monitor_create();
            _callback = OnPathUpdate;
            _handler = new ObjCBlock(_callback);
            nw_path_monitor_set_update_handler(_monitor, _handler.Handle);
            nw_path_monitor_set_queue(_monitor, dispatch_get_global_queue(0, 0));
            nw_path_monitor_start(_monitor);
        }
    }

    private void OnPathUpdate(IntPtr block, IntPtr path)
    {
        // nw_path_status_t: 1 = satisfied. 2 (unsatisfied) and 3 (satisfiable) are both "not now" —
        // satisfiable means a connection COULD come up on demand, which an app cannot send bytes
        // through yet, so it reads as offline rather than as a maybe.
        var online = nw_path_get_status(path) == 1;

        // nw_interface_type_t: 0 other, 1 wifi, 2 cellular, 3 wired, 4 loopback.
        var kind = !online ? NetworkKind.None
            : nw_path_uses_interface_type(path, 1) ? NetworkKind.Wifi
            : nw_path_uses_interface_type(path, 2) ? NetworkKind.Cellular
            : nw_path_uses_interface_type(path, 3) ? NetworkKind.Wired
            : NetworkKind.Other;

        var state = new NetworkState(online, kind);
        Subscription[] listeners;
        lock (_gate)
        {
            if (_current == state) return;   // the monitor re-reports on queue changes; changes only
            _current = state;
            listeners = _subscriptions.ToArray();
        }
        foreach (var listener in listeners) listener.Notify(state);
    }

    private sealed class Subscription(AppleNetworkStatus owner, Action<NetworkState> onChanged) : IDisposable
    {
        private Action<NetworkState>? _onChanged = onChanged;

        public void Notify(NetworkState state) => _onChanged?.Invoke(state);

        public void Dispose()
        {
            _onChanged = null;
            lock (owner._gate) owner._subscriptions.Remove(this);
        }
    }

    [DllImport(NetworkLib)]
    private static extern IntPtr nw_path_monitor_create();

    [DllImport(NetworkLib)]
    private static extern void nw_path_monitor_set_update_handler(IntPtr monitor, IntPtr block);

    [DllImport(NetworkLib)]
    private static extern void nw_path_monitor_set_queue(IntPtr monitor, IntPtr queue);

    [DllImport(NetworkLib)]
    private static extern void nw_path_monitor_start(IntPtr monitor);

    [DllImport(NetworkLib)]
    private static extern uint nw_path_get_status(IntPtr path);

    [DllImport(NetworkLib, EntryPoint = "nw_path_uses_interface_type")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool nw_path_uses_interface_type(IntPtr path, uint type);

    [DllImport("/usr/lib/system/libdispatch.dylib")]
    private static extern IntPtr dispatch_get_global_queue(long identifier, ulong flags);
}
