using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// What this app asks of the DEVICE, stated the way everything else about the app is stated —
/// fluently, on the builder, beside <c>Services</c> and <c>Configuration</c>.
///
/// <code>
/// var builder = PhotonApplication.CreateBuilder(args);
///
/// builder.Capabilities
///     .UseCamera("Reads the code on your card.")
///     .UsePhotoLibrary("Pick the picture for your profile.");
/// </code>
///
/// <para>
/// The reason is not decoration: it is the sentence the system shows at the moment the user
/// decides, and both Apple and Google require one. It also has to exist long before any of this
/// runs — a permission is declared in a manifest the OS reads at install time — which is why the
/// generator READS these calls at compile time and writes the platform keys from them. So the
/// string must be a constant: an interpolation or a variable is a build error rather than a
/// permission that quietly goes missing.
/// </para>
/// </summary>
public sealed class PhotonCapabilitiesBuilder
{
    private readonly Dictionary<DeviceCapability, string> _declared = new();

    /// <summary>What this app declared, in case it wants to explain itself before asking.</summary>
    public IReadOnlyDictionary<DeviceCapability, string> Declared => _declared;

    /// <summary>Taking a photo or recording video.</summary>
    public PhotonCapabilitiesBuilder UseCamera(string reason) => Use(DeviceCapability.Camera, reason);

    /// <summary>Recording sound.</summary>
    public PhotonCapabilitiesBuilder UseMicrophone(string reason) => Use(DeviceCapability.Microphone, reason);

    /// <summary>Reading the pictures the user already has.</summary>
    public PhotonCapabilitiesBuilder UsePhotoLibrary(string reason) => Use(DeviceCapability.PhotoLibrary, reason);

    /// <summary>Saving into the user's pictures.</summary>
    public PhotonCapabilitiesBuilder UsePhotoLibraryAdd(string reason) => Use(DeviceCapability.PhotoLibraryAdd, reason);

    /// <summary>Where the device is.</summary>
    public PhotonCapabilitiesBuilder UseLocation(string reason) => Use(DeviceCapability.Location, reason);

    /// <summary>The accelerometer and gyroscope.</summary>
    public PhotonCapabilitiesBuilder UseMotion(string reason) => Use(DeviceCapability.Motion, reason);

    /// <summary>The fingerprint or face reader.</summary>
    public PhotonCapabilitiesBuilder UseBiometrics(string reason) => Use(DeviceCapability.Biometrics, reason);

    /// <summary>Reading the contact list.</summary>
    public PhotonCapabilitiesBuilder UseContacts(string reason) => Use(DeviceCapability.Contacts, reason);

    /// <summary>Sending notifications.</summary>
    public PhotonCapabilitiesBuilder UseNotifications(string reason) => Use(DeviceCapability.Notifications, reason);

    /// <summary>
    /// The general form, for a capability this builder has no named method for yet. The named ones
    /// exist because they are discoverable — a developer finds `UseCamera` by typing `Use`.
    /// </summary>
    public PhotonCapabilitiesBuilder Use(DeviceCapability capability, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                $"A reason is required for {capability}. It is shown to the user at the moment they "
                + "decide, and a platform that receives an empty one rejects the app.", nameof(reason));

        _declared[capability] = reason;
        return this;
    }
}
