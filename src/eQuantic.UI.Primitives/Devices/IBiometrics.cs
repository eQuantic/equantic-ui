namespace eQuantic.UI.Primitives;

/// <summary>
/// How an attempt at the fingerprint or face reader ended. Not a boolean, and for the same reason a
/// permission is not: "did not succeed" hides four different situations, and an app that treats
/// them alike either nags someone whose device has no reader or gives up on someone who simply
/// mistyped.
/// </summary>
public enum BiometricResult
{
    /// <summary>The user proved who they are.</summary>
    Succeeded,

    /// <summary>The reader ran and said no — a wrong finger, a face it did not know. Trying again
    /// is reasonable; the system usually offers that itself before returning here.</summary>
    Failed,

    /// <summary>The user backed out. Not a failure, and never something to retry unasked.</summary>
    Cancelled,

    /// <summary>The user asked for the other way in — a passcode, a password. The app should let
    /// them: refusing is how a security feature becomes the reason someone stops using an app.</summary>
    FallbackRequested,

    /// <summary>There is a reader, but nothing is enrolled on it. The fix is in Settings, and an
    /// app that says so is more useful than one that says "failed".</summary>
    NotEnrolled,

    /// <summary>No reader, or policy forbids it. Nothing the user can do here.</summary>
    Unavailable,
}

/// <summary>
/// The fingerprint or face reader — proving the person holding the device is the person who owns
/// it, without the app ever seeing anything about them.
/// <para>
/// The contract is deliberately narrow: it asks, and it answers how it went. It hands back no
/// token, no biometric data and no cryptographic material, because it has none to hand back — the
/// system checks locally and says yes or no. An app that needs a signed proof is doing key
/// management, which is a different thing entirely and not this.
/// </para>
/// </summary>
public interface IBiometrics
{
    /// <summary>Whether this device has a reader with something enrolled on it. False covers both
    /// "no hardware" and "nothing enrolled" — <see cref="AuthenticateAsync"/> tells them apart when
    /// the difference matters.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Asks. <paramref name="reason"/> is shown by the system in its own prompt and is not
    /// optional — it is what the user reads while deciding whether to touch the sensor, and both
    /// Apple and Android put it on screen verbatim.
    /// </summary>
    ValueTask<BiometricResult> AuthenticateAsync(string reason,
        CancellationToken cancellationToken = default);
}
