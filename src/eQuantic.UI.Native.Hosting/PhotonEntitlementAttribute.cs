namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// Declares that this app needs the operating system to PERMIT something it would otherwise refuse
/// — the release-build counterpart of <see cref="PhotonCapabilityAttribute"/>. WHICH protection is
/// being asked about is the key's own business and not one question: this type carries the hardened
/// runtime's exceptions AND the App Sandbox's permissions, which answer to different switches. The
/// families are set out below, and getting them backwards is what this file's own summary used to
/// do.
/// <para>
/// A capability asks the USER (a sheet, a reason, an answer). An entitlement asks the SYSTEM, and
/// it is not negotiated at run time: it is signed into the binary, and code that needs one without
/// having it is not refused politely — the process is killed. An app that JITs (a WASM engine, a
/// scripting runtime) and ships with the hardened runtime dies on its first generated page with
/// SIGKILL/CODESIGNING, which no <c>catch</c> can see, and which never happens in an ad-hoc
/// development build — the trap that gives this type its urgency, though it is the trap of ONE
/// family rather than of entitlements as such.
/// </para>
/// <para>
/// One declaration in C#, like every other platform fact here — the SDK writes the entitlements
/// file and hands it to <c>codesign</c>, and no app author edits a plist. The value is Apple's own
/// key, because that list is Apple's and not ours; <see cref="PhotonEntitlements"/> names the ones
/// a Photon app actually reaches for, so the common cases are typed and discoverable without
/// fencing the rest out.
/// </para>
/// <para>
/// WHAT CONSULTS A KEY DEPENDS ON WHICH FAMILY IT IS IN, and reading that as one rule is how this
/// file used to be wrong. The hardened runtime's own exceptions
/// (<c>com.apple.security.cs.*</c> — see <see cref="PhotonEntitlements.IsHardenedRuntimeException"/>)
/// exist only to relax protections the hardened runtime imposes, so without it there is nothing to
/// relax and they grant nothing. The App Sandbox's permissions
/// (<c>com.apple.security.network.*</c>, <c>…files.*</c>, <c>…device.*</c>) answer to
/// <see cref="PhotonEntitlements.AppSandbox"/> instead, and the sandbox is enforced from the
/// SIGNATURE — including an ad-hoc one, which is how sandboxing is tested locally without a
/// certificate. So a development build is not a build where entitlements do nothing.
/// </para>
/// <para>
/// What IS invisible until release is the first family: an ad-hoc build signs without
/// <c>--options runtime</c>, so a missing hardened-runtime exception costs nothing until the
/// signed build reaches a machine you do not own. That is the trap, stated precisely.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PhotonEntitlementAttribute(string entitlement) : Attribute
{
    /// <summary>Apple's key, e.g. <c>com.apple.security.cs.allow-jit</c>.</summary>
    public string Entitlement { get; } = entitlement;
}
