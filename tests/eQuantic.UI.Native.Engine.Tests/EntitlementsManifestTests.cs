using eQuantic.UI.Build;
using eQuantic.UI.Primitives;
using FluentAssertions;

// THIS assembly is the fixture: the reader works off a compiled PE file, so declaring the
// attributes here means the test reads exactly what an app's build would.
[assembly: PhotonEntitlement(PhotonEntitlements.AllowJit)]
[assembly: PhotonEntitlement(PhotonEntitlements.DisableLibraryValidation)]
[assembly: PhotonEntitlement(PhotonEntitlements.AllowJit)]   // declared twice, deliberately

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the app needs the SYSTEM to permit, read from its own assembly and written where codesign
/// is handed it. The failure this guards is invisible in development: an ad-hoc build needs no
/// entitlements at all, so a missing one shows up as a signed app that dies on someone else's
/// machine — SIGKILL/CODESIGNING for a JIT page, or "different Team IDs" at launch.
/// </summary>
public class EntitlementsManifestTests
{
    private static string ThisAssembly => typeof(EntitlementsManifestTests).Assembly.Location;

    [Fact]
    public void ReadsWhatTheAssemblyDeclares_OnceEach_InAStableOrder()
    {
        var declared = EntitlementsManifest.Read(ThisAssembly);

        declared.Should().Equal(
            PhotonEntitlements.AllowJit,
            PhotonEntitlements.DisableLibraryValidation);
    }

    [Fact]
    public void WritesThePlistCodesignReads()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eq-{Guid.NewGuid():N}.entitlements");
        try
        {
            EntitlementsManifest.Write(ThisAssembly, path).Should().BeTrue();

            var plist = File.ReadAllText(path);
            plist.Should().Contain("<key>com.apple.security.cs.allow-jit</key>");
            plist.Should().Contain("<key>com.apple.security.cs.disable-library-validation</key>");
            plist.Should().Contain("<true/>");
            plist.Should().StartWith("<?xml");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnAssemblyThatDeclaresNone_GetsNoFile()
    {
        // Not an empty file: signing WITH an empty entitlements plist is not the same as signing
        // without one — it grants nothing and still changes the signature.
        var path = Path.Combine(Path.GetTempPath(), $"eq-{Guid.NewGuid():N}.entitlements");
        var noDeclarations = typeof(string).Assembly.Location;

        EntitlementsManifest.Write(noDeclarations, path).Should().BeFalse();
        File.Exists(path).Should().BeFalse();
    }
}
