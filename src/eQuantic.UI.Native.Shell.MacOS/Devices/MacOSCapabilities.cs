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
        // The URLs this app is opened WITH. The runner installs the handler as its FIRST act, well
        // before the run loop that delivers AppleEvents; Install() is idempotent, so whichever of
        // the two runs first — the runner, or the first app that resolves this — the other gets the
        // same relay. Building a second one here would give the app a listener that hears nothing.
        services.TryAddSingleton<IDeepLinks>(_ => AppleDeepLinks.Install());
        // Asking a PERSON for a file — the one way a sandboxed app touches what it was not given.
        services.TryAddSingleton<IFileDialogs, MacFileDialogs>();
        // And the other direction: handing a path or a URL to the system. NSWorkspace.
        services.TryAddSingleton<IWorkspace, MacWorkspace>();
    }
}
