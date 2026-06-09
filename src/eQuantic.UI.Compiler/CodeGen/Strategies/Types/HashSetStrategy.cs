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
            var typeSymbol = context.SemanticHelper.GetType(ma.Expression);
            var type = typeSymbol?.ToDisplayString();
            return type != null && type.StartsWith("System.Collections.Generic.HashSet");
            // No safe heuristic for variable name
        }
        
        return false;
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

            return name switch
            {
                "Add" => $"{expr}.add({arg})",
                "Contains" => $"{expr}.has({arg})",
                "Remove" => $"{expr}.delete({arg})",
                "Clear" => $"{expr}.clear()",
                _ => $"{expr}.{name.ToCamelCase()}({arg})"
            };
        }

        return node.ToString();
    }

    public int Priority => 10;
}
