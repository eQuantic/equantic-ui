using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: PhotonCapabilities(typeof(eQuantic.UI.Native.Shell.Windows.WindowsCapabilities))]

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>What a Windows PC can do, offered to the container. TryAdd throughout: an app that
/// registered its own — a fake in a test, a wrapper that logs — has already decided.</summary>
public sealed class WindowsCapabilities : IPhotonCapabilities
{
    public void Register(IServiceCollection services)
    {
        // Asking a PERSON for a file, and handing one back to the system — the two desktop seams.
        services.TryAddSingleton<IFileDialogs, WindowsFileDialogs>();
        services.TryAddSingleton<IWorkspace, WindowsWorkspace>();
        // The file system IS the photo library on a desktop; the picker is the grant.
        services.TryAddSingleton<IPhotoLibrary>(provider =>
            new WindowsPhotoLibrary(provider.GetRequiredService<IFileDialogs>()));
        // The same two-method clipboard the text fields use, offered as a SERVICE.
        services.TryAddSingleton<ITextClipboard, WindowsClipboard>();
        // The app's own durable key/value store — the registry, under the app's own key.
        services.TryAddSingleton<IAppStorage, WindowsAppStorage>();
        // The vault, for what IAppStorage must never hold — DPAPI, this user on this machine.
        services.TryAddSingleton<ISecretStore, WindowsSecretStore>();
        services.TryAddSingleton<INetworkStatus, WindowsNetworkStatus>();
        // The URLs this app is opened WITH. Install() is idempotent, so whichever of the two runs
        // first — the runner, or the first app that resolves this — the other gets the same relay.
        services.TryAddSingleton<IDeepLinks>(_ => WindowsDeepLinks.Install());
        // Honest absences — a head that reports itself unavailable rather than missing, so a page
        // written for a phone renders here and shows its "not on this device" branch instead of
        // failing to resolve a constructor argument.
        services.TryAddSingleton<IMotionSensor, AbsentMotionSensor>();
        services.TryAddSingleton<IBiometrics, WindowsBiometrics>();
        services.TryAddSingleton<ILocation, WindowsLocation>();
        services.TryAddSingleton<ICamera, WindowsCamera>();
    }
}

/// <summary>
/// Windows Hello is a WinRT API (<c>UserConsentVerifier</c>), and this shell has no WinRT activation
/// yet — the absence is REPORTED, which the contract allows, rather than faked. Registered so a
/// page that takes <see cref="IBiometrics"/> through its constructor still resolves and shows its
/// "not on this device" branch.
/// </summary>
internal sealed class WindowsBiometrics : IBiometrics
{
    public bool IsAvailable => false;

    public ValueTask<BiometricResult> AuthenticateAsync(string reason, CancellationToken cancellationToken = default) =>
        new(BiometricResult.Unavailable);
}

/// <summary>Geolocation is WinRT too (<c>Windows.Devices.Geolocation</c>): the same honest absence
/// until the shell speaks WinRT.</summary>
internal sealed class WindowsLocation : ILocation
{
    public bool IsAvailable => false;

    public PermissionState Permission => PermissionState.Unavailable;

    public ValueTask<GeoLocation?> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        new((GeoLocation?)null);

    public IDisposable Subscribe(Action<GeoLocation> onChanged) => Nothing.Instance;

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();
        public void Dispose() { }
    }
}

/// <summary>The camera is Media Foundation, a capture graph this shell does not build yet. Absent,
/// and said so — a preview node draws its unavailable state.</summary>
internal sealed class WindowsCamera : ICamera
{
    public bool IsAvailable => false;

    public PermissionState Permission => PermissionState.Unavailable;

    public ValueTask<ICameraSession?> StartPreviewAsync(CancellationToken cancellationToken = default) =>
        new((ICameraSession?)null);
}
