using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// CoreLocation, shared by the Mac and the iPhone.
/// <para>
/// CLLocationManager talks back through a DELEGATE — an Objective-C object with named methods —
/// so one is defined at runtime, the same way the content view that hears live resizes is:
/// an <see cref="ObjCDelegateClass"/>. Each manager gets its own delegate object, whose context is
/// this capability, so a callback answers for the instance that made the manager (#795).
/// </para>
/// <para>
/// The manager must be created on a thread with a RUN LOOP — its delegate speaks through the loop
/// of the thread that made it. Everything here funnels creation to first use, which in practice is
/// the main thread (a button's OnPressed), where the shell's event loop is already pumping.
/// </para>
/// </summary>
public sealed class AppleLocation : ILocation
{
    private const string CoreLocationLib = "/System/Library/Frameworks/CoreLocation.framework/CoreLocation";

    // CLAuthorizationStatus.
    private const int StatusNotDetermined = 0;
    private const int StatusRestricted = 1;
    private const int StatusDenied = 2;

    /// <summary>A fix that never comes resolves null rather than hanging the caller — the lesson
    /// the biometric reply taught, applied before it costs an afternoon this time.</summary>
    private static readonly TimeSpan FixTimeout = TimeSpan.FromSeconds(15);

    private static readonly Lock Gate = new();

    private readonly List<Subscription> _subscriptions = new();
    private IntPtr _manager;
    private IntPtr _delegate;
    private TaskCompletionSource<bool>? _authorizationAnswer;
    private TaskCompletionSource<GeoLocation?>? _fixAnswer;
    private bool _watching;
    // The delegate's context: held for as long as the manager, which is as long as this capability.
    private GCHandle _context;

    public bool IsAvailable
    {
        get
        {
            dlopen(CoreLocationLib, 2);
            return objc_getClass("CLLocationManager") != IntPtr.Zero;
        }
    }

    public PermissionState Permission => Status() switch
    {
        StatusNotDetermined => PermissionState.NotDetermined,
        StatusRestricted or StatusDenied => PermissionState.Denied,
        _ => PermissionState.Granted,
    };

    public async ValueTask<GeoLocation?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return null;

        if (Status() == StatusNotDetermined && !await AskAsync(cancellationToken)) return null;
        if (Permission != PermissionState.Granted) return null;

        TaskCompletionSource<GeoLocation?> answer;
        lock (Gate)
        {
            answer = _fixAnswer ??= new TaskCompletionSource<GeoLocation?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            SendVoid(Manager(), Sel("requestLocation"));
        }

