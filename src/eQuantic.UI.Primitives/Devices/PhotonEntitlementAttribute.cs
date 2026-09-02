namespace eQuantic.UI.Primitives;

/// <summary>
/// Declares that this app needs the operating system to PERMIT something the hardened runtime
/// forbids by default — the release-build counterpart of <see cref="PhotonCapabilityAttribute"/>.
/// <para>
/// A capability asks the USER (a sheet, a reason, an answer). An entitlement asks the SYSTEM, and
/// it is not negotiated at run time: it is signed into the binary, and code that needs one without
/// having it is not refused politely — the process is killed. An app that JITs (a WASM engine, a
/// scripting runtime) and ships with the hardened runtime dies on its first generated page with
/// SIGKILL/CODESIGNING, which no <c>catch</c> can see, and which never happens in an ad-hoc
/// development build. That is the trap this exists to close: the failure appears only in the build
/// you publish.
/// </para>
/// <para>
/// One declaration in C#, like every other platform fact here — the SDK writes the entitlements
/// file and hands it to <c>codesign</c>, and no app author edits a plist. The value is Apple's own
/// key, because that list is Apple's and not ours; <see cref="PhotonEntitlements"/> names the ones
/// a Photon app actually reaches for, so the common cases are typed and discoverable without
/// fencing the rest out.
/// </para>
/// <para>
/// Entitlements are only consulted when the app is signed with a real identity and the hardened
/// runtime (<c>EQuanticSigningIdentity</c> + <c>EQuanticHardenedRuntime</c>). A development build
/// signs ad hoc and needs none of them, which is exactly why the need is invisible until release.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PhotonEntitlementAttribute(string entitlement) : Attribute
{
    /// <summary>Apple's key, e.g. <c>com.apple.security.cs.allow-jit</c>.</summary>
    public string Entitlement { get; } = entitlement;
}

/// <summary>
/// The entitlements a Photon app reaches for, by name — so the declaration is typed at the call
/// site and the key is spelled once, here, where a typo is a compile error instead of an app that
/// is killed on a machine you do not own.
/// <para>
/// Not an enum, and not a closed set: Apple owns this list and adds to it, so an app needing one
/// that is not here passes the key itself. What this buys is that the ones that matter are
/// discoverable, and each says WHEN you need it.
/// </para>
/// </summary>
public static class PhotonEntitlements
{
    /// <summary>Required by any embedded engine that compiles code at run time — a WASM runtime
    /// (wasmtime, YARA-X), a scripting VM, a regex JIT. Without it the hardened runtime kills the
    /// process at the first executable page it maps, with SIGKILL/CODESIGNING: no exception, no
    /// stack, the process is simply gone.</summary>
    public const string AllowJit = "com.apple.security.cs.allow-jit";

    /// <summary>Executable memory the app writes itself and did not sign — some interpreters and
    /// older engines. Broader than <see cref="AllowJit"/>: reach for JIT first.</summary>
    public const string AllowUnsignedExecutableMemory =
        "com.apple.security.cs.allow-unsigned-executable-memory";

    /// <summary>
    /// Loading libraries this app's team did not sign — a plug-in host, a native dependency shipped
    /// by someone else, and (measured, not assumed) <b>the .NET runtime itself</b>.
    /// <para>
    /// A framework-dependent app loads Microsoft's <c>libhostfxr.dylib</c>, and under the hardened
    /// runtime library validation refuses it before a single line of the app runs: "mapping process
    /// and mapped file (non-platform) have different Team IDs". So this is the FIRST entitlement a
    /// hardened Photon app needs — earlier than <see cref="AllowJit"/>, which the app never reaches.
    /// </para>
    /// <para>
    /// Shipping the runtime INSIDE the bundle does not help, which is the reasonable guess and the
    /// wrong one: a self-contained hardened bundle re-signed with only <see cref="AllowJit"/> dies on
    /// the same dylib (measured, both ways). Only a fully AOT app, which loads no dylib at all, needs
    /// neither — and the SDK adds both for everything else, so an app author never meets this.
    /// </para>
    /// </summary>
    public const string DisableLibraryValidation = "com.apple.security.cs.disable-library-validation";

    /// <summary>Reading and writing the files a person picked in a dialog. The App Sandbox's whole
    /// point: what the user chose, and nothing else.</summary>
    public const string UserSelectedFiles = "com.apple.security.files.user-selected.read-write";

    /// <summary>Outgoing network connections from a sandboxed app (an update check, an API call).</summary>
    public const string NetworkClient = "com.apple.security.network.client";

    /// <summary>The App Sandbox itself — required by the Mac App Store, optional outside it.</summary>
    public const string AppSandbox = "com.apple.security.app-sandbox";
}
