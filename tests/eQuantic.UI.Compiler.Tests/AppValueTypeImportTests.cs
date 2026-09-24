using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// A module imports every one of the app's OWN types its emitted text names, and the record path is
/// no exception. It imported only what the hydration map named, so two names reached a record's
/// module unimported: a sibling record a method constructs, and a struct member's zero — which the
/// constructor now spells (<c>span: any = new Span2()</c>) because C# never holds a null struct.
/// <c>new Holder()</c> on the web then threw "Span2 is not defined" where C# built a zeroed Span2
/// (found while fixing the struct defaults in #359).
/// </summary>
public class AppValueTypeImportTests
{
    private static readonly Dictionary<string, string> Files = new()
    {
        ["Span2.cs"] = "namespace App; public readonly record struct Span2(int Start, int Length);",
        ["Mark.cs"] = "namespace App; public sealed record Mark(string Label);",
        // Span2 is named ONLY by the member's type, so its import can come from nowhere but the
        // zero the constructor writes; Mark is named only by the body.
        ["Holder.cs"] = """
            namespace App;
            public readonly record struct Holder(Span2 Span)
            {
                public Mark Flag() => new Mark("moved");
            }
            """,
        // Span2 is named only inside a default the conversion writes: `new Span2[n]` and
        // `default(Span2)` spell no `new Span2()`, and the signatures say `object`.
        ["Rows.cs"] = """
            namespace App;
            public static class Rows
            {
                public static object Blank(int n) => new Span2[n];
                public static object Origin() => default(Span2);
            }
            """,
        ["Board.cs"] = """
            using eQuantic.UI.Components;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Board : StatefulComponent
            {
                private Span2 _span;
                public override VisualNode Build(ComponentContext context) => new Text($"{_span.Start}");
            }
            """,
    };

    /// <summary>Compiled the way eqc compiles an app: every file in one compilation with the
    /// framework referenced, and the per-app scan telling the emitters which types became modules.</summary>
    private static string TypeScriptOf(string component)
    {
        var dir = Path.Combine(Path.GetTempPath(), "eq-app-value-imports-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var trees = Files.Select(file =>
            {
                var path = Path.Combine(dir, file.Key);
                File.WriteAllText(path, file.Value);
                return CSharpSyntaxTree.ParseText(file.Value, path: path);
            }).ToList();
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location))
                .Append(MetadataReference.CreateFromFile(typeof(eQuantic.UI.Components.CodeEditor).Assembly.Location));
            var compilation = CSharpCompilation.Create("App", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var resolver = new ComponentDependencyResolver();
            resolver.ScanSourceDirectories([dir]);
            var compiler = new ComponentCompiler();
            compiler.SetProjectCompilation(compilation);
            compiler.SetDependencyResolver(resolver);
            var result = compiler.CompileFile(Path.Combine(dir, component + ".cs"))
                .Single(r => r.ComponentName == component);
            result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
            return result.TypeScript;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ARecordImportsTheStructWhoseZeroItsConstructorWrites()
    {
        var ts = TypeScriptOf("Holder");

        ts.Should().Contain("span: any = new Span2()");
        ts.Should().Contain("import { Span2 } from \"./Span2\"");
    }

    [Fact]
    public void ARecordImportsTheSiblingItsBodyConstructs()
    {
        var ts = TypeScriptOf("Holder");

        ts.Should().Contain("new Mark(");
        ts.Should().Contain("import { Mark } from \"./Mark\"");
    }

    /// <summary>A zero the conversion writes names its struct where no syntax does, so a helper
    /// that returned `new Span2[n]` or `default(Span2)` spelled `new Span2()` in a module that never
    /// imported it, and the module failed when it loaded (found in review, #405).</summary>
    [Fact]
    public void AHelperImportsTheStructItsDefaultsName()
    {
        var ts = TypeScriptOf("Rows");

        ts.Should().Contain("Array.from({ length: n }, () => new Span2())");
        ts.Should().Contain("import { Span2 } from \"./Span2\"");
    }

    [Fact]
    public void AComponentFieldOfAnAppStructStartsAtItsZero()
    {
        var ts = TypeScriptOf("Board");

        ts.Should().MatchRegex(@"_span(: \w+)? = new Span2\(\)");
        ts.Should().Contain("import { Span2 } from \"./Span2\"");
    }
}
