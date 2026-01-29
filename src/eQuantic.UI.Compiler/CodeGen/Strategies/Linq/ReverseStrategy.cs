using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Reverse() to JavaScript [...array].reverse().
/// Note: We use spread to avoid mutating the original array.
/// - Reverse() -> [...array].reverse()
/// </summary>
public class ReverseStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "Reverse")
            return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms)
        {
            // Handle LINQ Enumerable.Reverse<T>()
            if (context.SemanticHelper.IsLinqExtension(ms.ContainingType))
                return true;

            // Handle List<T>.Reverse() instance method
            var containingType = ms.ContainingType.ToDisplayString();
            if (containingType.StartsWith("System.Collections.Generic.List<"))
                return true;
        }

        // Fallback when no semantic info
        if (context.SemanticModel == null || symbol == null)
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);

        // Use spread to create a copy before reversing (JS reverse mutates in place)
        return $"[...{caller}].reverse()";
    }

    public int Priority => 10;
}
