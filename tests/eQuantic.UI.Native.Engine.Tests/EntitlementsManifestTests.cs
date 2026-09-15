using System.Diagnostics;
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
/// is handed it. The failure this guards is invisible in development: an ad-hoc build signs without
/// the hardened runtime, so a missing exception from THAT family shows up as a signed app that dies
/// on someone else's machine — SIGKILL/CODESIGNING for a JIT page, or "different Team IDs" at
/// launch.
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
            EntitlementsManifest.Write(ThisAssembly, path).Should().Equal(
                PhotonEntitlements.AllowJit,
                PhotonEntitlements.DisableLibraryValidation);

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

        EntitlementsManifest.Write(noDeclarations, path).Should().BeEmpty();
        File.Exists(path).Should().BeFalse();
    }

    /// <summary>
    /// The families, which are the whole reason EQ4003 exists: Apple has one list of keys that only
    /// mean anything under the hardened runtime, and another that answers to the App Sandbox. A
    /// warning that read them as one rule told an app declaring <c>network.client</c> that its
    /// declaration did nothing, in a build where the sandbox would have honoured it.
    /// </summary>
    [Fact]
    public void OnlyTheHardenedRuntimesOwnExceptionsAnswerToIt()
    {
        new[]
        {
            PhotonEntitlements.AllowJit,
            PhotonEntitlements.AllowUnsignedExecutableMemory,
            PhotonEntitlements.DisableLibraryValidation,
        }.Should().OnlyContain(key => PhotonEntitlements.IsHardenedRuntimeException(key));

        new[]
        {
            PhotonEntitlements.AppSandbox,
            PhotonEntitlements.NetworkClient,
            PhotonEntitlements.UserSelectedFiles,
            // Not named by this class, and the point of a PREFIX rather than a list: Apple owns the
            // set and an app declares by key, so a rule written as an enumeration would be wrong the
            // first time one of these was passed to Require(string).
            "com.apple.security.device.camera",
            "com.apple.security.files.downloads.read-write",
        }.Should().OnlyContain(key => !PhotonEntitlements.IsHardenedRuntimeException(key));
    }

    // ---- EQ4003, run through the tool that raises it -------------------------------------------
    //
    // Unit-testing the predicate above proves the taxonomy and NOT the diagnostic: the two mistakes
    // being fixed here — firing in a development build, and firing for a sandbox key — are both in
    // the gate around it, which only exists inside eqicon's entitlements verb. So these rows drive
    // the real verb, with the real arguments the SDK passes, and read what it printed.

    /// <summary>The verb, with the four arguments the SDK passes it. <paramref name="also"/> is how
    /// a key reaches the file without a second fixture assembly to carry the attribute — the SDK
    /// uses it for what the .NET runtime itself needs.</summary>
    private static (string Output, int Exit) Eqicon(
        string appAssembly, string plist, string hardened, string also = "")
    {
        // Beside the test's own assembly, because eqicon is a ProjectReference of this project and
        // its output lands here. Never a path into src/bin: that one is whatever was built last.
        var tool = Path.Combine(AppContext.BaseDirectory, "eqicon.dll");
        File.Exists(tool).Should().BeTrue($"eqicon must be beside the tests, at {tool}");

        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
                 {
                     tool, "entitlements", "--assembly", appAssembly, "--plist", plist,
                     "--also", also, "--hardened", hardened,
                 })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (output, process.ExitCode);
    }

    private static void WithPlist(Action<string> body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"eq-{Guid.NewGuid():N}.entitlements");
        try { body(path); }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void HardeningTurnedOffByHand_OverAnExceptionThatNeedsIt_IsReported()
    {
        WithPlist(plist =>
        {
            var (output, exit) = Eqicon(ThisAssembly, plist, hardened: "false");

            exit.Should().Be(0, "it is a warning: the bundle is still built and still signed");
            output.Should().Contain("warning EQ4003");
            output.Should().Contain(PhotonEntitlements.AllowJit,
                "the message names the keys, because 'some entitlement' is not something to act on");
        });
    }

    [Fact]
    public void AnOrdinaryDevelopmentBuild_IsNotReported()
    {
        // THE REGRESSION. EQuanticHardenedRuntime is empty in every build that did not ask for it,
        // there is no certificate, the bundle is ad-hoc signed, and the hardened runtime is out of
        // reach by construction. The warning this replaced fired here — on every build, with advice
        // ("set EQuanticSigningIdentity") that turns the hardened runtime ON and so could never
        // have produced the warning it was attached to.
        WithPlist(plist =>
        {
            var (output, exit) = Eqicon(ThisAssembly, plist, hardened: "");

            exit.Should().Be(0);
            output.Should().NotContain("EQ4003");
        });
    }

    [Fact]
    public void ASandboxPermission_IsNotReported_EvenWithHardeningOff()
    {
        // The other half of the same mistake: `com.apple.security.network.client` is gated by the
        // App Sandbox, which an ad-hoc signature enforces, and the hardened runtime never consults
        // it. WalletMobile declares exactly this one key and nothing else, which is how the wrong
        // claim got a sample to print it on every build.
        WithPlist(plist =>
        {
            var noDeclarations = typeof(string).Assembly.Location;
            var (output, exit) = Eqicon(noDeclarations, plist, hardened: "false",
                also: PhotonEntitlements.NetworkClient);

            exit.Should().Be(0);
            File.ReadAllText(plist).Should().Contain(PhotonEntitlements.NetworkClient,
                "the key still reaches the signature — it is honoured, not ignored");
            output.Should().NotContain("EQ4003");
        });
    }
}
