using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: PhotonCapabilities(typeof(eQuantic.UI.Native.Shell.iOS.IosCapabilities))]

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>What an iPhone can do, offered to the container.</summary>
public sealed class IosCapabilities : IPhotonCapabilities
{
    public void Register(IServiceCollection services)
    {
        services.TryAddSingleton<IPhotoLibrary, IosPhotoLibrary>();
        services.TryAddSingleton<INetworkStatus, AppleNetworkStatus>();
        services.TryAddSingleton<IBiometrics, IosBiometrics>();
        services.TryAddSingleton<IMotionSensor, AppleMotionSensor>();
        services.TryAddSingleton<ILocation, AppleLocation>();
        services.TryAddSingleton<ICamera, AppleCamera>();
        // The app's own durable key/value store — NSUserDefaults, which the system already
        // backs up, migrates and deletes along with the app. One realization for both
        // Apple platforms because it is one API on both.
        services.TryAddSingleton<IAppStorage, AppleAppStorage>();
    }
}
