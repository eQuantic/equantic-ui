namespace eQuantic.UI.Primitives;

/// <summary>A direction with a size, in the device's own axes. X is across the screen, Y is up it,
/// Z is out of it — every platform's motion API agrees on this frame, so it crosses unchanged.</summary>
public readonly record struct MotionVector(float X, float Y, float Z);

/// <summary>
/// One sample of how the device is moving.
/// </summary>
/// <param name="Acceleration">The USER's contribution, in g — gravity already removed by the
/// platform's fusion. Holding the phone still reads (0,0,0), which is what makes shake detection a
/// threshold instead of a subtraction every app writes badly.</param>
/// <param name="Gravity">Where down is, as a unit-ish vector in g. Flat on a table reads
/// (0,0,-1); this is the input tilt UIs actually want.</param>
/// <param name="RotationRate">How fast the device is turning, in radians per second per axis.</param>
public readonly record struct MotionReading(
    MotionVector Acceleration,
    MotionVector Gravity,
    MotionVector RotationRate);

/// <summary>
/// The motion sensors — the accelerometer and gyroscope, fused by the platform into one reading.
/// <para>
/// The FIRST capability most devices simply do not have: no desktop has a gyroscope, and
/// <see cref="IsAvailable"/> false there is the design working, not a gap. A tilt interaction
/// belongs behind that check, with the pointer-based fallback as the desktop's answer.
/// </para>
/// <para>
/// Same subscription contract as <see cref="INetworkStatus"/> — an IDisposable, because a
/// component's lifetime is the subscription's lifetime — but at a very different rate: samples
/// arrive tens of times per second, on whatever thread the platform owns. A handler that does
/// more than store the reading and invalidate will drop frames; do the mathematics in Build.
/// </para>
/// </summary>
public interface IMotionSensor
{
    /// <summary>Whether this device can answer at all. False on every desktop, and on the rare
    /// phone without an accelerometer.</summary>
    bool IsAvailable { get; }

    /// <summary>Starts the stream. The platform picks the exact rate (targeting UI use, roughly
    /// 60 Hz); disposing stops this listener, and the hardware powers down with the last one —
    /// a running gyroscope is a battery cost an idle page should not carry.</summary>
    IDisposable Subscribe(Action<MotionReading> onReading);
}

/// <summary>
/// The answer for a device with no motion hardware: never available, and a subscription that
/// never speaks. Registered by the desktop shells so a page that takes an
/// <see cref="IMotionSensor"/> still constructs — the check is `IsAvailable`, not a null test,
/// and the fallback UI is the page's own.
/// </summary>
public sealed class AbsentMotionSensor : IMotionSensor
{
    public bool IsAvailable => false;

    public IDisposable Subscribe(Action<MotionReading> onReading) => Nothing.Instance;

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();
        public void Dispose() { }
    }
}
