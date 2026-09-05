using System.Reflection;
using System.Runtime.InteropServices;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Microsoft.Win32;
using static eQuantic.UI.Native.Shell.Windows.Win32;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The URLs a Windows app is opened WITH. Windows has no event for it: a protocol activation is a
/// NEW PROCESS started with the URL as its one command-line argument, from a registration under
/// <c>HKCU\Software\Classes\&lt;scheme&gt;</c>. So the three halves live here:
/// <list type="bullet">
/// <item>REGISTRATION — <c>builder.Bundle.UrlScheme("acme")</c> became an assembly attribute; at
/// launch the shell writes the per-user class for it, pointing at this executable. Per user needs
/// no elevation and is what an installer would write anyway; written only when it differs, so a
/// launch is not a registry churn.</item>
/// <item>THE LAUNCH URL — read off the arguments, offered to the same <see cref="DeepLinkRelay"/>
/// every platform feeds, which decides what a launch URL is and who hears the later ones.</item>
/// <item>FORWARDING — an app already running has a window of a known class; the new process hands
/// the URL to it with <c>WM_COPYDATA</c> and exits, so a link never opens a second window over the
/// one the person is looking at.</item>
/// </list>
/// </summary>
public static class WindowsDeepLinks
{
    private static readonly object Gate = new();
    private static DeepLinkRelay? _relay;

    /// <summary>The relay, created once. Idempotent — the container hands out this same instance.</summary>
    public static DeepLinkRelay Install(PhotonApplication? app = null)
    {
        lock (Gate)
        {
            _relay ??= new DeepLinkRelay();
        }
        if (app is not null)
        {
            foreach (var scheme in DeclaredSchemes(app)) RegisterScheme(scheme);
        }
        return _relay;
    }

    /// <summary>The window class the running instance can be found by — one per app.</summary>
    public static string WindowClassName(PhotonApplication app) =>
        "EQPhoton." + (Assembly.GetEntryAssembly()?.GetName().Name ?? app.Options.Title);

    /// <summary>The URL schemes the app declared on its builder, read back off its assembly.</summary>
    public static IReadOnlyList<string> DeclaredSchemes(PhotonApplication app)
    {
        var entry = Assembly.GetEntryAssembly();
        if (entry is null) return [];
        return entry.GetCustomAttributes<PhotonBundleKeyAttribute>()
            .Where(fact => fact.Kind == PhotonBundleValueKind.UrlScheme && fact.Value.Length > 0)
            .Select(fact => fact.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// The URL among the process's arguments, if one is there: absolute, and with one of the
    /// declared schemes — or, when nothing was declared, any non-file scheme, so a developer trying
    /// the mechanism sees it work before writing the declaration.
    /// </summary>
    public static Uri? LaunchUrl(PhotonApplication app)
    {
        var schemes = DeclaredSchemes(app);
        foreach (var argument in app.Args)
        {
            if (!Uri.TryCreate(argument, UriKind.Absolute, out var url)) continue;
            if (url.IsFile || url.Scheme.Length < 2) continue;
            if (schemes.Count == 0 || schemes.Contains(url.Scheme, StringComparer.OrdinalIgnoreCase)) return url;
        }
        return null;
    }

    /// <summary>Hands the URL to an already-running instance. True when one took it.</summary>
    public static unsafe bool ForwardToRunningInstance(string className, Uri url)
    {
        var target = FindWindowW(className, null);
        if (target == IntPtr.Zero) return false;
        var text = url.OriginalString;
        fixed (char* characters = text)
        {
            var data = new COPYDATASTRUCT
            {
                Data = 0,
                Size = (uint)((text.Length + 1) * sizeof(char)),
                Bytes = (IntPtr)characters,
            };
            return SendMessageW(target, WM_COPYDATA, 0, (nint)(&data)) != 0;
        }
    }

    /// <summary>
    /// <c>HKCU\Software\Classes\&lt;scheme&gt;</c>: the "URL Protocol" marker and an open command
    /// naming this executable. The app's own class — <c>URL:acme protocol</c> is the display name
    /// Windows expects, and the Mac side's CFBundleURLTypes is the same declaration in a plist.
    /// </summary>
    public static void RegisterScheme(string scheme)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable) || string.IsNullOrWhiteSpace(scheme)) return;
        var command = $"\"{executable}\" \"%1\"";
        try
        {
            using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + scheme);
            if (classes is null) return;
            using var open = classes.CreateSubKey(@"shell\open\command");
            if (open?.GetValue(null) as string == command) return;
            classes.SetValue(null, $"URL:{scheme} protocol");
            classes.SetValue("URL Protocol", "");
            open?.SetValue(null, command);
        }
        catch (UnauthorizedAccessException)
        {
            // A locked-down profile: the app still runs, and a link simply has nothing to open it.
        }
        catch (IOException)
        {
        }
    }
}
