using eQuantic.UI.Build;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What an app declared in C#, as the partial Info.plist Apple's packaging merges into an iPhone
/// app. The URL schemes reached the Mac's bundle and stopped there (#777): a phone build read the
/// permission sentences off the assembly and never the schemes beside them, so a link opened
/// nothing.
/// <para>
/// This assembly declares two schemes and no capability (see <c>MacAppPackagingTests</c>), which is
/// the case that wrote no file at all.
/// </para>
/// </summary>
public class PhonePartialManifestTests
{
    private static readonly string Declaring = typeof(PhonePartialManifestTests).Assembly.Location;
    private static readonly string DeclaringNothing = typeof(DisplayList).Assembly.Location;

    private static string Scratch() =>
        Path.Combine(Path.GetTempPath(), "eq-partial-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TheSchemesAnAppDeclares_ReachTheManifest_NamedByTheAppsIdentifier()
    {
        var directory = Scratch();
        try
        {
            var plist = Path.Combine(directory, "obj", "Capabilities.plist");

            CapabilityManifest.Write(Declaring, plist, "com.acme.app").Should().BeTrue(
                "an app that declares only a scheme still has something for the system to read");

            var written = File.ReadAllText(plist);
            written.Should().Contain("<key>CFBundleURLTypes</key>");
            written.Should().Contain("<key>CFBundleURLName</key>").And.Contain("<string>com.acme.app</string>");
            written.Should().Contain("<string>acme</string>").And.Contain("<string>acme-beta</string>");
            // One entry holding both schemes, as the Mac's bundle writes it: they are the same app.
            Occurrences(written, "<key>CFBundleURLSchemes</key>").Should().Be(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WithNoIdentifier_TheUrlTypeIsWrittenWithoutAName()
    {
        var directory = Scratch();
        try
        {
            var plist = Path.Combine(directory, "Capabilities.plist");

            CapabilityManifest.Write(Declaring, plist).Should().BeTrue();

            var written = File.ReadAllText(plist);
            written.Should().Contain("<key>CFBundleURLSchemes</key>");
            written.Should().NotContain("CFBundleURLName");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// The SDK merges the file whenever it exists. Left by an earlier build, it went on answering a
    /// scheme (and asking for a permission) the app had stopped declaring.
    /// </summary>
    [Fact]
    public void WithNothingDeclared_AManifestFromAnEarlierBuildIsRemoved()
    {
        var directory = Scratch();
        Directory.CreateDirectory(directory);
        try
        {
            var plist = Path.Combine(directory, "Capabilities.plist");
            File.WriteAllText(plist, "<plist><dict><key>CFBundleURLTypes</key></dict></plist>");

            CapabilityManifest.Write(DeclaringNothing, plist, "com.acme.app").Should().BeFalse();

            File.Exists(plist).Should().BeFalse("what it said is no longer what the app declares");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A first build: nothing declared, and the directory the file would go in is not there
    /// yet. Removing "the file" must not be an error.</summary>
    [Fact]
    public void WithNothingDeclared_AndNoDirectoryYet_ThereIsNothingToDo()
    {
        var plist = Path.Combine(Scratch(), "obj", "Capabilities.plist");

        CapabilityManifest.Write(DeclaringNothing, plist).Should().BeFalse();

        Directory.Exists(Path.GetDirectoryName(plist)).Should().BeFalse();
    }

    private static int Occurrences(string text, string needle)
    {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
