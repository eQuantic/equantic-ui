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
    }
}
