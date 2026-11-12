using eQuantic.UI.Native.Hosting;

[assembly: PhotonRunner(typeof(eQuantic.UI.Native.Shell.iOS.IosPhotonRunner))]

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// Hands the built app to UIKit. The tree is built AFTER the platform is up — a tree constructed at
/// process start would measure text through a font stack that is not loaded yet — which is exactly
/// why <see cref="PhotonApplication.Root"/> is a factory.
/// </summary>
public sealed class IosPhotonRunner : IPhotonRunner
{
    public void Run(PhotonApplication app)
    {
        // The screen's error boundary (#1452), armed with the app's report.
        var boundary = new eQuantic.UI.Primitives.ComponentBoundary();
        app.ArmBoundary(boundary);
        PhotonApp.Run(app.Args, app.Root, app.Theme, app.Options.Mode, app.Options.MaxFrames,
            app.Options.SmoothScroll, boundary);
    }
}
