using eQuantic.UI;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// What the operating system reads about this app before a line of it runs — its copyright line in
/// Get Info, its category, the oldest macOS it will start on, whether it takes a Dock tile, the
/// URLs it answers to.
/// <para>
/// Stated where every other app fact is stated: on the builder, in <c>Program.cs</c>, in C#. The
/// generator turns these calls into assembly declarations and the SDK writes the Info.plist, so an
/// app author never opens one — which is the promise the whole SDK is making, and the reason a
/// key that Apple spells <c>LSUIElement</c> is called <see cref="Agent"/> here.
/// </para>
/// <code>
/// builder.Bundle
///        .Copyright("© 2026 Acme")
///        .Category(AppCategory.Utilities)
///        .MinimumSystemVersion("13.0")
///        .UrlScheme("acme");
/// </code>
/// </summary>
public sealed class PhotonBundleBuilder
{
    private readonly Dictionary<string, (string Value, PhotonBundleValueKind Kind)> _keys = new(StringComparer.Ordinal);
    private readonly List<string> _schemes = [];

    /// <summary>What this app declared, keyed by Apple's key.</summary>
    public IReadOnlyDictionary<string, (string Value, PhotonBundleValueKind Kind)> Declared => _keys;

    /// <summary>The URL schemes this app answers to.</summary>
    public IReadOnlyList<string> UrlSchemes => _schemes;

    /// <summary>The line Finder shows under Copyright in Get Info.</summary>
    public PhotonBundleBuilder Copyright(string notice) => Key("NSHumanReadableCopyright", notice);

    /// <summary>Where this app belongs — Finder groups by it, and the App Store requires it.</summary>
    public PhotonBundleBuilder Category(AppCategory category) =>
        AppleCategory(category) is { } value ? Key("LSApplicationCategoryType", value) : this;

    /// <summary>
    /// The oldest macOS this app will start on. The SDK's own floor is the oldest release where
    /// every API the Metal backend uses exists; declare a newer one when YOUR code needs it, and
    /// the system refuses to launch the app on anything older instead of crashing inside it.
    /// </summary>
    public PhotonBundleBuilder MinimumSystemVersion(string version) =>
        Key("LSMinimumSystemVersion", version);

    /// <summary>
    /// This app lives in the menu bar and takes no Dock tile and no menu bar of its own — a status
    /// item, a launcher, a background agent with a preferences window.
    /// <para>Apple calls the key <c>LSUIElement</c>, which says nothing about what it does.</para>
    /// </summary>
    public PhotonBundleBuilder Agent() => Flag("LSUIElement", true);

    /// <summary>
    /// A URL scheme this app answers to — <c>acme://…</c> opens it and hands it the URL. Say it as
    /// many times as there are schemes; they are collected into the one array the system reads.
    /// </summary>
    public PhotonBundleBuilder UrlScheme(string scheme)
    {
        // Refused by NAME rather than dropped: a scheme the system would never route — "acme://", a
        // digit first, a space inside — is a mistake in THIS line, and the place to hear about it is
        // here, not a CFBundleURLTypes entry that nothing ever matches. The generator sees the same
        // call and drops it; the app never starts to find out, because this throws first.
        var accepted = BundleFactRule.Scheme(scheme)
            ?? throw new ArgumentException($"\"{scheme}\" is not a URL scheme. Give the NAME alone — "
                + "\"acme\", not \"acme://\".", nameof(scheme));
        if (!_schemes.Contains(accepted, StringComparer.Ordinal)) _schemes.Add(accepted);
        return this;
    }

    /// <summary>Any key by name, because the key space is Apple's and it grows. Prefer the named
    /// methods where one exists: they say what the key MEANS, which a key never does.</summary>
    public PhotonBundleBuilder Key(string key, string value)
    {
        if (BundleFactRule.Key(key) is { } named) _keys[named] = (value, PhotonBundleValueKind.Text);
        return this;
    }

    /// <summary>Any boolean key by name. Separate from <see cref="Key(string,string)"/> because a
    /// plist is typed: a reader asking for a boolean gets the wrong answer from a string.</summary>
    public PhotonBundleBuilder Flag(string key, bool value)
    {
        if (BundleFactRule.Key(key) is { } named)
            _keys[named] = (value ? "true" : "false", PhotonBundleValueKind.Flag);
        return this;
    }

    /// <summary>Apple's spelling of a category. Spelled here and in the generator, which sees the
    /// CALL and not this method body — the same split the entitlements builder lives with.</summary>
    internal static string? AppleCategory(AppCategory category) => category switch
    {
        AppCategory.Utilities => "public.app-category.utilities",
        AppCategory.DeveloperTools => "public.app-category.developer-tools",
        AppCategory.Productivity => "public.app-category.productivity",
        AppCategory.Business => "public.app-category.business",
        AppCategory.Finance => "public.app-category.finance",
        AppCategory.GraphicsDesign => "public.app-category.graphics-design",
        AppCategory.Photography => "public.app-category.photography",
        AppCategory.Music => "public.app-category.music",
        AppCategory.Video => "public.app-category.video",
        AppCategory.Education => "public.app-category.education",
        AppCategory.SocialNetworking => "public.app-category.social-networking",
        AppCategory.News => "public.app-category.news",
        AppCategory.Reference => "public.app-category.reference",
        AppCategory.HealthcareFitness => "public.app-category.healthcare-fitness",
        AppCategory.Games => "public.app-category.games",
        _ => null,
    };
}
