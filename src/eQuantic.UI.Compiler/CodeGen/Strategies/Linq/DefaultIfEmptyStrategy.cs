using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

public class DefaultIfEmptyStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name.Identifier.Text != "DefaultIfEmpty") return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType)) return true;

        return context.SemanticModel == null;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;

        // DefaultIfEmpty(val) -> source.length > 0 ? source : [val]
        // DefaultIfEmpty() -> source.length > 0 ? source : [null] (or default)
        
        var defaultVal = "null";
        if (args.Count > 0)
        {
            defaultVal = context.Converter.ConvertExpression(args[0].Expression);
        }

        return $"({caller}.length > 0 ? {caller} : [{defaultVal}])";
    }

    public int Priority => 10;
}
