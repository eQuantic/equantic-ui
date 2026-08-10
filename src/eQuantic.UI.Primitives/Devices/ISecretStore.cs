namespace eQuantic.UI.Primitives;

/// <summary>
/// The platform's SECRET store — a token, a refresh credential, a passphrase. Keychain on Apple,
/// Keystore-backed storage on Android.
/// <para>
/// A separate type from <see cref="IAppStorage"/>, and that is the entire point. Putting a token in
/// <c>localStorage</c> is the most-made security mistake on the web, and an API where safety is a
/// <c>secure: true</c> parameter is an API where safety is one forgotten argument away. Here the
/// choice cannot be made by omission: a secret needs a different service, and asking for it is a
/// decision somebody made on purpose.
/// </para>
/// <para>
/// THE BROWSER HAS NO VAULT, so on the web this resolves to NULL — the same way the framework
/// reports every capability a host does not have, rather than pretending with a store that any
/// script on the origin can read. An app that reaches for it on the web has to answer the question
/// it was avoiding: keep the secret on the server and hand the browser a session it can only spend,
/// or accept that what it is storing was never really a secret.
/// </para>
/// <para>
/// Values may be evicted by the platform — a Keychain item does not survive every restore, and the
/// user can revoke it. Treat a null read as "sign in again", never as an error.
/// </para>
/// </summary>
public interface ISecretStore
{
    /// <summary>The secret under <paramref name="key"/>, or null when there is none to read —
    /// including when the platform has since dropped it.</summary>
    string? Get(string key);

    /// <summary>Stores <paramref name="value"/> in the platform's vault.</summary>
    void Set(string key, string value);

    /// <summary>Removes the secret. This is what a sign-out calls.</summary>
    void Remove(string key);
}
