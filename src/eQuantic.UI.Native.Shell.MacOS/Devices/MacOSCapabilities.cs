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
        services.TryAddSingleton<ILocation, AppleLocation>();
        services.TryAddSingleton<ICamera, AppleCamera>();
        // The same two-method clipboard the text fields use, offered as a SERVICE — what a page's
        // own "Copy" button asks for.
        services.TryAddSingleton<ITextClipboard, MacClipboard>();
        // The app's own durable key/value store — NSUserDefaults, which the system already
        // backs up, migrates and deletes along with the app. One realization for both
        // Apple platforms because it is one API on both.
        services.TryAddSingleton<IAppStorage, AppleAppStorage>();
        // The platform's vault, for what IAppStorage must never hold. Keychain on both Apple
        // platforms — one realization, because SecItem* is one API on both.
        services.TryAddSingleton<ISecretStore, AppleSecretStore>();
        // The URLs this app is opened WITH. Registered as the INSTANCE the shell already installed:
        // the AppleEvent handler has to be listening before the app finishes launching, so the
        // object exists before the container does, and asking the container to build a second one
        // would give the app a listener that hears nothing.
        services.TryAddSingleton<IDeepLinks>(_ => AppleDeepLinks.Install());
        // Asking a PERSON for a file — the one way a sandboxed app touches what it was not given.
        services.TryAddSingleton<IFileDialogs, MacFileDialogs>();
    }
}
