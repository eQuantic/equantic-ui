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
        ts.Should().Contain("constructor(x = 0, y = 0)"); // per-member defaults cover omitted args
        ts.Should().Contain("equals(o)");
        ts.Should().Contain("with(patch)");
        ts.Should().Contain("sum()");        // user instance method
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
