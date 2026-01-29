using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for Aggregate.
/// Handles: source.Aggregate(seed, func) -> source.reduce(func, seed)
/// </summary>
public class AggregateStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "Aggregate");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        
        if (args.Count == 2)
        {
            var seed = context.Converter.ConvertExpression(args[0].Expression);
            var func = context.Converter.ConvertExpression(args[1].Expression);
            return $"{source}.reduce({func}, {seed})";
        }
        else if (args.Count == 1)
        {
             var func = context.Converter.ConvertExpression(args[0].Expression);
             return $"{source}.reduce({func})";
        }
        
        return source;
    }

    public int Priority => 10;
}
