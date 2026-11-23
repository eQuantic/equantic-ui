using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// An app states the SDK's version once, in global.json (#1788). The SDK reads its own version from its
/// package and pins its packages to it; a project that also sets <c>EQuanticUIVersion</c> to another
/// version fails with EQ4010, naming both, where it used to restore packages of one version under an SDK
/// of another and fail later in odd ways. A real site kept "change both together" as a rule in its own
/// CLAUDE.md.
/// <para>
/// Asked of MSBuild, for each SDK, over a throwaway copy of the SDK's folder laid out as its package is:
/// <c>Sdk/</c> beside the package's nuspec. Beside the source tree there is no nuspec, the packages are
/// projects, and nothing is compared.
/// </para>
/// </summary>
public class OneSdkVersionTests
{
    private const string SdkVersion = "9.9.9-preview.1";

    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    /// <summary>A package of <paramref name="sdk"/> at <see cref="SdkVersion"/>, and a consumer of it
    /// whose body sets what <paramref name="body"/> says.</summary>
    private static string Consumer(string sdk, string nuspec, string targetFramework, string body)
    {
        var root = Path.Combine(Path.GetTempPath(), "eq-one-version-" + Guid.NewGuid().ToString("n")[..12]);
        var package = Path.Combine(root, "package");
        var source = Path.Combine(RepoRoot(), "src", sdk, "Sdk");
        Directory.CreateDirectory(Path.Combine(package, "Sdk"));
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(package, "Sdk", Path.GetFileName(file)));
        File.WriteAllText(Path.Combine(package, nuspec),
            $"""<?xml version="1.0"?><package><metadata><id>{sdk}</id><version>{SdkVersion}</version></metadata></package>""");

        var app = Path.Combine(root, "app");
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(app, "App.csproj"), $"""
            <Project>
                <Import Project="{Path.Combine(package, "Sdk", "Sdk.props")}" />
                <PropertyGroup>
                    <TargetFramework>{targetFramework}</TargetFramework>
                    <ApplicationId>tech.equantic.eval</ApplicationId>
                    <ApplicationTitle>Eval</ApplicationTitle>
                    {body}
                </PropertyGroup>
                <Import Project="{Path.Combine(package, "Sdk", "Sdk.targets")}" />
            </Project>
            """);
        return Path.Combine(app, "App.csproj");
    }

    private static (int ExitCode, string Output) Collect(string project, params string[] extra)
    {
        try
        {
            // From the repository, so the child takes the SDK its global.json pins.
            var (exitCode, output, error) = ChildProcess.Dotnet(
                ["msbuild", project, "-nologo", "-t:CollectPackageReferences", "-getProperty:EQuanticUIVersion", .. extra],
                Deadline, RepoRoot());
            return (exitCode, output + error);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(project))!, recursive: true); }
            catch (IOException) { /* a temp dir */ }
        }
    }

    public static TheoryData<string, string, string> Sdks => new()
    {
        { "eQuantic.UI.Sdk", "equantic.ui.sdk.nuspec", "net10.0" },
        { "eQuantic.UI.Sdk.Native", "equantic.ui.sdk.native.nuspec", "net10.0" },
    };

    [Theory]
    [MemberData(nameof(Sdks))]
    public void TheSdk_PinsItsPackagesToItsOwnVersion(string sdk, string nuspec, string targetFramework)
    {
        var (exitCode, output) = Collect(Consumer(sdk, nuspec, targetFramework, ""));

        exitCode.Should().Be(0, output);
        output.Trim().Should().EndWith(SdkVersion, "an app that sets nothing gets the SDK's own version");
    }

    [Theory]
    [MemberData(nameof(Sdks))]
    public void AProjectThatSetsTheSameVersion_Builds(string sdk, string nuspec, string targetFramework)
    {
        var (exitCode, output) = Collect(Consumer(sdk, nuspec, targetFramework,
            $"<EQuanticUIVersion>{SdkVersion}</EQuanticUIVersion>"));

        exitCode.Should().Be(0, output);
        output.Should().NotContain("EQ4010");
    }

    [Theory]
    [MemberData(nameof(Sdks))]
    public void AProjectThatSetsAnotherVersion_FailsNamingBoth(string sdk, string nuspec, string targetFramework)
    {
        var (exitCode, output) = Collect(Consumer(sdk, nuspec, targetFramework,
            "<EQuanticUIVersion>9.9.8-preview.4</EQuanticUIVersion>"));

        exitCode.Should().NotBe(0, output);
        output.Should().Contain("EQ4010").And.Contain($"{sdk} {SdkVersion}").And.Contain("9.9.8-preview.4");
    }

    [Theory]
    [MemberData(nameof(Sdks))]
    public void AVersionSetBeforeTheSdk_IsComparedToo(string sdk, string nuspec, string targetFramework)
    {
        // Directory.Build.props, where a real site set it, is read before the SDK's props: a global
        // property stands in for it, as it overrides every assignment the same way.
        var (exitCode, output) = Collect(Consumer(sdk, nuspec, targetFramework, ""), "-p:EQuanticUIVersion=9.9.8-preview.4");

        exitCode.Should().NotBe(0, output);
        output.Should().Contain("EQ4010");
    }
}
