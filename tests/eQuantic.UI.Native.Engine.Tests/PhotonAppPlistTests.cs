using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The keys the SDK merges into every iOS app's Info.plist, read from the file the SDK ships. An app
/// author never sees this file, so nothing else would notice a key going: the app would build, and
/// the first sign would be a phone that refuses to launch it (#796).
/// </summary>
public class PhotonAppPlistTests
{
    private static readonly XElement Root = Load();

    /// <summary>
    /// Built with the iOS 27 SDK, an app without the scene life cycle does not launch (TN3187), and an
    /// app on Xcode 27 is built with that SDK whichever .NET pack resolves.
    /// </summary>
    [Fact]
    public void EveryIosAppDeclaresTheSceneLifeCycle()
    {
        var manifest = Value(Root, "UIApplicationSceneManifest");
        manifest.Should().NotBeNull("without the scene manifest an app built with the iOS 27 SDK does not launch");
        manifest!.Name.LocalName.Should().Be("dict");

        var multiple = Value(manifest, "UIApplicationSupportsMultipleScenes");
        multiple.Should().NotBeNull("a manifest that leaves it out leaves the answer to UIKit");
        multiple!.Name.LocalName.Should().Be("false",
            "Photon draws one window, and the shell keeps one screen for the process");
    }

    /// <summary>
    /// The delegate class is answered in C# by the shell's app delegate, where the compiler checks the
    /// type it names. A class NAME here would be a second answer, a string nothing checks.
    /// </summary>
    [Fact]
    public void TheSceneDelegateIsNamedInCodeNotInThePlist()
    {
        var manifest = Value(Root, "UIApplicationSceneManifest");
        manifest.Should().NotBeNull();
        Value(manifest!, "UISceneConfigurations").Should().BeNull(
            "PhotonAppDelegate.GetConfiguration names the scene delegate with typeof");
    }

    [Fact]
    public void TheLaunchScreenIsDeclared()
    {
        var launchScreen = Value(Root, "UILaunchScreen");
        launchScreen.Should().NotBeNull("without UILaunchScreen iOS runs the app in a compatibility window");
        launchScreen!.Name.LocalName.Should().Be("dict");
    }

    /// <summary>The element after a key in a plist dictionary, or null when the key is absent.</summary>
    private static XElement? Value(XElement dict, string key) =>
        dict.Elements("key").FirstOrDefault(element => element.Value == key)?.ElementsAfterSelf().FirstOrDefault();

    private static XElement Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the test reads the plist the SDK ships, so it has to find the tree");

        var path = Path.Combine(dir!.FullName, "src", "eQuantic.UI.Sdk.Native", "Sdk", "PhotonApp.plist");
        var document = XDocument.Load(path);
        return document.Root!.Element("dict")!;
    }
}
