using System.Diagnostics;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A generated file its generator stopped emitting is removed by the compile that stopped emitting
/// it, and a compile that did not run removes nothing (#253). eqc reads generated sources as FILES,
/// so a leftover in obj/.../generated is a type that no longer exists, transpiled.
/// <para>
/// These run the SDK's own two targets, taken from the <c>Sdk.targets</c> that ships, in a real
/// MSBuild, around a stand-in <c>CoreCompile</c> that behaves the way Roslyn was measured to: when
/// it runs, it rewrites every file it generates; when its inputs are unchanged, it is skipped and
/// writes nothing. The SKIPPED case is the one an earlier attempt broke — it wiped the directory
/// first, and an incremental build then had no generated surface at all — and the compile INPUT is
/// the one the first cut of this prune broke.
/// </para>
/// </summary>
public class StaleGeneratedFilesArePrunedTests
{
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    /// <summary>The prune's two targets, exactly as <c>Sdk.targets</c> declares them.</summary>
    private static IEnumerable<XElement> ShippedTargets()
    {
        var sdk = XDocument.Load(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Sdk", "Sdk", "Sdk.targets"));
        var names = new[] { "_EQuanticMarkCompileStart", "_EQuanticPruneStaleGeneratedFiles" };
        var targets = sdk.Root!.Elements("Target").Where(t => names.Contains((string?)t.Attribute("Name"))).ToList();
        targets.Should().HaveCount(2, "the test must run the targets the SDK ships, both of them");
        return targets;
    }

    private static string Project() => new XDocument(new XElement("Project",
        new XElement("PropertyGroup",
            new XElement("EmitCompilerGeneratedFiles", "true"),
            new XElement("CompilerGeneratedFilesOutputPath", "generated")),
        new XElement("ItemGroup",
            new XElement("IntermediateAssembly", new XAttribute("Include", "obj/probe.dll")),
            // A compile INPUT that lives in the generated folder, as the SDK's vector catalog does.
            new XElement("Compile", new XAttribute("Include", "generated/Vectors/Vectors.g.cs"))),
        // Roslyn as measured: when it runs, every generated file is rewritten; when the input is
        // older than the assembly, the target is skipped and nothing is written.
        new XElement("Target", new XAttribute("Name", "CoreCompile"),
            new XAttribute("Inputs", "input.cs"), new XAttribute("Outputs", "obj/probe.dll"),
            new XElement("MakeDir", new XAttribute("Directories", "obj;generated/Gen")),
            new XElement("WriteLinesToFile", new XAttribute("File", "generated/Gen/Current.g.cs"),
                new XAttribute("Lines", "// written by this compile"), new XAttribute("Overwrite", "true")),
            new XElement("Touch", new XAttribute("Files", "obj/probe.dll"), new XAttribute("AlwaysCreate", "true"))),
        new XElement("Target", new XAttribute("Name", "Build"), new XAttribute("DependsOnTargets", "CoreCompile")),
        ShippedTargets())).ToString();

    private static void Build(string directory)
    {
        var start = new ProcessStartInfo("dotnet", ["msbuild", "probe.proj", "-t:Build", "-nologo", "-v:m"])
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, output);
    }

    private static void InATemporaryProject(Action<string> body)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"eq-prune-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "generated", "Gen"));
        Directory.CreateDirectory(Path.Combine(directory, "generated", "Vectors"));
        File.WriteAllText(Path.Combine(directory, "probe.proj"), Project());
        File.WriteAllText(Path.Combine(directory, "input.cs"), "// the app's source");
        try { body(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Old(string path)
    {
        File.WriteAllText(path, "// written by an earlier compile");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-1));
        return path;
    }

    [Fact]
    public void ACompileThatRuns_RemovesTheGeneratedFileItDidNotWrite_AndKeepsTheOnesItDid()
    {
        InATemporaryProject(directory =>
        {
            var stale = Old(Path.Combine(directory, "generated", "Gen", "DeletedComponent.g.cs"));

            Build(directory);

            File.Exists(stale).Should().BeFalse("the compile ran and did not write it: its generator stopped");
            File.Exists(Path.Combine(directory, "generated", "Gen", "Current.g.cs")).Should().BeTrue();
        });
    }

    /// <summary>The SDK's own vector catalog is written into the same folder by its own target, only
    /// when the SVGs change — so it is old whenever they have not, and it is a compile INPUT. The
    /// first cut of the prune deleted it from a real sample and eqc then failed on `Vectors`.</summary>
    [Fact]
    public void AFileTheCompileTakesAsInput_IsNeverAGeneratorsLeftover()
    {
        InATemporaryProject(directory =>
        {
            var catalog = Old(Path.Combine(directory, "generated", "Vectors", "Vectors.g.cs"));

            Build(directory);

            File.Exists(catalog).Should().BeTrue("the compile read it; no generator was ever going to rewrite it");
        });
    }

    [Fact]
    public void ACompileThatIsSkipped_RemovesNothing()
    {
        InATemporaryProject(directory =>
        {
            Build(directory);
            // The input is now older than the assembly, so the next CoreCompile is skipped — and the
            // file the last compile wrote is old by the clock without being stale at all.
            File.SetLastWriteTimeUtc(Path.Combine(directory, "input.cs"), DateTime.UtcNow.AddHours(-2));
            var current = Old(Path.Combine(directory, "generated", "Gen", "Current.g.cs"));

            Build(directory);

            File.Exists(current).Should().BeTrue(
                "a skipped compile rewrote nothing, so an old file is still the current one — wiping it "
                + "is what left an incremental build with no generated surface");
        });
    }
}
