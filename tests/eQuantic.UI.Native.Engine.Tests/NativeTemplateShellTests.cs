using System.Reflection;
using System.Runtime.CompilerServices;
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

        var compilation = CSharpCompilation.Create($"Shell_{shell}", trees, References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{Path.GetFileName(diagnostic.Location.SourceTree?.FilePath)}"
                + $"{diagnostic.Location.GetLineSpan().StartLinePosition}: {diagnostic.Id} {diagnostic.GetMessage()}")
            .ToList();

        errors.Should().BeEmpty($"`dotnet new equantic-native --shell {shell}` has to compile");
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
