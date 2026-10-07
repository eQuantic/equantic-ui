using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Average() to JavaScript reduce + divide.
/// - Average() -> array.reduce(($a, $b) => $a + $b, 0) / array.length
/// - Average(selector) -> array.reduce(($sum, $x) => $sum + selector($x), 0) / array.length
/// The source is read once, though the division names it twice, and so is a selector that is not a
/// lambda: a call there ran once for the total and once more for the count. The names the reduce
/// declares take a `$`, which no C# name holds (#397).
/// </summary>
public class AverageStrategy : IExpressionIrStrategy
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

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var source = LinqSource.Ir(memberAccess.Expression, context);
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
        var seed = exact ? $"{Eq.Dec}(0)" : longElements ? "0n" : "0";
        // A FLOAT average is .NET's: the sum and the division in DOUBLE, converted once at the end
        // (`(float)Average<float, double, double>(source)`) — SinglePrecision.
        var single = SinglePrecision.Is(context.SemanticHelper.GetType(invocation));
        // {0} is the source, named twice and read once: the template writer binds it.
        string Divide(string sum) => exact
            ? $"{sum}.div({Eq.Dec}({{0}}.length))"
            : longElements
                ? $"(Number({sum}) / {{0}}.length)"
                : single
                    ? $"Math.fround({sum} / {{0}}.length)"
                    : $"({sum} / {{0}}.length)";

        // Average(x => x.Value): the lambda's body is the callback's, its parameter the element.
        if (args.Count > 0 && args[0].Expression is SimpleLambdaExpressionSyntax lambda)
        {
            var param = lambda.Parameter.Identifier.Text.ToJsIdentifier();
            var body = context.Converter.ConvertIr(lambda.Body as ExpressionSyntax ?? lambda.ExpressionBody!);
            var callback = JsExpr.Arrow($"$sum, {param}", LinqAccumulation.Add(JsExpr.Identifier("$sum"), body, exact));
            return JsExpr.Template(Divide($"{{0}}.reduce({{1}}, {seed})"), [source, callback], context.TypeAnnotations);
        }

        // Any other selector is evaluated once, before the reduce runs, as C# evaluates an argument.
        if (args.Count > 0)
            return JsExpr.Template(
                Divide($"{{0}}.reduce(($sum, $x) => {LinqAccumulation.Add("$sum", "{1}($x)", exact)}, {seed})"),
                [source, context.Converter.ConvertIr(args[0].Expression)], context.TypeAnnotations);

        // Average() without selector
        return JsExpr.Template(Divide($"{{0}}.reduce(($a, $b) => {LinqAccumulation.Add("$a", "$b", exact)}, {seed})"),
            [source], context.TypeAnnotations);
    }

    public int Priority => 10;
}
