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
        // Span2 is named only through an alias, so the member's syntax spells `S` and never `Span2`:
        // the zero of a member with no initializer, and a target-typed `new()` (found in review, #409).
        ["Aliased.cs"] = """
            using S = App.Span2;
            namespace App;
            public sealed record Aliased
            {
                public S Blank;
                public S Fresh = new();
            }
            """,
        // Inner is named by no syntax outside Outer: Outer's zero is built member by member, because
        // its constructor gives K more than its zero, and that zero constructs an Inner.
        ["Inner.cs"] = "namespace App; public struct Inner { public int N; }",
        ["Outer.cs"] = "namespace App; public struct Outer { public Inner In; public int K = 1; public Outer() { } }",
        ["Nest.cs"] = """
            namespace App;
            public sealed record Nest
            {
                public Outer O;
            }
            """,
        ["Deck.cs"] = """
            using eQuantic.UI.Components;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Deck : StatefulComponent
            {
                private Outer _outer;
                public override VisualNode Build(ComponentContext context) => new Text($"{_outer.K}");
            }
            """,
        ["Panel.cs"] = """
            using eQuantic.UI.Components;
            using eQuantic.UI.Primitives;
            namespace App;
            public sealed class Panel : StatefulComponent
            {
                public Outer Shape { get; set; }
                public override VisualNode Build(ComponentContext context) => new Text($"{Shape.K}");
            }
            """,
        ["Tray.cs"] = """
            namespace App;
            public sealed class Tray
            {
                public Outer Slot;
                public int Count() => Slot.K;
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

    /// <summary>The same zero, reached through an alias: the scan reads the member's syntax, which
    /// says `S`, so only the conversion that writes `new Span2()` can register the import, and the
    /// record's constructor asked for the zero without telling the conversion (found in review,
    /// #409).</summary>
    [Fact]
    public void ARecordImportsTheStructAnAliasNames()
    {
        var ts = TypeScriptOf("Aliased");

        ts.Should().MatchRegex(@"blank(: any)? = new Span2\(\)");
        ts.Should().MatchRegex(@"fresh(: any)? = new Span2\(\)");
        ts.Should().Contain("import { Span2 } from \"./Span2\"");
    }

    /// <summary>A zero that names a struct the declaration never mentions: Outer's is built member by
    /// member, so it constructs an Inner, and only the conversion that writes it knows. A record's
    /// constructor, a component's field and auto-property, and a plain class's field asked for the
    /// zero without telling the conversion, so nothing imported Inner (found in review, #409).</summary>
    [Theory]
    [InlineData("Nest")]
    [InlineData("Deck")]
    [InlineData("Panel")]
    [InlineData("Tray")]
    public void AZeroImportsTheStructsItConstructs(string module)
    {
        var ts = TypeScriptOf(module);

        ts.Should().Contain("new Outer(new Inner(), 0)");
        ts.Should().Contain("import { Outer } from \"./Outer\"");
        ts.Should().Contain("import { Inner } from \"./Inner\"");
    }

    [Fact]
    public void AComponentFieldOfAnAppStructStartsAtItsZero()
    {
        var ts = TypeScriptOf("Board");

        ts.Should().MatchRegex(@"_span(: \w+)? = new Span2\(\)");
        ts.Should().Contain("import { Span2 } from \"./Span2\"");
    }
}
