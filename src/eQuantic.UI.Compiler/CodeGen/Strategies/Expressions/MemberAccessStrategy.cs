using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// General strategy for member access.
/// Handles property mapping (Length -> length) and standard naming conventions.
/// Serves as a fallback after specialized strategies (Enum, Nullable).
/// </summary>
public class MemberAccessStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is MemberAccessExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)node;
        var name = memberAccess.Name.Identifier.Text;
        var expr = context.Converter.ConvertExpression(memberAccess.Expression);

        // Convert C# properties to JS
        // Note: Specialized mappings (HasValue, Value) are handled by NullableStrategy
        
        // Semantic check for DateTime.Now, Guid.Empty, etc.
        var symbol = context.SemanticHelper.GetSymbol(node);
        if (symbol != null)
        {
            var containingType = symbol.ContainingType.ToDisplayString();
            if (containingType == "System.DateTime" && (symbol.Name == "Now" || symbol.Name == "Today"))
            {
                return "new Date()";
            }
            if (containingType == "System.Guid" && symbol.Name == "Empty")
            {
                return "''";
            }
        }

        // Heuristic fallback
        if (expr == "DateTime" && (name == "Now" || name == "Today")) return "new Date()";
        if (expr == "Guid" && name == "Empty") return "''";
        if ((expr == "string" || expr == "String") && name == "Empty") return "''";

        // .Count is type-dependent: Set -> .size, Dictionary -> Object.keys(x).length,
        // List/array/ICollection -> .length.
        if (name == "Count")
        {
            var def = context.SemanticHelper.GetType(memberAccess.Expression)?.OriginalDefinition?.ToString() ?? "";
            if (def.StartsWith("System.Collections.Generic.HashSet") ||
                def.StartsWith("System.Collections.Generic.ISet") ||
                def.StartsWith("System.Collections.Generic.IReadOnlySet"))
                return $"{expr}.size";
            if (def.StartsWith("System.Collections.Generic.Dictionary") ||
                def.StartsWith("System.Collections.Generic.IDictionary") ||
                def.StartsWith("System.Collections.Generic.IReadOnlyDictionary"))
                return $"Object.keys({expr}).length";

            // A USER type with its own `Count` is not a collection — its property emits as
            // `get count()`, and rewriting the read to `.length` returned undefined. `.length` stays
            // the answer for every real sequence, and for an unresolved receiver (untyped code,
            // where guessing array is the useful default).
            var receiverType = context.SemanticHelper.GetType(memberAccess.Expression);
            if (receiverType is not null && receiverType.Locations.Any(location => location.IsInSource))
                return $"{expr}.{name.ToCamelCase()}";

            return $"{expr}.length";
        }

        name = name switch
        {
            "Length" => "length",
            "Count" => "length", // Arrays/Lists (fallback when type is unknown)
            _ => name.ToCamelCase()
        };

        if (string.IsNullOrEmpty(name)) return expr;

        var result = $"{expr}.{name}";

        // If it's a method reference (not being called), add .bind(expr)
        if (symbol is IMethodSymbol)
        {
            var isDirectInvocation = memberAccess.Parent is InvocationExpressionSyntax invocation && 
                                  invocation.Expression == memberAccess;
            
            if (!isDirectInvocation)
            {
                result += $".bind({expr})";
            }
        }

        return result;
    }

    public int Priority => 0; // Low priority (fallback)
}
