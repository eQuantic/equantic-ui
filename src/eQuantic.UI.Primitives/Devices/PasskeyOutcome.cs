namespace eQuantic.UI.Primitives;

/// <summary>
/// How a passkey ceremony ended. Not a boolean, and for the reason <see cref="BiometricResult"/> is
/// not one: "did not succeed" hides situations an app must tell apart, a person who backed out, a
/// device that already holds a passkey for the account, a host that cannot run a ceremony at all.
/// <para>
/// <see cref="Unavailable"/> is the zero, where <see cref="BiometricResult"/> puts its success. A
/// result nobody filled in, the default of <see cref="PasskeyResult"/>, then reads as nothing done
/// rather than as a sign-in, which is the one wrong answer a sign-in must never get by accident.
/// </para>
/// </summary>
public enum PasskeyOutcome
{
    /// <summary>This host cannot create or use a passkey: a browser outside a secure context, one
    /// without Web Authentication, the server render, a platform not realized yet. Nothing was asked
    /// of the person.</summary>
    Unavailable,

    /// <summary>The ceremony finished, and <see cref="PasskeyResult.Credential"/> carries what the
    /// relying party verifies.</summary>
    Succeeded,

    /// <summary>
    /// The person backed out, or the ceremony lapsed: Web Authentication reports a refusal and a
    /// timeout alike, on purpose, so a page cannot tell whether someone has a passkey by how they
    /// declined. Not a failure, and never something to retry unasked.
    /// <para>
    /// In Safari it can also mean the person's gesture was spent before the ceremony began: Safari
    /// runs one only inside the press, and a round trip to the server between the press and the
    /// call can use it up. Ask the server for the options when the screen opens, not when the
    /// person presses.
    /// </para>
    /// </summary>
    Cancelled,

    /// <summary>A creation the device refused because it already holds a passkey for this account,
    /// one the relying party listed to exclude. Say so: the person can sign in with the one they
    /// have.</summary>
    AlreadyRegistered,

    /// <summary>Any other refusal: options that are not the relying party's JSON, a relying party
    /// that does not match the page's address, an algorithm no authenticator supports.</summary>
    Failed,
}
