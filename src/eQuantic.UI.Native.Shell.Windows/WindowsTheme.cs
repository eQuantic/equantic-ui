using eQuantic.UI.Primitives;
using Microsoft.Win32;
using static eQuantic.UI.Native.Shell.Windows.Win32;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The system's light/dark choice — Settings → Personalization → Colors → "Choose your mode" —
/// which Windows keeps as one registry value and announces through <c>WM_SETTINGCHANGE</c> with
/// the section name <c>ImmersiveColorSet</c>. Nothing else reads it: there is no API for it, and
/// every framework on Windows reads this same key.
/// </summary>
public static class WindowsTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The mode apps should show right now. A missing value is the default, which is light.</summary>
    public static ThemeMode SystemMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0 ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch
        {
            return ThemeMode.Light;
        }
    }

    /// <summary>Whether a settings-change message is the colour set changing.</summary>
    public static unsafe bool IsColorSetChange(nint lParam)
    {
        if (lParam == 0) return false;
        var section = new string((char*)lParam);
        return string.Equals(section, "ImmersiveColorSet", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The system-drawn title bar follows the app's mode: without this a dark app opens
    /// under a white caption, which reads as two apps in one frame.</summary>
    public static unsafe void ApplyToTitleBar(IntPtr hwnd, ThemeMode mode)
    {
        int dark = mode == ThemeMode.Dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, &dark, sizeof(int));
    }
}
