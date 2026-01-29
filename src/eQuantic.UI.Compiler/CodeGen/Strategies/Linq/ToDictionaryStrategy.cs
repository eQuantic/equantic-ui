using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Strategy for ToDictionary.
/// Handles: source.ToDictionary(k => k.Id) -> Object.fromEntries / Map
/// </summary>
public class ToDictionaryStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return context.SemanticHelper.IsLinqMethod(node, "ToDictionary");
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var source = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        
        if (args.Count >= 1)
        {
            var keySelector = context.Converter.ConvertExpression(args[0].Expression);
            var valueSelector = args.Count > 1 
                ? context.Converter.ConvertExpression(args[1].Expression) 
                : "x => x";

            // Object.fromEntries(source.map(x => [key(x), val(x)]))
            return $"Object.fromEntries({source}.map(x => [({keySelector})(x), ({valueSelector})(x)]))";
        }
        
        return source;
    }

    public int Priority => 10;
}
