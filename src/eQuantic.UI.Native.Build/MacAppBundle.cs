using eQuantic.UI.Codegen;

namespace eQuantic.UI.Build;

/// <summary>
/// The <c>.app</c> a macOS head ships as. Everything a build already produces goes inside; what the
/// system reads about the app — its name, its identifier, its icon, that it is a windowed
/// application rather than a background process — goes in the Info.plist beside it.
/// <para>
/// Without the bundle the head is a PROCESS with a window: no icon in the Dock, no name in the menu
/// bar, no entry in the app switcher, and nothing that can be launched, scripted or granted
/// permissions the way a Mac application is. The app author writes none of this — the same
/// <c>Assets/AppIcon</c> that feeds iOS and Android feeds it here too.
/// </para>
/// </summary>
public static class MacAppBundle
{
    public static void Write(string bundlePath, string executable, string displayName,
        string identifier, string version, string? icnsSource, string? capabilitiesAssembly = null)
    {
        // What the app SAID about itself, read off the same assembly the capabilities come from.
        // Read before anything is written because a declared fact OVERRIDES the framework's
        // default: an app that states LSMinimumSystemVersion 13.0 means it, and a default written
        // first and overwritten later is a plist with the key twice.
        var declared = capabilitiesAssembly is not null
            ? BundleManifest.Read(capabilitiesAssembly)
            : [];
        var stated = declared
            .Where(fact => fact.Kind != BundleManifest.ValueKind.UrlScheme)
            .ToDictionary(fact => fact.Key, fact => fact, StringComparer.Ordinal);
        var schemes = declared
            .Where(fact => fact.Kind == BundleManifest.ValueKind.UrlScheme)
            .Select(fact => fact.Value)
            .ToList();

        var contents = Path.Combine(bundlePath, "Contents");
        var resources = Path.Combine(contents, "Resources");
        Directory.CreateDirectory(Path.Combine(contents, "MacOS"));
        Directory.CreateDirectory(resources);

        var icon = icnsSource is not null && File.Exists(icnsSource) ? "AppIcon" : null;
        if (icon is not null) File.Copy(icnsSource!, Path.Combine(resources, "AppIcon.icns"), overwrite: true);

        File.WriteAllText(Path.Combine(contents, "Info.plist"), PropertyListWriter.Document(plist =>
        {
            plist.String("CFBundleName", displayName)
                 .String("CFBundleDisplayName", displayName)
                 .String("CFBundleIdentifier", identifier)
                 .String("CFBundleExecutable", executable)
                 .String("CFBundlePackageType", "APPL")
                 .String("CFBundleShortVersionString", AppleVersion(version))
                 .String("CFBundleVersion", AppleVersion(version))
                 // The one that decides whether this is an APPLICATION: without it the process gets
                 // no Dock tile, no menu bar and no place in the app switcher, however many windows
                 // it opens.
                 .Bool("LSUIElement", Flag("LSUIElement", @default: false))
                 // 11.0 is where every API the Metal backend uses is present. An app that needs a
                 // newer floor says so, and the system then refuses to LAUNCH it on anything older
                 // instead of letting it crash inside an API that is not there.
                 .String("LSMinimumSystemVersion", Text("LSMinimumSystemVersion", "11.0"))
                 .Bool("NSHighResolutionCapable", true)
                 .Bool("NSSupportsAutomaticGraphicsSwitching", true);
            if (icon is not null) plist.String("CFBundleIconFile", icon);

            // The URLs this app answers to. One array with one type in it: a Photon app declares
            // schemes, not document types, and CFBundleURLName is the bundle id by convention so
            // two apps claiming the same scheme are at least distinguishable in the registry.
            if (schemes.Count > 0)
            {
                plist.DictionaryArray("CFBundleURLTypes", entry => entry
                    .String("CFBundleURLName", identifier)
                    .StringArray("CFBundleURLSchemes", [.. schemes]));
            }

            // And everything else the app stated, in key order. The framework's own keys above are
            // already resolved against these, so what lands here is what the SDK had no opinion
            // about — a copyright line, a category, whatever Apple adds next.
            foreach (var fact in stated.Values.OrderBy(fact => fact.Key, StringComparer.Ordinal))
            {
                if (Owned.Contains(fact.Key)) continue;
                if (fact.Kind == BundleManifest.ValueKind.Flag)
                    plist.Bool(fact.Key, fact.Value == "true");
                else
                    plist.String(fact.Key, fact.Value);
            }

            // What the app DECLARED, straight off its compiled assembly — the same attributes the
            // iOS partial plist is written from. The Mac spelling of the location key differs from
            // the phones' (Apple's names are historical, not derivable), so both are written: a
            // key the platform does not read is inert, a missing one silently suppresses the
            // system prompt.
            if (capabilitiesAssembly is not null && File.Exists(capabilitiesAssembly))
            {
                foreach (var (capability, reason) in CapabilityManifest.Read(capabilitiesAssembly))
                {
                    foreach (var key in AppleKeys(capability)) plist.String(key, reason);
                }
            }
        }));

        // The Finder reads this to know the directory IS a bundle even before the plist is parsed.
        File.WriteAllText(Path.Combine(contents, "PkgInfo"), "APPL????");
        return;

        string Text(string key, string @default) =>
            stated.TryGetValue(key, out var fact) ? fact.Value : @default;

        bool Flag(string key, bool @default) =>
            stated.TryGetValue(key, out var fact) ? fact.Value == "true" : @default;
    }

    /// <summary>
    /// The keys the framework writes itself, above. A declared value for one of these is honoured
    /// where it is written and must not be written AGAIN at the end — a plist with a key twice is
    /// not an error anywhere, it simply means whichever the parser reaches last, which is how a
    /// declaration silently loses to a default.
    /// </summary>
    private static readonly HashSet<string> Owned = new(StringComparer.Ordinal)
    {
        "LSUIElement", "LSMinimumSystemVersion",
    };

    /// <summary>
    /// The version, in the only shape Apple accepts: one to three integers separated by dots.
    /// <para>
    /// A .NET version is routinely not that. This repository's own is <c>0.2.0-preview.46</c>, and
    /// it went into the plist verbatim — which is a bundle the App Store rejects and notarization
    /// refuses, reported in a vocabulary ("CFBundleShortVersionString must be a period-separated
    /// list of at most three non-negative integers") that names nothing you wrote. The prerelease
    /// tag is dropped rather than mangled, and the build says so once, because a version that
    /// silently becomes a different version is worse than either.
    /// </para>
    /// </summary>
    public static string AppleVersion(string version)
    {
        var numeric = new List<string>();
        foreach (var part in (version ?? "").Split('.'))
        {
            // The NUMERIC PREFIX of each component, and the version ends at the first component
            // that has anything after it: "0.2.0-preview.46" is 0.2.0 and "1.0.0+sha.abc" is 1.0.0.
            // Reading the component as all-or-nothing instead loses the third number entirely,
            // which is how this method first answered "0.2" — caught by its own test.
            var digits = 0;
            while (digits < part.Length && char.IsAsciiDigit(part[digits])) digits++;
            if (digits == 0) break;

            var value = part[..digits].TrimStart('0');
            numeric.Add(value.Length > 0 ? value : "0");
            if (numeric.Count == 3 || digits < part.Length) break;
        }

        return numeric.Count > 0 ? string.Join('.', numeric) : "1.0.0";
    }

    private static IEnumerable<string> AppleKeys(string capability)
    {
        if (CapabilityManifest.AppleKey(capability) is { } shared) yield return shared;
        if (capability == "Location") yield return "NSLocationUsageDescription";   // the Mac's own spelling
    }
}
