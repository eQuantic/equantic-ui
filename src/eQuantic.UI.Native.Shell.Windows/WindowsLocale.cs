using System.Globalization;
using static eQuantic.UI.Native.Shell.Windows.Win32;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The platform's locale truth, as the D13 pair: the first of the user's PREFERRED UI LANGUAGES
/// (Settings → Language — what resources resolve against) and the user's DEFAULT LOCALE (Settings →
/// Region → formats: dates, decimal separators). The same split .NET models as
/// <c>CurrentUICulture</c> vs <c>CurrentCulture</c>, and Windows keeps them independently settable
/// exactly as .NET does. Read from the OS rather than trusted to .NET's own startup defaults so
/// the shell can be asked what the platform said, before or after anything overrode it.
/// </summary>
public static unsafe class WindowsLocale
{
    public static (CultureInfo Ui, CultureInfo Format) Resolve()
    {
        var ui = Culture(FirstPreferredLanguage()) ?? CultureInfo.CurrentUICulture;
        var format = Culture(UserLocaleName()) ?? ui;
        return (ui, format);
    }

    private static string? FirstPreferredLanguage()
    {
        uint count = 0;
        uint size = 0;
        if (!GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, &count, null, &size) || size == 0) return null;
        var buffer = new char[size];
        fixed (char* languages = buffer)
        {
            if (!GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, &count, languages, &size) || count == 0) return null;
        }
        // A double-null-terminated list; the first entry is the one the user put first.
        var end = Array.IndexOf(buffer, '\0');
        return end > 0 ? new string(buffer, 0, end) : null;
    }

    private static string? UserLocaleName()
    {
        const int LocaleNameMaxLength = 85;
        var buffer = new char[LocaleNameMaxLength];
        fixed (char* name = buffer)
        {
            var length = GetUserDefaultLocaleName(name, LocaleNameMaxLength);
            // The length INCLUDES the terminating null.
            return length > 1 ? new string(buffer, 0, length - 1) : null;
        }
    }

    private static CultureInfo? Culture(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
