using System.Xml.Linq;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// WHEN the SDK hands Apple's packaging what an iPhone app declared, read from the targets it ships.
/// Two ways of getting it wrong were both measured, and neither fails a build:
/// <list type="bullet">
/// <item>handed over after Compile into the EARLY list of manifests, the file is written and never
/// merged, and the app ships without its permission sentences (#872);</item>
/// <item>made to run before that early list, the target drags the compiler in front of the rest of
/// the build, and an app's <c>.resx</c> is no longer embedded in its assembly.</item>
/// </list>
/// What ships is proved by <c>scripts/check-ios-launch.sh</c>, on the built app. This holds the
/// shape that produces it, on every host.
/// </summary>
public class IosManifestTargetsTests
{
    private static readonly XElement Targets = Load();

    private static XElement Target(string name) =>
        Targets.Elements("Target").Single(target => (string?)target.Attribute("Name") == name);

    [Fact]
    public void TheManifestIsWrittenAfterTheCompiler_AndDoesNotBringItForward()
    {
        var writer = Target("EQuanticCapabilityManifest");

        ((string?)writer.Attribute("AfterTargets")).Should().Be("Compile",
            "the declarations are read off the compiled assembly");
        writer.Attribute("DependsOnTargets").Should().BeNull(
            "a dependency on Compile from a target the packaging runs early compiles before the " +
            "app's resources are prepared");
        Targets.Descendants("CollectAppManifestsDependsOn").Should().BeEmpty(
            "that list is collected before the compiler runs");
    }

    [Fact]
    public void ItIsHandedOverWhereTheFinalManifestIsWritten()
    {
        var contributor = Target("EQuanticContributeCapabilityManifest");

        ((string?)contributor.Attribute("BeforeTargets")).Should().Be("_WriteAppManifest");
        contributor.Descendants("_PostCompilePartialAppManifest")
            .Select(item => (string?)item.Attribute("Include"))
            .Should().Equal("$(_EqCapabilityPlist)");
        contributor.Descendants("PartialAppManifest").Should().BeEmpty(
            "the early list was compiled long before this runs");
    }

    private static XElement Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "eQuantic.UI.slnx"))
               && !Directory.Exists(Path.Combine(dir.FullName, "src", "eQuantic.UI.Sdk.Native")))
            dir = dir.Parent;
        return XDocument.Load(Path.Combine(dir!.FullName, "src", "eQuantic.UI.Sdk.Native", "Sdk", "Sdk.targets")).Root!;
    }
}
