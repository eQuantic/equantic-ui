using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// The <c>Math</c> and <c>MathF</c> surface. Both spell, on a static class, the same functions
/// .NET 7 put on the primitives themselves — <c>Math.Sqrt(x)</c> is <c>double.Sqrt(x)</c>,
/// <c>MathF.Sin(x)</c> is <c>float.Sin(x)</c> — so a member the numeric table names
/// (PrimitiveStaticStrategy) is translated BY that table, with the parameter's type as its home:
/// one table, so the two spellings cannot drift, and <c>MathF</c> answers in single precision
/// because <c>float</c> does. What the table does not name keeps this strategy's own forms.
/// </summary>
public class MathStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        // Check via semantic model if available
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        if (symbol != null)
        {
            var containingType = symbol.ContainingType.ToDisplayString();
            return containingType == "System.Math" || containingType.StartsWith("System.Math");
        }

        // Fallback: check expression text
        var callerText = memberAccess.Expression.ToString();
        return callerText is "Math" or "System.Math" or "MathF" or "System.MathF";
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;
        var arguments = invocation.ArgumentList.Arguments;

        // The numeric table first, by the type the overload computes on.
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol { Parameters.Length: > 0 } method
            && PrimitiveStaticStrategy.TemplateFor(method, method.Parameters[0].Type.SpecialType, arguments.Count)
                is { } template)
        {
            if (template.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);
            var irArgs = arguments.Select(a => context.Converter.ConvertIr(a.Expression)).ToArray();
            return JsExpr.Template(PrimitiveStaticStrategy.BindNamedArguments(template, invocation, method),
                irArgs, context.TypeAnnotations);
        }

        var argsList = arguments
            .Select(a => context.Converter.ConvertExpression(a.Expression))
            .ToList();

        // Below, no model bound the call. The class still says which numbers it computes on —
        // `MathF` on singles, `Math` on doubles — so everything but Round is answered by the SAME
        // table, by name: a fallback of its own guessed `Math.copySign`, `Math.bitIncrement` and a
        // `Math.log` that dropped its base, none of which JavaScript has.
        var single = memberAccess.Expression.ToString() is "MathF" or "System.MathF";
        var home = single ? SpecialType.System_Single : SpecialType.System_Double;
        if (PrimitiveStaticStrategy.TemplateByName(methodName, home, arguments.Count) is { } byName)
        {
            if (byName.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);
            var irArgs = arguments.Select(a => context.Converter.ConvertIr(a.Expression)).ToArray();
            return JsExpr.Template(byName, irArgs, context.TypeAnnotations);
        }
        JsExpr Answer(JsExpr value) => single ? SinglePrecision.Round(value) : value;

        // A DECIMAL rounds as a decimal — the number helper would round the object to NaN.
        // Half-to-even, the same MidpointRounding.ToEven the double path honours. The value IS a
        // Decimal (typed world); it lands in receiver position, so the writer fences it.
        if (methodName == "Round" && argsList.Count >= 1
            && context.SemanticHelper.GetType(arguments[0].Expression).IsDecimal())
        {
            var receiver = JsExprWriter.WriteIn(context.Converter.ConvertIr(arguments[0].Expression), JsPrecedence.Call);
            return JsExpr.Callish(argsList.Count >= 2
                ? $"{receiver}.round({argsList[1]})"
                : $"{receiver}.round()");
        }

        // Where no model can name the overload (the table above needs the bound method), a Round is
        // still banker's rounding with an optional digit count — .NET's default, and never the JS
        // Math.round, which sends halves up and ignores a digit count.
        if (methodName == "Round" && argsList.Count >= 1)
        {
            context.UsedHelpers.Add(Eq.Import);
            var round = single ? Eq.RoundSingle : Eq.Round;
            return JsExpr.Callish(argsList.Count >= 2
                ? $"{round}({argsList[0]}, {argsList[1]})"
                : $"{round}({argsList[0]})");
        }

        // Standard conversion: map .NET method names that differ from JS, else camelCase.
        // (camelCasing alone would produce Math.truncate / Math.ceiling, which don't exist.)
        var jsMethodName = methodName switch
        {
            "Truncate" => "trunc",
            "Ceiling" => "ceil",
            _ => methodName.ToCamelCase()
        };
        var args = string.Join(", ", argsList);

        return Answer(JsExpr.Callish($"Math.{jsMethodName}({args})"));
    }

    public int Priority => 10;
}