        var winner = await Task.WhenAny(answer.Task, Task.Delay(FixTimeout, cancellationToken));
        lock (Gate) _fixAnswer = null;
        return winner == answer.Task ? answer.Task.Result : null;
    }

    public IDisposable Subscribe(Action<GeoLocation> onChanged)
    {
        var subscription = new Subscription(this, onChanged);
        lock (Gate)
        {
            _subscriptions.Add(subscription);
            if (_subscriptions.Count == 1 && Permission == PermissionState.Granted)
            {
                _watching = true;
                SendVoid(Manager(), Sel("startUpdatingLocation"));
            }
        }
        return subscription;
    }

    private async Task<bool> AskAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> answer;
        lock (Gate)
        {
            answer = _authorizationAnswer ??= new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            SendVoid(Manager(), Sel("requestWhenInUseAuthorization"));
        }
        var winner = await Task.WhenAny(answer.Task, Task.Delay(FixTimeout, cancellationToken));
        lock (Gate) _authorizationAnswer = null;
        return winner == answer.Task && answer.Task.Result;
    }

    private int Status()
    {
        if (!IsAvailable) return StatusDenied;
        // The INSTANCE property (macOS 11 / iOS 14); the class method it replaced is deprecated.
        return (int)SendLong(Manager(), Sel("authorizationStatus"));
    }

    private IntPtr Manager()
    {
        lock (Gate)
        {
            if (_manager != IntPtr.Zero) return _manager;
            dlopen(CoreLocationLib, 2);
            _manager = Send(Send(objc_getClass("CLLocationManager"), Sel("alloc")), Sel("init"));
            _context = GCHandle.Alloc(this);
            _delegate = DelegateClass.Value.New(_context);
            SendVoid(_manager, Sel("setDelegate:"), _delegate);
            return _manager;
        }
    }

    // ---- the runtime delegate ----------------------------------------------------------------

    private static readonly Lazy<ObjCDelegateClass> DelegateClass = new(DefineDelegateClass);

    // Static entry points (#795): a device build has no JIT to make a wrapper for a delegate. Each
    // finds its capability in the delegate object it was called on.
    private static unsafe ObjCDelegateClass DefineDelegateClass() => ObjCDelegateClass.Define("EQLocationDelegate",
        ("locationManagerDidChangeAuthorization:",
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&DidChangeAuthorization, "v@:@"),
        ("locationManager:didUpdateLocations:",
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidUpdateLocations, "v@:@@"),
        ("locationManager:didFailWithError:",
            (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, void>)&DidFail, "v@:@@"));

    [UnmanagedCallersOnly]
    private static void DidChangeAuthorization(IntPtr self, IntPtr selector, IntPtr manager) =>
        DelegateClass.Value.Context<AppleLocation>(self)?.AuthorizationDidChange();

    [UnmanagedCallersOnly]
    private static void DidUpdateLocations(IntPtr self, IntPtr selector, IntPtr manager, IntPtr locations) =>
        DelegateClass.Value.Context<AppleLocation>(self)?.LocationsDidArrive(locations);

    [UnmanagedCallersOnly]
    private static void DidFail(IntPtr self, IntPtr selector, IntPtr manager, IntPtr error) =>
        DelegateClass.Value.Context<AppleLocation>(self)?.FixDidFail();

    private void AuthorizationDidChange()
    {
        TaskCompletionSource<bool>? waiting;
        bool granted;
        lock (Gate)
        {
            // CoreLocation calls this once IMMEDIATELY when the delegate is installed, with the
            // status as it already was. While the prompt is up that status is still NotDetermined —
            // which is not the user's answer, it is the absence of one, and resolving the wait on
            // it closed the ask three seconds after it opened.
            if (Status() == StatusNotDetermined) return;

            waiting = _authorizationAnswer;
            granted = Permission == PermissionState.Granted;
            // A subscription taken while the prompt was still up starts the hardware now.
            if (granted && _subscriptions.Count > 0 && !_watching)
            {
                _watching = true;
                SendVoid(Manager(), Sel("startUpdatingLocation"));
            }
        }
        waiting?.TrySetResult(granted);
    }

    private void LocationsDidArrive(IntPtr locations)
    {
        var last = Send(locations, Sel("lastObject"));
        if (last == IntPtr.Zero) return;

        var coordinate = SendCoordinate(last, Sel("coordinate"));
        var reading = new GeoLocation(coordinate.Latitude, coordinate.Longitude,
            SendDouble(last, Sel("horizontalAccuracy")));

        TaskCompletionSource<GeoLocation?>? waiting;
        Subscription[] listeners;
        lock (Gate)
        {
            waiting = _fixAnswer;
            listeners = _subscriptions.ToArray();
        }
        waiting?.TrySetResult(reading);
        foreach (var listener in listeners) listener.Notify(reading);
    }

    private void FixDidFail()
    {
        TaskCompletionSource<GeoLocation?>? waiting;
        lock (Gate) waiting = _fixAnswer;
        waiting?.TrySetResult(null);
    }

    private void Remove(Subscription subscription)
    {
        lock (Gate)
        {
            _subscriptions.Remove(subscription);
            if (_subscriptions.Count == 0 && _watching)
            {
                _watching = false;
                SendVoid(_manager, Sel("stopUpdatingLocation"));
            }
        }
    }

    private sealed class Subscription(AppleLocation owner, Action<GeoLocation> onChanged) : IDisposable
    {
        private Action<GeoLocation>? _onChanged = onChanged;

        public void Notify(GeoLocation reading) => _onChanged?.Invoke(reading);

        public void Dispose()
        {
            if (_onChanged is null) return;
            _onChanged = null;
            owner.Remove(this);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Coordinate
    {
        public double Latitude, Longitude;
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern Coordinate SendCoordinate(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern long SendLong(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr dlopen(string path, int mode);
}
