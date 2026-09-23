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

    /// <summary>
    /// The prune's two targets and the vector catalog's two, exactly as <c>Sdk.targets</c> declares
    /// them, except for the one task a test cannot carry: the catalog target starts eqicon with an
    /// Exec, and that Exec is swapped for a writer that lists the drawings it was given.
    /// </summary>
    private static IEnumerable<XElement> ShippedTargets()
    {
        var sdk = XDocument.Load(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Sdk", "Sdk", "Sdk.targets"));
        var names = new[]
        {
            "_EQuanticMarkCompileStart", "_EQuanticPruneStaleGeneratedFiles", "_EQuanticVectorInputs", "EQuanticVectors",
        };
        var targets = sdk.Root!.Elements("Target")
            .Where(t => names.Contains((string?)t.Attribute("Name")))
            .Select(t => new XElement(t))
            .ToList();
        targets.Should().HaveCount(names.Length, "the test must run the targets the SDK ships, all of them");

        var eqicon = targets.Single(t => (string?)t.Attribute("Name") == "EQuanticVectors").Elements("Exec").Single();
        eqicon.ReplaceWith(new XElement("WriteLinesToFile",
            new XAttribute("File", "$(EQuanticVectorsFile)"),
            new XAttribute("Lines", "@(_EqVectorSource->'%(Filename)')"),
            new XAttribute("Overwrite", "true")));
        return targets;
    }

    private static string Project() => new XDocument(new XElement("Project",
        new XElement("PropertyGroup",
            new XElement("EmitCompilerGeneratedFiles", "true"),
            new XElement("CompilerGeneratedFilesOutputPath", "generated"),
            new XElement("IntermediateOutputPath", "obj/"),
            // The catalog's settings as the SDK names them. The target's condition is an Assets
            // folder, which only the vector tests create.
            new XElement("EQuanticAssetsDir", "Assets"),
            new XElement("EQuanticVectorsFile", "generated/Vectors/Vectors.g.cs"),
            new XElement("EQuanticVectorsNamespace", "Probe"),
            new XElement("_EqIconToolDir", "tool/")),
        new XElement("ItemGroup",
            new XElement("IntermediateAssembly", new XAttribute("Include", "obj/probe.dll"))),
        // Roslyn as measured: when it runs, every generated file is rewritten; when its inputs are
        // older than the assembly, the target is skipped and nothing is written.
        new XElement("Target", new XAttribute("Name", "CoreCompile"),
            new XAttribute("Inputs", "input.cs;@(Compile)"), new XAttribute("Outputs", "obj/probe.dll"),
            new XElement("MakeDir", new XAttribute("Directories", "obj;generated/Gen")),
            new XElement("WriteLinesToFile", new XAttribute("File", "generated/Gen/Current.g.cs"),
                new XAttribute("Lines", "// written by this compile"), new XAttribute("Overwrite", "true")),
            new XElement("Touch", new XAttribute("Files", "obj/probe.dll"), new XAttribute("AlwaysCreate", "true"))),
        new XElement("Target", new XAttribute("Name", "BeforeCompile")),
        new XElement("Target", new XAttribute("Name", "Build"), new XAttribute("DependsOnTargets", "BeforeCompile;CoreCompile")),
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
        // The tool is one of the catalog's inputs, and an input that does not exist makes MSBuild
        // run the target on every build: that is how the shipped one never skipped.
        Directory.CreateDirectory(Path.Combine(directory, "tool"));
        File.WriteAllText(Path.Combine(directory, "tool", "eqicon.dll"), "");
        try { body(directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Svg(string directory, string name)
    {
        var path = Path.Combine(directory, "Assets", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        return path;
    }

    private static void Age(TimeSpan by, params string[] paths)
    {
        foreach (var path in paths) File.SetLastWriteTimeUtc(path, DateTime.UtcNow - by);
    }

    private static string Catalog(string directory) => Path.Combine(directory, "generated", "Vectors", "Vectors.g.cs");

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

    /// <summary>
    /// The SDK's own vector catalog lives in the same folder and is a compile INPUT, not a
    /// generator's output, and its target is skipped whenever the SVGs did not change, which is
    /// exactly when the catalog is old by the clock. The first cut of the prune deleted it from a
    /// real sample and eqc then failed on `Vectors`. What keeps it is that MSBuild still runs a
    /// skipped target's ItemGroups (output inference): the target is the only place the catalog
    /// joins @(Compile), the second build below skips it, and the catalog survives.
    /// </summary>
    [Fact]
    public void TheVectorCatalog_SurvivesACompileThatRuns_WhileItsOwnTargetIsSkipped()
    {
        InATemporaryProject(directory =>
        {
            var mark = Svg(directory, "mark.svg");
            Build(directory);
            var catalog = Catalog(directory);
            File.ReadAllText(catalog).Should().Contain("mark", "the first build writes the catalog");

            // Everything the catalog is made from is older than the catalog, and the catalog is
            // older than the next compile's start. The app's own source is new, so the compile runs.
            Age(TimeSpan.FromHours(2), mark, Path.Combine(directory, "obj", "equantic.vectors.inputs"),
                Path.Combine(directory, "tool", "eqicon.dll"), Path.Combine(directory, "obj", "probe.dll"));
            var earlier = File.ReadAllText(Old(catalog));
            var current = Old(Path.Combine(directory, "generated", "Gen", "Current.g.cs"));
            File.SetLastWriteTimeUtc(Path.Combine(directory, "input.cs"), DateTime.UtcNow);

            Build(directory);

            File.Exists(catalog).Should().BeTrue("the compile read it, and no generator was ever going to rewrite it");
            File.ReadAllText(catalog).Should().Be(earlier,
                "the catalog's target was skipped: nothing it is made from changed");
            File.ReadAllText(current).Should().Contain("written by this compile",
                "only a compile that runs rewrites it, and the prune runs after exactly those");
        });
    }

    /// <summary>
    /// A deleted SVG leaves every remaining one older than the catalog, so file times alone would
    /// skip the target and keep the drawing. The record of the set is what changes, the same way
    /// CoreCompile's own inputs cache notices a deleted source file.
    /// </summary>
    [Fact]
    public void ADeletedSvg_RewritesTheCatalogWithoutIt()
    {
        InATemporaryProject(directory =>
        {
            var mark = Svg(directory, "mark.svg");
            var gone = Svg(directory, "gone.svg");
            Build(directory);
            var catalog = Catalog(directory);
            File.ReadAllText(catalog).Should().Contain("gone");

            Age(TimeSpan.FromHours(2), mark, Path.Combine(directory, "obj", "equantic.vectors.inputs"),
                Path.Combine(directory, "tool", "eqicon.dll"));
            File.SetLastWriteTimeUtc(catalog, DateTime.UtcNow.AddHours(-1));
            File.Delete(gone);

            Build(directory);

            File.ReadAllText(catalog).Should().NotContain("gone", "its SVG was deleted")
                .And.Contain("mark");
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
