using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Strategy for HashSet.
/// Handles: 
/// - new HashSet<T>() -> new Set()
/// - set.Add(x) -> set.add(x)
/// - set.Contains(x) -> set.has(x)
/// </summary>
public class HashSetStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // Check for ObjectCreation
        if (node is ObjectCreationExpressionSyntax creation)
        {
            var typeSymbol = context.SemanticHelper.GetType(creation);
            if (typeSymbol != null) return typeSymbol.ToDisplayString().StartsWith("System.Collections.Generic.HashSet");
            
            return creation.Type.ToString().StartsWith("HashSet");
        }
        
        // Check for Invocation (Add, Contains)
        if (node is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma)
        {
            // The INTERFACES too. A member typed `IReadOnlySet<string>` is a JS Set at runtime
            // exactly as a HashSet is — matching only the concrete name left `Keywords.Contains(w)`
            // emitting `.contains`, which a Set does not have.
            return IsSet(context.SemanticHelper.GetType(ma.Expression));
        }
        
        return false;
    }

    /// <summary>Whether a type IS a JS Set on the other side — the class or any of its read-only
    /// faces.</summary>
    private static bool IsSet(Microsoft.CodeAnalysis.ITypeSymbol? type)
    {
        if (type is null) return false;
        var name = type.OriginalDefinition.ToDisplayString();
        return name is "System.Collections.Generic.HashSet<T>"
            or "System.Collections.Generic.ISet<T>"
            or "System.Collections.Generic.IReadOnlySet<T>"
            || type.ToDisplayString().StartsWith("System.Collections.Generic.HashSet");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is ObjectCreationExpressionSyntax creation)
        {
            // new HashSet<T>{ 1, 2, 3 } -> new Set([1, 2, 3])
            if (creation.Initializer is { Expressions.Count: > 0 } init)
            {
                var elements = init.Expressions.Select(e => context.Converter.ConvertExpression(e));
                return $"new Set([{string.Join(", ", elements)}])";
            }

            // new HashSet<T>(existingEnumerable) -> new Set(existingEnumerable)
            if (creation.ArgumentList is { Arguments.Count: 1 } argList)
            {
                return $"new Set({context.Converter.ConvertExpression(argList.Arguments[0].Expression)})";
            }

            return "new Set()";
        }
        
        if (node is InvocationExpressionSyntax invocation)
        {
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
            var name = memberAccess.Name.Identifier.Text;
            var expr = context.Converter.ConvertExpression(memberAccess.Expression);
            var arg = invocation.ArgumentList.Arguments.Count > 0 
                ? context.Converter.ConvertExpression(invocation.ArgumentList.Arguments[0].Expression) 
                : "";

            // `Add` ANSWERS something in C#: true when the value was new. A JS Set.add returns the
            // set — always truthy — so `if (!set.Add(x)) set.Remove(x)` quietly became "add, and
            // never remove", which is a checkbox that ticks once and then ignores you.
            if (name == "Add")
            {
                context.UsedHelpers.Add(Eq.Import);
                return $"{Eq.SetAdd}({expr}, {arg})";
            }

            return name switch
            {
                "Contains" => $"{expr}.has({arg})",
                "Remove" => $"{expr}.delete({arg})",
                "Clear" => $"{expr}.clear()",
                _ => $"{expr}.{name.ToCamelCase()}({arg})"
            };
        }

        return context.Unhandled(node, "HashSet");
    }

    public int Priority => 10;
}
