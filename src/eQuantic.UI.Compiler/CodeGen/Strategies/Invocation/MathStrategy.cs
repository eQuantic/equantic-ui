using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for Math method invocations.
/// Handles: Math.Abs, Math.Floor, Math.Round, Math.Clamp, etc.
/// Special case: Math.Clamp(val, min, max) → Math.min(Math.max(val, min), max)
/// </summary>
public class MathStrategy : IConversionStrategy
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
        return callerText is "Math" or "System.Math";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;
        
        var argsList = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertExpression(a.Expression))
            .ToList();
        
        // Special case: Math.Clamp(val, min, max) → Math.min(Math.max(val, min), max)
        if (methodName == "Clamp" && argsList.Count >= 3)
        {
            return $"Math.min(Math.max({argsList[0]}, {argsList[1]}), {argsList[2]})";
        }

        // Math.Round uses banker's rounding (MidpointRounding.ToEven) and supports a digit count —
        // neither matches JS Math.round. Route through the runtime $eq.math.round compat helper.
        if (methodName == "Round" && argsList.Count >= 1)
        {
            context.UsedHelpers.Add(Eq.Import);
            // A DECIMAL rounds as a decimal — the number helper would round the object to NaN.
            // Half-to-even, the same MidpointRounding.ToEven the double path honours. The value IS
            // a Decimal (typed world); it lands in receiver position, so the writer fences it.
            if (context.SemanticHelper.GetType(invocation.ArgumentList.Arguments[0].Expression).IsDecimal())
            {
                var receiver = JsExprWriter.WriteIn(
                    context.Converter.ConvertIr(invocation.ArgumentList.Arguments[0].Expression),
                    JsPrecedence.Call);
                return argsList.Count >= 2
                    ? $"{receiver}.round({argsList[1]})"
                    : $"{receiver}.round()";
            }
            return argsList.Count >= 2
                ? $"{Eq.Round}({argsList[0]}, {argsList[1]})"
                : $"{Eq.Round}({argsList[0]})";
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

        return $"Math.{jsMethodName}({args})";
    }

    public int Priority => 10;
}
