namespace eQuantic.UI.Primitives;

/// <summary>Where the device is, as well as the platform could tell.</summary>
/// <param name="Latitude">Degrees, north positive.</param>
/// <param name="Longitude">Degrees, east positive.</param>
/// <param name="AccuracyMeters">The radius the platform is confident about — a city-scale fix says
/// hundreds, a GPS fix says single digits. Apps that draw a dot should draw this circle.</param>
public readonly record struct GeoLocation(double Latitude, double Longitude, double AccuracyMeters);

/// <summary>
/// The device's position — and the first capability where <see cref="PermissionState"/> earns its
/// three values. A photo picker runs outside the app and needs no asking; location is the app
/// itself watching where someone is, and every platform interposes a prompt.
/// <para>
/// Asking IS the permission flow, as everywhere else in this vocabulary:
/// <see cref="GetCurrentAsync"/> shows the system prompt when the answer is still
/// <see cref="PermissionState.NotDetermined"/>, and resolves null when it ends up denied. The
/// <see cref="Permission"/> property is what an app checks to explain itself — a page that finds
/// <see cref="PermissionState.Denied"/> should say how to change it in Settings instead of asking
/// again, because asking again does nothing on any platform.
/// </para>
/// </summary>
public interface ILocation
{
    /// <summary>Whether this device can know where it is at all — hardware and system service,
    /// not permission. False on a desktop with location services switched off entirely.</summary>
    bool IsAvailable { get; }

    /// <summary>The current answer to "may this app know". Never prompts — reading a property
    /// must not put a dialog on screen.</summary>
    PermissionState Permission { get; }

    /// <summary>
    /// One position, prompting first if the user was never asked. Null when permission ends up
    /// denied, when the service is off, or when no fix arrives in a sensible time — all three are
    /// "you do not get a position", and <see cref="Permission"/> says which one it was.
    /// </summary>
    ValueTask<GeoLocation?> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>Follows the position as it changes — the delivery-tracking contract, at the slow
    /// cadence the platform decides. Same lifetime rule as every stream here: dispose to stop, and
    /// the hardware powers down with the last listener.</summary>
    IDisposable Subscribe(Action<GeoLocation> onChanged);
}
