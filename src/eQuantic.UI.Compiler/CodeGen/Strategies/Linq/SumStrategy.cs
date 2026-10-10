using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Sum() to JavaScript .reduce().
/// - Sum() -> array.reduce(($a, $b) => $a + $b, 0)
/// - Sum(x => x.Amount) -> array.reduce(($sum, x) => $sum + x.amount, 0), the lambda's body the callback's
/// - Sum(selector) -> array.reduce(($sum, $x) => $sum + selector($x), 0), the selector evaluated once
/// The names the reduce declares take a `$`, which no C# name holds: the accumulator was `_sum`, and a
/// captured local of that name read the running total instead (#397).
/// </summary>
public class SumStrategy : IExpressionIrStrategy
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

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

        var source = LinqSource.Ir(memberAccess.Expression, context);
        var args = invocation.ArgumentList.Arguments;

        // WHAT is being added decides how. A decimal is a runtime Decimal, not a JS number, so
        // `$sum + amount` concatenates their text: a payments total read "R$ 01240.50640.00" —
        // the seed, then each amount, glued end to end. A long is a BigInt, whose `+` throws next
        // to a NUMBER seed — the seed must be 0n. The result type of the call answers for both
        // forms, with and without a selector.
        var summed = context.SemanticHelper.GetType(invocation);
        var exact = summed.IsDecimal();
        if (exact) context.UsedHelpers.Add(Eq.Import);
        var seed = exact ? $"{Eq.Dec}(0)" : summed.IsLong() ? "0n" : "0";

        // A FLOAT sum is .NET's: accumulated in a DOUBLE and converted once at the end
        // (`(float)Sum<float, double>(source)`), so the reduce stays in doubles and only its
        // answer rounds to the single the call returns (SinglePrecision).
        JsExpr Settle(JsExpr total) => SinglePrecision.Is(summed) ? JsExpr.Template("Math.fround({0})", [total]) : total;

        // Sum(x => x.Amount): the lambda's body is the callback's, its parameter the element. A lambda
        // with a block goes down the selector's path, where it stays a lambda written in place.
        if (args.Count > 0 && args[0].Expression is SimpleLambdaExpressionSyntax { ExpressionBody: { } expression } lambda)
        {
            var param = lambda.Parameter.Identifier.Text.ToJsIdentifier();
            var body = context.Converter.ConvertIr(expression);
            var callback = JsExpr.Arrow($"$sum, {param}", LinqAccumulation.Add(JsExpr.Identifier("$sum"), body, exact));
            return Settle(JsExpr.Template($"{{0}}.reduce({{1}}, {seed})", [source, callback]));
        }

        // Any other selector is evaluated once, before the reduce runs, as C# evaluates an argument.
        if (args.Count > 0)
            return Settle(JsExpr.Template(
                $"{{0}}.reduce(($sum, $x) => {LinqAccumulation.Add("$sum", "{1}($x)", exact)}, {seed})",
                [source, context.Converter.ConvertIr(args[0].Expression)]));

        // Sum() without selector - the elements themselves.
        return Settle(JsExpr.Template($"{{0}}.reduce(($a, $b) => {LinqAccumulation.Add("$a", "$b", exact)}, {seed})",
            [source]));
    }

    public int Priority => 10;
}
