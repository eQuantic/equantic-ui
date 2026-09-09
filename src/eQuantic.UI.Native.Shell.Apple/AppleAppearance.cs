using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// The platform's light/dark truth on macOS, read the way <see cref="AppleLocale"/> reads the
/// locale: a platform fact the app never states, resolved before anything renders.
/// <para>
/// <c>PhotonOptions.Mode</c> has always documented null as "FOLLOWS the system's light/dark
/// setting", and iOS and Android have always honoured it — <c>PhotonViewController</c> reads
/// <c>TraitCollection.UserInterfaceStyle</c>, <c>PhotonActivity</c> reads <c>UiMode.NightYes</c>.
/// Both desktop shells answered <c>ThemeMode.Light</c> unconditionally instead, so a Mac in dark
/// mode opened every Photon app light unless its developer pinned the mode by hand. The vocabulary
/// promised something two of four targets delivered.
/// </para>
/// </summary>
public static class AppleAppearance
{
    /// <summary>
    /// The mode the system is in, or null when it cannot be read (which is not the same as light:
    /// the caller decides what an unreadable appearance falls back to).
    /// </summary>
    public static ThemeMode? Resolve()
    {
        // NSApp's EFFECTIVE appearance is the honest answer, because it accounts for what the app
        // itself has been told to do — a user can force one app light while the system is dark, and
        // `NSRequiresAquaSystemAppearance` in a bundle does the same at build time.
        var app = Send(objc_getClass("NSApplication"), Sel("sharedApplication"));
        if (app != IntPtr.Zero)
        {
            var appearance = Send(app, Sel("effectiveAppearance"));
            if (appearance != IntPtr.Zero && FromNSString(Send(appearance, Sel("name"))) is { } name)
                return name.Contains("Dark", StringComparison.Ordinal) ? ThemeMode.Dark : ThemeMode.Light;
        }

        // Before there is an NSApplication — the headless screenshot path builds no window — the
        // user default is the same fact one layer down. It reads "Dark" in dark mode and NOTHING at
        // all in light, which is why the absent case cannot be distinguished from an unreadable one
        // here and is left to the caller.
        var defaults = Send(objc_getClass("NSUserDefaults"), Sel("standardUserDefaults"));
        if (defaults == IntPtr.Zero) return null;
        var style = FromNSString(Send(defaults, Sel("stringForKey:"), NSString("AppleInterfaceStyle")));
        return style is null ? null : style.Contains("Dark", StringComparison.Ordinal)
            ? ThemeMode.Dark
            : ThemeMode.Light;
    }
}
