namespace eQuantic.UI;

/// <summary>
/// What a bundle fact's KEY and its URL SCHEME look like once accepted.
///
/// <para>
/// Two places have to agree, and they live in assemblies that cannot reference each other: the
/// builder an app calls (<c>PhotonBundleBuilder</c>, net10.0) and the generator that reads that
/// call (netstandard2.0, because that is what Roslyn loads analyzers as). They agreed by both
/// calling <c>Trim()</c> — right up until one of them did and the other did not.
/// </para>
/// <para>
/// The drift is invisible in C#: <c>builder.Bundle.Key(" LSUIElement ", …)</c> compiles, the
/// builder's own copy holds the trimmed key, and the GENERATED attribute — which is the one the
/// build actually reads — holds the untrimmed one. What ships is a plist key macOS does not
/// recognise, in an app whose source looks correct. Three separate review comments on one pull
/// request were this same drift at three call sites, which is the shape of a rule that should not
/// be written more than once.
/// </para>
/// </summary>
internal static class BundleFactRule
{
    /// <summary>
    /// The key as it should be stored, or null when there is nothing to store. Whitespace around an
    /// Apple key is never meaningful, and an empty key names nothing — both are dropped rather than
    /// reported, because a value that CAN be evaluated is not the problem EQ3005 describes.
    /// </summary>
    internal static string? Key(string? key) =>
        key?.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    /// <summary>
    /// A URL scheme as it should be stored, or null when there is nothing to store. Same rule as a
    /// key, and for one more reason: two spellings of one scheme defeat the de-duplication that
    /// collects them into a single <c>CFBundleURLTypes</c> array.
    /// </summary>
    internal static string? Scheme(string? scheme) => Key(scheme);
}
