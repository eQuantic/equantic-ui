using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Contains(item) to JavaScript .includes(item)
/// </summary>
public class ContainsStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "Contains")
            return false;

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms)
        {
            // Accept if it's a LINQ extension OR a collection method (List<T>.Contains, etc.)
            if (context.SemanticHelper.IsLinqExtension(ms.ContainingType))
                return true;

            // Also handle List<T>.Contains, ICollection<T>.Contains, etc.
            var containingType = ms.ContainingType?.ToString();
            if (containingType != null &&
                (containingType.StartsWith("System.Collections.Generic.List") ||
                 containingType.StartsWith("System.Collections.Generic.ICollection") ||
                 containingType.StartsWith("System.Collections.Generic.IEnumerable") ||
                 containingType == "string"))
            {
                return true;
            }
        }

        // Fallback: If no semantic model or unresolved symbol, assume it should be converted
        if (context.SemanticModel == null || symbol == null)
        {
            return true;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        if (args.Count > 0)
        {
            var item = context.Converter.ConvertExpression(args[0].Expression);
            return $"{caller}.includes({item})";
        }

        return $"{caller}.includes(undefined)";
    }

    public int Priority => 10;
}
