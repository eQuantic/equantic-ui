using eQuantic.UI.Build;
using eQuantic.UI.Primitives;
using FluentAssertions;

// THIS assembly is the fixture, like the entitlements test beside it: the reader works off a
// compiled PE file, so declaring here means the test reads exactly what an app's build would.
[assembly: PhotonBundleKey("NSHumanReadableCopyright", "© 2026 Acme", PhotonBundleValueKind.Text)]
[assembly: PhotonBundleKey("LSUIElement", "true", PhotonBundleValueKind.Flag)]
[assembly: PhotonBundleKey("LSMinimumSystemVersion", "13.0", PhotonBundleValueKind.Text)]
[assembly: PhotonBundleKey("", "acme", PhotonBundleValueKind.UrlScheme)]
[assembly: PhotonBundleKey("", "acme-beta", PhotonBundleValueKind.UrlScheme)]

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The three halves of shipping a macOS app that used to be unsayable: what the system reads about
/// it, what goes inside the bundle, and what version it claims to be.
/// </summary>
public class MacAppPackagingTests
{
    private static string ThisAssembly => typeof(MacAppPackagingTests).Assembly.Location;

    [Fact]
    public void ReadsEveryKindTheAppDeclared()
    {
        var facts = BundleManifest.Read(ThisAssembly);

        facts.Should().Contain(new BundleManifest.Fact(
            "NSHumanReadableCopyright", "© 2026 Acme", BundleManifest.ValueKind.Text));
        facts.Should().Contain(new BundleManifest.Fact(
            "LSUIElement", "true", BundleManifest.ValueKind.Flag));
        // The schemes keep the order they were declared in and are not keyed: they become one array.
        facts.Where(fact => fact.Kind == BundleManifest.ValueKind.UrlScheme)
             .Select(fact => fact.Value)
             .Should().Equal("acme", "acme-beta");
    }

    [Fact]
    public void AnAssemblyThatDeclaresNone_ReadsAsNone() =>
        BundleManifest.Read(typeof(string).Assembly.Location).Should().BeEmpty();

    [Fact]
    public void ADeclaredValueBeatsTheFrameworksDefault_AndIsWrittenOnce()
    {
        var bundle = Bundle();
        try
        {
            MacAppBundle.Write(bundle, "Acme", "Acme", "com.acme.app", "1.2.3", null, ThisAssembly);
            var plist = File.ReadAllText(Path.Combine(bundle, "Contents", "Info.plist"));

            // The framework writes 11.0 and false; this app said otherwise, and said it in C#.
            plist.Should().Contain("<key>LSMinimumSystemVersion</key>\n\t<string>13.0</string>");
            plist.Should().Contain("<key>LSUIElement</key>\n\t<true/>");

            // ONCE each. A plist with a key twice is not an error anywhere — it simply means
            // whichever the parser reaches last, which is how a declaration silently loses.
            Occurrences(plist, "<key>LSMinimumSystemVersion</key>").Should().Be(1);
            Occurrences(plist, "<key>LSUIElement</key>").Should().Be(1);
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }

    [Fact]
    public void TheUrlsTheAppAnswersTo_BecomeTheArrayTheSystemReads()
    {
        var bundle = Bundle();
        try
        {
            MacAppBundle.Write(bundle, "Acme", "Acme", "com.acme.app", "1.2.3", null, ThisAssembly);
            var plist = File.ReadAllText(Path.Combine(bundle, "Contents", "Info.plist"));

            plist.Should().Contain("<key>CFBundleURLTypes</key>");
            plist.Should().Contain("<string>acme</string>");
            plist.Should().Contain("<string>acme-beta</string>");
            // One <dict> holding both schemes, not one entry each: they are the same app.
            Occurrences(plist, "<key>CFBundleURLSchemes</key>").Should().Be(1);
        }
        finally
        {
            Directory.Delete(bundle, recursive: true);
        }
    }

    /// <summary>
    /// Apple's version is one to three integers, and .NET's routinely is not — this repository's own
    /// is <c>0.2.0-preview.46</c>, and it went into the plist verbatim until now. That bundle is
    /// refused by notarization and by the App Store, in a vocabulary that names nothing you wrote.
    /// </summary>
    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("0.2.0-preview.46", "0.2.0")]
    [InlineData("1.0.0+sha.abc1234", "1.0.0")]
    [InlineData("2.1", "2.1")]
    [InlineData("1.2.3.4", "1.2.3")]      // Apple takes at most three
    [InlineData("01.02", "1.2")]          // and no leading zeros
    [InlineData("preview", "1.0.0")]      // nothing numeric at all: a version that is at least legal
    [InlineData("", "1.0.0")]
    public void TheVersionIsTheOnlyShapeAppleAccepts(string declared, string expected) =>
        MacAppBundle.AppleVersion(declared).Should().Be(expected);

    private static string Bundle()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eq-{Guid.NewGuid():N}.app");
        Directory.CreateDirectory(path);
        return path;
    }

    private static int Occurrences(string text, string needle)
    {
        var count = 0;
        for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
