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
    /// A record's and a struct's twin says it is one, and a class's never does, whatever methods it
    /// declares: `with` and `equals` are names any class may have, so a hot reload that took them for a
    /// record's carried an app's class with an <c>Equals</c> override and a <c>With(...)</c> of its own as
    /// data, and rebuilt it without its constructor (Copilot's third round on #672).
    /// </summary>
    [Fact]
    public void ARecordAndAStruct_SayTheyAreValues_AndAClassWithEqualsAndWith_DoesNot()
    {
        var src = """
            public record Note(string Text);
            public struct Spot { public int X; }
            public record struct Pair(int A, int B);
            public class Tally
            {
                public int Count;
                public override bool Equals(object? obj) => obj is Tally other && other.Count == Count;
                public override int GetHashCode() => Count;
                public Tally With(int count) => new Tally { Count = count };
            }
            """;
        var modules = new ComponentCompiler().CompileSource(src).ToDictionary(result => result.ComponentName, result => result.TypeScript);

        foreach (var value in new[] { "Note", "Spot", "Pair" })
            modules[value].Should().Contain("static $record = true;", $"{value} is a record or a struct");
        modules["Tally"].Should().Contain("with(count").And.NotContain("$record", "Tally is a class");
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
    /// One writer quotes every string, and it escapes the separators a reader cannot see: a line or
    /// paragraph separator is legal inside a literal since ES2019, and invisible in the emitted source.
    /// </summary>
    [Fact]
    public void AStringsSeparatorsAreEscaped_InALiteralAndInADeclaredDefault()
    {
        var ts = new ComponentCompiler().CompileSource("""
            public sealed record Sep(string Line = "c\u2029d")
            {
                public string Text() => "a\u2028b";
            }
            """).Single().TypeScript;

        ts.Should().Contain(@"'a\u2028b'").And.Contain(@"'c\u2029d'");
        ts.Should().NotContain(((char)0x2028).ToString()).And.NotContain(((char)0x2029).ToString());
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
