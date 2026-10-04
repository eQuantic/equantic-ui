using System.Text.Json;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Services;

/// <summary>
/// A map names every file its segments come from, and each segment the file it came from (#490). A
/// class takes a default its interface supplies (#414), and the default's body converts from the
/// interface's own file into the class's module, its statements carrying their origins in that
/// file. The map named one source, the class's, so those segments led to lines of the class's file
/// that it does not have, and a breakpoint on the interface's file bound nowhere.
/// </summary>
public class SourceMapSourcesTests
{
    private const string InterfacePath = "IValidating.cs";
    private const string ClassPath = "Form.cs";

    private const string Interface = """
        using System.Collections.Generic;

        namespace Demo;

        public interface IValidating
        {
            bool Validate(List<string> errors)
            {
                var count = errors.Count;
                var ok = count == 0;
                return ok;
            }
        }
        """;

    private const string Class = """
        namespace Demo;

        public class Form : IValidating
        {
            public int Size() => 1;
        }
        """;

    private static CompilationResult Compile()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)TestReferences.Of(path));
        var compilation = CSharpCompilation.Create("Maps",
            [CSharpSyntaxTree.ParseText(Interface, path: InterfacePath), CSharpSyntaxTree.ParseText(Class, path: ClassPath)],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));

        var compiler = new ComponentCompiler { SourceMaps = SourceMapMode.Full };
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(Class, ClassPath).Single(result => result.ComponentName == "Form");
        Assert.True(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
        result.SourceMap.Should().NotBeNullOrEmpty();
        return result;
    }

    private static string[] Sources(string map)
    {
        using var json = JsonDocument.Parse(map);
        return json.RootElement.GetProperty("sources").EnumerateArray().Select(source => source.GetString()!).ToArray();
    }

    /// <summary>The 0-based line of the first line of <paramref name="source"/> that contains <paramref name="text"/>.</summary>
    private static int LineOf(string source, string text) =>
        source.Split('\n').Select((line, index) => (line, index)).First(pair => pair.line.Contains(text)).index;

    /// <summary>Where the module's first line holding <paramref name="emitted"/> leads, read as a debugger
    /// reads it: the last segment on that line at or before where the text starts.</summary>
    private static (string Source, int Line) Resolve(CompilationResult result, string emitted)
    {
        var lines = result.TypeScript.Split('\n');
        var generatedLine = Array.FindIndex(lines, line => line.Contains(emitted));
        generatedLine.Should().BeGreaterThanOrEqualTo(0, $"the module writes `{emitted}`:\n{result.TypeScript}");
        var column = lines[generatedLine].IndexOf(emitted, StringComparison.Ordinal);
        var segment = SourceMapMappings.Decode(SourceMapMappings.Extract(result.SourceMap!))
            .ElementAtOrDefault(generatedLine)?.LastOrDefault(s => s.GeneratedColumn <= column);
        segment.Should().NotBeNull($"line {generatedLine + 1} of the module, `{lines[generatedLine].Trim()}`, carries a mapping");
        return (Sources(result.SourceMap!)[segment!.SourceFile], segment.SourceLine);
    }

    [Theory]
    [InlineData("let count = errors.length;", "var count = errors.Count;")]
    [InlineData("let ok = count === 0;", "var ok = count == 0;")]
    [InlineData("return ok;", "return ok;")]
    public void AStatementOfADefaultAnInterfaceSupplies_LeadsToTheInterfacesFile(string emitted, string written) =>
        Resolve(Compile(), emitted).Should().Be((InterfacePath, LineOf(Interface, written)),
            $"`{emitted}` was written in {InterfacePath}, and the class's file has no such line");

    [Fact]
    public void AMemberTheClassDeclares_LeadsToTheClassesFile() =>
        Resolve(Compile(), "return 1;").Should().Be((ClassPath, LineOf(Class, "public int Size() => 1;")));

    [Fact]
    public void TheMap_NamesEachFileOnce_WithItsOwnText()
    {
        using var map = JsonDocument.Parse(Compile().SourceMap!);
        var sources = map.RootElement.GetProperty("sources").EnumerateArray().Select(source => source.GetString()).ToArray();
        var contents = map.RootElement.GetProperty("sourcesContent").EnumerateArray().Select(content => content.GetString()).ToArray();

        sources.Should().BeEquivalentTo([ClassPath, InterfacePath]);
        contents.Should().HaveCount(sources.Length, "each source carries its own text, in the same order");
        contents[Array.IndexOf(sources, InterfacePath)].Should().Be(Interface);
        contents[Array.IndexOf(sources, ClassPath)].Should().Be(Class);
    }
}
