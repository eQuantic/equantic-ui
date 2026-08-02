using Android.App;
using eQuantic.UI.Native.Hosting;

[assembly: PhotonRunner(typeof(eQuantic.UI.Native.Shell.Android.AndroidPhotonRunner))]

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// Hands the built app to Android. There is no loop to start here: the system launches the
/// Activity, so what a run amounts to is telling the Activity which app it belongs to — and the
/// tree is a factory for the same reason it is on iOS, because it must be built after the platform
/// is up rather than at process start.
/// </summary>
public sealed class AndroidPhotonRunner : IPhotonRunner
{
    public void Run(PhotonApplication app) => PhotonActivity.Application = app;
}
