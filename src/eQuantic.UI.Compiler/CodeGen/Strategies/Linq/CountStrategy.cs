using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Count() to JavaScript.
/// - Count() without predicate -> .length
/// - Count(predicate) -> .filter(predicate).length
/// </summary>
public class CountStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "Count")
            return false;

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
        {
            return true;
        }

        // Fallback: If no semantic model or unresolved symbol, assume it's LINQ if the name matches
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
        var hasArguments = invocation.ArgumentList.Arguments.Count > 0;

        if (hasArguments)
        {
            // Count(predicate) -> filter(predicate).length
            var predicate = context.Converter.ConvertExpression(
                invocation.ArgumentList.Arguments[0].Expression
            );
            return $"{caller}.filter({predicate}).length";
        }
        else
        {
            // Count() -> .length
            return $"{caller}.length";
        }
    }

    public int Priority => 10;
}
