using System.Diagnostics;
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

    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    /// <summary>Runs <c>dotnet</c> from the repository root, whose global.json picks the SDK, and fails with the child's output.</summary>
    private static string Dotnet(params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepoRoot(),
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(Deadline);
        try
        {
            process.WaitForExitAsync(deadline.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* it exited between the deadline and the kill */ }
            throw new TimeoutException($"dotnet {string.Join(' ', args)} did not finish within {Deadline}.");
        }
        process.ExitCode.Should().Be(0, output.Result + error.Result);
        return output.Result;
    }

    private static void InATemporaryFolder(Action<string> body)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"eq-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "wwwroot", "_equantic", "strings"));
        try { body(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Old(string path)
    {
        File.WriteAllText(path, "// written by an earlier compile");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-1));
        return path;
    }

    /// <summary>
    /// The SDK's two prune targets as <c>Sdk.targets</c> ships them, around a stand-in for eqc that
    /// behaves as the real one was measured to: it rewrites every file it writes, and before it writes
    /// it records what the folder held, which is what a page asking for a module during the compile met.
    /// </summary>
    private static string Project()
    {
        var names = new[] { "_EQuanticMarkOutputStart", "_EQuanticPruneStaleOutput" };
        var targets = ShippedSdk().Root!.Elements("Target")
            .Where(t => names.Contains((string?)t.Attribute("Name")))
            .Select(t => new XElement(t))
            .ToList();
        targets.Should().HaveCount(names.Length, "the test must run the targets the SDK ships, all of them");

        return new XDocument(new XElement("Project",
            new XElement("PropertyGroup",
                new XElement("EnableEQuanticUICompilation", "true"),
                new XElement("EQuanticOutputPath", "wwwroot/_equantic/")),
            new XElement("Target", new XAttribute("Name", "CompileEQuanticUI"),
                new XElement("ItemGroup",
                    new XElement("_Seen", new XAttribute("Include", "wwwroot/_equantic/**"))),
                new XElement("WriteLinesToFile", new XAttribute("File", "seen.txt"),
                    new XAttribute("Lines", "@(_Seen->'%(Filename)%(Extension)')"), new XAttribute("Overwrite", "true")),
                new XElement("WriteLinesToFile", new XAttribute("File", "wwwroot/_equantic/Kept.js"),
                    new XAttribute("Lines", "// written by this compile"), new XAttribute("Overwrite", "true")),
                new XElement("WriteLinesToFile", new XAttribute("File", "wwwroot/_equantic/Kept.js.map"),
                    new XAttribute("Lines", "{}"), new XAttribute("Overwrite", "true")),
                new XElement("WriteLinesToFile", new XAttribute("File", "wwwroot/_equantic/strings/neutral.json"),
                    new XAttribute("Lines", "{}"), new XAttribute("Overwrite", "true"))),
            new XElement("Target", new XAttribute("Name", "Build"), new XAttribute("DependsOnTargets", "CompileEQuanticUI")),
            targets)).ToString();
    }

    [Fact]
    public void ACompile_RemovesWhatEqcStoppedWriting_AfterItWroteTheRest()
    {
        InATemporaryFolder(directory =>
        {
            var output = Path.Combine(directory, "wwwroot", "_equantic");
            File.WriteAllText(Path.Combine(directory, "probe.proj"), Project());
            var kept = Old(Path.Combine(output, "Kept.js"));
            var gone = Old(Path.Combine(output, "DeletedComponent.js"));
            var map = Old(Path.Combine(output, "DeletedComponent.js.map"));
            var culture = Old(Path.Combine(output, "strings", "fr.json"));
            // CopyEQuanticRuntime writes it after eqc, keeping its source's time when it is unchanged.
            var runtime = Old(Path.Combine(output, "runtime.js"));

            Dotnet("msbuild", Path.Combine(directory, "probe.proj"), "-t:Build", "-nologo", "-v:m");

            File.ReadAllLines(Path.Combine(directory, "seen.txt")).Should().Contain(["Kept.js", "DeletedComponent.js", "runtime.js"],
                "the folder is never emptied before eqc writes it, or every module answers 404 while it compiles");
            File.ReadAllText(kept).Should().Contain("written by this compile");
            File.Exists(gone).Should().BeFalse("eqc did not write it: its component is gone");
            File.Exists(map).Should().BeFalse();
            File.Exists(culture).Should().BeFalse("a culture taken out leaves no catalog behind");
            File.Exists(runtime).Should().BeTrue("the runtime is not eqc's to remove");
        });
    }

    [Fact]
    public void TheCompile_DeletesNothingBeforeItWrites()
    {
        // The prune is the one place files leave the folder; the stand-in above cannot see a Delete put
        // back inside the shipped target itself.
        var compile = ShippedSdk().Root!.Elements("Target").Single(t => (string?)t.Attribute("Name") == "CompileEQuanticUI");

        compile.Descendants("Delete").Should().BeEmpty(
            "eqc's output is replaced in place, and only _EQuanticPruneStaleOutput removes what it stopped writing");
    }

    private static (string Excludes, string[] Content) Evaluate(string directory, params string[] properties)
    {
        File.WriteAllText(Path.Combine(directory, "Consumer.csproj"), $"""
            <Project>
                <Import Project="{Path.Combine(SdkDir(), "Sdk.props")}" />
                <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                </PropertyGroup>
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
    public void WithTheCompilerOff_TheFolderIsWhatAnyFileUnderWwwrootIs()
    {
        InATemporaryFolder(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "wwwroot", "_equantic", "Page.js"), "");

            var (excludes, content) = Evaluate(directory, "EnableEQuanticUICompilation=false");

            excludes.Split(';').Should().NotContain("wwwroot/_equantic/**");
            content.Should().Contain("wwwroot/_equantic/Page.js");
        });
    }
}
