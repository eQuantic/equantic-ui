using System.Runtime.Versioning;
using eQuantic.UI.Native.Shell.MacOS;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The Mac's answer to the QUESTION, asked of the real NSWorkspace: the query form, which puts
/// nothing on the screen. Opening is not run here, because on a desk it opens a browser; the order
/// before the system is asked is pinned by <see cref="UrlLauncherTests"/>, and the opening itself is
/// proved by hand on each system.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacUrlLauncherTests
{
    [MacFact]
    public void TheMacSaysWhatClaimsAScheme_WithoutShowingAnything()
    {
        var unclaimed = "nobody-claims-this-" + Guid.NewGuid().ToString("N")[..8];
        // The unclaimed scheme has to pass the POLICY to reach NSWorkspace at all.
        var launcher = new MacUrlLauncher(OpenUrlPolicy.Web.Allowing(unclaimed), NullLogger<MacUrlLauncher>.Instance);

        launcher.CanOpen(new Uri("https://equantic.tech")).Should().BeTrue("a browser always claims https");
        launcher.CanOpen(new Uri(unclaimed + "://x")).Should().BeFalse("NSWorkspace knows no app for it");
    }
}
