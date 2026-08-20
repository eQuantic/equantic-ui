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

        if (symbol == null && context.CanGuess(node))
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        // WHAT is being added decides how. A decimal crosses as a runtime Decimal, not a JS number,
        // so `_sum + amount` concatenates their text: a payments total read
        // "R$ 01240.50640.00" — the seed, then each amount, glued end to end. The result type of
        // the call answers for both forms, with and without a selector.
        var summed = context.SemanticHelper.GetType(invocation);
        var exact = summed.IsDecimal();

        string Add(string left, string right) => exact
            ? $"{Eq.Dec}({left}).add({Eq.Dec}({right}))"
            : $"{left} + {right}";
        var seed = exact ? $"{Eq.Dec}(0)" : "0";
        if (exact) context.UsedHelpers.Add(Eq.Import);

        if (args.Count > 0)
        {
            // Sum(x => x.Amount) -> reduce((sum, x) => sum + x.amount, 0)
            var selector = args[0].Expression;

            // Extract lambda parameter and body
            if (selector is SimpleLambdaExpressionSyntax lambda)
            {
                var param = lambda.Parameter.Identifier.Text;
                var body = context.Converter.ConvertExpression(lambda.Body as ExpressionSyntax ?? lambda.ExpressionBody!);
                return $"{caller}.reduce((_sum, {param}) => {Add("_sum", body)}, {seed})";
            }

            // Fallback for other expression types
            var selectorConverted = context.Converter.ConvertExpression(selector);
            return $"{caller}.reduce((_sum, _x) => {Add("_sum", $"{selectorConverted}(_x)")}, {seed})";
        }

        // Sum() without selector - the elements themselves.
        return $"{caller}.reduce((_a, _b) => {Add("_a", "_b")}, {seed})";
    }

    public int Priority => 10;
}
