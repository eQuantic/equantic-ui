using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Sum() to JavaScript .reduce(), adding by the type of the values the bound overload adds
/// (<see cref="LinqAccumulation"/>), as <see cref="AverageStrategy"/> reads it:
/// - Sum() over ints -> array.reduce(($a, $b) => $eq.num.addInt($a, $b), 0)
/// - Sum(x => x.Price) over doubles -> array.reduce(($sum, x) => $sum + x.price, 0), the lambda's body the callback's
/// - Sum(selector) -> array.reduce(($sum, $x) => $sum + selector($x), 0), the selector evaluated once
/// An int and a long add in a checked context whatever the context of the call, so a total past the range
/// throws OverflowException, where the reduce kept counting (#1808); a nullable's null is passed over, where
/// it was added to a BigInt or a Decimal and threw a TypeError. The type came from what the call answers,
/// which said a long for a <c>long?</c> sum no more than for an int's.
/// The reduce stays inline, where the runtime's own loop, called from every sum of a page with a selector
/// or without, measured five times slower on a million doubles: a real adds with <c>+</c> as it did.
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

        if (memberAccess.Name.Identifier.ValueText != "Sum")
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

        // WHAT is being added decides how, and the bound overload says it. With no model, the call's
        // answer is the guess: a decimal is a runtime Decimal, not a JS number, so `$sum + amount`
        // concatenated their text (a payments total read "R$ 01240.50640.00"), and a long is a BigInt,
        // whose `+` throws next to a NUMBER seed. An int it cannot tell from a double.
        string mean;
        var nullable = false;
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
        {
            if (LinqAccumulation.ValuesOf(method) is not var (bound, isNullable))
                return JsExpr.Opaque(context.Unhandled(invocation, $"LINQ Sum over {method.ReturnType.ToDisplayString()}"));
            (mean, nullable) = (bound, isNullable);
        }
        else
        {
            var summed = context.SemanticHelper.GetType(invocation);
            mean = summed.IsDecimal() ? "decimal" : summed.IsLong() ? "long" : SinglePrecision.Is(summed) ? "single" : "number";
        }
        if (LinqAccumulation.ReadsTheRuntime(mean)) context.UsedHelpers.Add(Eq.Import);
        var seed = LinqAccumulation.Zero(mean);

        // A FLOAT sum is .NET's: accumulated in a DOUBLE and converted once at the end
        // (`(float)Sum<float, double>(source)`), so the reduce stays in doubles and only its
        // answer rounds to the single the call returns (SinglePrecision).
        JsExpr Settle(JsExpr total) => mean == "single" ? JsExpr.Template("Math.fround({0})", [total]) : total;

        // Sum(x => x.Amount): the lambda's body is the callback's, its parameter the element. A lambda
        // with a block goes down the selector's path, where it stays a lambda written in place.
        if (args.Count > 0 && args[0].Expression is SimpleLambdaExpressionSyntax { ExpressionBody: { } expression } lambda)
        {
            var param = lambda.Parameter.Identifier.ValueText.ToJsIdentifier();
            var body = context.Converter.ConvertIr(expression);
            var callback = JsExpr.Arrow($"$sum, {param}", LinqAccumulation.Add(JsExpr.Identifier("$sum"), body, mean, nullable));
            return Settle(JsExpr.Template($"{{0}}.reduce({{1}}, {seed})", [source, callback]));
        }

        // Any other selector is evaluated once, before the reduce runs, as C# evaluates an argument.
        if (args.Count > 0)
            return Settle(JsExpr.Template(
                $"{{0}}.reduce(($sum, $x) => {LinqAccumulation.Add("$sum", "{1}($x)", mean, nullable)}, {seed})",
                [source, context.Converter.ConvertIr(args[0].Expression)]));

        // Sum() without selector - the elements themselves.
        return Settle(JsExpr.Template($"{{0}}.reduce(($a, $b) => {LinqAccumulation.Add("$a", "$b", mean, nullable)}, {seed})",
            [source]));
    }

    public int Priority => 10;
}
