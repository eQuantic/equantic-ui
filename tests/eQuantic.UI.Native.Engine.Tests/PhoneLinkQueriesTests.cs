using System.Xml.Linq;
using eQuantic.UI.Build;
using eQuantic.UI.Native.Hosting;
using FluentAssertions;

// What a Program.cs with `builder.Workspace.OpensMail().OpensPhone()` compiles to. This assembly also
// declares the schemes `acme` and `acme-beta` as its own (see MacAppPackagingTests).
[assembly: PhotonOpenedScheme("tel")]
[assembly: PhotonOpenedScheme("mailto")]

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The schemes an app opens, as each phone's build lists them. A phone answers "does something open
/// this link" only for what its manifest names (<c>LSApplicationQueriesSchemes</c> on iOS,
/// <c>queries</c> on Android 11 and later), so the build writes both from what the compiled assembly
/// declares, and the app keeps no manifest of its own for either.
/// </summary>
public class PhoneLinkQueriesTests
{
    private static readonly string Declaring = typeof(PhoneLinkQueriesTests).Assembly.Location;
    private static readonly string DeclaringNothing = typeof(DisplayList).Assembly.Location;
    private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

    private static string Scratch() =>
        Path.Combine(Path.GetTempPath(), "eq-queries-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TheOpenedSchemesAreReadOffTheAssembly_InOrdinalOrder()
    {
        OpenedSchemes.Read(Declaring).Should().Equal("mailto", "tel");
        OpenedSchemes.Read(DeclaringNothing).Should().BeEmpty();
    }

    /// <summary>
    /// iOS: what the app opens and its own schemes, the ones its policy hands over beyond the web, in
    /// the partial manifest the URL types and the permission sentences already travel in.
    /// </summary>
    [Fact]
    public void AnIphoneMayAskAboutWhatTheAppOpens_AndItsOwnSchemes()
    {
        var directory = Scratch();
        try
        {
            var plist = Path.Combine(directory, "Capabilities.plist");

            CapabilityManifest.Write(Declaring, plist, "com.acme.app").Should().BeTrue();

            var asked = XDocument.Load(plist).Descendants("key")
                .Single(key => key.Value == "LSApplicationQueriesSchemes")
                .ElementsAfterSelf().First();
            asked.Name.LocalName.Should().Be("array");
            asked.Elements("string").Select(element => element.Value)
                .Should().Equal("acme", "acme-beta", "mailto", "tel");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Android: one VIEW intent per scheme under <c>queries</c>, the web's two first, because every
    /// app's policy hands those over and resolving them answers "nothing" too when they are not
    /// listed. The app's own schemes are not there: an app always sees itself.
    /// </summary>
    [Fact]
    public void AnAndroidAppMayAskAboutTheWeb_AndWhatItOpens()
    {
        var directory = Scratch();
        try
        {
            var manifest = Path.Combine(directory, "obj", "Queries.xml");

            AndroidQueries.Write(Declaring, manifest);

            Queried(manifest).Should().Equal("http", "https", "mailto", "tel");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AnAndroidAppThatOpensNothingMore_StillAsksAboutTheWeb()
    {
        var directory = Scratch();
        try
        {
            var manifest = Path.Combine(directory, "Queries.xml");

            AndroidQueries.Write(DeclaringNothing, manifest);

            Queried(manifest).Should().Equal("http", "https");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// The merge runs again whenever this file is newer than the merged manifest, so a file touched on
    /// every build makes every build merge. It is written when what it says changed.
    /// </summary>
    [Fact]
    public void WrittenAgainWithNothingChanged_TheFragmentIsLeftAsItWas()
    {
        var directory = Scratch();
        try
        {
            var manifest = Path.Combine(directory, "Queries.xml");
            AndroidQueries.Write(Declaring, manifest);
            var before = DateTime.UtcNow.AddHours(-1);
            File.SetLastWriteTimeUtc(manifest, before);

            AndroidQueries.Write(Declaring, manifest);
            File.GetLastWriteTimeUtc(manifest).Should().Be(before);

            AndroidQueries.Write(DeclaringNothing, manifest);
            File.GetLastWriteTimeUtc(manifest).Should().BeAfter(before, "what it says changed");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>The schemes a fragment lets the app ask about, each one checked for the shape the
    /// Android merger reads: a VIEW action and a data element with only a scheme.</summary>
    private static string[] Queried(string path)
    {
        var root = XDocument.Load(path).Root!;
        root.Name.LocalName.Should().Be("manifest");
        return root.Element("queries")!.Elements("intent").Select(intent =>
        {
            intent.Element("action")!.Attribute(Android + "name")!.Value.Should().Be("android.intent.action.VIEW");
            return intent.Element("data")!.Attribute(Android + "scheme")!.Value;
        }).ToArray();
    }
}
