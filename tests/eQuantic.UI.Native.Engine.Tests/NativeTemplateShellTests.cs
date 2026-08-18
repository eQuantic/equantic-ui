using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Every shape `dotnet new equantic-native --shell` offers, COMPILED here.
///
/// <para>
/// The release gate builds them for real, but only on a tag, on macOS, after a full pack — so a
/// native shell that stopped compiling was found by a stranger a week later, or by a release that
/// had already published half its feeds. Four shapes went out that way once: the gate scaffolded
/// the default one and the other three were a promise.
/// </para>
/// <para>
/// This is the fast half of that guard: no scaffolding, no packages, just the sources a shape
/// actually contributes, compiled against the same assemblies an app would reference.
/// </para>
/// </summary>
public class NativeTemplateShellTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string TemplateRoot() =>
        Path.Combine(RepoRoot(), "src", "eQuantic.UI.Templates", "templates", "equantic-native");

    private static JsonDocument Manifest() =>
        JsonDocument.Parse(File.ReadAllText(
            Path.Combine(TemplateRoot(), ".template.config", "template.json")));

    /// <summary>The shapes the template offers, asked of the manifest — the same place
    /// `dotnet new` asks.</summary>
    public static TheoryData<string> Shells()
    {
        var data = new TheoryData<string>();
        using var manifest = Manifest();
        foreach (var choice in manifest.RootElement
                     .GetProperty("symbols").GetProperty("shell").GetProperty("choices").EnumerateArray())
        {
            data.Add(choice.GetProperty("choice").GetString()!);
        }
        return data;
    }

    /// <summary>
    /// The directories a shape scaffolds, read from the manifest's own conditions rather than
    /// listed here: `_destinations` belongs to tabs and drawer, `list-detail` brings its own
    /// screens, and a shape added later says so in the same place `dotnet new` reads.
    /// </summary>
    private static IEnumerable<string> SourceDirectories(string shell)
    {
        using var manifest = Manifest();
        foreach (var source in manifest.RootElement.GetProperty("sources").EnumerateArray())
        {
            var path = source.GetProperty("source").GetString()!;
            if (!source.TryGetProperty("condition", out var condition))
            {
                // The unconditional root: everything not under .shells (Program.cs, Resources).
                yield return path;
                continue;
            }

            if (condition.GetString()!.Contains($"'{shell}'", StringComparison.Ordinal))
                yield return path;
        }
    }

    private static IEnumerable<string> SourceFiles(string shell)
    {
        var root = TemplateRoot();
        foreach (var directory in SourceDirectories(shell))
        {
            var full = Path.GetFullPath(Path.Combine(root, directory));
            if (!Directory.Exists(full)) continue;

            foreach (var file in Directory.GetFiles(full, "*.cs", SearchOption.AllDirectories))
            {
                // The root entry excludes `.shells/**`: those arrive through the shape's own entry,
                // and taking them all would compile four AppShells into one assembly.
                if (file.Contains($"{Path.DirectorySeparatorChar}.shells{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)
                    && !full.Contains($"{Path.DirectorySeparatorChar}.shells{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)) continue;
                yield return file;
            }
        }
    }

    /// <summary>
    /// What `ImplicitUsings=enable` puts in every file — the standard .NET set, which the native
    /// SDK turns on and which a scaffolded app therefore has. NOT the eQuantic ones: the native SDK
    /// injects none of those, which is why every native shell declares its own framework usings.
    /// </summary>
    private const string ImplicitUsings = """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.IO;
        global using global::System.Linq;
        global using global::System.Net.Http;
        global using global::System.Threading;
        global using global::System.Threading.Tasks;
        """;

    [Theory]
    [MemberData(nameof(Shells))]
    public void EveryNativeShellCompiles(string shell)
    {
        var files = SourceFiles(shell).ToList();
        files.Should().NotBeEmpty($"--shell {shell} must actually have sources");

        var trees = files
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, path: "GlobalUsings.g.cs"))
            .ToList();

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create($"NativeShell_{shell.Replace("-", "")}",
            trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{Path.GetFileName(diagnostic.Location.SourceTree?.FilePath)}"
                + $"{diagnostic.Location.GetLineSpan().StartLinePosition}: "
                + $"{diagnostic.Id} {diagnostic.GetMessage()}")
            .ToList();

        errors.Should().BeEmpty($"`dotnet new equantic-native --shell {shell}` has to compile");
    }
}
