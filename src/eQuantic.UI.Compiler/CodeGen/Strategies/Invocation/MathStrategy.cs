using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        
        // Standard conversion: preserve method name in camelCase
        var jsMethodName = ToCamelCase(methodName);
        var args = string.Join(", ", argsList);
        
        return $"Math.{jsMethodName}({args})";
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 10;
}
