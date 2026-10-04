using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A type with a <c>Count</c> of its own reads it as its twin's <c>count</c>, wherever it is declared:
/// a library the app references holds its domain model, and its <c>Count</c> was read as
/// <c>.length</c>, which is undefined, because the rule asked whether the type was in the app's
/// source (#517). A .NET collection's <c>Count</c> stays the array's <c>length</c>.
/// </summary>
public class ReferencedCountTests
{
    private const string Library = """
        namespace Lib;

        public sealed class Tally
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }
        """;

    private const string Component = """
        using System.Collections.Generic;
        using eQuantic.UI.Primitives;
        using Lib;

        namespace App;

        public class Score : StatelessComponent
        {
            private readonly Tally _tally = new();
            private readonly List<int> _rows = new();

            public override VisualNode Build(ComponentContext context) =>
                new Text($"{_tally.Count} {_tally.Total} {_rows.Count}");
        }
        """;

    [Fact]
    public void ALibrarysTypeReadsItsOwnCount_AndAListItsLength()
    {
        var ts = Compile();

        ts.Should().Contain("this._tally.count");
        ts.Should().NotContain("this._tally.length");
        ts.Should().Contain("this._rows.length");
    }

    /// <summary>Compiled the way eqc compiles an app, with <see cref="Library"/> as a referenced
    /// assembly: <c>Tally</c> has no syntax in the app's compilation, only metadata.</summary>
    private static string Compile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-referenced-count-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(TestReferences.Of)
                .ToList();
            var library = CSharpCompilation.Create("Lib", [CSharpSyntaxTree.ParseText(Library)], platform,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var image = new MemoryStream();
            library.Emit(image).Success.Should().BeTrue();

            var path = Path.Combine(dir, "Score.cs");
            File.WriteAllText(path, Component);
            var references = platform
                .Append(MetadataReference.CreateFromImage(image.ToArray()))
                .Append(TestReferences.Of(typeof(eQuantic.UI.Primitives.VisualNode)));
            var compilation = CSharpCompilation.Create("App", [CSharpSyntaxTree.ParseText(Component, path: path)],
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var result = compiler.CompileFile(path).Single(r => r.ComponentName == "Score");
            result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
            return result.TypeScript;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
