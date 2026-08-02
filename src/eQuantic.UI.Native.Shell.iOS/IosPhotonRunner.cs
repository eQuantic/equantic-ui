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
    public void Run(PhotonApplication app) =>
        PhotonApp.Run(app.Args, app.Root, app.Options.Theme, app.Options.Mode, app.Options.MaxFrames);
}
