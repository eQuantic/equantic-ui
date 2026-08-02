using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class WithExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is WithExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var withExpr = (WithExpressionSyntax)node;
        var receiver = context.Converter.ConvertExpression(withExpr.Expression);

        // Build the changed members as `camelKey: value` entries from the initializer.
        var entries = new StringBuilder();
        if (withExpr.Initializer is InitializerExpressionSyntax initializer)
        {
            foreach (var expr in initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    if (entries.Length > 0) entries.Append(", ");
                    // The left side is a property name (`X`) — emit it as a camelCased object key.
                    var key = assignment.Left.ToString().ToCamelCase();
                    var value = context.Converter.ConvertExpression(assignment.Right);
                    entries.Append($"{key}: {value}");
                }
            }
        }

        var type = context.SemanticHelper.GetType(withExpr.Expression);

        // The abstract VOCABULARY (BoxStyle, TransitionSpec, …) is a hand-written TS twin whose
        // CONSTRUCTOR normalizes what it is given (`SizeValue.from(height)`, defaults for the rest).
        // A raw spread bypasses it, so `style with { Height = 44 }` would leave a bare number where a
        // SizeValue belongs — SSR keeps the height, the hydrated client drops it. Rebuild through the
        // constructor instead: the config carries the copy plus the changes.
        if (type != null && IsRuntimeVocabulary(type))
        {
            return $"new {type.Name}({{ ...{receiver}, {entries} }})";
        }

        // Records the compiler EMITS are JS classes — copy via their generated `with` so the
        // prototype (methods) survives. Other value shapes (non-record structs, anonymous types) are
        // plain objects — spread is fine.
        if (type is { IsRecord: true })
        {
            return $"{receiver}.with({{ {entries} }})";
        }
        return $"{{ ...{receiver}, {entries} }}";
    }

    /// <summary>
    /// Types the RUNTIME provides a hand-written twin for — the shared vocabulary. Same rule the
    /// object-creation path uses to choose a trailing config object over positional arguments.
    /// </summary>
    private static bool IsRuntimeVocabulary(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns == "eQuantic.UI.Primitives" || ns.StartsWith("eQuantic.UI.Primitives.");
    }

    public int Priority => 10;
}
