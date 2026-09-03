using eQuantic.UI.Build;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What goes inside <c>Contents/MacOS</c>. Every rule here was learned from a bundle that failed to
/// sign or an app that failed to launch, and every one of them lived in an MSBuild glob where none
/// of them could be tested.
/// <para>
/// The stakes are higher than "a few extra megabytes": <c>codesign</c> refuses a bundle containing
/// a file it cannot sign, and it refuses the WHOLE bundle. One stray file leaves the app unsigned,
/// and an unsigned app is quietly refused by the capabilities that check who is asking.
/// </para>
/// </summary>
public class MacAppPayloadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"eq-payload-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void Given(params string[] relativePaths)
    {
        foreach (var relative in relativePaths)
        {
            var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "x");
        }
    }

    private IReadOnlyList<string> Selected(params string[] excluded) =>
        MacAppPayload.Select(_root, excluded).Select(path => path.Replace(Path.DirectorySeparatorChar, '/')).ToList();

    [Fact]
    public void TheAppsOwnFilesAreThePayload()
    {
        Given("Acme", "Acme.dll", "appsettings.json", "es/Acme.resources.dll");

        Selected().Should().Equal("Acme", "Acme.dll", "appsettings.json", "es/Acme.resources.dll");
    }

    [Fact]
    public void DebugSymbolsAreNot()
    {
        Given("Acme.dll", "Acme.pdb", "Third.Party.pdb");

        Selected().Should().Equal("Acme.dll");
    }

    /// <summary>
    /// An ALLOWLIST, not a denylist. The denylist this replaces named win/linux/android/ios and let
    /// <c>browser-wasm</c> through — Microsoft.Data.Sqlite ships a static archive under it, which is
    /// not Mach-O, and codesign refused the entire bundle.
    /// </summary>
    [Fact]
    public void OnlyRuntimesAMacProcessCanLoad()
    {
        Given(
            "runtimes/osx-arm64/native/libe_sqlite3.dylib",
            "runtimes/osx-x64/native/libe_sqlite3.dylib",
            "runtimes/unix/lib/net10.0/System.Data.dll",
            "runtimes/win-x64/native/e_sqlite3.dll",
            "runtimes/browser-wasm/nativeassets/net10.0/e_sqlite3.a",
            "runtimes/linux-x64/native/libe_sqlite3.so");

        Selected().Should().Equal(
            "runtimes/osx-arm64/native/libe_sqlite3.dylib",
            "runtimes/osx-x64/native/libe_sqlite3.dylib",
            "runtimes/unix/lib/net10.0/System.Data.dll");
    }

    /// <summary>
    /// Two RIDs shipping the same file name must not collide. The bug this pins is subtle: rooting
    /// the glob at <c>runtimes/osx*/**</c> makes the recursive part start AFTER the RID folder, so
    /// both copies land on the same path inside the bundle and one silently wins.
    /// </summary>
    [Fact]
    public void TwoRuntimeIdentifiersKeepTheirOwnFolders()
    {
        Given("runtimes/osx-arm64/native/lib.dylib", "runtimes/osx-x64/native/lib.dylib");

        Selected().Should().HaveCount(2).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void ABundleInsideThePayloadIsOutput_NeverInput()
    {
        Given("Acme.dll", "Acme.app/Contents/MacOS/Acme", "Acme.app/Contents/Info.plist");

        Selected().Should().Equal("Acme.dll");
    }

    /// <summary>
    /// The publish directory lives UNDER the build output, so a build-output bundle that does not
    /// exclude it ships an entire second copy of the app inside itself — 86 MB of it, measured on
    /// this repository's own desktop sample after a single publish.
    /// </summary>
    [Fact]
    public void ThePublishDirectoryIsNotPayloadOfTheBuild()
    {
        Given("Acme.dll", "publish/Acme.dll", "publish/appsettings.json");

        Selected(Path.Combine(_root, "publish")).Should().Equal("Acme.dll");
    }

    /// <summary>
    /// And it is excluded BY PATH, never by name: an app whose own data folder is called "publish"
    /// still ships it. The caller knows where the publish directory is; a guess does not.
    /// </summary>
    [Fact]
    public void ADirectoryMerelyCalledPublishIsStillPayload()
    {
        Given("Acme.dll", "content/publish/template.html");

        Selected().Should().Contain("content/publish/template.html");
    }

    /// <summary>
    /// The disk image lands BESIDE the app, in the publish directory, so the next publish copies it
    /// into the bundle — and codesign refuses the whole bundle over it, because a .dmg is a
    /// subcomponent it cannot sign. Found by publishing twice, which is the only way to find it: a
    /// single clean publish writes the image after the bundle and passes.
    /// </summary>
    [Fact]
    public void TheDiskImageIsNotPayloadOfTheNextBundle()
    {
        Given("Acme.dll", "Acme.dmg");

        Selected(Path.Combine(_root, "Acme.dmg")).Should().Equal("Acme.dll");
    }

    /// <summary>And a file exclusion is the file, not a prefix: excluding <c>Acme.dmg</c> must not
    /// take <c>Acme.dmg.manifest</c> with it.</summary>
    [Fact]
    public void ExcludingAFileTakesOnlyThatFile()
    {
        Given("Acme.dmg", "Acme.dmg.manifest");

        Selected(Path.Combine(_root, "Acme.dmg")).Should().Equal("Acme.dmg.manifest");
    }

    [Fact]
    public void PopulateRebuildsTheBundleRatherThanToppingItUp()
    {
        Given("Acme.dll", "Stale.dll");
        var bundle = Path.Combine(_root, "out.app");
        MacAppPayload.Populate(_root, bundle).Should().Be(2);

        // The file leaves the payload — a package dropped, a RID newly excluded. A copy alone never
        // removes anything, so the stale file stayed in the bundle forever and kept failing
        // codesign long after the exclusion that should have removed it was fixed.
        File.Delete(Path.Combine(_root, "Stale.dll"));
        MacAppPayload.Populate(_root, bundle).Should().Be(1);

        File.Exists(Path.Combine(bundle, "Contents", "MacOS", "Stale.dll")).Should().BeFalse();
        File.Exists(Path.Combine(bundle, "Contents", "MacOS", "Acme.dll")).Should().BeTrue();
    }
}
