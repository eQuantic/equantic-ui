using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using eQuantic.UI.Generators;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Every project the app references gets its components' factories, wherever it comes in the list (#1803).
/// <para>
/// A real site's app referenced six projects, and only the first one's components got factories: the SDK
/// handed the generator their names joined with <c>;</c> in a compiler-visible property, and an analyzer
/// config ends a value at <c>;</c> or <c>#</c>, reading the rest as a comment. The generator read one name.
/// The unit tests handed the generator the list through a fake options provider, which cuts nothing, and the
/// shared library sample's project with components comes first, so nothing failed.
/// </para>
/// <para>
/// These build real projects with the SDK's own target, taken from the <c>Sdk.targets</c> that ships, and
/// with everything its <c>Sdk.props</c> makes compiler-visible, with the generator as the analyzer the
/// compiler runs: what reaches the generator is what MSBuild and the compiler hand it.
/// </para>
/// </summary>
public class ReferencedProjectFactoriesBuildTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(4);

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    /// <summary>What the SDK hands the factory surface's generator: its target, and the props' compiler-visible properties.</summary>
    private static IEnumerable<XElement> ShippedFactoryWiring()
    {
        var sdk = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Sdk", "Sdk");
        var visible = XDocument.Load(Path.Combine(sdk, "Sdk.props")).Descendants("CompilerVisibleProperty")
            .Select(property => new XElement(property))
            .ToList();
        if (visible.Count > 0) yield return new XElement("ItemGroup", visible);
        var target = XDocument.Load(Path.Combine(sdk, "Sdk.targets")).Root!.Elements("Target")
            .Single(t => (string?)t.Attribute("Name") == "_EQuanticFactoryReferences");
        yield return new XElement(target);
    }

    private static XElement Reference(Type type) =>
        new("Reference", new XAttribute("Include", type.Assembly.GetName().Name!),
            new XElement("HintPath", type.Assembly.Location));

    private static void Library(string dir, string name, string? assemblyName, string source)
    {
        Directory.CreateDirectory(Path.Combine(dir, name));
        File.WriteAllText(Path.Combine(dir, name, "Code.cs"), source);
        new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", "net10.0"),
                new XElement("Nullable", "enable"),
                assemblyName is null ? null : new XElement("AssemblyName", assemblyName)),
            new XElement("ItemGroup", Reference(typeof(eQuantic.UI.Primitives.VisualNode)))))
            .Save(Path.Combine(dir, name, name + ".csproj"));
    }

    /// <summary>
    /// A library with no components (<c>Domain</c>), one with a component (<c>Lib</c>), and an app that references
    /// both and composes the component by its factory. Both orders a list could take, by name and by
    /// declaration, put <c>Domain</c> first.
    /// </summary>
    private static string Solution(string? libAssemblyName = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-factories-" + Guid.NewGuid().ToString("n")[..12]);
        Library(dir, "Domain", null, """
            namespace Domain;

            public sealed record Order(string Id);
            """);
        Library(dir, "Lib", libAssemblyName, """
            using eQuantic.UI.Primitives;

            namespace Lib.Sections;

            public sealed class Seal(float size = 32) : StatelessComponent
            {
                public float Size { get; } = size;
                public override VisualNode Build(ComponentContext context) => new Text("seal " + Size);
            }
            """);
        Directory.CreateDirectory(Path.Combine(dir, "App"));
        File.WriteAllText(Path.Combine(dir, "App", "Shell.cs"), """
            using eQuantic.UI.Primitives;

            namespace App.Sections;

            public sealed class Shell : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) => Seal(24);
            }
            """);
        new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", "net10.0"),
                new XElement("Nullable", "enable"),
                new XElement("EmitCompilerGeneratedFiles", "true")),
            new XElement("ItemGroup",
                new XElement("ProjectReference", new XAttribute("Include", "../Domain/Domain.csproj")),
                new XElement("ProjectReference", new XAttribute("Include", "../Lib/Lib.csproj")),
                Reference(typeof(eQuantic.UI.Primitives.VisualNode)),
                new XElement("Analyzer", new XAttribute("Include", typeof(AppFactorySurfaceGenerator).Assembly.Location))),
            ShippedFactoryWiring()))
            .Save(Path.Combine(dir, "App", "App.csproj"));
        return dir;
    }

    private static string Surface(string dir) => File.ReadAllText(Path.Combine(dir, "App", "obj", "Debug", "net10.0", "generated",
        "eQuantic.UI.Generators", "eQuantic.UI.Generators.AppFactorySurfaceGenerator", "AppUI.g.cs"));

    private static void Build(string dir)
    {
        var (built, output, error) = ChildProcess.Dotnet(
            ["build", Path.Combine(dir, "App", "App.csproj"), "-nologo"], Deadline, RepoRoot());
        built.Should().Be(0, "the page composes a component of the second project by its factory\n" + output + error);
    }

    private static void Delete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { /* a temp dir */ }
    }

    [Fact]
    public void AProjectThatIsNotTheFirstReferenced_GetsItsComponentsFactories()
    {
        var dir = Solution();
        try
        {
            Build(dir);
            Surface(dir).Should().Contain(
                "public static global::Lib.Sections.Seal Seal(float size = 32f) => new global::Lib.Sections.Seal(size);");
        }
        finally
        {
            Delete(dir);
        }
    }

    /// <summary>
    /// A character an analyzer config value cannot hold, in an assembly's name, cuts nothing either: the names
    /// reach the generator as the lines of a file the compiler is handed, never through a value.
    /// </summary>
    [Fact]
    public void AnAssemblyNamedWithACharacterAnAnalyzerConfigCuts_GetsItsComponentsFactories()
    {
        var dir = Solution(libAssemblyName: "Brand#Lib");
        try
        {
            Build(dir);
            Surface(dir).Should().Contain("public static global::Lib.Sections.Seal Seal(float size = 32f)");
        }
        finally
        {
            Delete(dir);
        }
    }

    /// <summary>
    /// The compilation an editor and <c>dotnet watch</c> load comes from a design-time build of <c>Compile</c>
    /// alone: the list is among its additional files there too, or an editor marks the factory's call red
    /// in a page that builds.
    /// </summary>
    [Fact]
    public void ADesignTimeBuild_HandsTheCompilerTheList()
    {
        var dir = Solution();
        try
        {
            var project = Path.Combine(dir, "App", "App.csproj");
            var (restored, restoreOutput, restoreError) = ChildProcess.Dotnet(["restore", project, "-nologo"], Deadline, RepoRoot());
            restored.Should().Be(0, restoreOutput + restoreError);
            var (built, output, error) = ChildProcess.Dotnet(
                ["msbuild", project, "-nologo", "-t:Compile", "-p:DesignTimeBuild=true", "-p:SkipCompilerExecution=true",
                 "-p:ProvideCommandLineArgs=true", "-getItem:AdditionalFiles"], Deadline, RepoRoot());
            built.Should().Be(0, output + error);
            using var items = JsonDocument.Parse(output);
            var list = items.RootElement.GetProperty("Items").GetProperty("AdditionalFiles").EnumerateArray()
                .Select(item => item.GetProperty("FullPath").GetString()!)
                .Single(path => Path.GetFileName(path) == "equantic.factory-projects.txt");
            File.ReadAllLines(list).Should().BeEquivalentTo(["Domain", "Lib"]);
        }
        finally
        {
            Delete(dir);
        }
    }
}
