using System.Runtime.InteropServices;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// CoreMotion's CMMotionManager, through the device-motion feed — the platform's own fusion of
/// accelerometer and gyroscope, which is what hands over user acceleration and gravity as SEPARATE
/// vectors instead of leaving every app to subtract badly.
/// <para>
/// Shared by the Apple shells but only the phone answers: CoreMotion exists on macOS and reports
/// no device motion there, so <see cref="IsAvailable"/> is the framework's own answer rather than
/// a platform check written here. The handler block is asynchronous and repeated — the exact
/// lifetime the GLOBAL ObjCBlock exists for.
/// </para>
/// <para>
/// The hardware runs while anyone listens and powers down with the last subscriber: a gyroscope
/// left running is a battery cost an idle page should not carry.
/// </para>
/// </summary>
public sealed partial class AppleMotionSensor : IMotionSensor
{
    private readonly Lock _gate = new();
    private readonly List<Subscription> _subscriptions = new();

    private IntPtr _manager;
    private IntPtr _queue;
    private ObjCBlock? _handler;
    private MotionHandler? _callback;

    private delegate void MotionHandler(IntPtr block, IntPtr motion, IntPtr error);

    /// <summary>Three doubles by value — the CMAcceleration/CMRotationRate layout. Marshalled the
    /// same way CGRect already is: the runtime handles the large-struct return convention.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Triple
    {
        public double X, Y, Z;
    }

    public bool IsAvailable
    {
        get
        {
            var manager = Manager();
            return manager != IntPtr.Zero && SendBool(manager, Sel("isDeviceMotionAvailable"));
        }
    }

    public IDisposable Subscribe(Action<MotionReading> onReading)
    {
        var subscription = new Subscription(this, onReading);
        lock (_gate)
        {
            _subscriptions.Add(subscription);
            if (_subscriptions.Count == 1) Start();
        }
        return subscription;
    }

    private IntPtr Manager()
    {
        lock (_gate)
        {
            if (_manager != IntPtr.Zero) return _manager;
            // The class only exists once its framework is in the process; loading is idempotent.
            dlopen("/System/Library/Frameworks/CoreMotion.framework/CoreMotion", 2 /* RTLD_NOW */);
            var cls = objc_getClass("CMMotionManager");
            if (cls == IntPtr.Zero) return IntPtr.Zero;   // framework not present on this platform
            _manager = Send(Send(cls, Sel("alloc")), Sel("init"));
            return _manager;
        }
    }

    private void Start()
    {
        var manager = Manager();
        if (manager == IntPtr.Zero || !SendBool(manager, Sel("isDeviceMotionAvailable"))) return;

        // ~60 Hz: the UI rate. The platform clamps to what the hardware can do.
        SendVoid(manager, Sel("setDeviceMotionUpdateInterval:"), 1.0 / 60.0);

        _queue = Send(Send(objc_getClass("NSOperationQueue"), Sel("alloc")), Sel("init"));
        _callback = OnMotion;
        _handler = new ObjCBlock(_callback);
        SendVoid(manager, Sel("startDeviceMotionUpdatesToQueue:withHandler:"), _queue, _handler.Handle);
    }

    private void Stop()
    {
        if (_manager != IntPtr.Zero) SendVoid(_manager, Sel("stopDeviceMotionUpdates"));
    }

    private void OnMotion(IntPtr block, IntPtr motion, IntPtr error)
    {
        if (motion == IntPtr.Zero) return;

        var reading = new MotionReading(
            AsVector(SendTriple(motion, Sel("userAcceleration"))),
            AsVector(SendTriple(motion, Sel("gravity"))),
            AsVector(SendTriple(motion, Sel("rotationRate"))));

        Subscription[] listeners;
        lock (_gate) listeners = _subscriptions.ToArray();
        foreach (var listener in listeners) listener.Notify(reading);
    }

    private static MotionVector AsVector(Triple t) => new((float)t.X, (float)t.Y, (float)t.Z);

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
            if (_subscriptions.Count == 0) Stop();
        }
    }

    private sealed class Subscription(AppleMotionSensor owner, Action<MotionReading> onReading) : IDisposable
    {
        private Action<MotionReading>? _onReading = onReading;

        public void Notify(MotionReading reading) => _onReading?.Invoke(reading);

        public void Dispose()
        {
            if (_onReading is null) return;
            _onReading = null;
            owner.Remove(this);
        }
    }

    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static partial Triple SendTriple(IntPtr receiver, IntPtr selector);

    [LibraryImport("/usr/lib/libSystem.B.dylib", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr dlopen(string path, int mode);
}
