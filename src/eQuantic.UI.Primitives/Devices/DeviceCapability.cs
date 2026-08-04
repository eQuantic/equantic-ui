namespace eQuantic.UI.Primitives;

/// <summary>
/// Something of the DEVICE an app asks for, and which the user may refuse. Named once here so a
/// declaration reads the same in every place it has to appear — the app's own attribute, the key an
/// Apple platform wants in its manifest, the permission Android wants in its.
/// </summary>
public enum DeviceCapability
{
    /// <summary>Taking a photo or recording video.</summary>
    Camera,

    /// <summary>Recording sound.</summary>
    Microphone,

    /// <summary>Reading what the user already has: the photo library.</summary>
    PhotoLibrary,

    /// <summary>Writing into the photo library.</summary>
    PhotoLibraryAdd,

    /// <summary>Where the device is, roughly or exactly.</summary>
    Location,

    /// <summary>The accelerometer and gyroscope.</summary>
    Motion,

    /// <summary>The fingerprint or face reader.</summary>
    Biometrics,

    /// <summary>Reading the contact list.</summary>
    Contacts,

    /// <summary>Sending notifications.</summary>
    Notifications,
}

/// <summary>
/// What the user has decided about a capability. A permission is a THREE-way answer and code that
/// treats it as a boolean gets the third case wrong: a first-time ask and a permanent refusal look
/// the same to <c>if (!granted)</c>, and the app then either nags or gives up silently.
/// </summary>
public enum PermissionState
{
    /// <summary>Never asked. Asking is allowed and will show the system's own prompt.</summary>
    NotDetermined,

    /// <summary>Granted.</summary>
    Granted,

    /// <summary>Refused, and asking again does nothing — only the system's settings can change it.
    /// An app worth using says so, with a way to get there, instead of asking again.</summary>
    Denied,

    /// <summary>Granted, but narrowed by the user — a photo library limited to a chosen few, a
    /// location kept coarse. The app works; it just sees less.</summary>
    Limited,

    /// <summary>The device does not have it, or policy forbids it. Not a refusal, and not something
    /// asking will ever change: a desktop has no gyroscope.</summary>
    Unavailable,
}
