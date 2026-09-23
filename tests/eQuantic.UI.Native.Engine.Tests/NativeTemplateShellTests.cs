using System.Reflection;
using System.Runtime.CompilerServices;
using System.Resources;
using System.Runtime.Loader;
using System.Xml.Linq;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web.Tests;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Every <c>--shell</c> the native template offers, COMPILED.
///
/// <para>
/// A template is source nobody builds: it becomes a project on someone else's machine, and until
/// then a wrong overload or a property that moved is invisible. The release gate does scaffold and
/// build it — on a tag, on three operating systems, after a full pack — which is a very slow way to
/// find out that <c>dotnet new equantic-native --shell drawer</c> does not compile.
/// </para>
/// <para>
/// This compiles each shape's sources against the real assemblies, with the same implicit usings
/// the SDK injects (Sdk.props: the static factory surface and the two component aliases). It does
/// not replace the gate — MSBuild wiring, the generated platform head and packaging are still its
/// job — it just moves every C# mistake to the push that makes it.
/// </para>
/// </summary>
public class NativeTemplateShellTests
{
    private static string TemplateRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..",
            "src", "eQuantic.UI.Templates", "templates", "equantic-native"));

    /// <summary>What Sdk.props puts in every file of a consumer's project, without an import.</summary>
    private const string ImplicitUsings = """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.Linq;
        global using global::System.Threading.Tasks;
        global using global::eQuantic.UI.Components;
        global using global::eQuantic.UI.Primitives;
        global using static global::eQuantic.UI.Components.UI;
        global using StatefulComponent = global::eQuantic.UI.Primitives.StatefulComponent;
        global using StatelessComponent = global::eQuantic.UI.Primitives.StatelessComponent;
        """;

    public static TheoryData<string, string[]> Shells() => new()
    {
        // The shape, and the folders `sources` in template.json feeds it.
        { "blank", ["blank"] },
        { "tabs", ["tabs", "_destinations"] },
        { "drawer", ["drawer", "_destinations"] },
        { "list-detail", ["list-detail"] },
    };

    [Theory]
    [MemberData(nameof(Shells))]
    public void EveryShellCompiles(string shell, string[] folders)
    {
        var errors = CompileShell(shell, folders).GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{Path.GetFileName(diagnostic.Location.SourceTree?.FilePath)}"
                + $"{diagnostic.Location.GetLineSpan().StartLinePosition}: {diagnostic.Id} {diagnostic.GetMessage()}")
            .ToList();

        errors.Should().BeEmpty($"`dotnet new equantic-native --shell {shell}` has to compile");
    }

    /// <summary>
    /// Every shell BUILT and walked: no node is reachable by two paths, which is what "a node
    /// belongs to ONE tree" means for a shell with an AdaptiveNode in it. Photon lays out one arm,
    /// but these screens are write-once, and a page that serves one on the web mounts EVERY arm, so a
    /// node placed in two is one component mounted twice. The shells build per arm (a screen, the
    /// nav list), and ListDetail takes builders for the same reason: this is what keeps it so.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shells))]
    public void EveryShellPlacesEachNodeOnce(string shell, string[] folders)
    {
        var context = new AssemblyLoadContext($"Shell_{shell}", isCollectible: true);
        try
        {
            using var image = new MemoryStream();
            CompileShell(shell, folders).Emit(image, manifestResources: NeutralStrings())
                .Success.Should().BeTrue($"--shell {shell} has to emit");
            image.Position = 0;
            var shellType = context.LoadFromStream(image).GetType("EQuanticNativeApp.AppShell");
            shellType.Should().NotBeNull($"--shell {shell} is a shell: Program.cs roots an AppShell");

            var built = ((UiComponent)Activator.CreateInstance(shellType!)!)
                .Build(new ComponentContext(PhotonTheme.Instance));

            NodePlacements.Shared(built).Should().BeEmpty(
                $"--shell {shell}: a node belongs to ONE tree, so each arm builds its own");
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>
    /// The template's neutral <c>.resx</c> files as the manifest resources their Designer classes
    /// read. An in-memory emit has no build to compile them, and a shell that says a word through
    /// <c>Strings</c> — the blank one does — would otherwise fail on the missing resource rather
    /// than on anything this test is about.
    /// </summary>
    private static IEnumerable<ResourceDescription> NeutralStrings() =>
        Directory.GetFiles(Path.Combine(TemplateRoot(), "Resources"), "*.resx")
            // Strings.resx, not Strings.pt-BR.resx: the walk runs in the neutral culture.
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains('.'))
            .Select(path =>
            {
                var bytes = ResxAsResources(path);
                return new ResourceDescription(
                    $"EQuanticNativeApp.Resources.{Path.GetFileNameWithoutExtension(path)}.resources",
                    () => new MemoryStream(bytes), isPublic: true);
            });

    private static byte[] ResxAsResources(string resx)
    {
        using var stream = new MemoryStream();
        using (var writer = new ResourceWriter(stream))
        {
            foreach (var data in XDocument.Load(resx).Root!.Elements("data"))
                writer.AddResource((string)data.Attribute("name")!, (string?)data.Element("value") ?? "");
            writer.Generate();
        }
        return stream.ToArray();
    }

    /// <summary>A shape's sources, compiled the way the scaffolded project compiles them: the
    /// folders template.json feeds it, the shared Resources, and the native SDK's usings.</summary>
    private static Compilation CompileShell(string shell, string[] folders)
    {
        var root = TemplateRoot();
        var files = folders
            .Select(folder => Path.Combine(root, ".shells", folder))
            .SelectMany(folder => Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            // Resources/Strings.Designer.cs is the ordinary Designer accessor, and every shell that
            // reads a string needs it.
            .Concat(Directory.GetFiles(Path.Combine(root, "Resources"), "*.cs"))
            .ToList();

        files.Should().NotBeEmpty($"--shell {shell} must actually have sources");

        var trees = files
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings))
            .ToList();

        return CSharpCompilation.Create($"Shell_{shell}", trees, References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>The choices the manifest offers and the folders on disk are the same set — a shape
    /// listed with no sources scaffolds an empty project, and sources with no choice are dead.</summary>
    [Fact]
    public void TheManifestOffersExactlyTheShapesOnDisk()
    {
        var root = TemplateRoot();
        var manifest = File.ReadAllText(Path.Combine(root, ".template.config", "template.json"));

        var onDisk = Directory.GetDirectories(Path.Combine(root, ".shells"))
            .Select(Path.GetFileName)
            .Where(name => !name!.StartsWith('_'))   // _destinations is shared, not a choice
            .ToList();

        onDisk.Should().BeEquivalentTo(Shells().Select(row => (string)row[0]!));
        foreach (var shape in onDisk)
            manifest.Should().Contain($"\"choice\": \"{shape}\"");
    }

    private static IEnumerable<MetadataReference> References() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(
                Assembly.Load("eQuantic.UI.Primitives").Location))
            .Append(MetadataReference.CreateFromFile(
                Assembly.Load("eQuantic.UI.Components").Location));
}
