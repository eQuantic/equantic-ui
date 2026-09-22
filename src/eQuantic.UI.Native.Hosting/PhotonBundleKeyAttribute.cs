namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// One fact about the app BUNDLE — the answers the operating system reads before a line of the app
/// runs: what it is called in Finder's Get Info, which category it belongs to, the oldest macOS it
/// will start on, whether it takes a Dock tile at all, the URLs it answers to.
/// <para>
/// Written by the source generator from <c>builder.Bundle.…</c>, exactly as
/// <see cref="PhotonCapabilityAttribute"/> is written from <c>builder.Capabilities.…</c>. An app
/// author declares the fact once in C#; the SDK writes the Info.plist. Reaching for this attribute
/// by hand is legal and occasionally right, but the fluent surface is the path.
/// </para>
/// <para>
/// The escape hatch is deliberate and narrow: Apple owns this key space and adds to it, so the
/// facts that matter are typed on the builder (<c>Copyright</c>, <c>Category</c>, <c>Agent</c>) and
/// anything else is still sayable — in C#, in one line, without opening a plist.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PhotonBundleKeyAttribute(string key, string value, PhotonBundleValueKind kind)
    : Attribute
{
    /// <summary>Apple's key, e.g. <c>NSHumanReadableCopyright</c>. Empty for a
    /// <see cref="PhotonBundleValueKind.UrlScheme"/>, which is not written under a key of its own.</summary>
    public string Key { get; } = key;

    /// <summary>The value, as text. <see cref="Kind"/> decides how it is written.</summary>
    public string Value { get; } = value;

    /// <summary>How to write it.</summary>
    public PhotonBundleValueKind Kind { get; } = kind;
}
