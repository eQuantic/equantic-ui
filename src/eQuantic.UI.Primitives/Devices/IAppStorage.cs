namespace eQuantic.UI.Primitives;

/// <summary>
/// The app's own small, durable key/value store — the place a preference, a last-opened document
/// or a dismissed-banner flag lives between launches.
/// <para>
/// One signature over each platform's native mechanism: <c>localStorage</c> on the web,
/// <c>NSUserDefaults</c> on Apple, <c>SharedPreferences</c> on Android. Nothing invented and
/// nothing bundled — the same rule the rest of the framework follows, so an app's preferences end
/// up where that platform's tooling, backup and uninstall already expect to find them.
/// </para>
/// <para>
/// STRINGS ONLY, and deliberately. Every backing store here is a string map underneath; a typed
/// surface would be four serializers to keep identical, and the first disagreement between them is
/// a value that writes on one platform and cannot be read on another. Serialize in the app, where
/// the format is yours.
/// </para>
/// <para>
/// NOT FOR SECRETS. A token belongs in <see cref="ISecretStore"/>, which is a different type
/// precisely so that the choice cannot be made by forgetting a parameter. This one is readable by
/// anything running in the same origin or the same app sandbox.
/// </para>
/// <para>
/// A CLIENT capability, and this is the sharp edge worth knowing before the first use: THERE IS NO
/// SERVER REALIZATION, so during SSR the service resolves to null and reads answer nothing. A page
/// that branches its tree on stored state renders one thing on the server and another in the
/// browser, and the reconciler will faithfully reconcile the difference in front of the reader.
/// Read it in an event or after mount, not while building.
/// </para>
/// </summary>
public interface IAppStorage
{
    /// <summary>The value stored under <paramref name="key"/>, or null when nothing is.</summary>
    string? Get(string key);

    /// <summary>Stores <paramref name="value"/>, replacing whatever was there.</summary>
    void Set(string key, string value);

    /// <summary>Forgets <paramref name="key"/>. Forgetting what was never there is not an error.</summary>
    void Remove(string key);
}
