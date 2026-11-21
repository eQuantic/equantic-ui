using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A catalog's typed accessors are the build's to write (#1784). A real site kept a script that wrote
/// each <c>Strings.Designer.cs</c> from its neutral <c>.resx</c>, and a CI step that failed when the
/// committed file differed, which it did over one stray blank line.
/// <para>
/// These run the SDK's own targets, taken from the <c>Sdk.targets</c> that ships, in a real build of a
/// plain project, and then run the program: the accessor class MSBuild writes is the one the satellite
/// assemblies answer, in the neutral culture and in another. eqc reads a catalog's <c>.resx</c> beside
/// the file that declares the class, so the class lands in a mirror of the project's folders with copies
/// of every <c>.resx</c> beside it. The samples prove the rest: built without their Designers, the
/// dashboard's and the shared library's catalogs and modules are byte for byte what they were with them.
/// </para>
/// </summary>
public class ResourceAccessorsTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(4);

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static IEnumerable<XElement> ShippedTargets(string sdk, params string[] names)
    {
        var shipped = XDocument.Load(Path.Combine(RepoRoot(), "src", sdk, "Sdk", "Sdk.targets"));
        var targets = shipped.Root!.Elements("Target")
            .Where(t => names.Contains((string?)t.Attribute("Name")))
            .Select(t => new XElement(t))
            .ToList();
        targets.Should().HaveCount(names.Length, "the test runs the targets the SDK ships, all of them");
        return targets;
    }

    private static string Resx(params (string Key, string Value)[] entries) => new XDocument(
        new XElement("root",
            new XElement("resheader", new XAttribute("name", "resmimetype"), new XElement("value", "text/microsoft-resx")),
            new XElement("resheader", new XAttribute("name", "version"), new XElement("value", "2.0")),
            new XElement("resheader", new XAttribute("name", "reader"),
                new XElement("value", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
            new XElement("resheader", new XAttribute("name", "writer"),
                new XElement("value", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
            entries.Select(entry => new XElement("data",
                new XAttribute("name", entry.Key), new XAttribute(XNamespace.Xml + "space", "preserve"),
                new XElement("value", entry.Value))))).ToString();

    /// <summary>A program that reads one key in the neutral culture and in pt-BR, over the SDK's targets.</summary>
    private static string App(string sdk, bool handKept, params string[] targets)
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-resx-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(Path.Combine(dir, "Resources"));
        File.WriteAllText(Path.Combine(dir, "Resources", "Strings.resx"), Resx(("Hero.Title", "One page")));
        File.WriteAllText(Path.Combine(dir, "Resources", "Strings.pt-BR.resx"), Resx(("Hero.Title", "Uma página")));
        if (handKept)
            File.WriteAllText(Path.Combine(dir, "Resources", "Strings.Designer.cs"), """
                #nullable enable
                namespace Probe.Resources;
                internal static class Strings
                {
                    internal static global::System.Resources.ResourceManager ResourceManager { get; } =
                        new("Probe.Resources.Strings", typeof(Strings).Assembly);
                    internal static global::System.Globalization.CultureInfo? Culture { get; set; }
                    internal static string Hero_Title => "kept by hand";
                }
                """);
        File.WriteAllText(Path.Combine(dir, "Program.cs"), """
            System.Console.WriteLine(Probe.Resources.Strings.Hero_Title);
            System.Globalization.CultureInfo.CurrentUICulture = new("pt-BR");
            System.Console.WriteLine(Probe.Resources.Strings.Hero_Title);
            """);
        var project = new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",
                new XElement("TargetFramework", "net10.0"),
                new XElement("OutputType", "Exe"),
                new XElement("Nullable", "enable"),
                new XElement("TreatWarningsAsErrors", "true"),
                new XElement("RootNamespace", "Probe"),
                new XElement("EmitCompilerGeneratedFiles", "true")),
            ShippedTargets(sdk, targets)));
        project.Save(Path.Combine(dir, "Probe.csproj"));
        return dir;
    }

    private static string[] BuildAndRun(string dir)
    {
        var (built, output, error) = ChildProcess.Dotnet(["build", Path.Combine(dir, "Probe.csproj"), "-nologo"], Deadline, RepoRoot());
        built.Should().Be(0, output + error);
        var (ran, stdout, stderr) = ChildProcess.Dotnet(
            ["exec", Path.Combine(dir, "bin", "Debug", "net10.0", "Probe.dll")], Deadline, RepoRoot());
        ran.Should().Be(0, stdout + stderr);
        return stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void Delete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { /* a temp dir */ }
    }

    [Fact]
    public void ACatalogWithNoDesigner_GetsItsAccessors_BesideCopiesOfItsResx()
    {
        var dir = App("eQuantic.UI.Sdk", handKept: false, "EQuanticResourceAccessors", "_EQuanticMirrorResources");
        try
        {
            BuildAndRun(dir).Should().Equal(["One page", "Uma página"],
                "the class MSBuild writes is the one the satellite assembly answers");

            var mirror = Path.Combine(dir, "obj", "Debug", "net10.0", "generated", "eQuantic.UI.Resources", "Resources");
            var accessors = File.ReadAllText(Path.Combine(mirror, "Strings.Designer.cs"));
            // The manifest name MSBuild gave the neutral resource: under a folder reached through a
            // symbolic link (a temp folder on macOS) it leaves the folder out, and the class must follow
            // it, as its namespace follows the folder the code names.
            var manifest = Path.GetFileNameWithoutExtension(Directory.GetFiles(Path.Combine(dir, "obj", "Debug", "net10.0"), "*.resources")
                .Single(path => !path.Contains(".pt-BR.", StringComparison.Ordinal)));
            accessors.Should().Contain("namespace Probe.Resources")
                .And.Contain("static global::System.Resources.ResourceManager ResourceManager")
                .And.Contain("static global::System.Globalization.CultureInfo Culture")
                .And.Contain($"\"{manifest}\"", "its base name is the manifest name MSBuild gave the resource");
            File.Exists(Path.Combine(mirror, "Strings.resx")).Should().BeTrue("eqc reads the catalog beside the class");
            File.Exists(Path.Combine(mirror, "Strings.pt-BR.resx")).Should().BeTrue("and its cultures beside it");
            File.Exists(Path.Combine(mirror, "Strings.pt-BR.Designer.cs")).Should().BeFalse("a culture carries no keys of its own");
        }
        finally
        {
            Delete(dir);
        }
    }

    [Fact]
    public void ACatalogWithADesignerBesideIt_KeepsIt()
    {
        var dir = App("eQuantic.UI.Sdk", handKept: true, "EQuanticResourceAccessors", "_EQuanticMirrorResources");
        try
        {
            BuildAndRun(dir).Should().Equal(["kept by hand", "kept by hand"], "the app's own class is the one compiled");
            File.Exists(Path.Combine(dir, "obj", "Debug", "net10.0", "generated", "eQuantic.UI.Resources", "Resources", "Strings.Designer.cs"))
                .Should().BeFalse("nothing is generated beside a Designer the app keeps");
        }
        finally
        {
            Delete(dir);
        }
    }

    /// <summary>
    /// A .resx read the ASP.NET Core way, through <c>IStringLocalizer</c>, gets no class: one named by a
    /// dotted path no class can be named after, and one beside the marker class it is named for, which a
    /// generated class of the same name would collide with.
    /// </summary>
    [Fact]
    public void ACatalogReadThroughIStringLocalizer_GetsNoClass_AndTheAppStillBuilds()
    {
        var dir = App("eQuantic.UI.Sdk", handKept: false, "EQuanticResourceAccessors", "_EQuanticMirrorResources");
        try
        {
            File.WriteAllText(Path.Combine(dir, "Resources", "Pages.Index.resx"), Resx(("Title", "Home")));
            File.WriteAllText(Path.Combine(dir, "Resources", "SharedResource.resx"), Resx(("Hello", "Hello")));
            File.WriteAllText(Path.Combine(dir, "Resources", "SharedResource.cs"),
                "namespace Probe.Resources; public sealed class SharedResource { }");

            BuildAndRun(dir).Should().Equal(["One page", "Uma página"], "the other catalogs build as they did");
            var mirror = Path.Combine(dir, "obj", "Debug", "net10.0", "generated", "eQuantic.UI.Resources", "Resources");
            File.Exists(Path.Combine(mirror, "Pages.Index.Designer.cs")).Should().BeFalse();
            File.Exists(Path.Combine(mirror, "SharedResource.Designer.cs")).Should().BeFalse();
        }
        finally
        {
            Delete(dir);
        }
    }

    [Fact]
    public void ANativeAppsCatalog_GetsItsAccessorsToo()
    {
        var dir = App("eQuantic.UI.Sdk.Native", handKept: false, "EQuanticResourceAccessors");
        try
        {
            BuildAndRun(dir).Should().Equal(["One page", "Uma página"]);
        }
        finally
        {
            Delete(dir);
        }
    }
}
