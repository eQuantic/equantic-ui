using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;

[assembly: PhotonRunner(typeof(eQuantic.UI.Native.Shell.MacOS.MacOSPhotonRunner))]

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// Runs a built app in a window. The desktop is where a phone app is ITERATED on — same engine,
/// same trees, at the geometry the design was drawn for — so the options carry a size here that a
/// device would simply report for itself.
/// </summary>
public sealed class MacOSPhotonRunner : IPhotonRunner
{
    public void Run(PhotonApplication app)
    {
        var options = app.Options;
        var window = new PhotonWindow(options.Title, options.Width, options.Height,
            options.Chrome, options.Resizable, options.MinWidth, options.MinHeight,
            options.SmoothScroll);
        window.Run(app.Root(), options.Theme, options.Mode ?? ThemeMode.Light, options.MaxFrames);
        Console.WriteLine($"[photon] frames presented: {window.FramesPresented}");
    }
}
