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
    /// <summary>
    /// Pages the app maps through Apple's JIT protocol (<c>MAP_JIT</c>) — .NET's own JIT,
    /// JavaScriptCore. Without it the hardened runtime kills the process at the first such page,
    /// with SIGKILL/CODESIGNING: no exception, no stack, the process is simply gone.
    /// <para>
    /// NOT enough on its own for every engine that compiles at run time, which is what the name
    /// suggests and what this comment used to say. An engine that writes plain executable memory
    /// rather than going through <c>MAP_JIT</c> also needs
    /// <see cref="AllowUnsignedExecutableMemory"/>, and wasmtime — so YARA-X, which embeds it — is
    /// exactly that case. Measured by a consumer with a real certificate: <c>allow-jit</c> alone
    /// SIGKILLs on the first scan (<c>"namespace":"CODESIGNING"</c>, <c>"indicator":"Invalid
    /// Page"</c>); the two together pass.
    /// </para>
    /// </summary>
    public const string AllowJit = "com.apple.security.cs.allow-jit";

    /// <summary>
    /// Executable memory the app writes itself, outside <c>MAP_JIT</c> and unsigned — a WASM engine
    /// such as wasmtime, some interpreters, older engines.
    /// <para>
    /// NOT a broader fallback to try after <see cref="AllowJit"/>, which is what this comment used
    /// to say and which sent a consumer to ship a binary that died on its first scan. For an engine
    /// that maps executable pages the platform way, JIT is the answer; for one that does not, this
    /// is the REQUIREMENT, and the measured pair for wasmtime is both keys together.
    /// </para>
    /// <para>
    /// Nothing without a signing certificate exercises the hardened runtime, so a mistake here
    /// survives a fully green suite and appears at the first notarized release.
    /// </para>
    /// </summary>
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
