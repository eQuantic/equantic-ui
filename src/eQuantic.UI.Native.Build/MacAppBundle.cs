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
                 .String("CFBundleShortVersionString", version)
                 .String("CFBundleVersion", version)
                 // The one that decides whether this is an APPLICATION: without it the process gets
                 // no Dock tile, no menu bar and no place in the app switcher, however many windows
                 // it opens.
                 .Bool("LSUIElement", false)
                 // 11.0 is where every API the Metal backend uses is present.
                 .String("LSMinimumSystemVersion", "11.0")
                 .Bool("NSHighResolutionCapable", true)
                 .Bool("NSSupportsAutomaticGraphicsSwitching", true);
            if (icon is not null) plist.String("CFBundleIconFile", icon);

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
    }

    private static IEnumerable<string> AppleKeys(string capability)
    {
        if (CapabilityManifest.AppleKey(capability) is { } shared) yield return shared;
        if (capability == "Location") yield return "NSLocationUsageDescription";   // the Mac's own spelling
    }
}
