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
        services.TryAddSingleton<ICamera, AndroidCamera>();
        // The same two-method clipboard the text fields use, offered as a SERVICE: what a page's
        // own "Copy" button asks for.
        services.TryAddSingleton<ITextClipboard, AndroidClipboard>();
        // The app's own durable key/value store — SharedPreferences, in the app's sandbox,
        // removed with the app like every other preference on the platform.
        services.TryAddSingleton<IAppStorage, AndroidAppStorage>();
        // The vault, for what IAppStorage must never hold: a hardware-backed KeyStore key
        // encrypts the value and only the ciphertext reaches a preferences file.
        services.TryAddSingleton<ISecretStore, AndroidSecretStore>();
        // The URLs this app is opened WITH. The activity told the relay in onCreate, long before
        // this container existed, so the container hands out that same one.
        services.TryAddSingleton<IDeepLinks>(_ => AndroidDeepLinks.Relay);
        // And the URLs this app OPENS: a VIEW intent for the schemes the policy hands over.
        services.TryAddSingleton<IUrlLauncher, AndroidUrlLauncher>();
    }
}
