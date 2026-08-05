using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// The native realization of <see cref="IThemeController"/>. Registered by the builder (TryAdd, so
/// an app's own controller wins), ATTACHED by the runner once the window's host exists — services
/// are constructed before any window is, so the sink arrives late by design. An Apply before the
/// attach just records the mode; the runner seeds the host with whatever was recorded.
/// </summary>
public sealed class PhotonThemeController : IThemeController
{
    private Action<ThemeMode>? _sink;

    public ThemeMode Mode { get; private set; } = ThemeMode.Light;

    public void Apply(ThemeMode mode)
    {
        Mode = mode;
        _sink?.Invoke(mode);
    }

    /// <summary>Called by the runner: the initial mode the window opened with, and the hand that
    /// actually flips it (set the host's mode, invalidate the frame).</summary>
    public void Attach(ThemeMode initial, Action<ThemeMode> sink)
    {
        Mode = initial;
        _sink = sink;
    }
}
