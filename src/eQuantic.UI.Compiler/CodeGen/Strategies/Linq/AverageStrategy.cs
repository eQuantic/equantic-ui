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

        // Same rule as Sum: a decimal is a runtime Decimal, so `+` concatenates. Averaging then
        // divided that text by a count and produced NaN — visibly broken rather than quietly
        // wrong, which is the only mercy in it. LONG elements sum exactly as BigInt (a number
        // seed would throw) and divide as the double C# declares Average(long) to return.
        var exact = context.SemanticHelper.GetType(invocation).IsDecimal();
        if (exact) context.UsedHelpers.Add(Eq.Import);
        var longElements = !exact && (args.Count > 0 && args[0].Expression is SimpleLambdaExpressionSyntax selectorLambda
            ? context.SemanticHelper.GetType((SyntaxNode?)selectorLambda.ExpressionBody ?? selectorLambda.Body)
            : context.SemanticHelper.GetType(memberAccess.Expression).GetEnumerableElementType()).IsLong();

        // The accumulator starts as the Decimal seed and each element IS a Decimal (typed world).
        // The COUNT is a plain number, so the divisor converts — that one dec() is a conversion.
        string Add(string left, string right) => exact
            ? $"{left}.add({right})"
            : $"{left} + {right}";
        var seed = exact ? $"{Eq.Dec}(0)" : longElements ? "0n" : "0";
        string Divide(string sum) => exact
            ? $"{sum}.div({Eq.Dec}({caller}.length))"
            : longElements
                ? $"(Number({sum}) / {caller}.length)"
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
