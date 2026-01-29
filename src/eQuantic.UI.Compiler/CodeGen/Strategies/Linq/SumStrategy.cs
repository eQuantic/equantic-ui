using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Sum() to JavaScript .reduce().
/// - Sum() -> array.reduce((a, b) => a + b, 0)
/// - Sum(selector) -> array.reduce((sum, x) => sum + selector(x), 0)
/// </summary>
public class SumStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "Sum")
            return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
            return true;

        if (context.SemanticModel == null || symbol == null)
            return true;

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
            // Sum(x => x.Amount) -> reduce((sum, x) => sum + x.amount, 0)
            var selector = args[0].Expression;

            // Extract lambda parameter and body
            if (selector is SimpleLambdaExpressionSyntax lambda)
            {
                var param = lambda.Parameter.Identifier.Text;
                var body = context.Converter.ConvertExpression(lambda.Body as ExpressionSyntax ?? lambda.ExpressionBody!);
                return $"{caller}.reduce((_sum, {param}) => _sum + {body}, 0)";
            }

            // Fallback for other expression types
            var selectorConverted = context.Converter.ConvertExpression(selector);
            return $"{caller}.reduce((_sum, _x) => _sum + {selectorConverted}(_x), 0)";
        }

        // Sum() without selector - assume array of numbers
        return $"{caller}.reduce((_a, _b) => _a + _b, 0)";
    }

    public int Priority => 10;
}
