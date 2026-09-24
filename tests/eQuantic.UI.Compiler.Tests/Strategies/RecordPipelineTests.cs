using System.IO;
using System.Linq;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Tier 2 — the real build pipeline: positional records are discovered by scanning, emitted as
/// standalone named-class modules, and reactively imported by components that reference them (no
/// hardcoded type list).
/// </summary>
public class RecordPipelineTests
{
    [Fact]
    public void Record_IsEmittedAsNamedClassModule()
    {
        var src = "public record Point(int X, int Y) { public int Sum() => X + Y; }";
        var result = new ComponentCompiler().CompileSource(src).Single();

        result.Success.Should().BeTrue();
        var ts = result.TypeScript;
        ts.Should().Contain("import { $eq } from \"@equantic/runtime\"");
        ts.Should().Contain("export class Point");
        ts.Should().Contain("constructor(x: any = 0, y: any = 0)"); // per-member defaults cover omitted args
        ts.Should().Contain("equals(o: unknown)");
        ts.Should().Contain("with(patch: any)");
        ts.Should().Contain("sum()");        // user instance method
    }

    /// <summary>
    /// A record the RUNTIME provides (its namespace says so) never imports its own name. A call to
    /// its own static helper is written qualified, <c>Layout.twice(…)</c>, and the conversion
    /// registers the name it wrote after the module had already struck its own: the code engine's
    /// diff layout imported itself, which TypeScript refuses as a conflict with its declaration.
    /// </summary>
    [Fact]
    public void ARuntimeProvidedRecord_NeverImportsItself()
    {
        var src = """
            namespace eQuantic.UI.Code;
            public sealed record Layout(int Rows)
            {
                public static Layout Of(int rows) => new(Twice(rows));
                private static int Twice(int value) => value * 2;
            }
            """;
        var result = new ComponentCompiler().CompileSource(src).Single();

        result.Success.Should().BeTrue();
        result.TypeScript.Should().Contain("Layout.twice(", "the helper is called on the record's own name");
        var imported = System.Text.RegularExpressions.Regex
            .Match(result.TypeScript, "import \\{([^}]*)\\} from \"@equantic/runtime\"").Groups[1].Value
            .Split(',').Select(name => name.Trim());
        imported.Should().NotContain("Layout", "a module declares its own name and never imports it");
    }

    /// <summary>
    /// A declared default is the constant C# folds, whatever expression wrote it: `-1` is a minus
    /// over a literal, and the literal table had no row for it, so an optional `int SourceLine = -1`
    /// constructed as null, and a named call that skipped it passed null. In JavaScript `null >= 0`
    /// is true, and a diff's gap row drew the other document's first line.
    /// </summary>
    [Fact]
    public void ADeclaredDefault_IsTheConstantCSharpFolds_AndANamedCallPassesIt()
    {
        var source = """
            public readonly record struct Filler(int BeforeLine, int Rows, int SourceLine = -1, double Scale = -0.5,
                int Mask = 1 << 3, string Tag = "a" + "b", string? Label = null);

            public static class Gaps
            {
                public static Filler Gap(int line, string header) => new Filler(line, 1, Label: header);
            }
            """;
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source, path: "Probe.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Microsoft.CodeAnalysis.MetadataReference)Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path));
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Probe", [tree], references,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var results = compiler.CompileSource(source, "Probe.cs").ToList();

        results.Single(result => result.ComponentName == "Filler").TypeScript.Should().Contain(
            "constructor(beforeLine: any = 0, rows: any = 0, sourceLine: any = -1, scale: any = -0.5, mask: any = 8, tag: any = 'ab', label: any = null)");
        results.Single(result => result.ComponentName == "Gaps").TypeScript.Should().Contain(
            "new Filler(line, 1, -1, -0.5, 8, 'ab', header)", "a named call fills what it skips with the same constants");

        // Where no model folds it, a negative number is still one.
        new ComponentCompiler().CompileSource("public readonly record struct Row(int Line = -1);").Single().TypeScript
            .Should().Contain("constructor(line: any = -1)");
    }

    [Fact]
    public void Component_ReferencingRecord_ReactivelyImportsIt()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-record-pipeline-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Point.cs"),
                "namespace App; public record Point(int X, int Y);");
            File.WriteAllText(Path.Combine(dir, "Card.cs"),
                "namespace App; using eQuantic.UI.Web.Components; " +
                "public class Card : StatelessComponent { public Point Position { get; set; } " +
                "public override HtmlNode Build(BuildContext context) => null; }");

            // The resolver discovers Point as a record by scanning — no fixed list.
            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories(new[] { dir });
            resolver.GetAllRecords().Should().Contain("Point");

            var compiler = new ComponentCompiler();
            compiler.SetDependencyResolver(resolver);
            var card = compiler.CompileFile(Path.Combine(dir, "Card.cs"))
                .Single(r => r.ComponentName == "Card");

            // Card references Point (a record) -> it is imported from its generated module.
            card.TypeScript.Should().Contain("from \"./Point\"");
            card.TypeScript.Should().Contain("Point");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
