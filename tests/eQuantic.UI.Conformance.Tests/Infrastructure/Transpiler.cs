using System.Linq;
using System.Reflection;
using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// Transpiles a standalone C# expression (or statement block) to JavaScript using the real
/// CSharpToJsConverter, with a semantic model so type-aware strategies (integer division,
/// enums, etc.) behave exactly as in a real build.
/// </summary>
public static class Transpiler
{
    /// <summary>
    /// Emits any positional records declared in the prelude as named JS classes (so instance methods,
    /// structural equality, etc. are available to the program under test). Returns "" when there are none.
    /// </summary>
    public static string EmitDeclaredRecordTypes(string prelude)
    {
        if (string.IsNullOrWhiteSpace(prelude)) return string.Empty;

        var (tree, converter) = Compile("return 0;", prelude);
        var emitter = new RecordTypeEmitter(converter);
        var valueTypes = tree.GetRoot()
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(RecordTypeEmitter.CanEmit)
            .ToList();
        if (valueTypes.Count == 0) return string.Empty;

        // Emit base records before derived ones — JS `class X extends Base` needs Base already declared.
        var names = valueTypes.Select(t => t.Identifier.Text).ToHashSet();
        string? EmittedBaseOf(TypeDeclarationSyntax t)
        {
            var b = t.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault()?.Type.ToString();
            if (b != null && b.Contains('<')) b = b[..b.IndexOf('<')];
            return b != null && names.Contains(b) ? b : null;
        }

        var ordered = new List<TypeDeclarationSyntax>();
        var done = new HashSet<string>();
        void Add(TypeDeclarationSyntax t)
        {
            if (!done.Add(t.Identifier.Text)) return;
            var baseName = EmittedBaseOf(t);
            var baseType = baseName == null ? null : valueTypes.FirstOrDefault(x => x.Identifier.Text == baseName);
            if (baseType != null) Add(baseType);
            ordered.Add(t);
        }
        foreach (var t in valueTypes) Add(t);

        return string.Join("\n", ordered.Select(emitter.Emit)) + "\n";
    }

    public static string TranspileExpression(string csharpExpression, string prelude = "")
    {
        var (tree, converter) = Compile($"return {csharpExpression};", prelude);
        // Scope to the __Eval method body — a prelude type may now declare methods whose own `return`
        // statements would otherwise be picked up by a tree-wide First().
        var returnExpr = tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "__Eval")
            .Body!
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .First()
            .Expression!;

        return converter.ConvertExpression(returnExpr);
    }

    /// <summary>
    /// Transpiles a block of C# statements (the body of <c>__Eval</c>) to a JS block <c>{ … }</c>.
    /// The block is expected to <c>return</c> a value; the runner wraps it in an IIFE to capture it.
    /// Exercises the control-flow statement strategies (if/for/foreach/while/switch/try/…).
    /// </summary>
    public static string TranspileStatements(string csharpStatements, string prelude = "")
    {
        var (tree, converter) = Compile(csharpStatements, prelude);
        var body = tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "__Eval")
            .Body!;

        var js = converter.Convert(body); // dispatches to ConvertBlock -> "{ … }"

        // Hoist `out var` declarations to `let` at the top of the block, mirroring TypeScriptEmitter's
        // render-method emission. Without this, a program that uses `out var` (e.g.
        // `d.TryGetValue(k, out var v)`) assigns to an undeclared name, which throws under the ESM
        // strict mode the harness runs the emitted JS in.
        // Only single-variable `out var x` designations are hoisted. Parenthesised designations
        // (`var (a, b) = …` deconstruction) are emitted as `let { … } = …` by the assignment strategy,
        // so hoisting them here would produce an invalid `let (a, b);`.
        var outVars = body.DescendantNodes()
            .OfType<DeclarationExpressionSyntax>()
            .Select(d => d.Designation)
            .OfType<SingleVariableDesignationSyntax>()
            .Select(s => s.Identifier.Text)
            .Where(n => n != "_")
            .Distinct()
            .ToList();
        if (outVars.Count == 0) return js;

        var hoist = string.Join(" ", outVars.Select(v => $"let {v};"));
        var trimmed = js.TrimStart();
        return trimmed.StartsWith("{")
            ? "{ " + hoist + " " + trimmed[1..]
            : hoist + " " + js;
    }

    private static (SyntaxTree Tree, CSharpToJsConverter Converter) Compile(string evalBody, string prelude)
    {
        var code = $@"
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

{prelude}

public class __Conformance
{{
    public object? __Eval()
    {{
        {evalBody}
    }}
}}";

        var tree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "ConformanceAsm",
            new[] { tree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.Stack<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.DateOnly).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var converter = new CSharpToJsConverter();
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));
        return (tree, converter);
    }
}
