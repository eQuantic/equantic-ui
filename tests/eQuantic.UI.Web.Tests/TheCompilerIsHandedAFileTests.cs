using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What the SDK hands eqc reaches it as it is (#836). Every value was written into the command the SDK
/// runs eqc with, and a shell reads that command first: measured on macOS, an assembly name
/// <c>Cash$Flow</c> reached eqc as <c>Cash</c>, and a backtick ran as a command. And the list of
/// projects was one line a project, <c>assembly|project file</c>, which a bar in a path cut in the wrong
/// place.
/// <para>
/// The first case runs the target that writes what eqc is handed, taken from the <c>Sdk.targets</c>
/// that ships, in a real MSBuild, in a folder whose name holds what a shell reads, and reads the file
/// back the way eqc does. The second holds the command to the compiler and that file, so no value of the
/// project goes through a shell again.
/// </para>
/// </summary>
public class TheCompilerIsHandedAFileTests
{
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static XDocument ShippedSdk() =>
        XDocument.Load(Path.Combine(RepoRoot(), "src", "eQuantic.UI.Sdk", "Sdk", "Sdk.targets"));

    private static XElement ShippedTarget(string name) =>
        ShippedSdk().Root!.Elements("Target").Single(target => (string?)target.Attribute("Name") == name);

    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    /// <summary>Runs <c>dotnet</c> from the repository root, whose global.json picks the SDK, and fails
    /// with the child's output. Its arguments go to it as a list, never through a shell.</summary>
    private static void Dotnet(params string[] args)
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
    }

    /// <summary>A name a shell reads as its own: a variable, a command, quotes. Windows takes no bar in
    /// a file's name, and macOS and Linux take all of it.</summary>
    private static string Odd => OperatingSystem.IsWindows() ? "eq $args `c` 'd'" : "eq $args `c` 'd' |e";

    /// <summary>
    /// The probe: the inputs the target is handed, set the way the SDK's own inputs target leaves them,
    /// the two targets it depends on standing in empty, and the target itself as <c>Sdk.targets</c>
    /// ships it.
    /// </summary>
    private static string Project() => new XDocument(new XElement("Project",
        new XElement("PropertyGroup",
            new XElement("EnableEQuanticUICompilation", "true"),
            new XElement("IntermediateOutputPath", "obj/"),
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

            Dotnet("msbuild", probe, "-t:_EQuanticCompilerArguments", "-nologo", "-v:m");

            var file = Path.Combine(directory, "obj", "equantic.eqc.rsp");
            var options = AppBuildOptions.FromArguments(AppBuildOptions.ReadArguments(["@" + file]));
            options.AssemblyName.Should().Be("Cash$Flow`Back`", "a shell read `$Flow` as a variable and the backticks as a command");
            options.SourceDirectories.Should().Equal([directory, directory + "/components"]);
            options.OutputDirectory.Should().Be(directory + "/wwwroot/_equantic/");
            options.ReferencesFile.Should().Be(directory + "/obj/equantic.refs.txt");
            options.ProjectsFile.Should().Be(directory + "/obj/equantic.projects.txt");
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
    public void TheShippedCommand_NamesTheCompilerAndTheFile_AndNoValueOfTheProject()
    {
        var command = (string)ShippedTarget("CompileEQuanticUI").Descendants("Exec").Single().Attribute("Command")!;

        Regex.Matches(command, @"\$\(([^)]+)\)").Select(match => match.Groups[1].Value)
            .Should().BeEquivalentTo(["EqcCliPath", "_EqcArgumentsFile"],
                "a value written into the command is read by a shell before eqc reads it");
        command.Should().NotContain("@(").And.NotContain("%(");
    }
}
