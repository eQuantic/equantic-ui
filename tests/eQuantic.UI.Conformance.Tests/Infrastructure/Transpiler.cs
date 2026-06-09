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

        return valueTypes.Count == 0
            ? string.Empty
            : string.Join("\n", valueTypes.Select(emitter.Emit)) + "\n";
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

        return converter.Convert(body); // dispatches to ConvertBlock -> "{ … }"
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
