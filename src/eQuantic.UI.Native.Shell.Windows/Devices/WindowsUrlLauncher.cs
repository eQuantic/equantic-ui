using System.Runtime.InteropServices;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.Logging;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// The Windows shell, handed a URL: <c>ShellExecuteEx</c> routes it to whatever claims its scheme,
/// and the association API asks without acting.
/// <para>
/// Each call answers with what the shell answered, unchanged. False means the system declined (no
/// app claims the scheme), and inventing an exception for it would turn an ordinary outcome into
/// one every call site has to guard. What this app does not open never gets here: the base class
/// applies the <see cref="OpenUrlPolicy"/> first, the same rule from the same object the Mac
/// applies, so the two shells cannot disagree about one URL.
/// </para>
/// </summary>
public sealed unsafe class WindowsUrlLauncher(OpenUrlPolicy policy, ILogger<WindowsUrlLauncher> logger)
    : UrlLauncher(policy, logger)
{
    private const uint ASSOCF_IS_PROTOCOL = 0x00001000;
    private const uint ASSOCSTR_COMMAND = 1;

    [DllImport("shlwapi.dll", EntryPoint = "AssocQueryStringW", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags, uint kind, string association, string? extra,
        char* buffer, uint* size);

    /// <inheritdoc />
    protected override ValueTask<bool> Hand(Uri url) => new(ShellOpen.Execute(url.AbsoluteUri));

    /// <summary>
    /// The QUERY form: which command opens this protocol. S_OK with a command means something
    /// claims it; anything else means nothing does, and no dialog either way, which is exactly what
    /// a check must not put on the person's screen.
    /// </summary>
    protected override bool Claimed(Uri url)
    {
        uint size = 0;
        var probe = AssocQueryString(ASSOCF_IS_PROTOCOL, ASSOCSTR_COMMAND, url.Scheme, "open", null, &size);
        // S_FALSE with a size means "here is how big the answer is": a handler exists.
        return probe is Com.S_OK or 1 && size > 1;
    }
}
