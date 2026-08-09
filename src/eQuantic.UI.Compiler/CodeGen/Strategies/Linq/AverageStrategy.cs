using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Average() to JavaScript reduce + divide.
/// - Average() -> array.reduce((a, b) => a + b, 0) / array.length
/// - Average(selector) -> array.reduce((sum, x) => sum + selector(x), 0) / array.length
/// </summary>
public class AverageStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        if (memberAccess.Name.Identifier.Text != "Average")
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

        // Same rule as Sum: a decimal crosses as a runtime Decimal, so `+` concatenates. Averaging
        // then divided that text by a count and produced NaN — visibly broken rather than quietly
        // wrong, which is the only mercy in it.
        var exact = context.SemanticHelper.GetType(invocation).IsDecimal();
        if (exact) context.UsedHelpers.Add(Eq.Import);

        string Add(string left, string right) => exact
            ? $"{Eq.Dec}({left}).add({Eq.Dec}({right}))"
            : $"{left} + {right}";
        var seed = exact ? $"{Eq.Dec}(0)" : "0";
        string Divide(string sum) => exact
            ? $"{Eq.Dec}({sum}).div({Eq.Dec}({caller}.length))"
            : $"({sum} / {caller}.length)";

        if (args.Count > 0)
        {
            // Average(x => x.Value) -> reduce then divide
            var selector = args[0].Expression;

            if (selector is SimpleLambdaExpressionSyntax lambda)
            {
                var param = lambda.Parameter.Identifier.Text;
                var body = context.Converter.ConvertExpression(lambda.Body as ExpressionSyntax ?? lambda.ExpressionBody!);
                return Divide($"{caller}.reduce((_sum, {param}) => {Add("_sum", body)}, {seed})");
            }

            var selectorConverted = context.Converter.ConvertExpression(selector);
            return Divide($"{caller}.reduce((_sum, _x) => {Add("_sum", $"{selectorConverted}(_x)")}, {seed})");
        }

        // Average() without selector
        return Divide($"{caller}.reduce((_a, _b) => {Add("_a", "_b")}, {seed})");
    }

    public int Priority => 10;
}
