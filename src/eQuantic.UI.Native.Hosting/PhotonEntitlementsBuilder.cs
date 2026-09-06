using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// What this app needs the SYSTEM to permit, stated where every other app fact is stated — on the
/// builder, in <c>Program.cs</c>, in C#. Nobody opens an entitlements plist; the generator turns
/// these calls into assembly declarations and the SDK hands the file to <c>codesign</c>.
/// <para>
/// Only what YOUR code needs belongs here. What the .NET RUNTIME needs under the hardened runtime —
/// loading its own dylibs, executing JIT-compiled methods — the SDK adds by itself, because it
/// already knows whether you are AOT and whether hardening is on, and because an app author should
/// never have to learn that .NET on macOS has a library-validation problem.
/// </para>
/// <code>
/// builder.Entitlements.RequireNetworkClient();
/// builder.Entitlements.RequireUserSelectedFiles();
/// </code>
/// </summary>
public sealed class PhotonEntitlementsBuilder
{
    private readonly HashSet<string> _declared = new(StringComparer.Ordinal);

    /// <summary>What this app declared, in case it wants to explain itself.</summary>
    public IReadOnlyCollection<string> Declared => _declared;

    /// <summary>An engine that maps executable pages the platform way (<c>MAP_JIT</c>) — .NET's own
    /// JIT, JavaScriptCore. Rarely worth declaring: a hardened non-AOT build already gets this key
    /// from the SDK, because the .NET runtime needs it for itself. A WASM engine is NOT this case
    /// and calling this instead ships a binary that dies — see
    /// <see cref="RequireUnsignedExecutableMemory"/> and <see cref="PhotonEntitlements.AllowJit"/>
    /// for what was measured.</summary>
    public PhotonEntitlementsBuilder RequireJit() => Require(PhotonEntitlements.AllowJit);

    /// <summary>Executable memory the app writes itself, outside <c>MAP_JIT</c> and unsigned — a
    /// WASM engine such as wasmtime. NOT a fallback to reach for after <see cref="RequireJit"/>: for
    /// an engine of that shape it is the requirement, and for a hardened non-AOT app it is the ONE
    /// key to declare, since the SDK already puts the JIT key in the bundle.</summary>
    public PhotonEntitlementsBuilder RequireUnsignedExecutableMemory() =>
        Require(PhotonEntitlements.AllowUnsignedExecutableMemory);

    /// <summary>Loading libraries this app's team did not sign — a plug-in host, a third-party
    /// native dependency.</summary>
    public PhotonEntitlementsBuilder RequireForeignLibraries() =>
        Require(PhotonEntitlements.DisableLibraryValidation);

    /// <summary>Reading and writing the files a person picked in a dialog.</summary>
    public PhotonEntitlementsBuilder RequireUserSelectedFiles() =>
        Require(PhotonEntitlements.UserSelectedFiles);

    /// <summary>Outgoing network connections from a sandboxed app.</summary>
    public PhotonEntitlementsBuilder RequireNetworkClient() =>
        Require(PhotonEntitlements.NetworkClient);

    /// <summary>The App Sandbox itself — required by the Mac App Store, optional outside it.</summary>
    public PhotonEntitlementsBuilder RequireAppSandbox() => Require(PhotonEntitlements.AppSandbox);

    /// <summary>Any key by name, because the list is Apple's and it grows. Prefer the named methods
    /// above where one exists: they say WHEN you need it, which a key never does.</summary>
    public PhotonEntitlementsBuilder Require(string entitlement)
    {
        if (!string.IsNullOrWhiteSpace(entitlement)) _declared.Add(entitlement.Trim());
        return this;
    }
}
