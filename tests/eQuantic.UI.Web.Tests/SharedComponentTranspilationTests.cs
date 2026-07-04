using System.Runtime.CompilerServices;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// Transpiles the REAL shared component sources (<c>eQuantic.UI.Components.Shared</c>) with the real
/// compiler — the same pipeline an app build runs — and pins the emitted modules committed at
/// <c>src/eQuantic.UI.Runtime/src/shared/__transpiled__/</c>, where the runtime's vitest suite
/// EXECUTES them against the vocabulary classes and the generated theme (the write-once proof on
/// web: C# source → eqc → JS → the same DOM the C# WebRealizer produces). Refresh the fixtures with
/// <c>EQ_UPDATE_TRANSPILED=1</c> after compiler or component changes.
/// </summary>
public class SharedComponentTranspilationTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static readonly string[] SharedSources = ["Button.cs", "Card.cs"];

    private static Dictionary<string, string> TranspileSharedComponents()
    {
        var root = RepoRoot();
        var sourcePaths = SharedSources
            .Select(name => Path.Combine(root, "src", "eQuantic.UI.Components.Shared", name))
            .ToList();

        // The same semantic setup the SDK gives eqc: the sources + the real Primitives reference.
        var trees = sourcePaths
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .ToList();
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(Primitives.Color).Assembly.Location),
        };
        var compilation = CSharpCompilation.Create("SharedComponents", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);

        var modules = new Dictionary<string, string>();
        foreach (var path in sourcePaths)
        {
            foreach (var result in compiler.CompileFile(path))
            {
                result.Success.Should().BeTrue(
                    $"{result.ComponentName} must transpile cleanly: " +
                    string.Join("; ", result.Errors.Select(e => e.Message)));
                modules[result.ComponentName] = result.TypeScript;
            }
        }
        return modules;
    }

    [Fact]
    public void SharedComponents_TranspiledFixtures_MatchCommittedModules()
    {
        var modules = TranspileSharedComponents();
        var fixtureDir = Path.Combine(RepoRoot(), "src", "eQuantic.UI.Runtime", "src", "shared", "__transpiled__");

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_TRANSPILED") == "1")
        {
            Directory.CreateDirectory(fixtureDir);
            foreach (var (name, typeScript) in modules)
                File.WriteAllText(Path.Combine(fixtureDir, $"{name}.generated.ts"), typeScript);
            return;
        }

        foreach (var (name, typeScript) in modules)
        {
            var path = Path.Combine(fixtureDir, $"{name}.generated.ts");
            File.Exists(path).Should().BeTrue(
                $"the runtime executes the transpiled {name} in vitest — generate once with EQ_UPDATE_TRANSPILED=1");
            File.ReadAllText(path).Should().Be(typeScript,
                $"the committed transpiled {name} must be regenerated (EQ_UPDATE_TRANSPILED=1) after compiler/component changes");
        }
    }

    [Fact]
    public void TranspiledButton_ImportsTheVocabularyFromTheRuntime()
    {
        var button = TranspileSharedComponents()["Button"];

        // Vocabulary types resolve to the runtime package (semantic namespace discovery), never ./modules.
        button.Should().Contain("from \"@equantic/runtime\"");
        button.Should().NotContain("from \"./Box\"");
        button.Should().NotContain("from \"./Variant\"", "enums lower to string literals and are never imported");

        // The C# defaults survive as JS parameter defaults.
        button.Should().Contain("variant: any = 'primary'");
        button.Should().Contain("size: any = 'medium'");

        // The BoxStyle object initializer survives as a config object (was silently dropped before).
        button.Should().Contain("new BoxStyle({ height: height");
        button.Should().Contain("minWidth: ButtonStyles.minWidth");

        // The size-table tuple deconstructs as an array — the generated ButtonStyles.metrics shape.
        button.Should().Contain("let [height, padX, gap, labelSize, , radius, ] = ButtonStyles.metrics(this.size)");
    }
}
