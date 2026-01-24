using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Any() to JavaScript.
/// - Any() without predicate -> length > 0
/// - Any(predicate) -> some(predicate)
/// </summary>
public class AnyStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "Any")
            return false;

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
        {
            return true;
        }

        // Fallback: If no semantic model, assume .Any() is LINQ-like if on enumerable
        if (context.SemanticModel == null)
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
            // Any(predicate) -> some(predicate)
            var predicate = context.Converter.ConvertExpression(
                invocation.ArgumentList.Arguments[0].Expression
            );
            return $"{caller}.some({predicate})";
        }
        else
        {
            // Any() -> length > 0
            return $"{caller}.length > 0";
        }
    }

    public int Priority => 10;
}
