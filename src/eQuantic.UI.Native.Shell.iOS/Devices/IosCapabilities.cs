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
        // The same two-method clipboard the text fields use, offered as a SERVICE: what a page's
        // own "Copy" button asks for.
        services.TryAddSingleton<ITextClipboard, IosClipboard>();
        // The app's own durable key/value store — NSUserDefaults, which the system already
        // backs up, migrates and deletes along with the app. One realization for both
        // Apple platforms because it is one API on both.
        services.TryAddSingleton<IAppStorage, AppleAppStorage>();
        // The platform's vault, for what IAppStorage must never hold. Keychain on both Apple
        // platforms — one realization, because SecItem* is one API on both.
        services.TryAddSingleton<ISecretStore, AppleSecretStore>();
        // The URLs this app is opened WITH. The scene told the relay as it connected, before the
        // first tree was built, so the container hands out that same one.
        services.TryAddSingleton<IDeepLinks>(_ => IosDeepLinks.Relay);
        // And the URLs this app OPENS: UIApplication, for the schemes the policy hands over.
        services.TryAddSingleton<IUrlLauncher, IosUrlLauncher>();
    }
}
