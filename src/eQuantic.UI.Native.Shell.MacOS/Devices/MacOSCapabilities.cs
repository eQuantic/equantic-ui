using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Native.Shell.Apple;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: PhotonCapabilities(typeof(eQuantic.UI.Native.Shell.MacOS.MacOSCapabilities))]

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>What a Mac can do, offered to the container. TryAdd throughout: an app that registered
/// its own — a fake in a test, a wrapper that logs — has already decided.</summary>
public sealed class MacOSCapabilities : IPhotonCapabilities
{
    public void Register(IServiceCollection services)
    {
        services.TryAddSingleton<IPhotoLibrary, MacOSPhotoLibrary>();
        services.TryAddSingleton<INetworkStatus, AppleNetworkStatus>();
        services.TryAddSingleton<IBiometrics, AppleBiometrics>();
        // CoreMotion exists on the Mac and answers "no device motion" itself — the shared
        // realization IS the absence report here, with no platform check written by hand.
        services.TryAddSingleton<IMotionSensor, AppleMotionSensor>();
    }
}
