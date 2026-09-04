using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// Plain-JavaScript mode has to produce something a BROWSER can parse.
/// <para>
/// The distinction matters more than it sounds. In TypeScript mode a stray annotation is invisible —
/// the bundler strips it. In plain mode the module goes to a browser as written, and one <c>: T</c>
/// is a parse error, which means the module never executes at all: no line of it runs, no handler it
/// would have installed exists, and the only symptom is an empty frame. That is what the playground,
/// the conformance harness and the design host all consume.
/// </para>
/// <para>
/// The members here are the ones that were getting it wrong: they are written through <c>Raw</c>
/// rather than through <c>Field</c>, so each had to ask about annotations itself, and none did.
/// </para>
/// </summary>
public class PlainJavaScriptSyntaxTests
{
    private const string AnnotationHeavySource = """
        using System.Collections.Generic;
        using eQuantic.UI.Web;
        using eQuantic.UI.Primitives;

        public sealed record Row(string Id, string Label);

        // A PRIMITIVE component (an HtmlElement) takes the generic props constructor, which is where
        // the optional-parameter marker is written by hand.
        public sealed class Marker : HtmlElement
        {
        }

        public static class Catalogue
        {
            // A static collection: emitted as a lazy getter with a backing slot, because a static
            // that CONSTRUCTS runs at module-evaluation time and the modules import in a cycle.
            public static readonly List<Row> Rows = new() { new Row("1", "one"), new Row("2", "two") };
        }

        // An ABSTRACT property is declared for the type checker and emitted by nobody: a field here
        // would become an own property that shadows the derived class's getter.
        public abstract class Shape
        {
            public abstract string Name { get; }
        }

        public sealed class Square : Shape
        {
            public override string Name => "square";
        }

        // A PLAIN class — the emission path where getters and setters carry their own annotations,
        // which is a different one from a component's.
        public sealed class Model
        {
            // Getter and setter with bodies over an explicit backing field — the guarded-assignment
            // idiom, which is where a model keeps its invariants.
            private string _label = "none";
            public string Label
            {
                get { return _label; }
                set { if (value == _label) return; _label = value; }
            }
        }

        public sealed class Probe : StatefulComponent
        {
            private string? _notice;
            private readonly Model _model = new();

            // Expression-bodied getter.
            public int Count => Catalogue.Rows.Count;

            // An auto-property of reference type: declared, never emitted as a field.
            public string? Subtitle { get; set; }

            public override VisualNode Build(ComponentContext context)
            {
                // A pattern variable is hoisted out of the condition it is assigned in.
                var column = new Column(gap: Space.S2);
                // C#'s null-forgiving `!` — an assertion that produces no code.
                var first = _model!.Label;
                column.Add(new Text(first, TypeRole.BodyM, context.Theme.TextPrimary));
                if (_notice is { } notice) column.Add(new Text(notice, TypeRole.BodyM, context.Theme.TextPrimary));
                column.Add(new Text(_model.Label, TypeRole.BodyM, context.Theme.TextPrimary));
                return column;
            }
        }
        """;

    private static string EmitPlainJs()
    {
        var compiler = new ComponentCompiler { TypeAnnotations = false };
        var modules = compiler.CompileSource(AnnotationHeavySource, "Probe.cs")
            .Where(r => r.Success && r.TypeScript.Length > 0)
            .ToList();
        Assert.NotEmpty(modules);
        return string.Join("\n\n", modules.Select(m => m.TypeScript));
    }

    /// <summary>
    /// The three keywords JavaScript has no idea what to do with. <c>abstract</c> and <c>declare</c>
    /// are type-only and must vanish entirely; an annotation must simply not be written.
    /// </summary>
    [Theory]
    [InlineData(@"\bdeclare\s", "declare")]
    [InlineData(@"\babstract\s", "abstract")]
    [InlineData(@"^\s*static\s+_\w+\s*:", "an annotated lazy-static backing slot")]
    [InlineData(@"\bget\s+\w+\(\)\s*:", "an annotated getter")]
    [InlineData(@"\bset\s+\w+\(value\s*:", "an annotated setter")]
    [InlineData(@"\blet\s+\w+\s*:\s*any\b", "an annotated hoisted pattern variable")]
    [InlineData(@"\w\?\s*:", "an optional-parameter marker")]
    [InlineData(@"\w!\s*[.);,]", "a null-forgiving assertion")]
    public void PlainJavaScript_CarriesNoTypeScriptOnlySyntax(string pattern, string what)
    {
        var js = EmitPlainJs();
        var match = Regex.Match(js, pattern, RegexOptions.Multiline);
        Assert.False(match.Success,
            $"plain-JavaScript output still contains {what}: \"{match.Value}\" — a browser rejects the "
            + "whole module at parse time, so nothing in it runs.");
    }

    /// <summary>
    /// TypeScript mode is the SDK's path and must be untouched by any of the above: the annotations
    /// are the point there, and the two-layer type checking is what they exist for.
    /// </summary>
    [Fact]
    public void TypeScriptMode_StillCarriesItsAnnotations()
    {
        var compiler = new ComponentCompiler { TypeAnnotations = true };
        var ts = string.Join("\n\n", compiler.CompileSource(AnnotationHeavySource, "Probe.cs")
            .Where(r => r.Success)
            .Select(r => r.TypeScript));

        // Each of these also PROVES the matching plain-mode guard above is not vacuous: it is the
        // evidence that this source really does drive the code path that guard watches.
        var missing = new[]
            {
                (@"\bget\s+\w+\(\)\s*:", "an annotated getter"),
                (@"\bset\s+\w+\(value\s*:", "an annotated setter"),
                (@"^\s*static\s+_\w+\s*:", "an annotated lazy-static backing slot"),
                (@"\blet\s+\w+\s*:\s*any\b", "an annotated hoisted pattern variable"),
                (@"\bdeclare\s", "a type-only declared property"),
                (@"\w\?\s*:", "an optional parameter"),
                (@"\w!\s*[.);,]", "a null-forgiving assertion"),
                (@"\babstract\s+\w+\s*:", "a type-only abstract property"),
            }
            .Where(probe => !Regex.IsMatch(ts, probe.Item1, RegexOptions.Multiline))
            .Select(probe => probe.Item2)
            .ToArray();

        Assert.True(missing.Length == 0,
            "the probe source no longer produces " + string.Join(", ", missing)
            + " — so the matching plain-JavaScript guard is passing vacuously. Restore the member "
            + "that produced it, or drop the guard.\n\n" + ts);
    }
}
