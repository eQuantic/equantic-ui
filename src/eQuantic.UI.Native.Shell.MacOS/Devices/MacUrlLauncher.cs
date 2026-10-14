using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.Logging;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// <c>NSWorkspace</c>, handed a URL: <c>openURL:</c> routes it to whatever claims its scheme, and
/// <c>URLForApplicationToOpenURL:</c> asks without acting.
/// <para>
/// Each call answers with what NSWorkspace answered, unchanged. A false means the system declined
/// (no app claims the scheme), and inventing an exception for it would turn an ordinary outcome
/// into one every call site has to guard. What this app does not open never gets here: the base
/// class applies the <see cref="OpenUrlPolicy"/> first.
/// </para>
/// </summary>
public sealed class MacUrlLauncher(OpenUrlPolicy policy, ILogger<MacUrlLauncher> logger) : UrlLauncher(policy, logger)
{
    /// <inheritdoc />
    protected override ValueTask<bool> Hand(Uri url)
    {
        var native = Url(url);
        return new(native != IntPtr.Zero && SendBool(SharedWorkspace.Get(), Sel("openURL:"), native));
    }

    /// <summary>
    /// The QUERY form of <c>openURL:</c>: nil when nothing claims the scheme, and no dialog either
    /// way. The action form puts up "There is no application set to open the URL…" on the person's
    /// screen, which is exactly what a check must not do.
    /// </summary>
    protected override bool Claimed(Uri url)
    {
        var native = Url(url);
        return native != IntPtr.Zero
            && Send(SharedWorkspace.Get(), Sel("URLForApplicationToOpenURL:"), native) != IntPtr.Zero;
    }

    private static IntPtr Url(Uri url) =>
        Send(AppKit.Class("NSURL"), Sel("URLWithString:"), NSString(url.AbsoluteUri));
}
