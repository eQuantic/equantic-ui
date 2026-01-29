using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Min() and .Max() to JavaScript Math.min/max with spread.
/// - Min() -> Math.min(...array)
/// - Min(selector) -> Math.min(...array.map(selector))
/// - Max() -> Math.max(...array)
/// - Max(selector) -> Math.max(...array.map(selector))
/// </summary>
public class MinMaxStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName != "Min" && methodName != "Max")
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
        var methodName = memberAccess.Name.Identifier.Text;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        var mathFunc = methodName == "Min" ? "Math.min" : "Math.max";

        if (args.Count > 0)
        {
            // Min(x => x.Value) -> Math.min(...array.map(selector))
            var selector = context.Converter.ConvertExpression(args[0].Expression);
            return $"{mathFunc}(...{caller}.map({selector}))";
        }

        // Min() without selector
        return $"{mathFunc}(...{caller})";
    }

    public int Priority => 10;
}
