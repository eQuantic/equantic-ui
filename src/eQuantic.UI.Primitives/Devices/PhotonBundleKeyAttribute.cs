namespace eQuantic.UI.Primitives;

/// <summary>How a value is written into the app's manifest — a plist is TYPED, and the reader that
/// asks it for a boolean gets the wrong answer from <c>&lt;string&gt;true&lt;/string&gt;</c>.</summary>
public enum PhotonBundleValueKind
{
    /// <summary>A plain string value.</summary>
    Text,

    /// <summary>A real boolean.</summary>
    Flag,

    /// <summary>A URL scheme this app answers to. These do not each get a key: they are collected
    /// into the one <c>CFBundleURLTypes</c> array the system reads, which is why the kind exists
    /// rather than the caller building the array.</summary>
    UrlScheme,
}

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

/// <summary>
/// The Finder and App Store category an app declares, as C# rather than as
/// <c>public.app-category.utilities</c> — one more platform string that is a typo away from being
/// silently ignored, and that nobody should have to look up.
/// <para>
/// Apple's list is longer than this (it includes every game genre); these are the ones a Photon
/// desktop app reaches for. Anything else is one line away:
/// <c>builder.Bundle.Key("LSApplicationCategoryType", "public.app-category.puzzle-games")</c>.
/// </para>
/// </summary>
public enum AppCategory
{
    /// <summary>Not declared.</summary>
    None,

    /// <summary>Utilities — the default home of a tool that does one job well.</summary>
    Utilities,

    /// <summary>Developer tools.</summary>
    DeveloperTools,

    /// <summary>Productivity.</summary>
    Productivity,

    /// <summary>Business.</summary>
    Business,

    /// <summary>Finance.</summary>
    Finance,

    /// <summary>Graphics and design.</summary>
    GraphicsDesign,

    /// <summary>Photography.</summary>
    Photography,

    /// <summary>Music.</summary>
    Music,

    /// <summary>Video.</summary>
    Video,

    /// <summary>Education.</summary>
    Education,

    /// <summary>Social networking.</summary>
    SocialNetworking,

    /// <summary>News.</summary>
    News,

    /// <summary>Reference.</summary>
    Reference,

    /// <summary>Healthcare and fitness.</summary>
    HealthcareFitness,

    /// <summary>Games.</summary>
    Games,
}
