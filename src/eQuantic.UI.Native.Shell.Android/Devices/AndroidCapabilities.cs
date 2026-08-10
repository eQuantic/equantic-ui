using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: PhotonCapabilities(typeof(eQuantic.UI.Native.Shell.Android.AndroidCapabilities))]

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>What an Android device can do, offered to the container.</summary>
public sealed class AndroidCapabilities : IPhotonCapabilities
{
    public void Register(IServiceCollection services)
    {
        services.TryAddSingleton<IPhotoLibrary, AndroidPhotoLibrary>();
        services.TryAddSingleton<IBiometrics, AndroidBiometrics>();
        services.TryAddSingleton<INetworkStatus, AndroidNetworkStatus>();
        services.TryAddSingleton<IMotionSensor, AndroidMotionSensor>();
        services.TryAddSingleton<ILocation, AndroidLocation>();
        // The app's own durable key/value store — SharedPreferences, in the app's sandbox,
        // removed with the app like every other preference on the platform.
        services.TryAddSingleton<IAppStorage, AndroidAppStorage>();
    }
}
