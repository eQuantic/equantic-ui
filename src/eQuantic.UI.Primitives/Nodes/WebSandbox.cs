namespace eQuantic.UI.Primitives;

/// <summary>
/// What an embedded document is ALLOWED to do, composed instead of spelled. <see cref="None"/> is the
/// fully locked-down frame, and every flag hands one thing back.
/// <para>
/// Two kinds of thing, one set of flags. The first four loosen the frame's own isolation: script, an
/// origin, forms, new windows. The rest hand the document something its PAGE may use, a wallet, the
/// camera, a passkey, which a browser refuses an embedded document of another origin until the page
/// delegates it (#1228). Where each one is written is the realizer's business, so an app states what
/// the document may do in one place, and a checkout that offers a wallet says
/// <c>WebSandbox.Scripts | WebSandbox.SameOrigin | WebSandbox.Forms | WebSandbox.Popups | WebSandbox.Payment</c>.
/// </para>
/// <para>
/// A capability handed over is still the person's to grant: a document given the camera asks for it
/// as the page would, and the person may refuse. Handing it over is what lets the document ask at all,
/// and the page can only hand over what it has: a <c>Permissions-Policy</c> the host or a proxy in
/// front of it sends that denies the page a capability denies it to every frame on it.
/// </para>
/// </summary>
[Flags]
public enum WebSandbox
{
    /// <summary>Maximum isolation: no scripts, no origin, no forms, no popups, and nothing of the
    /// page's handed over.</summary>
    None = 0,

    /// <summary>The document may run script.</summary>
    Scripts = 1,

    /// <summary>The document keeps the host's origin — required for it to fetch same-origin
    /// resources (module imports, styles). Trust the content before granting it.</summary>
    SameOrigin = 2,

    /// <summary>The document may submit forms.</summary>
    Forms = 4,

    /// <summary>The document may open new windows.</summary>
    Popups = 8,

    /// <summary>The document may take a payment through the device's wallet (Apple Pay, Google Pay):
    /// a checkout embedded from a payment provider offers them only when it is handed this, and with
    /// <see cref="SameOrigin"/>, since a document without its origin is refused the payment.</summary>
    Payment = 16,

    /// <summary>The document may ask for the camera.</summary>
    Camera = 32,

    /// <summary>The document may ask for the microphone.</summary>
    Microphone = 64,

    /// <summary>The document may ask where the device is.</summary>
    Location = 128,

    /// <summary>The document may fill the screen, as a video player does.</summary>
    Fullscreen = 256,

    /// <summary>The document may copy into the clipboard. Reading the clipboard is not handed over.</summary>
    CopyToClipboard = 512,

    /// <summary>The document may sign in with a passkey, and create one, as <see cref="IPasskeys"/>
    /// does for the page. With <see cref="SameOrigin"/>, since a passkey belongs to an origin.</summary>
    Passkeys = 1024,
}
