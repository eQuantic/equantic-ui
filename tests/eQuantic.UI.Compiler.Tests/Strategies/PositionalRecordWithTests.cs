using System;
using System.Linq;
using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// `record TypeStyle(float Size, …)` — a POSITIONAL record — cannot be rebuilt from one object,
/// because its twin takes its arguments in order. A plain spread would also drop the prototype,
/// and the type's name appears nowhere in the source, so a wrong emission reached the browser as
/// "TypeStyle is not defined" with no build error at all: `$eq.withPatch` is what makes it work.
/// <para>
/// This used to be pinned through whichever shared component happened to write `with` that day,
/// which is a test that moves when a component is refactored. The rule belongs to the COMPILER,
/// so the fixture does too.
/// </para>
/// </summary>
public class PositionalRecordWithTests
{
    private static string Convert(string bodyCode, string declarations)
    {
        // The semantic model is what tells a POSITIONAL record from one with settable properties.
        var code = declarations + $"\nclass Wrapper {{ void Method() {{ {bodyCode} }} }}";
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create("probe", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var converter = new CSharpToJsConverter();
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "Method");
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(diagnostics.Length == 0,
            "the fixture must COMPILE, or the semantic model answers null and the rule never runs: "
            + string.Join("; ", diagnostics.Select(d => d.GetMessage())));
        return converter.Convert(method.Body!).Trim();
    }

    [Fact]
    public void WithOverAPositionalRecord_PatchesInsteadOfRebuilding()
    {
        var js = Convert(
            "var patched = Theme.Type() with { Size = 15 };",
            // The rule is scoped to the VOCABULARY namespace: those are the types the runtime
            // hand-writes a twin for, and the only ones whose copy shape the compiler must know.
            "namespace eQuantic.UI.Primitives { public record TypeStyle(float Size, float LineHeight); }\n"
            + "static class Theme { public static eQuantic.UI.Primitives.TypeStyle Type() "
            + "=> new(13, 16); }");

        Assert.Contains("$eq.withPatch(", js);
        Assert.DoesNotContain("new TypeStyle(", js);
    }

    [Fact]
    public void WithOverAPlainRecord_StillSpreads()
    {
        // A record with settable properties rebuilds from an object perfectly well, and the
        // spread is the cheaper, more idiomatic emission — the patch helper is for the case that
        // cannot use it, not for every case.
        var js = Convert(
            "var person = new Person();\n"
            + "var patched = person with { Age = 31 };",
            "record Person { public string Name { get; init; } public int Age { get; init; } }");

        Assert.Contains("age: 31", js);
        Assert.DoesNotContain("$eq.withPatch(", js,
            StringComparison.Ordinal);
    }
}
