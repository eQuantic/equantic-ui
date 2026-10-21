namespace eQuantic.UI.Primitives;

/// <summary>How a passkey ceremony ended, and what it produced.</summary>
/// <param name="Outcome">How it ended; <see cref="PasskeyOutcome.Unavailable"/> in a result nobody
/// filled in.</param>
/// <param name="Credential">
/// When <paramref name="Outcome"/> is <see cref="PasskeyOutcome.Succeeded"/>, the credential as the
/// W3C Web Authentication JSON (<c>RegistrationResponseJSON</c> from a creation,
/// <c>AuthenticationResponseJSON</c> from a signature), for the relying party to verify; null
/// otherwise. The app hands it to its server as it is: the server's Web Authentication library
/// reads it, and the app never needs a field of it.
/// </param>
[ZeroConstructs("primitive-values.ts: `constructor(outcome = 'unavailable', credential = null)`.")]
public readonly record struct PasskeyResult(PasskeyOutcome Outcome, string? Credential);
