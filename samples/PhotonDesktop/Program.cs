using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using eQuantic.Studio;

/// <summary>
/// eQUANTIC STUDIO — the component gallery. `--Photon:MaxFrames 120` presents that many frames and
/// exits, which is what the self-test runs; every other setting reaches the app from appsettings.json
/// or the command line the way a .NET developer already expects.
/// </summary>
public partial class Program
{
    public static PhotonApplication CreateApp(string[] args)
    {
        var builder = PhotonApplication.CreateBuilder(args);

        // The DECLARATION half of D6: the generator reads this call, the bundle writes the
        // NSLocationUsageDescription the system shows in its own prompt. No plist anywhere.
        builder.Capabilities.UseLocation("Shows where this Mac is — the capability demo.");
        builder.Capabilities.UseCamera("Shows a live preview in the Device section.");

        // And what the SYSTEM reads about the app before it runs — the Get Info panel, the Finder
        // category, the URLs it answers to. The same journey again: a call here, an assembly
        // declaration from the generator, the Info.plist from the build. No plist anywhere.
        builder.Bundle
               .Copyright("© 2026 eQuantic")
               .Category(AppCategory.DeveloperTools)
               .UrlScheme("equantic-studio");

        builder.Configure(photon =>
        {
            photon.Theme = PhotonTheme.Instance;
            photon.Title = "eQuantic Studio";
            // The handoff's own canvas, to the pixel: a window 100dp narrower squeezes every
            // column and reads as "the proportions are off" before a single token is wrong.
            photon.Width = 1280;
            photon.Height = 824;
            // The handoff drew this screen with no native title bar: the gallery's own top row IS
            // the window's. The strip the traffic lights sit in comes back as a safe-area inset,
            // and the shell insets its header by it — the same node an iPhone's notch drives.
            photon.Chrome = WindowChrome.Unified;
            photon.MinWidth = 900;
            photon.MinHeight = 600;
        });

        return builder.Build().UseRoot<StudioShell>();
    }
}
