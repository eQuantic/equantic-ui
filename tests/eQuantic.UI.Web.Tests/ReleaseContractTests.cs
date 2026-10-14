using System.Reflection;
using System.Runtime.Loader;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What a release promises, and to which assembly (#899, #324, #323).
/// <para>
/// Every shipping assembly says its surface class: <c>contract</c>, what an app compiles against and
/// the compatibility promise covers, <c>tool</c>, the SDK's own compiler and code writers, or
/// <c>experimental</c>, the native track, which 1.0 does not promise yet and which .NET's own
/// <c>[Experimental("EQX0001")]</c> marks. And a stable version removes no member of the contract:
/// the build refuses it, so it fails the pull request that makes it.
/// </para>
/// <para>
/// The cases that build something run <c>dotnet</c>, as the release runs it, because the rule lives
/// in MSBuild and a test of a copy of it would prove the copy.
/// </para>
/// </summary>
public class ReleaseContractTests
{
    [Fact]
    public void EveryShippingAssemblyThisRunCarries_SaysTheClassItsNameGivesIt()
    {
        var read = SurfaceClass.ShippingAssemblies()
            .Select(assembly => (Name: assembly.GetName().Name!, Class: SurfaceClass.Of(assembly)))
            .ToList();

        read.Select(assembly => assembly.Name).Should().Contain(
            ["eQuantic.UI.Primitives", "eQuantic.UI.Components", "eQuantic.UI.Web", "eQuantic.UI.Compiler"],
            "the run carries the vocabulary, the components, the web realizer and the compiler, and a scan that "
            + "found fewer has lost its way");

        var wrong = read
            .Where(assembly => assembly.Class != Expected(assembly.Name))
            .Select(assembly => $"{assembly.Name} says {assembly.Class ?? "nothing"}, where {Expected(assembly.Name)} was due");
        string.Join(Environment.NewLine, wrong).Should().BeEmpty(
            "Directory.Build.targets writes the class into every shipping assembly: the native track is "
            + "experimental, the compiler and the code writers are tools, and every other one is the contract");
    }

    /// <summary>
    /// The build target that holds the promise, run as a build runs it, on a contract project and on a
    /// tool, at a stable version and at a preview, with a declared API made for the case.
    /// </summary>
    [Theory]
    [InlineData("eQuantic.UI.Primitives", "1.1.0", true, false)]
    [InlineData("eQuantic.UI.Primitives", "2.0.0-preview.1", true, true)]
    [InlineData("eQuantic.UI.Primitives", "2.0.0", true, true)]
    [InlineData("eQuantic.UI.Primitives", "1.1.0+5a1b2c3", true, false)]
    [InlineData("eQuantic.UI.Primitives", "1.1.0", false, true)]
    [InlineData("eQuantic.UI.Compiler", "1.1.0", true, true)]
    public void AStableVersionRemovesNoContractMember_AndTheBuildSaysSo(
        string project, string version, bool removes, bool builds)
    {
        var scratch = Directory.CreateTempSubdirectory("eq-contract-").FullName;
        try
        {
            var unshipped = Path.Combine(scratch, "PublicAPI.Unshipped.txt");
            File.WriteAllText(unshipped, "#nullable enable\n" + (removes
                ? "*REMOVED*static eQuantic.UI.Primitives.Space.Probe.get -> int\n"
                : "static eQuantic.UI.Primitives.Space.Probe.get -> int\n"));

            var (exitCode, output, error) = SdkProbe.Run(null, null, "msbuild",
                Path.Combine("src", project, $"{project}.csproj"),
                "-nologo", "-nr:false", "-v:m", "-t:EqStableVersionRemovesNoContractMember",
                $"-p:Version={version}", $"-p:_EqUnshippedFile={unshipped}");

            if (builds)
            {
                exitCode.Should().Be(0, output + error);
                return;
            }

            exitCode.Should().NotBe(0, "a stable version removes no member of the contract");
            (output + error).Should()
                .Contain("eQuantic.UI.Primitives.Space.Probe.get", "the refusal names the member")
                .And.Contain("[Obsolete]", "and the way on");
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    /// <summary>
    /// The case the mark exists for: a project that is not a Photon app uses the native track, and
    /// .NET stops its build with EQX0001 and the wiki's address. The project lives outside this
    /// repository on purpose, where none of its own build files reach it, as an app's project would.
    /// </summary>
    [Fact]
    public void AProjectThatUsesTheNativeTrackWithoutItsSdk_FailsWithEqx0001_UntilItSaysItMeansTo()
    {
        var scratch = Directory.CreateTempSubdirectory("eq-experimental-").FullName;
        try
        {
            var engine = Path.Combine(SdkProbe.RepoRoot(), "src", "eQuantic.UI.Native.Engine", "eQuantic.UI.Native.Engine.csproj");
            // The suppression goes in the probe's own project, the way an app writes it. Handed to the
            // build as a global property it would replace the NoWarn of every project the build
            // reaches, the engine's own among them.
            string Project(string noWarn) => $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <NoWarn>$(NoWarn){noWarn}</NoWarn>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="{engine}" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(scratch, "Probe.csproj"), Project(string.Empty));
            File.WriteAllText(Path.Combine(scratch, "Probe.cs"),
                "public static class Probe { public static float Alpha() => eQuantic.UI.Native.Engine.LinearColor.Transparent.A; }\n");

            var (refused, output, error) = SdkProbe.Run(scratch, null, "build", "-nologo", "-v:m");
            refused.Should().NotBe(0, "a project that uses the native track without its SDK has to say it means to");
            (output + error).Should()
                .Contain("error EQX0001", "the mark is .NET's own, and it stops the build")
                .And.Contain("wiki/Photon#the-native-track-is-experimental-in-10", "and it says where to read why");

            File.WriteAllText(Path.Combine(scratch, "Probe.csproj"), Project(";EQX0001"));
            var (built, quietOutput, quietError) = SdkProbe.Run(scratch, null, "build", "-nologo", "-v:m");
            built.Should().Be(0, quietOutput + quietError);

            var copied = Directory.GetFiles(scratch, "eQuantic.UI.Native.Engine.dll", SearchOption.AllDirectories)
                .Single(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
            var context = new AssemblyLoadContext("eq-experimental-probe", isCollectible: true);
            try
            {
                var native = context.LoadFromAssemblyPath(copied);
                SurfaceClass.Of(native).Should().Be(SurfaceClass.Experimental);
            }
            finally
            {
                context.Unload();
            }
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    /// <summary>
    /// The address EQX0001 gives, and the one the native SDK prints at a publish, name a heading the
    /// wiki's Photon page has. A heading renamed in the wiki would leave every developer who follows
    /// the diagnostic on the page's top, with nothing saying why.
    /// </summary>
    [Fact]
    public void TheAddressTheExperimentalMarkGives_NamesAHeadingOfTheWiki()
    {
        if (WikiClone.Absent()) return;

        var root = SdkProbe.RepoRoot();
        var address = new System.Text.RegularExpressions.Regex(
            @"https://github\.com/eQuantic/equantic-ui/wiki/(?<page>[A-Za-z]+)#(?<anchor>[a-z0-9-]+)");
        var given = new[]
            {
                Path.Combine(root, "Directory.Build.targets"),
                Path.Combine(root, "src", "eQuantic.UI.Sdk.Native", "Sdk", "Sdk.targets"),
            }
            .SelectMany(file => address.Matches(File.ReadAllText(file)))
            .Where(match => match.Groups["anchor"].Value.Contains("experimental", StringComparison.Ordinal))
            .Select(match => (Page: match.Groups["page"].Value, Anchor: match.Groups["anchor"].Value))
            .Distinct()
            .ToList();
        given.Should().NotBeEmpty("the experimental mark and the native SDK's notice both give the wiki's address");

        foreach (var (page, anchor) in given)
        {
            var headings = File.ReadAllLines(Path.Combine(WikiClone.Location!, $"{page}.md"))
                .Where(line => line.StartsWith('#'))
                .Select(line => System.Text.RegularExpressions.Regex.Replace(
                    line.TrimStart('#').Trim().ToLowerInvariant(), @"[^a-z0-9 -]", string.Empty).Replace(' ', '-'));
            headings.Should().Contain(anchor,
                $"the address names {page}#{anchor}, which has to be a heading of that page. Read from {WikiClone.Describe()}");
        }
    }

    /// <summary>
    /// The native SDK is the one place an app's suppression of EQX0001 comes from, and nothing that
    /// runs on a pull request builds a Photon app outside this repository, where the repository's own
    /// suppression does not reach. So the targets the SDK ships are read: the id goes into NoWarn in
    /// Sdk.targets, after the app's own project, so a NoWarn the app sets does not drop it.
    /// </summary>
    [Fact]
    public void TheNativeSdkSuppressesEqx0001_AfterTheAppsOwnProject()
    {
        SdkProbe.Shipped("eQuantic.UI.Sdk.Native").Descendants("NoWarn").Select(element => element.Value)
            .Should().Contain(value => value.Contains("$(NoWarn)", StringComparison.Ordinal) && value.Contains("EQX0001", StringComparison.Ordinal),
                "an app built with the native SDK chose the native track, and writes nothing for it");
    }

    private static string Expected(string name) =>
        name.StartsWith("eQuantic.UI.Native.", StringComparison.Ordinal) ? SurfaceClass.Experimental
        : name is "eQuantic.UI.Compiler" or "eQuantic.UI.Codegen" ? SurfaceClass.Tool
        : SurfaceClass.Contract;
}
