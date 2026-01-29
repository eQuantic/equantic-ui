using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .Last() and .LastOrDefault() to JavaScript array access.
/// - Last() -> array[array.length - 1]
/// - Last(predicate) -> array.filter(predicate).pop()
/// - LastOrDefault() -> array[array.length - 1] ?? null
/// - LastOrDefault(predicate) -> array.filter(predicate).pop() ?? null
/// </summary>
public class LastStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName != "Last" && methodName != "LastOrDefault")
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
        var isOrDefault = methodName == "LastOrDefault";
        var defaultSuffix = isOrDefault ? " ?? null" : "";

        if (args.Count > 0)
        {
            // Last(predicate) -> filter then get last
            var predicate = context.Converter.ConvertExpression(args[0].Expression);
            return $"({caller}.filter({predicate}).pop(){defaultSuffix})";
        }

        // Last() -> array[array.length - 1]
        return $"({caller}[{caller}.length - 1]{defaultSuffix})";
    }

    public int Priority => 10;
}
