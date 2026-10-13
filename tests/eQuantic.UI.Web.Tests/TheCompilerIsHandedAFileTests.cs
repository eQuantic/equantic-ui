using System.Xml.Linq;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;
using static eQuantic.UI.Web.Tests.SdkProbe;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What the SDK hands eqc reaches it as it is (#836). Every value was written into the command the SDK
/// runs eqc with, and a shell reads that command first: measured on macOS, an assembly name
/// <c>Cash$Flow</c> reached eqc as <c>Cash</c>, and a backtick ran as a command. And the list of
/// projects was one line a project, <c>assembly|project file</c>, which a bar in a path cut in the wrong
/// place.
/// <para>
/// The case runs the target that writes what eqc is handed, taken from the <c>Sdk.targets</c> that
/// ships, in a real MSBuild, in a folder whose name holds what a shell reads, and reads the file back
/// the way eqc does. How eqc itself is started, with no shell at all since #841, is
/// <see cref="EveryProgramTheSdksStartTests"/>'s to show, with every other program the SDKs start.
/// </para>
/// </summary>
public class TheCompilerIsHandedAFileTests
{
    private static XElement ShippedTarget(string name) => Target(Shipped("eQuantic.UI.Sdk"), name);

    /// <summary>A name a shell reads as its own: a variable, a command, quotes. Windows takes no bar in
    /// a file's name, and macOS and Linux take all of it.</summary>
    private static string Odd => OperatingSystem.IsWindows() ? "eq $args `c` 'd'" : "eq $args `c` 'd' |e";

    /// <summary>
    /// The probe: the inputs the target is handed, set the way the SDK's own inputs target leaves them,
    /// the two targets it depends on standing in empty, and the target itself as <c>Sdk.targets</c>
    /// ships it.
    /// </summary>
    private static string Project(string intermediateOutputPath = "obj/", bool assets = true) => new XDocument(new XElement("Project",
        new XElement("PropertyGroup",
            assets ? new XElement("ProjectAssetsFile", "$(MSBuildProjectDirectory)/obj/project.assets.json") : null,
            new XElement("EnableEQuanticUICompilation", "true"),
            new XElement("IntermediateOutputPath", intermediateOutputPath),
            new XElement("EQuanticOutputPath", "wwwroot/_equantic/"),
            // Written on two lines, as a project file may write a property: the roots are read as
            // eqc reads them, with no line break carried into an argument.
            new XElement("_EqcSourceDirs", "$(MSBuildProjectDirectory);\n            $(MSBuildProjectDirectory)/components"),
            new XElement("_EqcRefsFile", "$(MSBuildProjectDirectory)/obj/equantic.refs.txt"),
            new XElement("_EqcProjectsFile", "$(MSBuildProjectDirectory)/obj/equantic.projects.txt"),
            new XElement("_EqcRefSourcesFile", "$(MSBuildProjectDirectory)/obj/equantic.ref-sources.txt"),
            new XElement("AssemblyName", "Cash$Flow`Back`"),
            new XElement("CompilerGeneratedFilesOutputPath", "generated"),
            new XElement("BunPath", "$(MSBuildProjectDirectory)/tools/bun"),
            new XElement("EQuanticSourceMaps", "full")),
        new XElement("ItemGroup",
            new XElement("_EqcProject",
                new XAttribute("Include", "$(MSBuildProjectDirectory)/Common/Common.csproj"),
                new XElement("EqAssembly", "$(MSBuildProjectDirectory)/Common/bin/Common.dll")),
            new XElement("_EqcRefSourceFinal", new XAttribute("Include", "$(MSBuildProjectDirectory)/pack"))),
        new XElement("Target", new XAttribute("Name", "_EQuanticCompilerInputs")),
        new XElement("Target", new XAttribute("Name", "ResolveBunPath")),
        new XElement(ShippedTarget("_EQuanticCompilerArguments")))).ToString();

    [Fact]
    public void TheShippedTarget_HandsEqcEachValueAsItIs()
    {
        var directory = Path.Combine(Directory.CreateTempSubdirectory("eq-handed-").FullName, Odd);
        Directory.CreateDirectory(directory);
        try
        {
            var probe = Path.Combine(directory, "probe.proj");
            File.WriteAllText(probe, Project());

            Dotnet(null, null, "msbuild", probe, "-t:_EQuanticCompilerArguments", "-nologo", "-v:m");

            var file = Path.Combine(directory, "obj", "equantic.eqc.rsp");
            var options = AppBuildOptions.FromArguments(AppBuildOptions.ReadArguments(["@" + file]));
            options.AssemblyName.Should().Be("Cash$Flow`Back`", "a shell read `$Flow` as a variable and the backticks as a command");
            options.SourceDirectories.Should().Equal([directory, directory + "/components"]);
            options.OutputDirectory.Should().Be(directory + "/wwwroot/_equantic/");
            options.ReferencesFile.Should().Be(directory + "/obj/equantic.refs.txt");
            options.ProjectsFile.Should().Be(directory + "/obj/equantic.projects.txt");
            options.ProjectAssetsFile.Should().Be(directory + "/obj/project.assets.json",
                "the restore graph says which of the projects references which (#887)");
            options.ReferenceSourcesFile.Should().Be(directory + "/obj/equantic.ref-sources.txt");
            options.GeneratedDirectory.Should().Be(directory + "/generated");
            options.BunPath.Should().Be(directory + "/tools/bun");
            options.SourceMaps.Should().Be(SourceMapMode.Full);

            File.ReadAllLines(Path.Combine(directory, "obj", "equantic.projects.txt")).Should().Equal(
                [directory + "/Common/bin/Common.dll", Path.GetFullPath(Path.Combine(directory, "Common", "Common.csproj"))],
                "a project is two lines, so a path is a line whole whatever it holds");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true);
        }
    }

    [Fact]
    public void AProjectWithNoAssetsFile_IsHandedNoEmptyValue()
    {
        // An empty line is no argument, so an option handed with an empty value takes the next line
        // for its own and every argument after it shifts: the first version of #887 lost the app's
        // assembly name that way. The option is left out, and eqc stops on the projects it was handed
        // with no graph.
        var directory = Path.Combine(Directory.CreateTempSubdirectory("eq-handed-").FullName, Odd);
        Directory.CreateDirectory(directory);
        try
        {
            var probe = Path.Combine(directory, "probe.proj");
            File.WriteAllText(probe, Project(assets: false));

            Dotnet(null, null, "msbuild", probe, "-t:_EQuanticCompilerArguments", "-nologo", "-v:m");

            var file = Path.Combine(directory, "obj", "equantic.eqc.rsp");
            File.ReadAllLines(file).Should().NotContain("--project-assets");
            var options = AppBuildOptions.FromArguments(AppBuildOptions.ReadArguments(["@" + file]));
            options.ProjectAssetsFile.Should().BeNull();
            options.AssemblyName.Should().Be("Cash$Flow`Back`", "every argument after the one left out is where it was");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true);
        }
    }
}
