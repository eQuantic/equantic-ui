namespace eQuantic.UI.Primitives;

/// <summary>
/// Passkeys: a key pair the device keeps for one relying party, created once and then used to sign
/// that relying party's challenge, so a person signs in with the device's own lock and no password.
/// <para>
/// A contract of its own, and not a mode of <see cref="IBiometrics"/>. A biometric check answers
/// whether the person holding the device owns it and hands back nothing; a passkey is registered
/// with a server, signs the server's challenge, and its answer is the proof the server verifies.
/// </para>
/// <para>
/// The device creates and signs, and the relying party verifies, so the options and the answers are
/// the relying party's own: the W3C Web Authentication JSON that every relying-party library writes
/// and verifies. An app asks its server for the options, hands them to the device, and hands the
/// device's answer back to the server, as text, without learning a field of either.
/// </para>
/// <para>
/// Ask the server for the options when the screen opens, and call the ceremony from the press
/// itself: Safari runs one only inside the person's gesture, and a round trip between the press and
/// the call can spend it (see <see cref="PasskeyOutcome.Cancelled"/>). A challenge is good for one
/// ceremony, so ask again after each, and for a while only: a screen that keeps its options while it
/// sits idle renews them on a clock (<see cref="IClock"/>), inside the lifetime the relying party gives
/// a challenge, and starts no ceremony with options older than that, since the server would refuse the
/// answer and a passkey created for it would stay on the device unrecorded.
/// </para>
/// </summary>
public interface IPasskeys
{
    /// <summary>Whether this host can create and use a passkey at all: in a browser, a secure
    /// context with Web Authentication. When false, both ceremonies answer
    /// <see cref="PasskeyOutcome.Unavailable"/> and ask nothing of the person.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Creates a passkey from the relying party's creation options, and answers the new credential
    /// for the server to verify and store.
    /// </summary>
    /// <param name="options">The creation options as the relying party wrote them
    /// (<c>PublicKeyCredentialCreationOptionsJSON</c>), alone or under a <c>publicKey</c>
    /// member.</param>
    /// <param name="cancellationToken">Aborts the ceremony, which then answers
    /// <see cref="PasskeyOutcome.Cancelled"/>.</param>
    /// <returns>How it ended, never an exception for what the device refused, and on success the
    /// credential as <c>RegistrationResponseJSON</c>.</returns>
    ValueTask<PasskeyResult> CreateAsync(string options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs the relying party's challenge with a passkey, and answers the assertion for the server
    /// to verify.
    /// </summary>
    /// <param name="options">The request options as the relying party wrote them
    /// (<c>PublicKeyCredentialRequestOptionsJSON</c>), alone or under a <c>publicKey</c>
    /// member.</param>
    /// <param name="cancellationToken">Aborts the ceremony, which then answers
    /// <see cref="PasskeyOutcome.Cancelled"/>.</param>
    /// <returns>How it ended, never an exception for what the device refused, and on success the
    /// assertion as <c>AuthenticationResponseJSON</c>.</returns>
    ValueTask<PasskeyResult> AuthenticateAsync(string options, CancellationToken cancellationToken = default);
}
