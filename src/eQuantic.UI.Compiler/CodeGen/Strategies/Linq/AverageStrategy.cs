using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// LINQ's <c>Average()</c> and <c>Average(selector)</c>, through the runtime's <c>$eq.linq.average</c>
/// with what only the bound overload says: the type of the values it means, which decides how they add
/// (a long's total exact as a BigInt, a float's in a double rounded once, a decimal's as a Decimal), and
/// whether they are nullable, which passes a null over and answers null for a sequence with no value
/// (#719). An inline reduce read the type from a selector's lambda body or else from the source's
/// element type, so a method group or a delegate that answered a long added a BigInt to a number seed,
/// a nullable's total was divided by its nulls too, and an empty sequence answered NaN where .NET
/// throws. A call no model binds keeps that reduce:
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

        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
        {
            if (MeanOf(method) is not var (mean, nullable))
                return JsExpr.Opaque(context.Unhandled(invocation, $"LINQ Average over {method.ReturnType.ToDisplayString()}"));
            return LinqArguments.Call(Eq.LinqAverage, source, ["selector"], invocation,
                    argument => context.Converter.ConvertIr(argument.Expression), context,
                    [$"'{mean}'", nullable ? "true" : "false"])
                ?? JsExpr.Opaque(context.Unhandled(invocation, "LINQ Average"));
        }

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

        // Average(x => x.Value): the lambda's body is the callback's, its parameter the element. A lambda
        // with a block goes down the selector's path, where it stays a lambda written in place.
        if (args.Count > 0 && args[0].Expression is SimpleLambdaExpressionSyntax { ExpressionBody: { } expression } lambda)
        {
            var param = lambda.Parameter.Identifier.Text.ToJsIdentifier();
            var body = context.Converter.ConvertIr(expression);
            var callback = JsExpr.Arrow($"$sum, {param}", LinqAccumulation.Add(JsExpr.Identifier("$sum"), body, exact));
            return JsExpr.Template(Divide($"{{0}}.reduce({{1}}, {seed})"), [source, callback]);
        }

        // Any other selector is evaluated once, before the reduce runs, as C# evaluates an argument.
        if (args.Count > 0)
            return JsExpr.Template(
                Divide($"{{0}}.reduce(($sum, $x) => {LinqAccumulation.Add("$sum", "{1}($x)", exact)}, {seed})"),
                [source, context.Converter.ConvertIr(args[0].Expression)]);

        // Average() without selector
        return JsExpr.Template(Divide($"{{0}}.reduce(($a, $b) => {LinqAccumulation.Add("$a", "$b", exact)}, {seed})"),
            [source]);
    }

    /// <summary>
    /// How the runtime adds the values an overload means (<c>Mean</c> in <c>utils/linq.ts</c>), and whether
    /// they are nullable: the selector's result type where the overload takes one, and the source's element
    /// type otherwise, which every overload without a selector names outright. Null for any other type,
    /// which no overload of .NET 10 means.
    /// </summary>
    private static (string Mean, bool Nullable)? MeanOf(IMethodSymbol method)
    {
        var values = method.Parameters.FirstOrDefault(parameter => parameter.Name == "selector")?.Type is INamedTypeSymbol
        {
            TypeArguments: [_, var result],
        }
            ? result
            : ((method.ReducedFrom ?? method).Parameters.FirstOrDefault()?.Type).GetEnumerableElementType();
        var mean = values.UnwrapNullable()?.SpecialType switch
        {
            SpecialType.System_Int32 => "int",
            SpecialType.System_Double => "number",
            SpecialType.System_Int64 => "long",
            SpecialType.System_Single => "single",
            SpecialType.System_Decimal => "decimal",
            _ => null,
        };
        return mean is null ? null : (mean, values.IsNullableValue());
    }

    public int Priority => 10;
}
