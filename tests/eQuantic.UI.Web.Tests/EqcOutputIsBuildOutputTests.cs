using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// eqc's output folder is build output, and is written the way build output is: declared where .NET
/// declares it, and replaced in place, the files eqc stopped writing removed after it wrote the rest.
/// <para>
/// Under dotnet watch both mattered (#627). Every module, map and chunk eqc wrote again was a file
/// ADDED to the project, which dotnet watch stopped on (dotnet/sdk#55335), until the folder joined
/// DefaultItemExcludes, the glob dotnet watch ignores a change by. And the folder was emptied before
/// eqc compiled, so for the seconds it took every module answered 404, which is where dotnet watch's
/// own refresh of the browser landed on every edit.
/// </para>
/// </summary>
public class EqcOutputIsBuildOutputTests
{
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string SdkDir() => Path.Combine(RepoRoot(), "src", "eQuantic.UI.Sdk", "Sdk");

    private static XDocument ShippedSdk() => XDocument.Load(Path.Combine(SdkDir(), "Sdk.targets"));

    /// <summary>Runs <c>dotnet</c> from the repository root, whose global.json picks the SDK, and fails with the child's output.</summary>
    private static string Dotnet(params string[] args) => SdkProbe.Dotnet(null, null, args);

    private static void InATemporaryFolder(Action<string> body)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"eq-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "wwwroot", "_equantic", "strings"));
        try { body(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>
    /// The SDK removes nothing eqc wrote by a file's time: eqc removes what it stopped writing, by
    /// what the compile produced (#1279). The SDK's prune read every file older than the compile's
    /// start as one the compile did not write, which held only while eqc rewrote every file on every
    /// compile, and never ran for the eqc a running app keeps (#1276). eqc's own prune is pinned by
    /// ModuleBundlerMapsTests (the bundle's output, through bun) and the catalogue tests.
    /// </summary>
    [Fact]
    public void TheSdk_ReadsNoFileTimeOverEqcsFolder()
    {
        var timed = ShippedSdk().Root!.Elements("Target")
            .Where(t => t.ToString().Contains("EQuanticOutputPath", StringComparison.Ordinal)
                && t.ToString().Contains("GetLastWriteTime", StringComparison.Ordinal))
            .Select(t => (string?)t.Attribute("Name"))
            .ToList();

        timed.Should().BeEmpty("a file eqc did not write again is one whose text did not change, not one it stopped writing");
    }

    [Fact]
    public void TheCompile_DeletesNothingBeforeItWrites()
    {
        var compile = ShippedSdk().Root!.Elements("Target").Single(t => (string?)t.Attribute("Name") == "CompileEQuanticUI");

        compile.Descendants("Delete").Should().BeEmpty(
            "eqc's output is replaced in place, and eqc removes what it stopped writing after it wrote the rest");
    }

    private static (string Excludes, string[] Content) Evaluate(string directory, string body = "", params string[] properties)
    {
        File.WriteAllText(Path.Combine(directory, "Consumer.csproj"), $"""
            <Project>
                <Import Project="{Path.Combine(SdkDir(), "Sdk.props")}" />
                <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                </PropertyGroup>
                {body}
                <Import Project="{Path.Combine(SdkDir(), "Sdk.targets")}" />
            </Project>
            """);
        var json = Dotnet([
            "msbuild", Path.Combine(directory, "Consumer.csproj"), "-nologo",
            "-getProperty:DefaultItemExcludes", "-getItem:Content", .. properties.Select(p => $"-p:{p}"),
        ]);
        using var document = JsonDocument.Parse(json);
        var excludes = document.RootElement.GetProperty("Properties").GetProperty("DefaultItemExcludes").GetString() ?? "";
        var content = document.RootElement.GetProperty("Items").TryGetProperty("Content", out var items)
            ? items.EnumerateArray().Select(i => i.GetProperty("Identity").GetString()!.Replace('\\', '/')).ToArray()
            : [];
        return (excludes, content);
    }

    [Fact]
    public void TheOutputFolder_IsExcludedAsBuildOutput_SoDotnetWatchIgnoresWhatEqcWrites()
    {
        InATemporaryFolder(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "wwwroot", "_equantic", "Page.js"), "");
            File.WriteAllText(Path.Combine(directory, "wwwroot", "site.css"), "");

            var (excludes, content) = Evaluate(directory);

            excludes.Split(';').Should().Contain("wwwroot/_equantic/**",
                "dotnet watch ignores a change that matches DefaultItemExcludes, and stopped on every file eqc added");
            content.Should().Contain("wwwroot/site.css", "the rest of wwwroot stays content");
            content.Should().NotContain(path => path.Contains("_equantic"),
                "the static web assets pipeline would register at evaluation what the build rewrites (#352, #361)");
        });
    }

    [Fact]
    public void AnAppThatIncludesWwwrootByHand_StillLeavesTheFolderOutOfContent()
    {
        // No default glob reaches an app's own include, so the exclusion alone would hand the folder
        // back to the static web assets pipeline (#352, #361).
        InATemporaryFolder(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "wwwroot", "_equantic", "Page.js"), "");
            File.WriteAllText(Path.Combine(directory, "wwwroot", "site.css"), "");

            var (_, content) = Evaluate(directory, """
                <PropertyGroup><EnableDefaultContentItems>false</EnableDefaultContentItems></PropertyGroup>
                <ItemGroup><Content Include="wwwroot/**" /></ItemGroup>
                """);

            content.Should().Contain("wwwroot/site.css");
            content.Should().NotContain(path => path.Contains("_equantic"));
        });
    }

    [Fact]
    public void WithTheCompilerOff_TheFolderIsWhatAnyFileUnderWwwrootIs()
    {
        InATemporaryFolder(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "wwwroot", "_equantic", "Page.js"), "");

            var (excludes, content) = Evaluate(directory, properties: "EnableEQuanticUICompilation=false");

            excludes.Split(';').Should().NotContain("wwwroot/_equantic/**");
            content.Should().Contain("wwwroot/_equantic/Page.js");
        });
    }
}
