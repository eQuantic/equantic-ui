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
    }
}
