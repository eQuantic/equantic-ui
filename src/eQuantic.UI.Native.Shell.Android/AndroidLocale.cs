using System.Globalization;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The device's locale truth (<c>java.util.Locale</c>) — one value carrying language AND region,
/// so the same culture feeds both .NET statics (the D13 pair collapses on this platform).
/// <c>toLanguageTag()</c> is BCP-47, which <see cref="CultureInfo"/> parses directly.
/// </summary>
internal static class AndroidLocale
{
    internal static CultureInfo Resolve()
    {
        try
        {
            return CultureInfo.GetCultureInfo(Java.Util.Locale.Default.ToLanguageTag());
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentUICulture;
        }
    }
}
