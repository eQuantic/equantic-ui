using System.Globalization;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// The platform's locale truth (<c>NSLocale</c>), as the D13 pair: <c>preferredLanguages[0]</c>
/// is the LANGUAGE the user chose (BCP-47 — the UI culture, what resources resolve against);
/// <c>currentLocale</c> is the REGION's formats (dates, decimal separators — the format culture).
/// The same split .NET models as <c>CurrentUICulture</c> vs <c>CurrentCulture</c>, and Apple keeps
/// them independently settable exactly as .NET does. Environment variables are NOT this truth: a
/// Finder-launched process carries no <c>LANG</c>, so without this read .NET sits invariant on a
/// pt-BR machine.
/// </summary>
public static class AppleLocale
{
    public static (CultureInfo Ui, CultureInfo Format) Resolve()
    {
        var ui = Culture(FirstPreferredLanguage()) ?? CultureInfo.CurrentUICulture;
        var format = Culture(CurrentLocaleIdentifier()) ?? ui;
        return (ui, format);
    }

    private static string? FirstPreferredLanguage()
    {
        var languages = Send(objc_getClass("NSLocale"), Sel("preferredLanguages"));
        if (languages == IntPtr.Zero || SendULong(languages, Sel("count")) == 0) return null;
        return FromNSString(Send(languages, Sel("objectAtIndex:"), 0L));
    }

    private static string? CurrentLocaleIdentifier() =>
        FromNSString(Send(Send(objc_getClass("NSLocale"), Sel("currentLocale")),
            Sel("localeIdentifier")));

    private static CultureInfo? Culture(string? identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return null;
        // "pt_BR@calendar=gregorian" → "pt-BR": the @ tail is ICU keywords .NET does not parse,
        // and NSLocale spells the region with an underscore where BCP-47 (and .NET) hyphenate.
        var keywords = identifier.IndexOf('@');
        if (keywords >= 0) identifier = identifier[..keywords];
        identifier = identifier.Replace('_', '-');
        try
        {
            return CultureInfo.GetCultureInfo(identifier);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
