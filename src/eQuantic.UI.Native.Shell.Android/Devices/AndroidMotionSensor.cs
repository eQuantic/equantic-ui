using Android.Content;
using Android.Hardware;
using Android.Runtime;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// SensorManager, fused the way CoreMotion fuses: LinearAcceleration (gravity already removed),
/// Gravity, and Gyroscope — three listeners folded into one reading, because the contract promises
/// what apps want, not what the hardware separates.
/// <para>
/// Android reports acceleration in m/s²; the contract speaks g (CoreMotion's unit), so the values
/// divide by standard gravity on the way through. One unit across platforms, converted where the
/// difference lives.
/// </para>
/// </summary>
internal sealed class AndroidMotionSensor : IMotionSensor
{
    private const float StandardGravity = 9.80665f;

    private readonly Lock _gate = new();
    private readonly List<Subscription> _subscriptions = new();
    private Listener? _listener;

    private static SensorManager? Manager =>
        PhotonActivity.Current?.GetSystemService(Context.SensorService) as SensorManager;

    public bool IsAvailable =>
        Manager?.GetDefaultSensor(SensorType.Accelerometer) is not null;

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

    private void Start()
    {
        if (Manager is not { } manager) return;
        _listener = new Listener(this);
        foreach (var type in new[]
            { SensorType.LinearAcceleration, SensorType.Gravity, SensorType.Gyroscope })
        {
            if (manager.GetDefaultSensor(type) is { } sensor)
                manager.RegisterListener(_listener, sensor, SensorDelay.Game); // ~50 Hz
        }
    }

    private void Stop()
    {
        if (_listener is { } listener) Manager?.UnregisterListener(listener);
        _listener = null;
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
            if (_subscriptions.Count == 0) Stop();
        }
    }

    private sealed class Listener(AndroidMotionSensor owner) : Java.Lang.Object, ISensorEventListener
    {
        private MotionVector _acceleration;
        private MotionVector _gravity;
        private MotionVector _rotation;

        public void OnSensorChanged(SensorEvent? e)
        {
            if (e?.Sensor is null || e.Values is not { Count: >= 3 } v) return;
            switch (e.Sensor.Type)
            {
                case SensorType.LinearAcceleration:
                    _acceleration = new MotionVector(
                        v[0] / StandardGravity, v[1] / StandardGravity, v[2] / StandardGravity);
                    break;
                case SensorType.Gravity:
                    _gravity = new MotionVector(
                        v[0] / StandardGravity, v[1] / StandardGravity, v[2] / StandardGravity);
                    break;
                case SensorType.Gyroscope:
                    _rotation = new MotionVector(v[0], v[1], v[2]);
                    break;
                default:
                    return;
            }

            var reading = new MotionReading(_acceleration, _gravity, _rotation);
            Subscription[] listeners;
            lock (owner._gate) listeners = owner._subscriptions.ToArray();
            foreach (var listener in listeners) listener.Notify(reading);
        }

        public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy) { }
    }

    private sealed class Subscription(AndroidMotionSensor owner, Action<MotionReading> onReading) : IDisposable
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
}
