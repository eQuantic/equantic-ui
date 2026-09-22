namespace eQuantic.UI.Native.Hosting;

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
    /// rather than going through <c>MAP_JIT</c> needs <see cref="AllowUnsignedExecutableMemory"/>,
    /// and wasmtime — so YARA-X, which embeds it — is measured to be that shape. Which shape a
    /// given engine is, is the engine's own business: "a WASM runtime" is not the answer, because
    /// nothing stops one from mapping the platform way.
    /// </para>
    /// <para>
    /// AN APP RARELY DECLARES THIS ONE. A hardened non-AOT build already gets it from the SDK
    /// beside <see cref="DisableLibraryValidation"/>, because the .NET runtime JITs its own methods
    /// (see <c>_EqRuntimeEntitlements</c> in the native SDK's targets). Declaring it again is a
    /// harmless no-op — and worth knowing, because a consumer declared it, watched the crash stop,
    /// and credited the wrong key: the entitlement that changed was the other one, and this had
    /// been in every one of their bundles all along. Under <c>PublishAot</c> the SDK adds nothing,
    /// so an AOT app that embeds such an engine declares whatever it needs itself — a case nobody
    /// here has measured.
    /// </para>
    /// </summary>
    public const string AllowJit = "com.apple.security.cs.allow-jit";

    /// <summary>
    /// Executable memory the app writes itself, outside <c>MAP_JIT</c> and unsigned — wasmtime,
    /// some interpreters, older engines. The shape is what decides, not the category: an engine is
    /// this case because of how it maps its pages, and another WASM runtime may well not be.
    /// <para>
    /// NOT a broader fallback to try after <see cref="AllowJit"/>, which is what this comment used
    /// to say and which sent a consumer to ship a binary that died on its first scan. For an engine
    /// that maps executable pages the platform way, JIT is the answer; for one that does not, this
    /// is the REQUIREMENT.
    /// </para>
    /// <para>
    /// So for a hardened NON-AOT app embedding an engine of that shape — wasmtime is the one
    /// measured — this is the ONE key the app declares: <see cref="AllowJit"/> is already in the
    /// bundle from the SDK. Measured, all three rows with a
    /// real certificate — JIT alone SIGKILLs (<c>"namespace":"CODESIGNING"</c>,
    /// <c>"indicator":"Invalid Page"</c>); JIT plus this passes; and THIS ALONE passes too, which is
    /// what proved the JIT declaration was never the variable. Under <c>PublishAot</c> the SDK
    /// declares nothing and the recipe is the app's own to work out; nobody has measured it.
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
    /// point: what the user chose, and nothing else. Its gate is <see cref="AppSandbox"/>, not the
    /// hardened runtime — an app that is not sandboxed reaches the file system regardless, and this
    /// key grants it nothing it did not already have.</summary>
    public const string UserSelectedFiles = "com.apple.security.files.user-selected.read-write";

    /// <summary>Outgoing network connections from a sandboxed app (an update check, an API call).
    /// Gated by <see cref="AppSandbox"/> in the same way: outside the sandbox nothing blocks an
    /// outgoing connection, so declaring this alone changes nothing.</summary>
    public const string NetworkClient = "com.apple.security.network.client";

    /// <summary>The App Sandbox itself — required by the Mac App Store, optional outside it.
    /// <para>
    /// The SWITCH for the permissions above, and independent of the hardened runtime in both
    /// directions: an app can be sandboxed without being hardened and hardened without being
    /// sandboxed. It is enforced from the signature, an AD-HOC one included, which is how sandboxing
    /// is exercised on a machine with no certificate — so unlike
    /// <see cref="IsHardenedRuntimeException"/>'s family, this one is not invisible in development.
    /// </para>
    /// <para>
    /// The exception worth knowing: a few sandbox entitlements — app groups, iCloud, push — must be
    /// authorised by a provisioning profile and do NOT take under a plain ad-hoc signature. The ones
    /// named here are not among them.
    /// </para>
    /// </summary>
    public const string AppSandbox = "com.apple.security.app-sandbox";

    /// <summary>
    /// Apple's namespace for the hardened runtime's own exceptions. A PREFIX rather than a list,
    /// because the list is Apple's and it grows: an app declares by KEY — this class names the
    /// common ones and fences none of the rest out — so a rule written as an enumeration would be
    /// wrong the first time Apple added one.
    /// </summary>
    public const string HardenedRuntimePrefix = "com.apple.security.cs.";

    /// <summary>
    /// Whether the HARDENED RUNTIME is what consults this key — the one family that grants nothing
    /// without it, because every member of it exists to relax a protection the hardened runtime
    /// imposes. False for the App Sandbox's permissions, which answer to <see cref="AppSandbox"/>.
    /// <para>
    /// One caveat this deliberately does not try to express: a handful of keys are in BOTH lists
    /// (<c>com.apple.security.device.camera</c>, <c>…personal-information.*</c>,
    /// <c>…automation.apple-events</c>), because the hardened runtime gates those resources too.
    /// That makes them consulted under either regime, so calling them false here is the safe answer:
    /// this question is only ever asked to find declarations that do NOTHING, and a key in both
    /// lists never does nothing.
    /// </para>
    /// </summary>
    public static bool IsHardenedRuntimeException(string entitlement) =>
        entitlement.StartsWith(HardenedRuntimePrefix, StringComparison.Ordinal);
}
